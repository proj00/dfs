using common;
using Google.Protobuf;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using System.Net.WebSockets;

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
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (!Path.IsPathFullyQualified(path) || FixPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories or invalid chars");
            }
            await PathByHash.SetAsync(hash, path);
        }

        public void RevealFile(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            if (FixPath(path) != path)
            {
                throw new ArgumentException("Path contains relative directories or invalid chars");
            }
            StartProcess("explorer.exe", path);
        }

        private static string FixPath(string path)
        {
            Console.WriteLine(path);
            var root = Path.GetPathRoot(path) ?? "";
            path = path.Substring(root.Length);
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var chars = new HashSet<char>(Path.GetInvalidFileNameChars());

            foreach (var p in parts)
            {
                if (p.Any(chars.Contains) || p == "." || p == "..")
                {
                    return "";
                }
            }

            return root + path;
        }

        public async Task RevealHashAsync(ByteString hash)
        {
            var path = await GetPathAsync(hash);
            RevealFile(path);
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
