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

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyContainer_Delete_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("left_child", [file.Hash]);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("right_child", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("root_dir", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };
            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            var guid = Guid.NewGuid();
            await manager.CreateObjectContainer(list, root.Hash, guid);
            var diff = await manager.ModifyContainer(
                new()
                {
                    ContainerGuid = guid.ToString(),
                    Parent = new() { Data = parent.Hash },
                    Target = new() { Data = file.Hash },
                    Type = Ui.OperationType.Delete
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diff.Item1, Is.Not.EqualTo(root.Hash));
                Assert.That(diff.Item2, Has.Count.EqualTo(list.Length - 1));
                Assert.That(diff.Item2, Does.Not.Contain(file));
                Assert.That(diff.Item2, Does.Contain(parent2));

                var new_root = diff.Item2.Find(o => o.Object.Name == "root_dir");
                Assert.That(new_root, Is.Not.Null);
                var new_left = diff.Item2.Find(o => o.Object.Name == "left_child");
                Assert.That(new_left, Is.Not.Null);
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_left.Hash));
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(parent2.Hash));
                Assert.That(new_left.Object.Directory.Entries, Does.Not.Contain(file.Hash));
                Assert.That(parent2.Object.Directory.Entries, Does.Contain(file.Hash));
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyContainer_Create_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("left_child", []);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("right_child", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("root_dir", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };
            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            var guid = Guid.NewGuid();
            await manager.CreateObjectContainer(list, root.Hash, guid);
            var diff = await manager.ModifyContainer(
                new()
                {
                    ContainerGuid = guid.ToString(),
                    Parent = new() { Data = parent.Hash },
                    Target = new() { Data = file.Hash },
                    Type = Ui.OperationType.Create,
                    Objects = new() { Data = { file } }
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diff.Item1, Is.Not.EqualTo(root.Hash));
                Assert.That(diff.Item2, Has.Count.EqualTo(list.Length));
                Assert.That(diff.Item2, Does.Not.Contain(root));
                Assert.That(diff.Item2, Does.Not.Contain(parent));
                Assert.That(diff.Item2, Does.Contain(parent2));

                var new_root = diff.Item2.Find(o => o.Object.Name == "root_dir");
                Assert.That(new_root, Is.Not.Null);
                var new_left = diff.Item2.Find(o => o.Object.Name == "left_child");
                Assert.That(new_left, Is.Not.Null);
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_left.Hash));
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(parent2.Hash));
                Assert.That(new_left.Object.Directory.Entries, Does.Contain(file.Hash));
                Assert.That(parent2.Object.Directory.Entries, Does.Contain(file.Hash));
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyContainer_Move_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("left_child", []);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("right_child", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("root_dir", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };
            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            var guid = Guid.NewGuid();
            await manager.CreateObjectContainer(list, root.Hash, guid);
            var diff = await manager.ModifyContainer(
                new()
                {
                    ContainerGuid = guid.ToString(),
                    Parent = new() { Data = parent2.Hash },
                    Target = new() { Data = file.Hash },
                    Type = Ui.OperationType.Move,
                    NewParent = new() { Data = parent.Hash },
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diff.Item1, Is.Not.EqualTo(root.Hash));
                Assert.That(diff.Item2, Has.Count.EqualTo(list.Length - 1));
                Assert.That(diff.Item2, Does.Not.Contain(root));
                Assert.That(diff.Item2, Does.Not.Contain(parent));
                Assert.That(diff.Item2, Does.Not.Contain(parent2));

                var new_root = diff.Item2.Find(o => o.Object.Name == "root_dir");
                Assert.That(new_root, Is.Not.Null);
                var new_left = diff.Item2.Find(o => o.Object.Name == "left_child");
                Assert.That(new_left, Is.Not.Null);
                var new_right = diff.Item2.Find(o => o.Object.Name == "right_child");
                Assert.That(new_right, Is.Not.Null);

                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_left.Hash));
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_right.Hash));

                Assert.That(new_left.Object.Directory.Entries, Does.Contain(file.Hash));
                Assert.That(new_right.Object.Directory.Entries, Does.Not.Contain(file.Hash));
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyContainer_Copy_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("left_child", []);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("right_child", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("root_dir", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };
            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            var guid = Guid.NewGuid();
            await manager.CreateObjectContainer(list, root.Hash, guid);
            var diff = await manager.ModifyContainer(
                new()
                {
                    ContainerGuid = guid.ToString(),
                    Parent = new() { Data = parent2.Hash },
                    Target = new() { Data = file.Hash },
                    Type = Ui.OperationType.Copy,
                    NewParent = new() { Data = parent.Hash },
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diff.Item1, Is.Not.EqualTo(root.Hash));
                Assert.That(diff.Item2, Has.Count.EqualTo(list.Length));
                Assert.That(diff.Item2, Does.Not.Contain(root));
                Assert.That(diff.Item2, Does.Not.Contain(parent));
                Assert.That(diff.Item2, Does.Contain(parent2));

                var new_root = diff.Item2.Find(o => o.Object.Name == "root_dir");
                Assert.That(new_root, Is.Not.Null);
                var new_left = diff.Item2.Find(o => o.Object.Name == "left_child");
                Assert.That(new_left, Is.Not.Null);
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_left.Hash));
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(parent2.Hash));
                Assert.That(new_left.Object.Directory.Entries, Does.Contain(file.Hash));
                Assert.That(parent2.Object.Directory.Entries, Does.Contain(file.Hash));
            }
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyContainer_Rename_ReturnsAsync(CancellationToken token)
        {
            var file = MockFsUtils.GenerateObject(faker, false);
            var parentObj = FilesystemUtils.GetDirectoryObject("left_child", []);
            var parent = new ObjectWithHash() { Hash = HashUtils.GetHash(parentObj), Object = parentObj };
            var parent2Obj = FilesystemUtils.GetDirectoryObject("right_child", [file.Hash]);
            var parent2 = new ObjectWithHash() { Hash = HashUtils.GetHash(parent2Obj), Object = parent2Obj };
            var rootObj = FilesystemUtils.GetDirectoryObject("root_dir", [parent.Hash, parent2.Hash]);
            var root = new ObjectWithHash() { Hash = HashUtils.GetHash(rootObj), Object = rootObj };
            var list = new ObjectWithHash[] { root, parent, file, parent2 };

            var guid = Guid.NewGuid();
            await manager.CreateObjectContainer(list, root.Hash, guid);
            var actualFile = file.Clone();
            var diff = await manager.ModifyContainer(
                new()
                {
                    ContainerGuid = guid.ToString(),
                    Parent = new() { Data = parent2.Hash },
                    Target = new() { Data = file.Hash },
                    Type = Ui.OperationType.Rename,
                    NewName = "new_file"
                });

            using (Assert.EnterMultipleScope())
            {
                Assert.That(diff.Item1, Is.Not.EqualTo(root.Hash));
                Assert.That(diff.Item2, Has.Count.EqualTo(list.Length));
                Assert.That(diff.Item2, Does.Not.Contain(root));
                Assert.That(diff.Item2, Does.Not.Contain(parent2));
                Assert.That(diff.Item2, Does.Contain(parent));

                var new_root = diff.Item2.Find(o => o.Object.Name == "root_dir");
                Assert.That(new_root, Is.Not.Null);
                var new_right = diff.Item2.Find(o => o.Object.Name == "right_child");
                Assert.That(new_right, Is.Not.Null);
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(new_right.Hash));
                Assert.That(new_root.Object.Directory.Entries, Does.Contain(parent.Hash));
                Assert.That(new_right.Object.Directory.Entries, Does.Not.Contain(actualFile.Hash));
                var new_file = diff.Item2.Find(o => o.Object.Name == "new_file");
                Assert.That(new_file, Is.Not.Null);
                Assert.That(new_right.Object.Directory.Entries, Does.Contain(new_file.Hash));
            }
        }
    }
}
