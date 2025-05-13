using common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace unit_tests.common
{
    class AsyncLockTests
    {
        [Test]
        [CancelAfter(1000)]
        public async Task TestLock_WorksAsync(CancellationToken token)
        {
            bool scopeEntered = false;
            using var @lock = new AsyncLock();
            using (await @lock.LockAsync(token))
            {
                scopeEntered = true;
            }

            Assert.That(scopeEntered, Is.True);
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestLock_Cancelled_ThrowsAsync(CancellationToken token)
        {
            using var @lock = new AsyncLock();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            Assert.ThrowsAsync<TaskCanceledException>(async () => await @lock.LockAsync(cts.Token));
        }

        [Test]
        [CancelAfter(1000)]
        public async Task TestLock_WorksRecursiveAsync(CancellationToken token)
        {
            bool scopeEntered = false;
            bool internalScopeEntered = false;
            using var @lock = new AsyncLock();
            using (await @lock.LockAsync(token))
            {
                scopeEntered = true;
                using (await @lock.LockAsync(token, true))
                {
                    internalScopeEntered = true;
                }
            }

            Assert.That(scopeEntered, Is.True);
            Assert.That(internalScopeEntered, Is.True);
        }
    }
}
