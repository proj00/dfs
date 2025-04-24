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
        public PersistentDictionary<ByteString, ByteString[]> ChunkParents { get; private set; }
        public PersistentDictionary<Guid, ByteString> Container { get; private set; }
        public PersistentDictionary<ByteString, List<ByteString>> Parent { get; private set; }
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

            ChunkParents = new PersistentDictionary<ByteString, ByteString[]>(
                Path.Combine(DbPath, "ChunkParents"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                SerializeByteStringArray,
                DeserializeByteStringArray
            );

            Container = new PersistentDictionary<Guid, ByteString>(
                Path.Combine(DbPath, "Container"),
                guid => guid.ToByteArray(),
                bytes => new Guid(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );

            Parent = new PersistentDictionary<ByteString, List<ByteString>>(
                Path.Combine(DbPath, "Parent"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                list => SerializeByteStringArray(list.ToArray()),
                bytes => DeserializeByteStringArray(bytes).ToList()
            );

            NewerVersion = new PersistentDictionary<ByteString, ByteString>(
                Path.Combine(DbPath, "NewerVersion"),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes),
                bs => bs.ToByteArray(),
                bytes => ByteString.CopyFrom(bytes)
            );
        }

        private static byte[] SerializeByteStringArray(ByteString[] array)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(array.Length);
            foreach (var bs in array)
            {
                var data = bs.ToByteArray();
                bw.Write(data.Length);
                bw.Write(data);
            }
            return ms.ToArray();
        }

        private static ByteString[] DeserializeByteStringArray(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms);
            int count = br.ReadInt32();
            var arr = new ByteString[count];
            for (int i = 0; i < count; i++)
            {
                int len = br.ReadInt32();
                var data = br.ReadBytes(len);
                arr[i] = ByteString.CopyFrom(data);
            }
            return arr;
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
                                if (Parent.TryGetValue(child, out List<ByteString>? value))
                                {
                                    value.Add(obj.Hash);
                                }
                                else
                                {
                                    Parent[child] = [obj.Hash];
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
                var root = Container[containerGuid];
                if (root == null)
                {
                    return [];
                }

                return GetObjectTree(root);
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
                    if (ChunkParents.TryGetValue(chunkHash, out ByteString[]? parents))
                    {
                        ChunkParents[chunkHash] = [.. parents, parentHash];
                        continue;
                    }

                    ChunkParents[chunkHash] = [parentHash];
                }
            }
        }
    }
}
