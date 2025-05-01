using Fs;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public sealed class FilesystemManager : IDisposable
    {
        private readonly AsyncLock _syncRoot = new();
        public PersistentCache<ByteString, ObjectWithHash> ObjectByHash { get; private set; }
        public PersistentCache<ByteString, RpcCommon.HashList> ChunkParents { get; private set; }
        public PersistentCache<Guid, ByteString> Container { get; private set; }
        public PersistentCache<ByteString, RpcCommon.HashList> Parent { get; private set; }
        public PersistentCache<ByteString, ByteString> NewerVersion { get; private set; }
        public string DbPath { get; private set; }

        public FilesystemManager(string dbBasePath)
        {
            DbPath = dbBasePath;
            ObjectByHash = new PersistentCache<ByteString, ObjectWithHash>(
                Path.Combine(DbPath, "ObjectByHash"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: bytes => ByteString.CopyFrom(bytes),
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: bytes => ObjectWithHash.Parser.ParseFrom(bytes)
            );

            ChunkParents = new PersistentCache<ByteString, RpcCommon.HashList>(
                Path.Combine(DbPath, "ChunkParents"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                list => list.ToByteArray(),
                RpcCommon.HashList.Parser.ParseFrom
            );

            Container = new PersistentCache<Guid, ByteString>(
                Path.Combine(DbPath, "Container"),
                guid => guid.ToByteArray(),
                bytes => new Guid(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );

            Parent = new PersistentCache<ByteString, RpcCommon.HashList>(
                Path.Combine(DbPath, "Parent"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                list => list.ToByteArray(),
                RpcCommon.HashList.Parser.ParseFrom
            );

            NewerVersion = new PersistentCache<ByteString, ByteString>(
                Path.Combine(DbPath, "NewerVersion"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );
        }

        public void Dispose()
        {
            ChunkParents.Dispose();
            ObjectByHash.Dispose();
            Container.Dispose();
            Parent.Dispose();
            NewerVersion.Dispose();
            _syncRoot.Dispose();
        }

        public async Task<Guid> CreateObjectContainer(ObjectWithHash[] objects, ByteString rootObject, Guid container)
        {
            ArgumentNullException.ThrowIfNull(objects);
            using (await _syncRoot.LockAsync())
            {
                foreach (var obj in objects)
                {
                    if (await ObjectByHash.ContainsKey(obj.Hash))
                    {
                        continue;
                    }

                    await ObjectByHash.SetAsync(obj.Hash, obj);
                    switch (obj.Object.TypeCase)
                    {
                        case FileSystemObject.TypeOneofCase.File:
                            await SetFileChunkParents(obj);
                            break;
                        case FileSystemObject.TypeOneofCase.Directory:
                            foreach (var child in obj.Object.Directory.Entries)
                            {
                                var value = await Parent.TryGetValue(child);
                                if (value != null)
                                {
                                    value.Data.Add(obj.Hash);
                                    await Parent.SetAsync(child, value);
                                }
                                else
                                {
                                    await Parent.SetAsync(child, new RpcCommon.HashList() { Data = { obj.Hash } });
                                }
                            }
                            break;
                        case FileSystemObject.TypeOneofCase.Link:
                            break;
                        default:
                            throw new ArgumentException("ObjectWithHash has an invalid type");
                    }
                }

                ByteString? oldRoot = await Container.TryGetValue(container);
                if (oldRoot != null)
                {
                    await NewerVersion.SetAsync(oldRoot, rootObject);
                }

                await Container.SetAsync(container, rootObject);

                return container;
            }
        }

        public async Task<List<ObjectWithHash>> GetContainerTree(Guid containerGuid)
        {
            return await GetContainerTree(containerGuid, false);
        }

        private async Task<List<ObjectWithHash>> GetContainerTree(Guid containerGuid, bool noLock)
        {
            using (await _syncRoot.LockAsync(noLock))
            {
                ByteString? root = await Container.TryGetValue(containerGuid);
                if (root != null)
                {
                    return await GetObjectTree(root, true);
                }
                else
                {
                    return [];
                }
            }
        }

        public async Task<List<ObjectWithHash>> ModifyContainer(Ui.FsOperation operation)
        {
            ArgumentNullException.ThrowIfNull(operation);
            using (await _syncRoot.LockAsync())
            {
                var root = await Container.GetAsync(Guid.Parse(operation.ContainerGuid));
                var objects = await GetObjectTree(root);
                return objects;
            }
        }

        public async Task<List<ObjectWithHash>> GetObjectTree(ByteString root)
        {
            return await GetObjectTree(root, false);
        }

        private async Task<List<ObjectWithHash>> GetObjectTree(ByteString root, bool noLock)
        {
            using (await _syncRoot.LockAsync(noLock))
            {
                Dictionary<ByteString, ObjectWithHash> obj = new(new ByteStringComparer());
                async Task Traverse(ByteString hash)
                {
                    var o = await ObjectByHash.GetAsync(hash);
                    obj[hash] = o;
                    if (o.Object.TypeCase != FileSystemObject.TypeOneofCase.Directory)
                    {
                        return;
                    }

                    foreach (var next in o.Object.Directory.Entries)
                    {
                        await Traverse(next);
                    }
                }

                await Traverse(root);
                return [.. obj.Values];
            }
        }

        private async Task SetFileChunkParents(ObjectWithHash fileObject)
        {
            if (fileObject.Object.TypeCase != Fs.FileSystemObject.TypeOneofCase.File)
            {
                throw new ArgumentException("fileObject doesn't represent a file");
            }

            var parentHash = fileObject.Hash;
            var file = fileObject.Object.File;
            foreach (var chunkHash in file.Hashes.Hash)
            {
                // if there is a large amount of duplicate files this will be slow, but imports (ie writes) are rare
                // in comparison to reads, so this is... fine?
                RpcCommon.HashList? parents = await ChunkParents.TryGetValue(chunkHash);
                if (parents != null)
                {
                    parents.Data.Add(parentHash);
                    await ChunkParents.SetAsync(chunkHash, parents);
                    continue;
                }

                await ChunkParents.SetAsync(chunkHash, new RpcCommon.HashList() { Data = { parentHash } });
            }
        }
    }
}
