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
    }
}
