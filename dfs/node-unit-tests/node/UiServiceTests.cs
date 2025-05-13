using Google.Protobuf;
using Grpc.Core;
using Moq;
using Newtonsoft.Json.Linq;
using node;
using Org.BouncyCastle.Asn1.Mozilla;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tests.Server.UnitTests.Helpers;

namespace unit_tests.node
{
    class UiServiceTests
    {
        private static readonly Bogus.Faker faker = new();
        UiService service;
        Mock<INodeState> mock = new Mock<INodeState>();
        TestServerCallContext ctx;
        [SetUp]
        public void Setup()
        {
            mock.Reset();
            service = new UiService(mock.Object, new Uri(faker.Internet.Url()));
            ctx = TestServerCallContext.Create();
        }

        [TearDown]
        public void Teardown()
        {
            mock.Reset();
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestShutdown_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            await service.Shutdown(new(), ctx);
            Assert.That(service.ShutdownEvent.IsSet, Is.True);
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestRevealLogFile_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            await service.RevealLogFile(new(), ctx);
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetObjectPath_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            var pathMock = new Mock<IFilePathHandler>();
            pathMock.Setup(self => self.GetPathAsync(It.IsAny<ByteString>())).Returns(Task.FromResult("hi"));
            mock.SetupGet(self => self.PathHandler).Returns(pathMock.Object);
            await service.GetObjectPath(new(), ctx);
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestRevealObjectInExplorer_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            await service.RevealObjectInExplorer(new(), ctx);
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetDownloadProgress_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.GetDownloadProgress(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetAllContainers_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.GetAllContainers(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetContainerObjects_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<FormatException>(async () => await service.GetContainerObjects(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestSearchForObjects_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<UriFormatException>(async () => await service.SearchForObjects(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetContainerRootHash_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.GetContainerRootHash(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestImportObjectToContainer_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<RpcException>(async () => await service.ImportObjectToContainer(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestImportObjectFromDisk_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<RpcException>(async () => await service.ImportObjectFromDisk(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestPauseFileDownload_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.PauseFileDownload(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestResumeFileDownload_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.ResumeFileDownload(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestDownloadContainer_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<UriFormatException>(async () => await service.DownloadContainer(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetDataUsage_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<UriFormatException>(async () => await service.GetDataUsage(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestLogMessage_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<ArgumentNullException>(async () => await service.LogMessage(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestGetBlockList_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.GetBlockList(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestModifyBlockListEntry_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.ModifyBlockListEntry(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestApplyFsOperation_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<NullReferenceException>(async () => await service.ApplyFsOperation(new(), ctx));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestPublishToTracker_WorksAsync(CancellationToken token)
        {
            ctx._cancellationToken = token;
            mock.SetupGet(self => self.PathHandler).Returns(new Mock<IFilePathHandler>().Object);
            Assert.ThrowsAsync<FormatException>(async () => await service.PublishToTracker(new(), ctx));
        }
    }
}
