using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Moq;
using node;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using unit_tests.mocks;

namespace unit_tests.node
{
    using GrpcChannelFactory = Func<Uri, GrpcChannelOptions, ChannelBase>;
    class GrpcClientHandlerTests
    {
        [Test]
        public void TestGetNodeClient_ChannelCached()
        {
            int callCount = 0;
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                callCount++;
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMinutes(1),
                factory,
                new Mock<ILoggerFactory>().Object);

            GrpcChannelOptions options = new GrpcChannelOptions();
            int tries = 5;
            for (int i = 0; i < tries; i++)
            {
                var client = clientHandler.GetNodeClient(new Uri("http://test.test"), i % 2 == 0 ? options : null);
                Assert.That(client, Is.Not.Null);
            }

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void TestGetTrackerWrapper_ChannelCached()
        {
            int callCount = 0;
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                callCount++;
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMinutes(1),
                factory,
                new Mock<ILoggerFactory>().Object);

            GrpcChannelOptions options = new GrpcChannelOptions();
            int tries = 5;
            for (int i = 0; i < tries; i++)
            {
                var client = clientHandler.GetTrackerWrapper(new Uri("http://test.test"));
                Assert.That(client, Is.Not.Null);
            }

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void TestGetClients_ChannelNotCached_ClientsCreated()
        {
            int callCount = 0;
            GrpcChannelFactory factory = (Uri uri, GrpcChannelOptions opt) =>
            {
                callCount++;
                return new MockChannel(uri.ToString());
            };

            using var clientHandler = new GrpcClientHandler(TimeSpan.FromMicroseconds(1),
                factory,
                new Mock<ILoggerFactory>().Object);

            GrpcChannelOptions options = new GrpcChannelOptions();
            int tries = 5;
            for (int i = 0; i < tries; i++)
            {
                var wrapper = clientHandler.GetTrackerWrapper(new Uri("http://test.test"));
                Assert.That(wrapper, Is.Not.Null);
                var client = clientHandler.GetNodeClient(new Uri("http://test.test"));
                Assert.That(client, Is.Not.Null);
            }

            Assert.That(callCount, Is.EqualTo(10));
        }
    }
}
