using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace node
{
    public class TaskProcessor : IDisposable
    {
        private readonly Channel<Func<Task>> channel;
        private readonly SemaphoreSlim semaphore;
        private readonly CancellationTokenSource cts = new();
        private readonly Task cmdLoop;

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

        public void Dispose()
        {
            cts.Cancel();
            channel.Writer.Complete();
            cmdLoop.Wait();
            semaphore.Dispose();
            cts.Dispose();
        }
    }
}
