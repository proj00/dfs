using common;
using Google.Protobuf;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace node
{
    public class FilePathHandler : IDisposable
    {
        private readonly Action<string, string> StartProcess;
        private bool disposedValue;

        public FilePathHandler(IPersistentCache<ByteString, string> pathByHash, Action<string, string> startProcess)
        {
            StartProcess = startProcess;
            PathByHash = pathByHash;
        }

        private readonly IPersistentCache<ByteString, string> PathByHash;

        public async Task<string> GetPathAsync(ByteString hash)
        {
            return await PathByHash.GetAsync(hash);
        }

        public async Task SetPathAsync(ByteString hash, string path)
        {
            if (!System.IO.Path.IsPathFullyQualified(path) || System.IO.Path.GetFullPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories");
            }
            await PathByHash.SetAsync(hash, path);
        }

        public void RevealFile(string path)
        {
            if (System.IO.Path.GetFullPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories");
            }
            StartProcess("explorer.exe", path);
        }

        public async Task RevealHashAsync(ByteString hash)
        {
            var path = await PathByHash.GetAsync(hash);
            if (!System.IO.Path.IsPathFullyQualified(path) || System.IO.Path.GetFullPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories");
            }
            StartProcess("explorer.exe", path);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PathByHash.Dispose();
                }

                disposedValue = true;
            }
        }

        ~FilePathHandler()
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
