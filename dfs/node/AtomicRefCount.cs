using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace node
{
    // an illusion of a synchronization primitive
    // count can be negative ;D
    public class AtomicRefCount
    {
        private int count;
        private readonly AsyncManualResetEvent resetEvent;
        public AtomicRefCount()
        {
            count = 0;
            resetEvent = new(true);
            resetEvent.Reset();
        }

        public void Increment()
        {
            Interlocked.Increment(ref count);
            resetEvent.Reset();
        }

        public void Decrement(Action? cleanupCallback = null)
        {
            Debug.Assert(count > 0); // non-atomic compare without Interlocked o_O
            if (Interlocked.Decrement(ref count) <= 0)
            {
                cleanupCallback?.Invoke();
                resetEvent.Set();
            }
        }

        public async Task WaitForZeroAsync(CancellationToken token)
        {
            await resetEvent.WaitAsync(token);
        }
    }
}
