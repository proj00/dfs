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
    class PersistentDictionary<_Key, _Value> : IDisposable
    {
        RocksDb db;

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
            get => valueDeserializer(db.Get(keySerializer(key)));
            set =>
                db.Put(keySerializer(key), valueSerializer(value));
        }

        public long CountEstimate
        {
            get
            {
                return int.Parse(db.GetProperty("rocksdb-estimate-num-keys"));
            }
        }

        public bool ContainsKey(_Key key)
        {
            return db.HasKey(keySerializer(key));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public IEnumerator<KeyValuePair<_Key, _Value>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(_Key key)
        {
            try
            {
                db.Remove(keySerializer(key));
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool Remove(KeyValuePair<_Key, _Value> item)
        {
            try
            {
                db.Remove(keySerializer(item.Key));
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool TryGetValue(_Key key, [MaybeNullWhen(false)] out _Value value)
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
}
