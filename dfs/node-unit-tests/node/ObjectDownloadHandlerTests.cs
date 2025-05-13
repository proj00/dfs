using common;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unit_tests.mocks;

namespace unit_tests.node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, ChannelBase>;
    class ObjectDownloadHandlerTests
    {
        [Test]
        [CancelAfter(1000)]
        public async Task TestAddObjectFromDiskAsync(CancellationToken token)
        {
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMinutes(1),
                factory,
                new Mock<ILoggerFactory>().Object);
            var handler = new ObjectDownloadHandler
                (
                new MockFileSystem(),
                new Mock<ILogger>().Object,
                new Mock<IFilePathHandler>().Object,
                clientHandler,
                new Mock<IDownloadManager>().Object,
                new Mock<IAsyncIOWrapper>().Object,
                new Mock<IFilesystemManager>().Object,
                new Mock<INativeMethods>().Object
                );

            var res = await handler.AddObjectFromDiskAsync("path.txt", 1024);
        }
    }
}
