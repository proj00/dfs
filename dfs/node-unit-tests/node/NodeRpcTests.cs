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
        [CancelAfter(30000)]
        public async Task TestGetChunkAsync(CancellationToken token)
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

            var ctx = TestServerCallContext.Create(cancellationToken: token);
            var writer = new TestServerStreamWriter<ChunkResponse>(ctx);
            var service = new NodeRpc(mock.Object);

            await service.GetChunk(new ChunkRequest() { Hash = obj.Object.File.Hashes.Hash[0], Offset = 0, TrackerUri = "http://localhost:1" },
                writer, ctx);

            writer.Complete();
            var returned = writer.ReadAllAsync().ToEnumerable().SelectMany(h => h.Response.ToByteArray()).ToArray();
            Assert.That(returned, Is.EqualTo(buf));
        }
    }
}
