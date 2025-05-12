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
        private ActionBlock<Func<Task>> block { get; }
        private readonly CancellationTokenSource cts = new();
        private bool disposedValue;
        private readonly AtomicRefCount taskCounter;
        public TaskProcessor(AtomicRefCount taskCounter, int maxConcurrency, int boundedCapacity = 100000)
        {
            this.taskCounter = taskCounter;
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

        public async Task AddAsync(Func<Task> taskFunc)
        {
            taskCounter.Increment();
            bool good = await block.SendAsync(async () =>
            {
                try
                {
                    await taskFunc();
                }
                finally
                {
                    taskCounter.Decrement();
                }
            });
            if (!good)
            {
                taskCounter.Decrement();
                throw new OperationCanceledException("TaskProcessor::AddAsync failed, already full");
            }
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
