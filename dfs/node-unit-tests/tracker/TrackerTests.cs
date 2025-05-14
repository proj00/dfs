using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using common;
using Fs;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;
using RpcCommon;
using tracker;
using Tracker;
using Guid = System.Guid;
using NUnit.Framework;



namespace unit_tests.tracker
{
    class TrackerTests : Tracker.Tracker.TrackerBase
    {
        private Mock<IFilesystemManager> _fs;
        private Mock<IPersistentCache<string, DataUsage>> _cache;
        private Mock<ILogger> _logger;
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private CancellationTokenSource _cts;
        private TrackerRpc _tracker;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method

        [SetUp]
        public void SetUp()
        {
            _fs = new Mock<IFilesystemManager>();
            _cache = new Mock<IPersistentCache<string, DataUsage>>();
            _logger = new Mock<ILogger>();
            _cts = new CancellationTokenSource();

            // Explicitly specify the constructor to resolve ambiguity
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8601 // Possible null reference assignment.
            _tracker = (TrackerRpc)Activator.CreateInstance(
                typeof(TrackerRpc),
                _logger.Object,
                _fs.Object,
                _cache.Object,
                _cts
            );
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
        }


        [Test]
        public async Task MarkReachable_AddsPeerAndGetPeerList_ReturnsIt()
        {
            // Arrange
            var hash = ByteString.CopyFromUtf8("h1");
            const string peerId = "peerA";
            var markReq = new MarkRequest { Peer = peerId };
            markReq.Hash.Add(hash);

            var responses = new List<PeerResponse>();
            var writer = new TestStreamWriter<PeerResponse>(responses);

            var context = Grpc.Core.Testing.TestServerCallContext.Create(
                method: "GetPeerList",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: "ipv4:127.0.0.1:5000",
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null // Added this parameter to match the method signature
            );

            // Act
            await _tracker.MarkReachable(markReq, context);
            await _tracker.GetPeerList(new PeerRequest { ChunkHash = hash }, writer, context);

            // Assert
            Assert.That(responses.Count, Is.EqualTo(1));
            Assert.That(responses[0].Peer, Is.EqualTo(peerId));
        }

        [Test]
        public async Task MarkUnreachable_RemovesPeer()
        {
            // Arrange: pirmiau pažymim kaip reachable
            var hash = ByteString.CopyFromUtf8("h2");
            const string peerId = "peerB";
            var mk = new MarkRequest { Peer = peerId };
            mk.Hash.Add(hash);
            var ctx1 = Grpc.Core.Testing.TestServerCallContext.Create(
                method: "m1",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: "",
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null // Added this parameter to match the method signature
            );
            await _tracker.MarkReachable(mk, ctx1);

            var responses = new List<PeerResponse>();
            var writer = new TestStreamWriter<PeerResponse>(responses);
            var ctx2 = Grpc.Core.Testing.TestServerCallContext.Create(
                method: "g2",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: "ipv4:127.0.0.1:5000",
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null // Added this parameter to match the method signature
            );

            // Act: nuimti
            var unmk = new MarkRequest { Peer = peerId };
            unmk.Hash.Add(hash);
            await _tracker.MarkUnreachable(unmk, ctx2);
            await _tracker.GetPeerList(new PeerRequest { ChunkHash = hash }, writer, ctx2);

            // Assert
            Assert.That(responses, Is.Empty);
        }

        [Test]
        public async Task StartTransaction_FirstOk_SecondLocked()
        {
            // Arrange
            var containerId = Guid.NewGuid().ToString();
            var req = new TransactionRequest { ContainerGuid = containerId };
            var ctx = Grpc.Core.Testing.TestServerCallContext.Create(
                method: "sd",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: "",
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null // Added this parameter to match the method signature
            );

            // Act
            var first = await _tracker.StartTransaction(req, ctx);
            var second = await _tracker.StartTransaction(req, ctx);

            // Assert
            Assert.That(first.State, Is.EqualTo(TransactionState.Ok));
            Assert.That(second.State, Is.EqualTo(TransactionState.Locked));
        }

        [Test]
        public async Task Shutdown_CancelsTokenSource()
        {
            // Arrange
            var ctx = Grpc.Core.Testing.TestServerCallContext.Create(
                method: "sd",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: "",
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null // Added this parameter to match the method signature
            );

            // Act
            await _tracker.Shutdown(new Empty(), ctx);

            // Assert
            Assert.That(_cts.IsCancellationRequested, Is.True);
        }

