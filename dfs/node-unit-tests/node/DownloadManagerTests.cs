using common;
using common_test;
using Google.Protobuf;
using Moq;
using node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.node
{
    public class DownloadManagerTests
    {
        Mock<IPersistentCache<ByteString, Ui.Progress>> progress;
        Mock<IPersistentCache<ByteString, Node.FileChunk>> chunks;

        [SetUp]
        public void Setup()
        {
            progress = new();
            chunks = new();
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_ThrowsOnInvalidDbPath(string? path)
        {
            if (path == null)
                Assert.Throws<ArgumentNullException>(() => new DownloadManager(path, 1,
                    progress.Object,
                    chunks.Object
                    ));
            else
                Assert.Throws<ArgumentException>(() => new DownloadManager(path, 1,
                    progress.Object,
                    chunks.Object
                    ));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ThrowsOnInvalidTaskCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadManager("dir", capacity,
                progress.Object,
                chunks.Object
                    ));
        }

        [Test]
        public void Constructor_Returns()
        {
            Assert.DoesNotThrow(() => new DownloadManager
                (
                "dir",
                1,
                progress.Object,
                chunks.Object
                ));
        }
    }
}
