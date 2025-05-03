using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace node
{
    public class TaskProcessor : IAsyncDisposable, IDisposable
    {
        private readonly Channel<Func<CancellationToken, Task>> channel;
        private readonly SemaphoreSlim semaphore;
        private readonly CancellationTokenSource cts = new();
        private readonly Task cmdLoop;
        private bool disposedValue;
        private readonly List<Task> tasks = [];

        public TaskProcessor(int maxConcurrency, int boundedCapacity = 100000)
        {
            channel = Channel.CreateBounded<Func<CancellationToken, Task>>(new BoundedChannelOptions(boundedCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

            semaphore = new SemaphoreSlim(maxConcurrency);
            cmdLoop = Task.Run(ProcessQueueAsync);
        }

        public async Task AddAsync(Func<CancellationToken, Task> taskFunc)
        {
            await channel.Writer.WriteAsync(taskFunc);
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var taskFunc in channel.Reader.ReadAllAsync(cts.Token))
                {
                    await semaphore.WaitAsync(cts.Token);
                    try
                    {
                        tasks.Add(taskFunc(CancellationToken.None));
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(tasks);
            await cts.CancelAsync();
            await cmdLoop.WaitAsync(CancellationToken.None);
            semaphore.Dispose();
            cts.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
#pragma warning disable VSTHRD002
                    Task.WhenAll(tasks).Wait();
                    cts.Cancel();
                    cmdLoop.Wait();
                    semaphore.Dispose();
                    cts.Dispose();
#pragma warning restore VSTHRD002
                }

                disposedValue = true;
            }
        }

        ~TaskProcessor()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
