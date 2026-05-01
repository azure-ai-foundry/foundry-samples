using System;
using System.Diagnostics;
using Microsoft.Azure.Cosmos;

namespace Microsoft.Agents.Storage.Tests
{
    public class CosmosDbPartitionStorageFixture : IDisposable
    {
        public CosmosDbPartitionStorageFixture()
        {
            if (IgnoreOnNoEmulatorFact.HasEmulator.Value)
            {
                var client = new CosmosClient(
                    CosmosDbConstants.CosmosServiceEndpoint,
                    CosmosDbConstants.CosmosAuthKey,
                    new CosmosClientOptions());

                client.CreateDatabaseIfNotExistsAsync(CosmosDbConstants.CosmosDatabaseName);
            }
        }

        public async void Dispose()
        {
            if (IgnoreOnNoEmulatorFact.HasEmulator.Value)
            {
                var client = new CosmosClient(
                    CosmosDbConstants.CosmosServiceEndpoint,
                    CosmosDbConstants.CosmosAuthKey,
                    new CosmosClientOptions());
                try
                {
                    await client.GetDatabase(CosmosDbConstants.CosmosDatabaseName).DeleteAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error cleaning up resources: {0}", ex.ToString());
                }
            }
        }
    }
}