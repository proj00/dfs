using common;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using Org.BouncyCastle.Utilities;
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
        private static readonly Bogus.Faker faker = new();
        [Test]
        [CancelAfter(1000)]
        public async Task TestAddObjectFromDiskAsync(CancellationToken token)
        {
            var contents = faker.Random.Bytes(1024);
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMinutes(1),
                factory,
                new Mock<ILoggerFactory>().Object);

            var fs = new MockFileSystem(
                new Dictionary<string, MockFileData>()
                {
                    {"root_dir/subdir/file", new(contents) }
                },
                new MockFileSystemOptions() { CurrentDirectory = "C:/" });

            var handler = new ObjectDownloadHandler
                (
                fs,
                new Mock<ILogger>().Object,
                new Mock<IFilePathHandler>().Object,
                clientHandler,
                new Mock<IDownloadManager>().Object,
                new Mock<IAsyncIOWrapper>().Object,
                new Mock<IFilesystemManager>().Object,
                new Mock<INativeMethods>().Object
                );

            var (objectArray, rootHash) = await handler.AddObjectFromDiskAsync("./root_dir", 1024);
            var objects = objectArray.ToList();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(objects, Has.Count.EqualTo(3));
                var root = objects.Find(o => o.Object.Name == "root_dir");
                var subdir = objects.Find(o => o.Object.Name == "subdir");
                var file = objects.Find(o => o.Object.Name == "file");
                Assert.That(root, Is.Not.Null);
                Assert.That(subdir, Is.Not.Null);
                Assert.That(file, Is.Not.Null);

                Assert.That(root.Object.Directory.Entries, Does.Contain(subdir.Hash));
                Assert.That(subdir.Object.Directory.Entries, Does.Contain(file.Hash));
            }
        }
    }
}
