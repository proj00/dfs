using common;
using Google.Protobuf;
using Microsoft;
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
        private MockPersistentCache<string, string> whitelist = new();
        private MockPersistentCache<string, string> blacklist = new();

        [TearDown]
        public void TearDown()
        {
            whitelist._dict.Clear();
            blacklist._dict.Clear();
        }

        [SetUp]
        public void SetUp()
        {
            whitelist = new();
            blacklist = new();
        }

        [Test]
        [Combinatorial]
        public async Task TestBlockListInsertionAsync([Values] bool inWhitelist)
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = inWhitelist,
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
        public async Task TestIsInBlockList_BothEmptyAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.False);
        }

        [Test]
        public async Task TestIsInBlockList_PassWhitelistAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = "0.0.0.0/0"
            };
            await state.FixBlockListAsync(request);

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.False);
        }

        [Test]
        public async Task TestIsInBlockList_PassBlocklistAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = false,
                ShouldRemove = false,
                Url = "192.168.0.1/32"
            };
            await state.FixBlockListAsync(request);

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.False);
        }

        [Test]
        public async Task TestIsInBlockList_PassAllAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = false,
                ShouldRemove = false,
                Url = "192.168.0.1/32"
            };
            await state.FixBlockListAsync(request);

            request = new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = "0.0.0.0/0"
            };
            await state.FixBlockListAsync(request);

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.False);
        }

        [Test]
        public async Task TestIsInBlockList_DontPassWhitelistAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = "192.168.0.1/32"
            };
            await state.FixBlockListAsync(request);

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.True);
        }

        [Test]
        [Combinatorial]
        public async Task TestIsInBlockList_DontPassBlacklistAsync([Values] bool useWhitelist)
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = false,
                ShouldRemove = false,
                Url = "127.0.0.1/32"
            };
            await state.FixBlockListAsync(request);

            request = new()
            {
                InWhitelist = useWhitelist,
                ShouldRemove = false,
                Url = "127.0.0.1/32"
            };
            await state.FixBlockListAsync(request);

            Assert.That(await state.IsInBlockListAsync(new Uri("http://127.0.0.1")), Is.True);
        }

        [Test]
        public async Task TestIsInBlockList_InvalidUrlAsync()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = true,
                ShouldRemove = false,
                Url = "0.0.0.0/24"
            };
            await state.FixBlockListAsync(request);

            Assert.ThrowsAsync<FormatException>(async () => await state.IsInBlockListAsync(new Uri("http://ghdghdghdgfhgf")));
        }

        [Test]
        [NonParallelizable]
        [Combinatorial]
        public async Task TestBlockListRemovalAsync([Values] bool inWhitelist)
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
            );

            Ui.BlockListRequest request = new()
            {
                InWhitelist = inWhitelist,
                ShouldRemove = false,
                Url = "0.0.0.0/24"
            };
            await state.FixBlockListAsync(request);

            var entries = await state.GetBlockListAsync();

            Assert.That(entries.Entries, Has.Count.EqualTo(1));
            Assert.That(entries.Entries[0].InWhitelist, Is.EqualTo(request.InWhitelist));
            Assert.That(entries.Entries[0].Url, Is.EqualTo(request.Url));

            request.ShouldRemove = true;
            await state.FixBlockListAsync(request);
            entries = await state.GetBlockListAsync();

            var mock = inWhitelist ? whitelist : blacklist;
            Assert.That(entries.Entries, Has.Count.EqualTo(0));
        }

        [Test]
        public void TestBlockListInvalidInsert()
        {
            using var state = new NodeState
            (
                new TimeSpan(),
                new Mock<ILoggerFactory>().Object,
                "",
                new Mock<IFilesystemManager>().Object,
                new Mock<IDownloadManager>().Object,
                new Mock<IPersistentCache<ByteString, string>>().Object,
                whitelist,
                blacklist
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
