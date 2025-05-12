using common;
using Microsoft.Extensions.Logging;

namespace node
{
    public interface INodeState : IDisposable
    {
        IAsyncIOWrapper AsyncIO { get; }
        IBlockListHandler BlockList { get; }
        GrpcClientHandler ClientHandler { get; }
        IDownloadManager Downloads { get; }
        ILogger Logger { get; }
        string LogPath { get; }
        IFilesystemManager Manager { get; }
        ObjectDownloadHandler Objects { get; }
        FilePathHandler PathHandler { get; }
        TransactionManager Transactions { get; }
    }
}
