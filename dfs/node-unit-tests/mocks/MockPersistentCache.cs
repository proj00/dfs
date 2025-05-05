using common;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace node_unit_tests.mocks
{
    public class MockPersistentCache<TKey, TValue> : IPersistentCache<TKey, TValue> where TValue : class
    {
        public readonly ConcurrentDictionary<TKey, TValue> _dict = new ConcurrentDictionary<TKey, TValue>();

        public Task<bool> ContainsKey(TKey key)
        {
            return Task.FromResult(_dict.ContainsKey(key));
        }

        public Task<long> CountEstimate()
        {
            return Task.FromResult((long)_dict.Count);
        }

        public Task ForEach(Func<TKey, TValue, bool> action)
        {
            foreach (var kv in _dict)
            {
                if (!action(kv.Key, kv.Value))
                    break;
            }
            return Task.CompletedTask;
        }

        public Task<TValue> GetAsync(TKey key)
        {
            if (_dict.TryGetValue(key, out var value))
                return Task.FromResult(value);
            throw new KeyNotFoundException($"Key '{key}' not found in cache.");
        }

        public async Task MutateAsync(TKey key, Func<TValue, Task<TValue>> mutate)
        {
            if (!_dict.TryGetValue(key, out var current))
                throw new KeyNotFoundException($"Key '{key}' not found in cache.");

            var updated = await mutate(current).ConfigureAwait(false);
            _dict[key] = updated;
        }

        public Task MutateAsync(TKey key, Func<TValue?, TValue> mutate, bool ignoreNull = false)
        {
            _dict.AddOrUpdate(
                key,
                k =>
                {
                    var result = mutate(default);
                    if (result == null && ignoreNull)
                        throw new InvalidOperationException($"Mutation returned null for missing key '{key}'.");
                    return result!;
                },
                (k, existing) =>
                {
                    var result = mutate(existing);
                    if (result == null && ignoreNull)
                        return existing;
                    return result!;
                });
            return Task.CompletedTask;
        }

        public Task Remove(TKey key)
        {
            _dict.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task SetAsync(TKey key, TValue value)
        {
            _dict[key] = value;
            return Task.CompletedTask;
        }

        public Task<TValue?> TryGetValue(TKey key)
        {
            _dict.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public void Dispose()
        {
            _dict.Clear();
        }
    }
}
