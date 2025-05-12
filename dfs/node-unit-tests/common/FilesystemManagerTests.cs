using common;
using Fs;
using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unit_tests.mocks;

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
        public async Task TestCreateObjectContainer_ReturnsAsync(CancellationToken token)
        {
            await manager.CreateObjectContainer([], ByteString.Empty, Guid.Empty);
        }
    }
}
