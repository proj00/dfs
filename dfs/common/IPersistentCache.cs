using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace common
{
    public interface IPersistentCache<TKey, TValue> where TValue : class
    {
        Task<bool> ContainsKey(TKey key);
        Task<long> CountEstimate();
        void Dispose();
        Task ForEach(Func<TKey, TValue, bool> action);
        Task<TValue> GetAsync(TKey key);
        Task MutateAsync(TKey key, Func<TValue, Task<TValue>> mutate);
        Task MutateAsync(TKey key, Func<TValue?, TValue> mutate, bool ignoreNull = false);
        Task PrefixScan(ByteString prefix, Action<TKey, TValue> action);
        Task Remove(TKey key);
        Task SetAsync(TKey key, TValue value);
        Task<TValue?> TryGetValue(TKey key);
    }
}
