// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Agents.Storage.CosmosDb;
using Moq;
using Xunit;
using static Microsoft.Agents.Storage.CosmosDb.CosmosDbPartitionedStorage;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Builder.Dialogs;

namespace Microsoft.Agents.Storage.Tests
{
    [Trait("TestCategory", "Storage")]
    [Trait("TestCategory", "Storage - CosmosDB Partitioned")]
    public class CosmosDbPartitionedStorageTests
    {
        private CosmosDbPartitionedStorage _storage;
        private readonly Mock<Container> _container = new Mock<Container>();
        
        [Fact]
        public void ConstructorValidation()
        {
            // Should work.
            _ = new CosmosDbPartitionedStorage(
                cosmosDbStorageOptions: new CosmosDbPartitionedStorageOptions
                {
                    CosmosDbEndpoint = "CosmosDbEndpoint",
                    AuthKey = "AuthKey",
                    DatabaseId = "DatabaseId",
                    ContainerId = "ContainerId",
                },
                jsonSerializerOptions: new JsonSerializerOptions());

            // No Options. Should throw.
            Assert.Throws<ArgumentNullException>(() => new CosmosDbPartitionedStorage(null));

            // No Endpoint. Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = null,
            }));

            // No Auth Key or TokenCredential. Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = null,
                TokenCredential = null
            }));

            // No Database Id. Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = "AuthKey",
                DatabaseId = null,
            }));

            // No Container Id. Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = "AuthKey",
                DatabaseId = "DatabaseId",
                ContainerId = null,
            }));

            // KeySuffix with CompatibilityMode == "true". Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = "AuthKey",
                DatabaseId = "DatabaseId",
                ContainerId = "ContainerId",
                KeySuffix = "KeySuffix",
                CompatibilityMode = true
            }));

            // KeySuffix with CompatibilityMode == "false" and invalid characters. Should throw.
            Assert.Throws<ArgumentException>(() => new CosmosDbPartitionedStorage(new CosmosDbPartitionedStorageOptions()
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = "AuthKey",
                DatabaseId = "DatabaseId",
                ContainerId = "ContainerId",
                KeySuffix = "?#*test",
                CompatibilityMode = false
            }));
        }
        
        [Fact]
        public async Task ReadAsyncValidation()
        {
            InitStorage();

            await StorageBaseTests.ReadValidation(_storage);
        }

        [Fact]
        public async Task ReadAsync()
        {
            InitStorage();

            var resource = new CosmosDbPartitionedStorage.DocumentStoreItem
            {
                RealId = "RealId",
                ETag = "ETag1",
                Document = JsonObject.Parse("{ \"ETag\":\"ETag2\" }").AsObject()
            };
            var itemResponse = new DocumentStoreItemResponseMock(resource);

            _container.Setup(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(itemResponse);

            var items = await _storage.ReadAsync(["key"]);

            Assert.Single(items);
            _container.Verify(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReadAsyncPartitionKey()
        {
            InitStorage("/_partitionKey");

            var resource = new CosmosDbPartitionedStorage.DocumentStoreItem
            {
                RealId = "RealId",
                ETag = "ETag1",
                Document = JsonObject.Parse("{ \"ETag\":\"ETag2\" }").AsObject()
            };
            var itemResponse = new DocumentStoreItemResponseMock(resource);

            _container.Setup(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(itemResponse);

            var items = await _storage.ReadAsync(["key"]);

            Assert.Single(items);
            _container.Verify(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReadAsyncNotFound()
        {
            InitStorage();

            _container.Setup(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("NotFound", HttpStatusCode.NotFound, 0, "0", 0));

            var items = await _storage.ReadAsync(["key"]);

            Assert.Empty(items);
            _container.Verify(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReadAsyncFailure()
        {
            InitStorage();

            _container.Setup(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("InternalServerError", HttpStatusCode.InternalServerError, 0, "0", 0));

            await Assert.ThrowsAsync<CosmosException>(() => _storage.ReadAsync(["key"]));
            _container.Verify(e => e.ReadItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReadAsyncCustomPartitionKeyFailure()
        {
            InitStorage("/customKey");

            await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.ReadAsync(["key"]));
        }

        [Fact]
        public async Task ReadAsync_ShouldReturnStoreItem()
        {
            InitStorage();

            var item = new StoreItem();
            var document = JsonObject.Parse(JsonSerializer.Serialize(item)).AsObject();
            document.AddTypeInfo(item);

            var resource = new DocumentStoreItem
            {
                RealId = "RealId",
                ETag = "ETag1",
                Document = document
            };
            var itemResponse = new DocumentStoreItemResponseMock(resource);

            _container.Setup(e => e.ReadItemAsync<DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(itemResponse);

            var items = await _storage.ReadAsync<StoreItem>(["key"]);

            Assert.Single(items);
            _container.Verify(e => e.ReadItemAsync<DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WriteAsyncValidation()
        {
            InitStorage();

            // No changes. Should throw.
            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.WriteAsync(null));

            // Empty changes. Should return.
            await _storage.WriteAsync(new Dictionary<string, object>());
        }

        [Fact]
        public async Task WriteAsync()
        {
            InitStorage();

            _container.Setup(e => e.UpsertItemAsync(It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()));

            var changes = new Dictionary<string, object>
            {
                { "key1", new CosmosDbPartitionedStorage.DocumentStoreItem() },
                { "key2", new CosmosDbPartitionedStorage.DocumentStoreItem { ETag = "*" } },
                { "key3", new CosmosDbPartitionedStorage.DocumentStoreItem { ETag = "ETag" } },
            };

            await _storage.WriteAsync(changes);

            _container.Verify(e => e.UpsertItemAsync(It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task WriteAsyncEmptyTagFailure()
        {
            InitStorage();

            var changes = new Dictionary<string, object>
            {
                { "key", new CosmosDbPartitionedStorage.DocumentStoreItem { ETag = string.Empty } },
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _storage.WriteAsync(changes));
        }

        [Fact]
        public async Task WriteAsyncFailure()
        {
            InitStorage();

            _container.Setup(e => e.UpsertItemAsync(It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("InternalServerError", HttpStatusCode.InternalServerError, 0, "0", 0));

            var changes = new Dictionary<string, object> { { "key", new CosmosDbPartitionedStorage.DocumentStoreItem() } };

            await Assert.ThrowsAsync<CosmosException>(() => _storage.WriteAsync(changes));
            _container.Verify(e => e.UpsertItemAsync(It.IsAny<CosmosDbPartitionedStorage.DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WriteAsync_ShouldCallUpsertItemAsync()
        {
            InitStorage();

            _container.Setup(e => e.UpsertItemAsync(It.IsAny<DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()));

            var changes = new Dictionary<string, DocumentStoreItem>
            {
                { "key1", new DocumentStoreItem() },
                { "key2", new DocumentStoreItem { ETag = "*" } },
                { "key3", new DocumentStoreItem { ETag = "ETag" } },
            };

            await _storage.WriteAsync(changes);

            _container.Verify(e => e.UpsertItemAsync(It.IsAny<DocumentStoreItem>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Fact]
        public async Task WriteAsync_ShouldThrowOnNullStoreItemChanges()
        {
            InitStorage();

            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.WriteAsync<DocumentStoreItem>(null, CancellationToken.None));
        }

        [Fact]
        public async Task WriteAsyncWithNestedFailure()
        {
            InitStorage();

            var nestedJson = GenerateNestedDict();

            await Assert.ThrowsAsync<JsonException>(() => _storage.WriteAsync(nestedJson));
        }

        [Fact]
        public async Task WriteAsyncWithNestedDialogFailure()
        {
            InitStorage();

            var nestedJson = GenerateNestedDict();

            var dialogInstance = new DialogInstance { State = nestedJson };
            var dialogState = new DialogState([dialogInstance]);
            var changes = new Dictionary<string, object> { { "state", dialogState } };

            await Assert.ThrowsAsync<JsonException>(() => _storage.WriteAsync(changes));
        }

        [Fact]
        public async Task DeleteAsyncValidation()
        {
            InitStorage();

            // No keys. Should throw.
            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.DeleteAsync(null));
        }

        [Fact]
        public async Task DeleteAsync()
        {
            InitStorage();

            _container.Setup(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()));

            await _storage.DeleteAsync(["key"]);

            _container.Verify(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsyncNotFound()
        {
            InitStorage();

            _container.Setup(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("NotFound", HttpStatusCode.NotFound, 0, "0", 0));

            await _storage.DeleteAsync(["key"]);

            _container.Verify(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsyncFailure()
        {
            InitStorage();

            _container.Setup(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("InternalServerError", HttpStatusCode.InternalServerError, 0, "0", 0));

            await Assert.ThrowsAsync<CosmosException>(() => _storage.DeleteAsync(["key"]));
            _container.Verify(e => e.DeleteItemAsync<CosmosDbPartitionedStorage.DocumentStoreItem>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private void InitStorage(string partitionKey = "/id", CosmosDbPartitionedStorageOptions storageOptions = default, JsonSerializerOptions jsonSerializerSettings = default)
        {
            var client = new Mock<CosmosClient>();
            var containerProperties = new ContainerProperties("id", partitionKey);
            var containerResponse = new Mock<ContainerResponse>();

            containerResponse.SetupGet(e => e.Resource)
                .Returns(containerProperties);
            client.Setup(e => e.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(_container.Object);
            _container.Setup(e => e.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerResponse.Object);

            var options = storageOptions ?? new CosmosDbPartitionedStorageOptions
            {
                CosmosDbEndpoint = "CosmosDbEndpoint",
                AuthKey = "AuthKey",
                DatabaseId = "DatabaseId",
                ContainerId = "ContainerId",
            };

            _storage = new CosmosDbPartitionedStorage(client.Object, options, jsonSerializerSettings);
        }

        private static Dictionary<string, object> GenerateNestedDict()
        {
            var nested = new Dictionary<string, object>();
            var current = new Dictionary<string, object>();

            nested.Add("0", current);
            for (var i = 1; i <= 127; i++)
            {
                var child = new Dictionary<string, object>();
                current.Add(i.ToString(), child);
                current = child;
            }

            return nested;
        }

        private class DocumentStoreItemResponseMock : ItemResponse<CosmosDbPartitionedStorage.DocumentStoreItem>
        {
            public DocumentStoreItemResponseMock(CosmosDbPartitionedStorage.DocumentStoreItem resource)
            {
                Resource = resource;
            }

            public override CosmosDbPartitionedStorage.DocumentStoreItem Resource { get; }
        }
        
        private class StoreItem : IStoreItem
        {
            public int Id { get; set; } = 0;

            public string Topic { get; set; } = "car";

            public string ETag { get; set; }
        }
    }
}
