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
using Grpc.Core.Testing;



namespace unit_tests.tracker
{
    class TrackerTests : Tracker.Tracker.TrackerBase, IDisposable
    {
        private Mock<IFilesystemManager> _fs;
        private Mock<IPersistentCache<string, DataUsage>> _cache;
        private Mock<ILogger> _logger;
        private CancellationTokenSource _cts;
        private TrackerRpc _tracker;

        [SetUp]
        public void SetUp()
        {
            _fs = new Mock<IFilesystemManager>();
            _cache = new Mock<IPersistentCache<string, DataUsage>>();
            _logger = new Mock<ILogger>();
            _cts = new CancellationTokenSource();
            _tracker = new TrackerRpc(
                _logger.Object,
                _fs.Object,
                _cache.Object,
                _cts);
        }

        [Test]
        public void Constructor_NullArgs_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TrackerRpc(null!, _fs.Object, _cache.Object, _cts));
            Assert.Throws<ArgumentNullException>(() => new TrackerRpc(_logger.Object, null!, _cache.Object, _cts));
            Assert.Throws<ArgumentNullException>(() => new TrackerRpc(_logger.Object, _fs.Object, null!, _cts));
            Assert.Throws<ArgumentNullException>(() => new TrackerRpc(_logger.Object, _fs.Object, _cache.Object, null!));
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
            // Update the TestServerCallContext.Create calls to remove the 'responseTrailers' parameter, as it is not part of the method's signature.

            var context = TestServerCallContext.Create(
               method: "GetPeerList",
               host: null,
               deadline: DateTime.UtcNow.AddSeconds(30),
               requestHeaders: null,
               cancellationToken: CancellationToken.None,
               peer: "ipv4:127.0.0.1:5000",
               authContext: null,
               contextPropagationToken: null,
               writeOptionsGetter: null,
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
            var ctx1 = TestServerCallContext.Create(
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
                writeOptionsSetter: null
            );
            await _tracker.MarkReachable(mk, ctx1);

            var responses = new List<PeerResponse>();
            var writer = new TestStreamWriter<PeerResponse>(responses);
            var ctx2 = TestServerCallContext.Create(
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
                writeOptionsSetter: null
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
            // Update the TestServerCallContext.Create calls to match the correct number of arguments
            var ctx = TestServerCallContext.Create(
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
                writeOptionsSetter: null
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
            var ctx = TestServerCallContext.Create("sd", null, DateTime.UtcNow.AddSeconds(30),
                null, CancellationToken.None, "", null, null, null, null, null, null);

            // Act
            await _tracker.Shutdown(new Empty(), ctx);

            // Assert
            Assert.That(_cts.IsCancellationRequested, Is.True);
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
    }
}
