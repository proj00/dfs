using common;
using Grpc.Net.Client;
using System.IO;
namespace node
{
    public class AsyncIOWrapper : IAsyncIOWrapper
    {
        public async Task<long> ReadBufferAsync(string path, byte[] buffer, long offset, CancellationToken token)
        {
            using var stream = new FileStream
                    (
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        bufferSize: 4096,
                        FileOptions.Asynchronous |
                        FileOptions.WriteThrough
                    );

            return await RandomAccess.ReadAsync
                (
                stream.SafeFileHandle,
                buffer.AsMemory(),
                offset,
                token
                );
        }

        public async Task WriteBufferAsync(string path, byte[] buffer, long offset)
        {
            using var stream = new FileStream
                                (
                                    path,
                                    FileMode.OpenOrCreate,
                                    FileAccess.Write,
                                    FileShare.ReadWrite,
                                    bufferSize: 4096,
                                    FileOptions.Asynchronous |
                                    FileOptions.WriteThrough
                                );

            await RandomAccess.WriteAsync(stream.SafeFileHandle, buffer, offset, CancellationToken.None);
        }
    }
}
