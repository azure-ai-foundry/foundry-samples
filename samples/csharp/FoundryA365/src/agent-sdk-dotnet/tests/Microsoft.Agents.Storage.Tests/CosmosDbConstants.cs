

using System;

namespace Microsoft.Agents.Storage.Tests
{
    public static class CosmosDbConstants
    {
        public const string CosmosServiceEndpoint = "https://localhost:8081";
        public const string CosmosAuthKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        public const string CosmosDatabaseName = "test-CosmosDbPartitionStorageTests";
        public const string CosmosCollectionName = "bot-storage";
        public static readonly string EmulatorPath = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe");
    }
}