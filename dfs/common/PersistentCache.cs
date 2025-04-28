using Google.Protobuf;
using Grpc.Net.Client.Balancer;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using NativeImport;
using RocksDbSharp;
using RpcCommon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public class PersistentCache<TKey, TValue> : IDisposable
        where TValue : class
    {
        private readonly RocksDb db;
        private readonly AsyncLock dbLock = new();

        private readonly Func<TKey, byte[]> keySerializer;
        private readonly Func<byte[], TKey> keyDeserializer;
        private readonly Func<TValue, byte[]> valueSerializer;
        private readonly Func<byte[], TValue> valueDeserializer;
        private bool disposedValue;

        public PersistentCache(string dbPath, Func<TKey, byte[]> keySerializer, Func<byte[], TKey> keyDeserializer, Func<TValue, byte[]> valueSerializer, Func<byte[], TValue> valueDeserializer, DbOptions? options = null)
        {
            var opt = options ?? new DbOptions().SetCreateIfMissing(true);
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }
            db = RocksDb.Open(opt, dbPath);
            this.keySerializer = keySerializer;
            this.keyDeserializer = keyDeserializer;
            this.valueSerializer = valueSerializer;
            this.valueDeserializer = valueDeserializer;
        }

        public async Task<TValue> GetAsync(TKey key)
        {
            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                var v = db.Get(keySerializer(key));
                if (v == null)
                {
                    throw new InvalidOperationException();
                }
                return valueDeserializer(v);
            }
        }
        public async Task SetAsync(TKey key, TValue value)
        {
            if (value == null)
            {
                throw new InvalidOperationException();
            }
            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                db.Put(keySerializer(key), valueSerializer(value));
            }
        }

        public async Task MutateAsync(TKey key, Func<TValue, Task<TValue>> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                var result = db.Get(keySerializer(key));
                if (result == null)
                {
                    return;
                }
                var value = valueDeserializer(result);
                var newValue = await mutate(value).ConfigureAwait(false);
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                db.Put(keySerializer(key), valueSerializer(newValue));
            }
        }
        public async Task MutateAsync(TKey key, Func<TValue?, TValue> mutate, bool ignoreNull = false)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                var result = db.Get(keySerializer(key));
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

                    newValue = mutate(valueDeserializer(result));
                }
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                db.Put(keySerializer(key), valueSerializer(newValue));
            }
        }

        public async Task<long> CountEstimate()
        {
            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                return db.GetProperty("rocksdb-estimate-num-keys") == null
                    ? 0
                    : long.Parse(db.GetProperty("rocksdb-estimate-num-keys"), System.Globalization.NumberStyles.Any, null);
            }
        }

        public async Task<bool> ContainsKey(TKey key)
        {
            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                return db.HasKey(keySerializer(key));
            }
        }

        public async Task Remove(TKey key)
        {
            using (await dbLock.LockAsync().ConfigureAwait(false))
                db.Remove(keySerializer(key));
        }

        public async Task<TValue?> TryGetValue(TKey key)
        {
            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                var result = db.Get(keySerializer(key));
                if (result == null)
                {
                    return null;
                }

                return valueDeserializer(result);
            }
        }

        // Only works with fixed length keys
        public async Task PrefixScan(ByteString prefix, Action<TKey, TValue> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(prefix);

            using (await dbLock.LockAsync().ConfigureAwait(false))
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
                    var key = keyDeserializer(it.Key());
                    var value = valueDeserializer(it.Value());
                    action(key, value);
                }
            }
        }

        // action returns true to continue, false to stop
        public async Task ForEach(Func<TKey, TValue, bool> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            using (await dbLock.LockAsync().ConfigureAwait(false))
            {
                using var it = db.NewIterator();
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    var key = keyDeserializer(it.Key());
                    var value = valueDeserializer(it.Value());
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
