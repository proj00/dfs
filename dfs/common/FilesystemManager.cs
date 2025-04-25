using Fs;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public class FilesystemManager
    {
        private const string DbBasePath = "./db";

        private readonly object _syncRoot = new object();
        public PersistentDictionary<ByteString, ObjectWithHash> ObjectByHash { get; private set; }
        public PersistentDictionary<ByteString, RpcCommon.HashList> ChunkParents { get; private set; }
        public PersistentDictionary<Guid, ByteString> Container { get; private set; }
        public PersistentDictionary<ByteString, RpcCommon.HashList> Parent { get; private set; }
        public PersistentDictionary<ByteString, ByteString> NewerVersion { get; private set; }

        public string DbPath { get; private set; } = Path.Combine(DbBasePath, Guid.NewGuid().ToString());

        public FilesystemManager()
        {
            ObjectByHash = new PersistentDictionary<ByteString, ObjectWithHash>(
                Path.Combine(DbPath, "ObjectByHash"),
                keySerializer: bs => bs.ToByteArray(),
                keyDeserializer: bytes => ByteString.CopyFrom(bytes),
                valueSerializer: o => o.ToByteArray(),
                valueDeserializer: bytes => ObjectWithHash.Parser.ParseFrom(bytes)
            );

            ChunkParents = new PersistentDictionary<ByteString, RpcCommon.HashList>(
                Path.Combine(DbPath, "ChunkParents"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                list => list.ToByteArray(),
                RpcCommon.HashList.Parser.ParseFrom
            );

            Container = new PersistentDictionary<Guid, ByteString>(
                Path.Combine(DbPath, "Container"),
                guid => guid.ToByteArray(),
                bytes => new Guid(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );

            Parent = new PersistentDictionary<ByteString, RpcCommon.HashList>(
                Path.Combine(DbPath, "Parent"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                list => list.ToByteArray(),
                RpcCommon.HashList.Parser.ParseFrom
            );

            NewerVersion = new PersistentDictionary<ByteString, ByteString>(
                Path.Combine(DbPath, "NewerVersion"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );
        }

        public void Dispose()
        {
            ObjectByHash.Dispose();
            ChunkParents.Dispose();
            Container.Dispose();
            Parent.Dispose();
            NewerVersion.Dispose();
        }

        public Guid CreateObjectContainer(ObjectWithHash[] objects, ByteString rootObject, Guid? guid = null)
        {
            lock (_syncRoot)
            {
                foreach (var obj in objects)
                {
                    if (ObjectByHash.ContainsKey(obj.Hash))
                    {
                        continue;
                    }

                    ObjectByHash[obj.Hash] = obj;
                    switch (obj.Object.TypeCase)
                    {
                        case FileSystemObject.TypeOneofCase.File:
                            SetFileChunkParents(obj);
                            break;
                        case FileSystemObject.TypeOneofCase.Directory:
                            foreach (var child in obj.Object.Directory.Entries)
                            {
                                if (Parent.TryGetValue(child, out RpcCommon.HashList? value))
                                {
                                    value.Data.Add(obj.Hash);
                                    Parent[child] = value;
                                }
                                else
                                {
                                    Parent[child] = new RpcCommon.HashList() { Data = { obj.Hash } };
                                }
                            }
                            break;
                        case FileSystemObject.TypeOneofCase.Link:
                            break;
                        default:
                            throw new ArgumentException("ObjectWithHash has an invalid type");
                    }
                }

                var g = guid ?? Guid.NewGuid();
                if (Container.TryGetValue(g, out var oldRoot))
                {
                    NewerVersion[oldRoot] = rootObject;
                }

                Container[g] = rootObject;

                return g;
            }
        }

        public List<ObjectWithHash> GetContainerTree(Guid containerGuid)
        {
            lock (_syncRoot)
            {
                ByteString root;
                if (Container.TryGetValue(containerGuid, out root))
                {
                    return GetObjectTree(root);

                }
                else
                {
                    return [];
                }
            }
        }

        public List<ObjectWithHash> GetObjectTree(ByteString root)
        {
            lock (_syncRoot)
            {
                Dictionary<ByteString, ObjectWithHash> obj = new(new HashUtils.ByteStringComparer());
                void Traverse(ByteString hash)
                {
                    var o = ObjectByHash[hash];
                    obj[hash] = o;
                    if (o.Object.TypeCase != FileSystemObject.TypeOneofCase.Directory)
                    {
                        return;
                    }

                    foreach (var next in o.Object.Directory.Entries)
                    {
                        Traverse(next);
                    }
                }

                Traverse(root);
                return [.. obj.Values];
            }
        }

        public void SetFileChunkParents(ObjectWithHash fileObject)
        {
            lock (_syncRoot)
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
                    if (ChunkParents.TryGetValue(chunkHash, out RpcCommon.HashList? parents))
                    {
                        parents.Data.Add(parentHash);
                        ChunkParents[chunkHash] = parents;
                        continue;
                    }

                    ChunkParents[chunkHash] = new RpcCommon.HashList() { Data = { parentHash } };
                }
            }
        }
    }
}
