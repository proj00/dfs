using Fs;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace common
{
    public class FilesystemManager
    {
        public ConcurrentDictionary<ByteString, ObjectWithHash> ObjectByHash { get; }
        public ConcurrentDictionary<ByteString, ByteString[]> ChunkParents { get; }
        public ConcurrentDictionary<Guid, ByteString> Container { get; }
        public ConcurrentDictionary<ByteString, List<ByteString>> Parent { get; }
        public ConcurrentDictionary<ByteString, ByteString> NewerVersion { get; }

        public FilesystemManager()
        {
            ObjectByHash = new(new HashUtils.ByteStringComparer());
            ChunkParents = new(new HashUtils.ByteStringComparer());
            Container = [];
            Parent = [];
            NewerVersion = [];
        }

        public Guid CreateObjectContainer(ObjectWithHash[] objects, ByteString rootObject, Guid? guid = null)
        {
            foreach (var obj in objects)
            {
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
            Container[g] = rootObject;
            return g;
        }

        public List<ObjectWithHash> GetContainerTree(Guid containerGuid)
        {
            var root = Container[containerGuid];
            if (root == null)
            {
                return [];
            }

            return GetObjectTree(root);
        }

        public List<ObjectWithHash> GetObjectTree(ByteString root)
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

        public void SetFileChunkParents(ObjectWithHash fileObject)
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
