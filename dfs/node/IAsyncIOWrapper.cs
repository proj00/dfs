
namespace node
{
    public interface IAsyncIOWrapper
    {
        Task<long> ReadBufferAsync(string path, byte[] buffer, long offset, CancellationToken token);
        Task WriteBufferAsync(string path, byte[] buffer, long offset);
    }
}
