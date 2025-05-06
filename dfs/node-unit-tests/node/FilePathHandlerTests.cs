using common;
using Google.Protobuf;
using Moq;
using node;
using System.Configuration;

namespace unit_tests.node
{
    public class FilePathHandlerTests
    {

        [Test]
        public void TestFilePathHandler_Disposed()
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();

            var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { });

            handler.Dispose();

            // second dispose should be ignored
            handler.Dispose();

            cache.Verify(self => self.Dispose(), Times.Once());
        }

        [Test]
        public void TestRevealFile_FileOpened()
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();
            int startCount = 0;

            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { Assert.That(a, Is.EqualTo("explorer.exe")); startCount++; });

            handler.RevealFile(Path.Combine("C:", "dir", "text.txt"));

            Assert.That(startCount, Is.EqualTo(1));
        }

        [TestCase("..")]
        [TestCase("<")]
        [TestCase(" : ")]
        [TestCase(".")]
        public void TestRevealFile_InvalidFile_Throws(string part)
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();
            int startCount = 0;

            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { Assert.That(a, Is.EqualTo("explorer.exe")); startCount++; });

            Assert.Throws<ArgumentException>(() => handler.RevealFile($"C:\\{part}\\test.txt"));
            Assert.That(startCount, Is.EqualTo(0));
        }

        [TestCase("..")]
        [TestCase("<")]
        [TestCase(" : ")]
        [TestCase(".")]
        public void TestRevealHash_InvalidFile_Throws(string part)
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult($"C:\\{part}\\test.txt"));
            int startCount = 0;

            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { Assert.That(a, Is.EqualTo("explorer.exe")); startCount++; });

            Assert.ThrowsAsync<ArgumentException>(async () => await handler.RevealHashAsync(ByteString.Empty));
            Assert.That(startCount, Is.EqualTo(0));
        }

        [Test]
        public async Task TestRevealHash_ReturnsAsync()
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();
            var path = $"C:\\test.txt";
            cache.Setup(self => self.GetAsync(It.IsAny<ByteString>())).Returns(Task.FromResult(path));
            int startCount = 0;
            var result = "";
            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { Assert.That(a, Is.EqualTo("explorer.exe")); startCount++; result = b; });

            await handler.RevealHashAsync(ByteString.Empty);
            Assert.That(path, Is.EqualTo(result));
        }

        [Test]
        public async Task TestSetPathAsync_ReturnsAsync()
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();

            var used = ByteString.CopyFrom([154]);
            var received = "";
            cache
                .Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<string>()))
                .Callback(
                (ByteString k, string v) =>
                {
                    used = k;
                    received = v;
                });

            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { });

            var path = $"C:\\test.txt";
            await handler.SetPathAsync(ByteString.Empty, path);

            Assert.That(path, Is.EqualTo(received));
            Assert.That(used, Is.EqualTo(ByteString.Empty));
        }

        [TestCase("")]
        [TestCase(".. ..")]
        [TestCase(" ")]
        [TestCase("C:sds")]
        [TestCase("C:\\sds.?")]
        public async Task TestSetPathAsync_InvalidPathThrowsAsync(string path)
        {
            var cache = new Mock<IPersistentCache<ByteString, string>>();
            var called = 0;
            cache
                .Setup(self => self.SetAsync(It.IsAny<ByteString>(), It.IsAny<string>()))
                .Callback(
                (ByteString k, string v) =>
                {
                    called++;
                });

            using var handler = new FilePathHandler(
                cache.Object,
                (string a, string b) => { });

            Assert.ThrowsAsync<ArgumentException>(async () => await handler.SetPathAsync(ByteString.Empty, path));
            Assert.That(called, Is.EqualTo(0));
        }
    }
}
