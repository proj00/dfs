using Fs;
using Google.Protobuf;
using Node;
using Ui;

namespace node
{
    public interface IDownloadManager : System.IAsyncDisposable
    {
        void AddChunkUpdateCallback(Func<FileChunk, CancellationToken, Task<FileChunk>> callback);
        Task AddNewFileAsync(ObjectWithHash obj, Uri trackerUri, string destinationDir);
        Task<Progress> GetFileProgressAsync(ByteString hash);
        Task<ByteString[]> GetIncompleteFilesAsync();
        Task PauseDownloadAsync(ObjectWithHash file, CancellationToken token);
        Task ResumeDownloadAsync(ObjectWithHash file, CancellationToken token);
        Task UpdateFileProgressAsync(ByteString hash, long newProgress);
    }
}
