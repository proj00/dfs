using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace common
{
    public sealed class AsyncLock : IDisposable
    {
        public void Dispose()
        {
            semaphore.Dispose();
        }

        private readonly SemaphoreSlim semaphore;
        public AsyncLock(int initialCount = 1, int maxCount = 1)
        {
            semaphore = new SemaphoreSlim(initialCount, maxCount);
        }

        public async Task<IDisposable> LockAsync(bool noLock = false)
        {
            if (!noLock)
            {
                await semaphore.WaitAsync();
            }
            return new Releaser(semaphore, noLock);
        }

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim semaphore;
            private readonly bool noLock;
            public Releaser(SemaphoreSlim semaphore, bool noLock)
            {
                this.semaphore = semaphore;
                this.noLock = noLock;
            }
            public void Dispose()
            {
                if (!noLock)
                {
                    semaphore.Release();
                }
            }
        }
    }
}
