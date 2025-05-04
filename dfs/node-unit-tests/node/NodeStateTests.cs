using common;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.node
{
    class NodeStateTests
    {
        private static Bogus.Faker faker = new();
        private readonly ConcurrentDictionary<string, string> whitelistDict = new();
        private readonly ConcurrentDictionary<string, string> blacklistDict = new();
        private Mock<IPersistentCache<string, string>> whitelist = new();
        private Mock<IPersistentCache<string, string>> blacklist = new();

        [TearDown]
        public void TearDown()
        {
            whitelist.Reset();
            blacklist.Reset();
            whitelistDict.Clear();
            blacklistDict.Clear();
        }

        [SetUp]
        public void SetUp()
        {
            whitelistDict.Clear();
            blacklistDict.Clear();
            whitelist = MockPersistentCache.CreateMock(whitelistDict);
            blacklist = MockPersistentCache.CreateMock(blacklistDict);
        }

        [Test]
        public async Task TestBlockListInsertionAsync()
        {
            whitelist
                .Setup(self => self.ContainsKey(It.IsAny<string>()));
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist.Object,
                blacklist.Object
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = "0.0.0.0/24"
            };
            await state.FixBlockListAsync(request);

            var entries = await state.GetBlockListAsync();

            Assert.That(entries.Entries, Has.Count.EqualTo(1));
            Assert.That(entries.Entries[0].InWhitelist, Is.EqualTo(request.InWhitelist));
            Assert.That(entries.Entries[0].Url, Is.EqualTo(request.Url));
        }

        [Test]
        public void TestBlockListInvalidInsert()
        {
            whitelist
                .Setup(self => self.ContainsKey(It.IsAny<string>()));
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist.Object,
                blacklist.Object
            );

            Assert.ThrowsAsync<FormatException>(async () => await state.FixBlockListAsync(new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = faker.Internet.IpAddress().ToString()
            }));
        }
    }
}
