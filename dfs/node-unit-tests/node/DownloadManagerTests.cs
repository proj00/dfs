using common_test;
using Google.Protobuf;
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
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Constructor_ThrowsOnInvalidDbPath(string? path)
        {
            if (path == null)
                Assert.Throws<ArgumentNullException>(() => new DownloadManager(path, 1,
                    new MockPersistentCache<ByteString, Ui.Progress>(),
                    new MockPersistentCache<ByteString, Node.FileChunk>()
                    ));
            else
                Assert.Throws<ArgumentException>(() => new DownloadManager(path, 1,
                    new MockPersistentCache<ByteString, Ui.Progress>(),
                    new MockPersistentCache<ByteString, Node.FileChunk>()
                    ));
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_ThrowsOnInvalidTaskCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownloadManager("dir", capacity,
                new MockPersistentCache<ByteString, Ui.Progress>(),
                new MockPersistentCache<ByteString, Node.FileChunk>()
                    ));
        }

        [Test]
        public void Constructor_Returns()
        {
            Assert.DoesNotThrow(() => new DownloadManager
                (
                "dir",
                1,
                new MockPersistentCache<ByteString, Ui.Progress>(),
                new MockPersistentCache<ByteString, Node.FileChunk>()
                ));
        }
    }
}
