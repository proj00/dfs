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
        private readonly Channel<Func<Task>> channel;
        private readonly SemaphoreSlim semaphore;
        private readonly CancellationTokenSource cts = new();
        private readonly Task cmdLoop;
        private bool disposedValue;

        public TaskProcessor(int maxConcurrency, int boundedCapacity = 100000)
        {
            channel = Channel.CreateBounded<Func<Task>>(new BoundedChannelOptions(boundedCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            semaphore = new SemaphoreSlim(maxConcurrency);
            cmdLoop = Task.Run(ProcessQueueAsync);
        }

        public async Task AddAsync(Func<Task> taskFunc)
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

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await taskFunc();
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Task error: {ex}");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync();
            channel.Writer.Complete();
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
                    cts.Cancel();
                    channel.Writer.Complete();
#pragma warning disable VSTHRD002
                    cmdLoop.Wait();
#pragma warning restore VSTHRD002
                    semaphore.Dispose();
                    cts.Dispose();
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
