using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace node
{
    public class AtomicRefCount
    {
        private int count;
        private AsyncManualResetEvent resetEvent;
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