        [Test]
        public async Task GetContainerRootHash_ReturnsHash_WhenExists()
        {
            var id = Guid.NewGuid();
            var data = ByteString.CopyFromUtf8("root");
            _fs.Setup(m => m.Container.TryGetValue(id)).ReturnsAsync(data);
            var req = new RpcCommon.Guid { Guid_ = id.ToString() };
            var res = await _tracker.GetContainerRootHash(req, CreateContext());

            // Fix: Use Assert.That with a proper constraint to compare ByteString values
            Assert.That(res.Data, Is.EqualTo(data));
        }

        [Test]
        public void GetContainerRootHash_ThrowsNotFound_WhenMissing()
        {
            var id = Guid.NewGuid();
            _fs.Setup(m => m.Container.TryGetValue(id)).ReturnsAsync((ByteString?)null);
            var req = new RpcCommon.Guid { Guid_ = id.ToString() };
            Assert.ThrowsAsync<RpcException>(() => _tracker.GetContainerRootHash(req, CreateContext()));
        }

        [Test]
        public async Task GetObjectTree_WritesObjects_WhenExists()
        {
            var hash = ByteString.CopyFromUtf8("obj");
            var item = new ObjectWithHash { /* minimal init */ };
            _fs.Setup(m => m.ObjectByHash.ContainsKey(hash)).ReturnsAsync(true);
            // Fix for CS1929: Adjust the setup to use the correct type for ReturnsAsync
            _fs.Setup(m => m.GetObjectTree(It.IsAny<ByteString>()))
               .ReturnsAsync(new List<ObjectWithHash> { item });
            var list = new List<ObjectWithHash>();
            await _tracker.GetObjectTree(new Hash { Data = hash }, new TestStreamWriter<ObjectWithHash>(list), CreateContext());
            Assert.That(list.Count, Is.EqualTo(1)); // Fixed the argument order
        }

        [Test]
        public void GetObjectTree_ThrowsNotFound_WhenMissing()
        {
            var hash = ByteString.CopyFromUtf8("obj");
            _fs.Setup(m => m.ObjectByHash.ContainsKey(hash)).ReturnsAsync(false);
            Assert.ThrowsAsync<RpcException>(() => _tracker.GetObjectTree(new Hash { Data = hash }, new TestStreamWriter<ObjectWithHash>(new List<ObjectWithHash>()), CreateContext()));
        }

