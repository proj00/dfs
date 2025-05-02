using Google.Protobuf;
using Grpc.Net.Client.Balancer;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using NativeImport;
using Org.BouncyCastle.Crypto.Paddings;
using RocksDbSharp;
using RpcCommon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public interface ISerializer<T>
    {
        byte[] Serialize(T value);
        T Deserialize(byte[] bytes);
    }

    public class Serializer<T> : ISerializer<T> where T : IMessage<T>
    {
        private readonly MessageParser<T> parser;
        public Serializer()
        {
            var parserField = typeof(T).GetProperty(
                name: "Parser",
                bindingAttr: BindingFlags.Public | BindingFlags.Static
            );

            ArgumentNullException.ThrowIfNull(parserField);
            var checkedField = (MessageParser<T>?)parserField.GetValue(null);
            ArgumentNullException.ThrowIfNull(checkedField);
            parser = checkedField;
        }
        public T Deserialize(byte[] bytes)
        {
            return parser.ParseFrom(bytes);
        }

        public byte[] Serialize(T value)
        {
            return value.ToByteArray();
        }
    }

    public class StringSerializer : ISerializer<string>
    {
        public string Deserialize(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public byte[] Serialize(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }
    }

    public class ByteStringSerializer : ISerializer<ByteString>
    {
        public ByteString Deserialize(byte[] bytes)
        {
            return ByteString.CopyFrom(bytes);
        }

        public byte[] Serialize(ByteString value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.ToByteArray();
        }
    }

    public class GuidSerializer : ISerializer<System.Guid>
    {
        public System.Guid Deserialize(byte[] bytes)
        {
            return new System.Guid(bytes);
        }

        public byte[] Serialize(System.Guid value)
        {
            return value.ToByteArray();
        }
    }

    public class PersistentCache<TKey, TValue> : IDisposable, IPersistentCache<TKey, TValue> where TValue : class
    {
        private readonly RocksDb db;
        private readonly AsyncLock dbLock = new();

        private readonly ISerializer<TKey> keySerializer;
        private readonly ISerializer<TValue> valueSerializer;
        private bool disposedValue;

        public PersistentCache(string dbPath, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerializer, DbOptions? options = null)
        {
            var opt = options ?? new DbOptions().SetCreateIfMissing(true);
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }
            db = RocksDb.Open(opt, dbPath);
            this.keySerializer = keySerializer;
            this.valueSerializer = valueSerializer;
        }

        public async Task<TValue> GetAsync(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                var v = db.Get(keySerializer.Serialize(key));
                if (v == null)
                {
                    throw new InvalidOperationException();
                }
                return valueSerializer.Deserialize(v);
            }
        }
        public async Task SetAsync(TKey key, TValue value)
        {
            if (value == null)
            {
                throw new InvalidOperationException();
            }
            using (await dbLock.LockAsync())
            {
                db.Put(keySerializer.Serialize(key), valueSerializer.Serialize(value));
            }
        }

        public async Task MutateAsync(TKey key, Func<TValue, Task<TValue>> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync())
            {
                var result = db.Get(keySerializer.Serialize(key));
                if (result == null)
                {
                    return;
                }
                var value = valueSerializer.Deserialize(result);
                var newValue = await mutate(value);
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                db.Put(keySerializer.Serialize(key), valueSerializer.Serialize(newValue));
            }
        }
        public async Task MutateAsync(TKey key, Func<TValue?, TValue> mutate, bool ignoreNull = false)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync())
            {
                var result = db.Get(keySerializer.Serialize(key));
                TValue? newValue = null;
                if (ignoreNull)
                {
                    newValue = mutate(null);
                }
                else
                {
                    if (result == null)
                    {
                        return;
                    }

                    newValue = mutate(valueSerializer.Deserialize(result));
                }
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                db.Put(keySerializer.Serialize(key), valueSerializer.Serialize(newValue));
            }
        }

        public async Task<long> CountEstimate()
        {
            using (await dbLock.LockAsync())
            {
                return db.GetProperty("rocksdb-estimate-num-keys") == null
                    ? 0
                    : long.Parse(db.GetProperty("rocksdb-estimate-num-keys"), System.Globalization.NumberStyles.Any, null);
            }
        }

        public async Task<bool> ContainsKey(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                return db.HasKey(keySerializer.Serialize(key));
            }
        }

        public async Task Remove(TKey key)
        {
            using (await dbLock.LockAsync())
                db.Remove(keySerializer.Serialize(key));
        }

        public async Task<TValue?> TryGetValue(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                var result = db.Get(keySerializer.Serialize(key));
                if (result == null)
                {
                    return null;
                }

                return valueSerializer.Deserialize(result);
            }
        }

        // Only works with fixed length keys
        public async Task PrefixScan(ByteString prefix, Action<TKey, TValue> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(prefix);

            using (await dbLock.LockAsync())
            {
                int length = 0;
                using (var tempIt = db.NewIterator())
                {
                    tempIt.SeekToFirst();
                    if (!tempIt.Valid())
                    {
                        return;
                    }
                    length = tempIt.Key().Length;
                }

                var actualPrefix = HashUtils.ConcatHashes([prefix, ByteString.CopyFrom(new byte[length - prefix.Length])]).ToByteArray();

                using var it = db.NewIterator();
                for (it.Seek(actualPrefix); it.Valid(); it.Next())
                {
                    if (!it.Key().SequenceEqual(actualPrefix))
                    {
                        break;
                    }
                    var key = keySerializer.Deserialize(it.Key());
                    var value = valueSerializer.Deserialize(it.Value());
                    action(key, value);
                }
            }
        }

        // action returns true to continue, false to stop
        public async Task ForEach(Func<TKey, TValue, bool> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            using (await dbLock.LockAsync())
            {
                using var it = db.NewIterator();
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    var key = keySerializer.Deserialize(it.Key());
                    var value = valueSerializer.Deserialize(it.Value());
                    if (!action(key, value))
                    {
                        break;
                    }
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    db.Dispose();
                    dbLock.Dispose();
                }

                disposedValue = true;
            }
        }

        ~PersistentCache()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
