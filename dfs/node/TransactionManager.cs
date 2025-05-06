using common;
using Fs;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
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
    public class TransactionManager
    {
        private readonly ILogger logger;

        public TransactionManager(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<Guid> PublishObjectsAsync(ITrackerWrapper client, Guid containerGuid, IEnumerable<ObjectWithHash> objects, ByteString rootHash, CancellationToken token)
        {
            Guid newGuid = Guid.Empty;

            try
            {
                newGuid = await RunTransactionAsync(client, containerGuid, objects, rootHash, token);
            }
            catch (Exception e)
            {
                logger.LogError(e, e.Message);
                throw;
            }

            if (newGuid == Guid.Empty)
            {
                throw new SystemException($"Failed to publish objects, received empty container GUID");
            }
            return newGuid;
        }

        private static async Task<Guid> RunTransactionAsync(ITrackerWrapper client, Guid containerGuid,
            IEnumerable<ObjectWithHash> objects, ByteString rootHash, CancellationToken token)
        {
            TransactionRequest request = new()
            {
                ContainerGuid = containerGuid.ToString()
            };

            var start = await client.StartTransaction(request, token);
            for (var i = 0; i < 5; i++)
            {
                if (start.State == TransactionState.Ok)
                {
                    break;
                }
                await Task.Delay(1000, token);
                start = await client.StartTransaction(request, token);
            }

            var actualGuid = Guid.Parse(start.ActualContainerGuid);

            if (start.State != TransactionState.Ok)
            {
                throw new Exception($"Failed to start transaction, received state: {start.State}");
            }

            _ = await client.Publish(
                objects.Select(o => new PublishedObject
                {
                    TransactionGuid = start.TransactionGuid,
                    Object = o,
                    IsRoot = o.Hash == rootHash
                })
                .ToList(), token);

            return actualGuid;
        }
    }
}
