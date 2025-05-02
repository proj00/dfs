using common;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common_test
{
    public class MockPersistentCache<TKey, TValue> : IPersistentCache<TKey, TValue> where TValue : class
    {
        private ConcurrentDictionary<TKey, TValue> cache = new();
        private readonly AsyncLock dbLock = new();

        public void Dispose()
        {
            dbLock.Dispose();
        }

        public async Task<TValue> GetAsync(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                return cache[key];
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
                cache[key] = value;
            }
        }

        public async Task MutateAsync(TKey key, Func<TValue, Task<TValue>> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync())
            {
                var result = cache[key];
                if (result == null)
                {
                    return;
                }
                var newValue = await mutate(result);
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                cache[key] = newValue;
            }
        }
        public async Task MutateAsync(TKey key, Func<TValue?, TValue> mutate, bool ignoreNull = false)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            using (await dbLock.LockAsync())
            {
                var result = cache[key];
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

                    newValue = mutate(result);
                }
                if (newValue == null)
                {
                    throw new InvalidOperationException();
                }
                cache[key] = newValue;
            }
        }

        public async Task<long> CountEstimate()
        {
            using (await dbLock.LockAsync())
            {
                return cache.Count;
            }
        }

        public async Task<bool> ContainsKey(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                return cache.ContainsKey(key);
            }
        }

        public async Task Remove(TKey key)
        {
            using (await dbLock.LockAsync())
                cache.Remove(key, out _);
        }

        public async Task<TValue?> TryGetValue(TKey key)
        {
            using (await dbLock.LockAsync())
            {
                if (cache.TryGetValue(key, out TValue? value))
                {
                    Debug.Assert(value != null, "?????");
                    return value;
                }
                return null;
            }
        }

        // action returns true to continue, false to stop
        public async Task ForEach(Func<TKey, TValue, bool> action)
        {
            ArgumentNullException.ThrowIfNull(action);

            using (await dbLock.LockAsync())
            {
                foreach (var (k, v) in cache)
                {
                    if (!action(k, v))
                    {
                        break;
                    }
                }
            }
        }
    }
}
