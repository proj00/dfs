using Fs;
using Google.Protobuf;
using RpcCommon;
using Ui;

namespace common
{
    public interface IFilesystemManager : IDisposable
    {
        IPersistentCache<ByteString, HashList> ChunkParents { get; }
        IPersistentCache<System.Guid, ByteString> Container { get; }
        string DbPath { get; }
        IPersistentCache<ByteString, ByteString> NewerVersion { get; }
        IPersistentCache<ByteString, ObjectWithHash> ObjectByHash { get; }
        IPersistentCache<ByteString, HashList> Parent { get; }

        Task<System.Guid> CreateObjectContainer(ObjectWithHash[] objects, ByteString rootObject, System.Guid container);
        Task<List<ObjectWithHash>> GetContainerTree(System.Guid containerGuid);
        Task<List<ObjectWithHash>> GetObjectTree(ByteString root);
        Task<(ByteString, List<ObjectWithHash>)> ModifyContainer(FsOperation operation);
    }
}
