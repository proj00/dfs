using common;
using Fs;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unit_tests.mocks;
using unit_tests.node;

namespace unit_tests.common
{
    class FilesystemManagerTests
    {
        private static readonly Bogus.Faker faker = new();
        FilesystemManager manager;
        MockPersistentCache<ByteString, ObjectWithHash> ObjectByHash;
        MockPersistentCache<ByteString, RpcCommon.HashList> ChunkParents;
        MockPersistentCache<Guid, ByteString> Container;
        MockPersistentCache<ByteString, RpcCommon.HashList> Parent;
        MockPersistentCache<ByteString, ByteString> NewerVersion;

        [SetUp]
        public void Setup()
        {
            ObjectByHash = new(new ByteStringComparer());
            ChunkParents = new(new ByteStringComparer());
            Container = new();
            Parent = new(new ByteStringComparer());
            NewerVersion = new(new ByteStringComparer());
            manager = new FilesystemManager(ObjectByHash, ChunkParents, Container, Parent, NewerVersion, faker.Lorem.Word());
        }

        [TearDown]
        public void Teardown()
        {
            manager?.Dispose();
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetObjectTree_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("dir", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var rootObj = FilesystemUtils.GetDirectoryObject("dir1", [parent.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };

            var list = new ObjectWithHash[] { root, parent, file };

            foreach (var a in list)
            {
                ObjectByHash._dict[a.Hash] = a;
            }

            var result = await manager.GetObjectTree(root.Hash);
            using (Assert.EnterMultipleScope())
            {
                foreach (var a in list)
                {
                    Assert.That(result, Does.Contain(a));
                }
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetContainerTree_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("dir", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var rootObj = FilesystemUtils.GetDirectoryObject("dir1", [parent.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };

            var list = new ObjectWithHash[] { root, parent, file };

            foreach (var a in list)
            {
                ObjectByHash._dict[a.Hash] = a;
            }

            System.Guid guid = Guid.NewGuid();
            Container._dict[guid] = root.Hash;

            var result = await manager.GetContainerTree(guid);
            using (Assert.EnterMultipleScope())
            {
                foreach (var a in list)
                {
                    Assert.That(result, Does.Contain(a));
                }
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetContainerTree_NoContainer_ReturnsEmptyAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("dir", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var rootObj = FilesystemUtils.GetDirectoryObject("dir1", [parent.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };

            var list = new ObjectWithHash[] { root, parent, file };

            foreach (var a in list)
            {
                ObjectByHash._dict[a.Hash] = a;
            }

            System.Guid guid = Guid.NewGuid();

            var result = await manager.GetContainerTree(guid);
            Assert.That(result, Has.Count.EqualTo(0));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestCreateObjectContainer_BuildsTreeAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("dir", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("dir2", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("dir1", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };

            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            System.Guid guid = Guid.NewGuid();

            var result = await manager.CreateObjectContainer(list, root.Hash, guid);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(guid));
                Assert.That(Parent._dict[file.Hash].Data, Does.Contain(parent.Hash));
                Assert.That(Parent._dict[parent.Hash].Data, Does.Contain(root.Hash));
                Assert.That(ObjectByHash._dict, Has.Count.EqualTo(list.Length));
                Assert.That(Parent._dict, Has.Count.EqualTo(list.Length - 1));

                foreach (var a in list)
                {
                    Assert.That(ObjectByHash._dict, Does.ContainKey(a.Hash));
                }
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestCreateObjectContainer_SkipsDuplicatesAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("dir", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var rootObj = FilesystemUtils.GetDirectoryObject("dir1", [parent.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };

            var list = new ObjectWithHash[] { root, parent, file };

            System.Guid guid = Guid.NewGuid();

            var result = await manager.CreateObjectContainer(list, root.Hash, guid);
            var newResult = await manager.CreateObjectContainer(list, root.Hash, guid);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result, Is.EqualTo(guid));
                Assert.That(newResult, Is.EqualTo(guid));
                Assert.That(Parent._dict[file.Hash].Data, Does.Contain(parent.Hash));
                Assert.That(Parent._dict[parent.Hash].Data, Does.Contain(root.Hash));
                Assert.That(ObjectByHash._dict, Has.Count.EqualTo(list.Length));
                Assert.That(Parent._dict, Has.Count.EqualTo(list.Length - 1));
                Assert.That(NewerVersion._dict, Does.ContainKey(root.Hash));

                foreach (var a in list)
                {
                    Assert.That(ObjectByHash._dict, Does.ContainKey(a.Hash));
                }
            }
        }
    }
}
