using common;
using Fs;
using Google.Protobuf;
using Microsoft.VisualStudio.Threading;
using RocksDbSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tracker;

namespace node
{
    class TransactionManager : System.IAsyncDisposable, IDisposable
    {
        private readonly TaskProcessor processor;
        private bool disposedValue;

        public TransactionManager()
        {
            processor = new TaskProcessor(1000);
        }

        public async Task<Guid> PublishObjectsAsync(ITrackerWrapper client, Guid containerGuid, IEnumerable<ObjectWithHash> objects, ByteString rootHash)
        {
            Guid newGuid = Guid.Empty;
            TransactionState state = TransactionState.Pending;
            AsyncManualResetEvent changedEvent = new(true);
            changedEvent.Reset();
            Action<Guid, TransactionState> registerGuid = (g, s) => { newGuid = g; state = s; changedEvent.Set(); };

            await processor.AddAsync((token) => RunTransactionAsync(client, containerGuid, registerGuid, objects, rootHash));
            await changedEvent.WaitAsync();
            if (state != TransactionState.Ok)
            {
                throw new InternalBufferOverflowException($"Failed to publish objects, received state: {state.ToString()}");
            }
            return newGuid;
        }

        private static async Task RunTransactionAsync(ITrackerWrapper client, Guid containerGuid,
            Action<Guid, TransactionState> registerGuid, IEnumerable<ObjectWithHash> objects, ByteString rootHash)
        {
            TransactionRequest request = new()
            {
                ContainerGuid = containerGuid.ToString()
            };

            var start = await client.StartTransaction(request, CancellationToken.None);
            for (var i = 0; i < 5; i++)
            {
                if (start.State == TransactionState.Ok)
                {
                    break;
                }
                await Task.Delay(1000);
                start = await client.StartTransaction(request, CancellationToken.None);
            }

            var actualGuid = Guid.Parse(start.ActualContainerGuid);

            if (start.State != TransactionState.Ok)
            {
                registerGuid(Guid.Empty, start.State);
                return;
            }

            try
            {
                _ = await client.Publish(
                    objects.Select(o => new PublishedObject
                    {
                        TransactionGuid = start.TransactionGuid,
                        Object = o,
                        IsRoot = o.Hash == rootHash
                    })
                    .ToList(), CancellationToken.None);
            }
            catch
            {
                registerGuid(Guid.Empty, TransactionState.Failed);
                return;
            }

            registerGuid(actualGuid, TransactionState.Ok);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    processor.Dispose();
                }
                disposedValue = true;
            }
        }

        ~TransactionManager()
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

        public async ValueTask DisposeAsync()
        {
            await ((System.IAsyncDisposable)processor).DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
