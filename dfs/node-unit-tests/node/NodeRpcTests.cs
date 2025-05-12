using Bogus;
using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using Node;
using RpcCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Server.UnitTests.Helpers;
using unit_tests.mocks;

namespace unit_tests.node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, ChannelBase>;
    class NodeRpcTests
    {
        private static readonly Bogus.Faker faker = new();
        [Test]
        [CancelAfter(1000)]
        public async Task TestGetChunk_WritesToStreamAsync(CancellationToken token)
        {
            var obj = MockFsUtils.GenerateObject(faker);

            var cache = new Mock<IPersistentCache<ByteString, ObjectWithHash>>();
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult(obj));
            var chunkCache = new Mock<IPersistentCache<ByteString, HashList>>();
            chunkCache
                .Setup(self => self.TryGetValue(It.IsAny<ByteString>()))
                .Returns(Task.FromResult<HashList?>(new HashList() { Data = { obj.Hash } }));

            var fs = new Mock<IFilesystemManager>();
            fs.SetupGet(self => self.ObjectByHash).Returns(cache.Object);
            fs.SetupGet(self => self.ChunkParents).Returns(chunkCache.Object);
            var io = new Mock<IAsyncIOWrapper>();

            var buf = faker.Random.Bytes((int)obj.Object.File.Hashes.ChunkSize);
            io.Setup(self => self.ReadBufferAsync(It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, byte[], long, CancellationToken>((path, buffer, offset, token) =>
                {
                    Array.Copy(buf, buffer, buf.Length);
                })
                .Returns(Task.FromResult((long)obj.Object.File.Hashes.ChunkSize));

            var mock = new Mock<INodeState>();
            mock.SetupGet(self => self.AsyncIO).Returns(io.Object);
            mock.SetupGet(self => self.Manager).Returns(fs.Object);
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMicroseconds(1),
                factory,
                new Mock<ILoggerFactory>().Object);
            mock.SetupGet(self => self.ClientHandler).Returns(clientHandler);
            mock.SetupGet(self => self.PathHandler)
                .Returns(new Mock<FilePathHandler>(
                    new Mock<IPersistentCache<ByteString, string>>().Object,
                    (string a, string b) => { }).Object);

            mock.SetupGet(self => self.Logger).Returns(new Mock<ILogger>().Object);
            mock.SetupGet(self => self.BlockList).Returns(new Mock<IBlockListHandler>().Object);

            var ctx = TestServerCallContext.Create(cancellationToken: token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            await service.GetChunk(new ChunkRequest() { Hash = obj.Object.File.Hashes.Hash[0], Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx);

            writer.Complete();
            var returned = writer.ReadAllAsync().ToEnumerable().SelectMany(h => h.Response.ToByteArray()).ToArray();
            Assert.That(returned, Is.EqualTo(buf));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetChunk_CancelledAsync(CancellationToken token)
        {
            var obj = MockFsUtils.GenerateObject(faker);

            var cache = new Mock<IPersistentCache<ByteString, ObjectWithHash>>();
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult(obj));
            var chunkCache = new Mock<IPersistentCache<ByteString, HashList>>();
            chunkCache.Setup(self => self.TryGetValue(It.IsAny<ByteString>())).Returns(Task.FromResult(new HashList() { Data = { obj.Hash } }));

            var fs = new Mock<IFilesystemManager>();
            fs.SetupGet(self => self.ObjectByHash).Returns(cache.Object);
            fs.SetupGet(self => self.ChunkParents).Returns(chunkCache.Object);
            var io = new Mock<IAsyncIOWrapper>();

            var buf = faker.Random.Bytes((int)obj.Object.File.Hashes.ChunkSize);
            io.Setup(self => self.ReadBufferAsync(It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, byte[], long, CancellationToken>((path, buffer, offset, token) =>
                {
                    Array.Copy(buf, buffer, buf.Length);
                })
                .Returns(Task.FromResult((long)obj.Object.File.Hashes.ChunkSize));

            var mock = new Mock<INodeState>();
            mock.SetupGet(self => self.AsyncIO).Returns(io.Object);
            mock.SetupGet(self => self.Manager).Returns(fs.Object);
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMicroseconds(1),
                factory,
                new Mock<ILoggerFactory>().Object);
            mock.SetupGet(self => self.ClientHandler).Returns(clientHandler);
            mock.SetupGet(self => self.PathHandler)
                .Returns(new Mock<FilePathHandler>(
                    new Mock<IPersistentCache<ByteString, string>>().Object,
                    (string a, string b) => { }).Object);

            mock.SetupGet(self => self.Logger).Returns(new Mock<ILogger>().Object);
            mock.SetupGet(self => self.BlockList).Returns(new Mock<IBlockListHandler>().Object);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var ctx = TestServerCallContext.Create(cancellationToken: cts.Token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            await service.GetChunk(new ChunkRequest() { Hash = obj.Object.File.Hashes.Hash[0], Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx);

            writer.Complete();
            var returned = writer.ReadAllAsync().ToEnumerable().SelectMany(h => h.Response.ToByteArray()).ToArray();
            Assert.That(returned, Has.Length.EqualTo(0));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetChunk_IsInBlockList_ThrowsAsync(CancellationToken token)
        {
            var obj = MockFsUtils.GenerateObject(faker);
            var mock = new Mock<INodeState>();
            var blockList = new Mock<IBlockListHandler>();
            blockList.Setup(self => self.IsInBlockListAsync(It.IsAny<Uri>())).Returns(Task.FromResult(true));
            mock.SetupGet(self => self.Logger).Returns(new Mock<ILogger>().Object);
            mock.SetupGet(self => self.BlockList).Returns(blockList.Object);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var ctx = TestServerCallContext.Create(cancellationToken: cts.Token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await service.GetChunk(new ChunkRequest() { Hash = obj.Object.File.Hashes.Hash[0], Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx));
            Assert.That(ex.Message, Does.Contain("request blocked"));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetChunk_NoChunkParentFound_ThrowsAsync(CancellationToken token)
        {
            var obj = MockFsUtils.GenerateObject(faker);
            var mock = new Mock<INodeState>();
            mock.SetupGet(self => self.Logger).Returns(new Mock<ILogger>().Object);
            mock.SetupGet(self => self.BlockList).Returns(new Mock<IBlockListHandler>().Object);

            var cache = new Mock<IPersistentCache<ByteString, ObjectWithHash>>();
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult(obj));
            var chunkCache = new Mock<IPersistentCache<ByteString, HashList>>();
            chunkCache.Setup(self => self.TryGetValue(It.IsAny<ByteString>())).Returns(Task.FromResult<HashList?>(null));

            var fs = new Mock<IFilesystemManager>();
            fs.SetupGet(self => self.ChunkParents).Returns(chunkCache.Object);
            mock.SetupGet(self => self.Manager).Returns(fs.Object);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var ctx = TestServerCallContext.Create(cancellationToken: cts.Token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await service.GetChunk(new ChunkRequest() { Hash = obj.Object.File.Hashes.Hash[0], Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx));
            Assert.That(ex.Message, Does.Contain("chunk id invalid or not found"));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetChunk_InvalidObject_ThrowsAsync(CancellationToken token)
        {
            var obj = MockFsUtils.GenerateObject(faker);
            var mock = new Mock<INodeState>();
            mock.SetupGet(self => self.Logger).Returns(new Mock<ILogger>().Object);
            mock.SetupGet(self => self.BlockList).Returns(new Mock<IBlockListHandler>().Object);

            var cache = new Mock<IPersistentCache<ByteString, ObjectWithHash>>();
            var hash = obj.Object.File.Hashes.Hash[0];
            obj.Object = null;
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult(obj));
            var chunkCache = new Mock<IPersistentCache<ByteString, HashList>>();
            chunkCache
                .Setup(self => self.TryGetValue(It.IsAny<ByteString>()))
                .Returns(Task.FromResult<HashList?>(new HashList() { Data = { obj.Hash } }));

            var fs = new Mock<IFilesystemManager>();
            fs.SetupGet(self => self.ChunkParents).Returns(chunkCache.Object);
            fs.SetupGet(self => self.ObjectByHash).Returns(cache.Object);
            mock.SetupGet(self => self.Manager).Returns(fs.Object);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var ctx = TestServerCallContext.Create(cancellationToken: cts.Token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            var ex = Assert.ThrowsAsync<RpcException>(async () =>
            await service.GetChunk(new ChunkRequest() { Hash = hash, Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx));
            Assert.That(ex.Message, Does.Contain("internal failure"));
        }
    }
}
