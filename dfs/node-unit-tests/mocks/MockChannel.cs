using Grpc.Core;
using Grpc.Net.Client;

namespace unit_tests.mocks
{
    /// <summary>
    /// only for testing client constructors
    /// </summary>
    public class MockChannel : ChannelBase
    {
        public MockChannel(string target) : base(target)
        {
        }

        public override CallInvoker CreateCallInvoker()
        {
            return new MockCallInvoker();
        }
    }
}
