using Grpc.Net.Client.Balancer;
using RocksDbSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public class PersistentDictionary<_Key, _Value> : IDisposable
    {
        RocksDb db;
        private object dbLock = new object();

        private Func<_Key, byte[]> keySerializer;
        private Func<byte[], _Key> keyDeserializer;
        private Func<_Value, byte[]> valueSerializer;
        private Func<byte[], _Value> valueDeserializer;

        public PersistentDictionary(string dbPath, Func<_Key, byte[]> keySerializer, Func<byte[], _Key> keyDeserializer, Func<_Value, byte[]> valueSerializer, Func<byte[], _Value> valueDeserializer, DbOptions? options = null)
        {
            var opt = options ?? new DbOptions().SetCreateIfMissing(true);
            db = RocksDb.Open(opt, dbPath);
            this.keySerializer = keySerializer;
            this.keyDeserializer = keyDeserializer;
            this.valueSerializer = valueSerializer;
            this.valueDeserializer = valueDeserializer;
        }

        public _Value this[_Key key]
        {
            get
            {
                lock (dbLock)
                {
                    return valueDeserializer(db.Get(keySerializer(key)));
                }
            }
            set
            {
                lock (dbLock)
                {
                    db.Put(keySerializer(key), valueSerializer(value));
                }
            }
        }

        public long CountEstimate
        {
            get
            {
                lock (dbLock)
                {
                    return db.GetProperty("rocksdb-estimate-num-keys") == null
                        ? 0
                        : int.Parse(db.GetProperty("rocksdb-estimate-num-keys"));
                }
            }
        }

        public bool ContainsKey(_Key key)
        {
            lock (dbLock)
            {
                return db.HasKey(keySerializer(key));
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public void Remove(_Key key, out object _)
        {
            lock (dbLock)
                db.Remove(keySerializer(key));
        }

        public bool TryGetValue(_Key key, [MaybeNullWhen(false)] out _Value value)
        {
            lock (dbLock)
            {
                var result = db.Get(keySerializer(key));
                if (result == null)
                {
                    value = default;
                    return false;
                }

                value = valueDeserializer(result);
                return true;
            }
        }

        // action returns true to continue, false to stop
        public void ForEach(Func<_Key, _Value, bool> action)
        {
            lock (dbLock)
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
    }
}