        [Test]
        public async Task SearchForObjects_FiltersAndWritesMatches()
        {
            // Fix for CS1929: Adjust the setup to use the correct type for ReturnsAsync
            
            var guid1 = Guid.NewGuid();
            _fs.Setup(m => m.Container.ForEach(It.IsAny<Func<Guid, ByteString, bool>>()))
              .Callback<Func<Guid, ByteString, bool>>(cb => { cb(Guid.NewGuid(), ByteString.CopyFrom(new byte[0])); })
              .Returns(Task.CompletedTask);
            var obj = new ObjectWithHash { Object = new Fs.FileSystemObject { Name = "test" } };
            _fs.Setup(m => m.GetContainerTree(It.IsAny<Guid>()))
               .ReturnsAsync(new List<ObjectWithHash> { obj });
            var list = new List<SearchResponse>();
            await _tracker.SearchForObjects(new SearchRequest { Query = "test" }, new TestStreamWriter<SearchResponse>(list), CreateContext());
            Assert.That(list.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetDataUsage_ReturnsUsage_WhenExists()
        {
            _cache.Setup(m => m.GetAsync("127.0.0.1")).ReturnsAsync(new DataUsage { Upload = 10, Download = 20 });
            var res = await _tracker.GetDataUsage(new Empty(), CreateContext("ipv4:127.0.0.1:1234"));
            Assert.That(res.Upload, Is.EqualTo(10));
            Assert.That(res.Download, Is.EqualTo(20));
        }

        [Test]
        public async Task GetDataUsage_ReturnsEmpty_WhenError()
        {
            _cache.Setup(m => m.GetAsync(It.IsAny<string>())).ThrowsAsync(new Exception());
            var res = await _tracker.GetDataUsage(new Empty(), CreateContext());
            Assert.That(res.Upload, Is.EqualTo(0)); // Fixed argument order
            Assert.That(res.Download, Is.EqualTo(0)); // Fixed argument order
        }

        [Test]
        public async Task GetTotalDataUsage_ReturnsAllEntries()
        {
            var entries = new List<(string, DataUsage)> {
                ("a", new DataUsage { Upload = 1, Download = 2 }),
                ("b", new DataUsage { Upload = 3, Download = 4 })
            };
            _cache.Setup(m => m.ForEach(It.IsAny<Func<string, DataUsage, bool>>()))
                  .Callback<Func<string, DataUsage, bool>>(cb => { foreach (var e in entries) cb(e.Item1, e.Item2); })
                  .Returns(Task.CompletedTask);
            var res = await _tracker.GetTotalDataUsage();

            // Replace the following line:  
            // Assert.AreEqual(2, res.Length);  

            // With this corrected line:  
            Assert.That(res.Length, Is.EqualTo(2));
            Assert.That(res.Any(e => e.key == "a"), Is.True);
            Assert.That(res.Any(e => e.key == "b"), Is.True);
        }

        [Test]
        public async Task ReportDataUsage_CallsMutateWithCorrectValues()
        {
            UsageReport rep = new UsageReport { IsUpload = true, Bytes = 100 };

            // Fix for CS8620: Use a lambda that explicitly handles nullable DataUsage
            _cache.Setup(m => m.MutateAsync(
                "127.0.0.1",
                It.IsAny<Func<DataUsage?, DataUsage>>(), // Adjusted to match the nullable signature
                false // Fix for CS0854: Explicitly pass the optional argument
            )).Returns(Task.CompletedTask).Verifiable();

            await _tracker.ReportDataUsage(rep, CreateContext("ipv4:127.0.0.1:9999"));
            _cache.Verify();
        }

        [Test]
        public async Task Publish_Succeeds_WithValidTransaction()
        {
            // start transaction
            var tr = await _tracker.StartTransaction(new TransactionRequest { ContainerGuid = Guid.NewGuid().ToString() }, CreateContext());
            var guid = tr.TransactionGuid;
            var objs = new List<PublishedObject> {
                new PublishedObject { TransactionGuid = guid, Object = new ObjectWithHash { Hash = ByteString.CopyFromUtf8("h") }, IsRoot = true }
            };
            _fs.Setup(m => m.CreateObjectContainer(It.IsAny<ObjectWithHash[]>(), It.IsAny<ByteString>(), It.IsAny<Guid>()))
               .ReturnsAsync(Guid.NewGuid()) // Fix: Ensure the return type matches Task<Guid>
               .Verifiable();
            var reader = new TestAsyncStreamReader<PublishedObject>(objs);
            await _tracker.Publish(reader, CreateContext());
            _fs.Verify();
            
        }

        [Test]
        public void Publish_ThrowsCanceled_WithInvalidTransaction()
        {
            var reader = new TestAsyncStreamReader<PublishedObject>(new[] {
                new PublishedObject { TransactionGuid = Guid.NewGuid().ToString() }
            });
            Assert.ThrowsAsync<RpcException>(() => _tracker.Publish(reader, CreateContext()));
        }

        // Helpers for capturing server-stream writes
        private class TestStreamWriter<T> : IServerStreamWriter<T>
        {
            private readonly IList<T> _list;
            public TestStreamWriter(IList<T> list) => _list = list;
            public WriteOptions WriteOptions { get; set; }
            public Task WriteAsync(T message)
            {
                _list.Add(message);
                return Task.CompletedTask;
            }
        }

        private class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
        {
            private readonly IEnumerator<T> _items;
            public TestAsyncStreamReader(IEnumerable<T> items) => _items = items.GetEnumerator();
            public T Current => _items.Current;
            public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(_items.MoveNext());
            public void Dispose() => _items.Dispose();
        }
        private static ServerCallContext CreateContext(string peer = "ipv4:127.0.0.1:5000")
        {
            return Grpc.Core.Testing.TestServerCallContext.Create(
                method: "testMethod",
                host: null,
                deadline: DateTime.UtcNow.AddSeconds(30),
                requestHeaders: null,
                cancellationToken: CancellationToken.None,
                peer: peer,
                authContext: null,
                contextPropagationToken: null,
                writeHeadersFunc: null,
                writeOptionsGetter: null,
                writeOptionsSetter: null
            );
        }
    }
}
