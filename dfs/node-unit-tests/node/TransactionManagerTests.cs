using common;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using System.Threading.Tasks;
using Tracker;
using unit_tests.mocks;

namespace unit_tests.node;

public class TransactionManagerTests
{
    private static Bogus.Faker faker = new();

    [Test]
    [CancelAfter(100)]
    public void TestPublishObjects_InvalidTracker_Throws(CancellationToken token)
    {
        var manager = new TransactionManager(new Mock<ILogger>().Object);
        var client = new Mock<ITrackerWrapper>();
        var guid = Guid.NewGuid();
        var obj = MockFsUtils.GenerateObject(faker);

        Assert.ThrowsAsync<NullReferenceException>(async () =>
            await manager.PublishObjectsAsync(client.Object, guid, [obj], obj.Hash, token));
    }

    [Test]
    [CancelAfter(200)]
    public async Task TestPublishObjects_TransactionSucceedsAsync(CancellationToken token)
    {
        var manager = new TransactionManager(new Mock<ILogger>().Object);
        var client = new Mock<ITrackerWrapper>();
        var response = new TransactionStartResponse()
        {
            ActualContainerGuid = Guid.NewGuid().ToString(),
            State = TransactionState.Ok,
            TransactionGuid = Guid.NewGuid().ToString(),
            TtlMs = 500
        };

        client
            .Setup(self => self.StartTransaction(It.IsAny<TransactionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(response));
        client
            .Setup(self => self.Publish(It.IsAny<IReadOnlyList<PublishedObject>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new RpcCommon.Empty()));

        var guid = Guid.NewGuid();
        var obj = MockFsUtils.GenerateObject(faker);

        var newGuid = await manager.PublishObjectsAsync(client.Object, guid, [obj], obj.Hash, token);
    }
}
