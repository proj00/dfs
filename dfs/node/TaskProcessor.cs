using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace node
{
    public class TaskProcessor : IDisposable
    {
        public ActionBlock<Func<Task>> block { get; }
        private readonly CancellationTokenSource cts = new();
        private bool disposedValue;

        public TaskProcessor(int maxConcurrency, int boundedCapacity = 100000)
        {
            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                BoundedCapacity = boundedCapacity,
                CancellationToken = cts.Token
            };

            block = new ActionBlock<Func<Task>>(
                async taskFunc =>
                {
                    await taskFunc();
                },
                options
            );
        }

        public async Task<bool> AddAsync(Func<Task> taskFunc)
        {
            return await block.SendAsync(taskFunc);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    block.Complete();
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    block.Completion.Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                    cts.Cancel();
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
