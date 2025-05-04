using common;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests
{
    public static class MockPersistentCache
    {
        public static Mock<IPersistentCache<TKey, TValue>> CreateMock<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dict) where TValue : class
        {
            var mock = new Mock<IPersistentCache<TKey, TValue>>();

            mock.Setup(c => c.ContainsKey(It.IsAny<TKey>()))
                .Returns<TKey>(key => Task.FromResult(dict.ContainsKey(key)));

            mock.Setup(c => c.CountEstimate())
                .Returns(() => Task.FromResult((long)dict.Count));

            mock.Setup(c => c.ForEach(It.IsAny<Func<TKey, TValue, bool>>()))
                .Returns<Func<TKey, TValue, bool>>(func =>
                {
                    foreach (var kv in dict)
                    {
                        if (!func(kv.Key, kv.Value)) break;
                    }
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.GetAsync(It.IsAny<TKey>()))
                .Returns<TKey>(key => Task.FromResult(dict[key]));

            mock.Setup(c => c.TryGetValue(It.IsAny<TKey>()))
                .Returns<TKey>(key =>
                {
                    dict.TryGetValue(key, out var value);
                    return Task.FromResult(value);
                });

            mock.Setup(c => c.SetAsync(It.IsAny<TKey>(), It.IsAny<TValue>()))
                .Returns<TKey, TValue>((key, value) =>
                {
                    dict[key] = value;
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.Remove(It.IsAny<TKey>()))
                .Returns<TKey>(key =>
                {
                    dict.TryRemove(key, out _);
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.MutateAsync(It.IsAny<TKey>(), It.IsAny<Func<TValue, Task<TValue>>>()))
                .Returns<TKey, Func<TValue, Task<TValue>>>(
                    async (key, func) =>
                    {
                        var newVal = await func(dict[key]);
                        dict[key] = newVal;
                    });

            mock.Setup(c => c.MutateAsync(It.IsAny<TKey>(), It.IsAny<Func<TValue?, TValue>>(), It.IsAny<bool>()))
                .Returns<TKey, Func<TValue?, TValue>, bool>((key, func, ignoreNull) =>
                {
                    dict.TryGetValue(key, out var existing);
                    var result = func(existing);
                    if (result != null || !ignoreNull)
                        dict[key] = result!;
                    return Task.CompletedTask;
                });

            mock.Setup(c => c.Dispose()).Callback(() => dict.Clear());

            return mock;
        }
    }
}
