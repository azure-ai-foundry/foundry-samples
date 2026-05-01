// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Storage.Blobs;
using Moq;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class BlobsStorageTests
    {
        private const string ConnectionString = @"UseDevelopmentStorage=true";

        private BlobsStorage _storage;
        private readonly Mock<BlobClient> _client = new Mock<BlobClient>();
        
        [Fact]
        public void ConstructorValidation()
        {
            // Should work.
            _ = new BlobsStorage(
                ConnectionString,
                "containerName",
                jsonSerializerOptions: new JsonSerializerOptions());

            // No dataConnectionString. Should throw.
            Assert.Throws<ArgumentNullException>(() => new BlobsStorage(null, "containerName"));
            Assert.Throws<ArgumentException>(() => new BlobsStorage(string.Empty, "containerName"));

            // No containerName. Should throw.
            Assert.Throws<ArgumentNullException>(() => new BlobsStorage(ConnectionString, null));
            Assert.Throws<ArgumentException>(() => new BlobsStorage(ConnectionString, string.Empty));
        }

        [Fact]
        public void ConstructorWithTokenCredentialValidation()
        {
            var mockTokenCredential = new Moq.Mock<TokenCredential>();
            var storageTransferOptions = new StorageTransferOptions();
            var uri = new Uri("https://uritest.com");

            // Should work.
            _ = new BlobsStorage(
                uri,
                mockTokenCredential.Object,
                storageTransferOptions,
                new BlobClientOptions(),
                new JsonSerializerOptions());

            // No blobContainerUri. Should throw.
            Assert.Throws<ArgumentNullException>(() => new BlobsStorage(null, mockTokenCredential.Object, storageTransferOptions));

            // No tokenCredential. Should throw.
            Assert.Throws<ArgumentNullException>(() => new BlobsStorage(uri, null, storageTransferOptions));
        }

        [Fact]
        public async Task WriteAsyncValidation()
        {
            InitStorage();

            // No changes. Should throw.
            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.WriteAsync(null));
        }

        [Fact]
        public async Task WriteAsync()
        {
            InitStorage();

            _client.Setup(e => e.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobHttpHeaders>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<AccessTier?>(),
                It.IsAny<StorageTransferOptions>(),
                It.IsAny<CancellationToken>()));

            var changes = new Dictionary<string, object>
            {
                { "key1", new StoreItem() },
                { "key2", new StoreItem { ETag = "*" } },
                //{ "key3", new StoreItem { ETag = "ETag" } },
                //{ "key4", new List<StoreItem>() { new StoreItem() } },
                //{ "key5", new Dictionary<string, StoreItem>() { { "key1", new StoreItem() } } },
                //{ "key6", "value1" },
            };

            await _storage.WriteAsync(changes);

            _client.Verify(
                e => e.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<BlobHttpHeaders>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<IProgress<long>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<StorageTransferOptions>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task WriteAsyncHttpBadRequestFailure()
        {
            InitStorage();

            _client.Setup(e => e.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<BlobHttpHeaders>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<IProgress<long>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<StorageTransferOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.BadRequest, "error", BlobErrorCode.InvalidBlockList.ToString(), null));

            var changes = new Dictionary<string, object> { { "key", new StoreItem() } };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.WriteAsync(changes));
        }

        [Fact]
        public async Task WriteAsyncHttpPreconditionFailure()
        {
            InitStorage();

            _client.Setup(e => e.UploadAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<BlobHttpHeaders>(),
                    It.IsAny<IDictionary<string, string>>(),
                    It.IsAny<BlobRequestConditions>(),
                    It.IsAny<IProgress<long>>(),
                    It.IsAny<AccessTier?>(),
                    It.IsAny<StorageTransferOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.PreconditionFailed, "PreconditionFailed error"));

            var changes = new Dictionary<string, object> { { "key", new StoreItem() } };

            await Assert.ThrowsAsync<EtagException>(() => _storage.WriteAsync(changes));
        }

        [Fact]
        public async Task WriteAsync_ShouldThrowOnNullStoreItemChanges()
        {
            InitStorage();

            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.WriteAsync<StoreItem>(null, CancellationToken.None));
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

            _client.Setup(e => e.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()));

            await _storage.DeleteAsync(new string[] { "key" });

            _client.Verify(e => e.DeleteIfExistsAsync(It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
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

            Stream stream = new MemoryStream(Encoding.ASCII.GetBytes("{\"ETag\":\"*\", \"$type\": \"Microsoft.Agents.Storage.Tests.StoreItem\", \"$typeAssembly\": \"Microsoft.Agents.Storage.Tests\"}"));
            var blobDownloadInfo = BlobsModelFactory.BlobDownloadInfo(content: stream);
            var response = new Mock<Response<BlobDownloadInfo>>();

            response.SetupGet(e => e.Value).Returns(blobDownloadInfo);

            _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);

            var items = await _storage.ReadAsync(new string[] { "key" });

            Assert.Single(items);
            Assert.IsAssignableFrom<StoreItem>(items["key"]);
            _client.Verify(e => e.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReadAsyncHttpPreconditionFailure()
        {
            InitStorage();

            Stream stream = new MemoryStream(Encoding.ASCII.GetBytes("{\"ETag\":\"*\"}"));
            var blobDownloadInfo = BlobsModelFactory.BlobDownloadInfo(content: stream);
            var response = new Mock<Response<BlobDownloadInfo>>();

            response.SetupGet(e => e.Value).Returns(blobDownloadInfo);

            _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.PreconditionFailed, "error"))
                .Callback(() =>
                {
                    // Break the retry process.
                    _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(response.Object);
                });

            var items = await _storage.ReadAsync(new string[] { "key" });

            Assert.Single(items);
            //Assert.Equal("*", JObject.FromObject(items).GetValue("key")?.Value<string>("ETag"));
            _client.Verify(e => e.DownloadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ReadAsyncHttpNotFoundFailure()
        {
            InitStorage();

            // RequestFailedException => NotFound
            var requestFailedException = new RequestFailedException((int)HttpStatusCode.NotFound, "error");
            _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(requestFailedException)
                .Callback(() =>
                {
                    // AggregateException => RequestFailedException => NotFound
                    _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new AggregateException(requestFailedException));
                });

            var items = await _storage.ReadAsync(new string[] { "key1", "key2" });

            Assert.Empty(items);
            _client.Verify(e => e.DownloadAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ReadAsync_ShouldReturnWrittenStoreItem()
        {
            InitStorage();

            var key = "key1";

            var changes = new Dictionary<string, StoreItem>
            {
                { key, new StoreItem() }
            };

            await _storage.WriteAsync(changes, CancellationToken.None);

            Stream stream = new MemoryStream(Encoding.ASCII.GetBytes("{\"ETag\":\"*\", \"$type\": \"Microsoft.Agents.Storage.Tests.StoreItem\", \"$typeAssembly\": \"Microsoft.Agents.Storage.Tests\"}"));
            var blobDownloadInfo = BlobsModelFactory.BlobDownloadInfo(content: stream);
            var response = new Mock<Response<BlobDownloadInfo>>();

            response.SetupGet(e => e.Value).Returns(blobDownloadInfo);
            _client.Setup(e => e.DownloadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(response.Object);

            var readStoreItems = new Dictionary<string, StoreItem>(await _storage.ReadAsync<StoreItem>([key], CancellationToken.None));

            Assert.Single(readStoreItems);
            Assert.IsAssignableFrom<StoreItem>(readStoreItems[key]);
            _client.Verify(e => e.DownloadAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetBlobNameValidation()
        {
            InitStorage();

            // Empty keys. Should throw.
            await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.DeleteAsync(new string[] { string.Empty }));
        }

        private void InitStorage(JsonSerializerOptions jsonSerializerSettings = default)
        {
            var container = new Mock<BlobContainerClient>();
            var jsonSerializer = jsonSerializerSettings;

            container.Setup(e => e.GetBlobClient(It.IsAny<string>()))
                .Returns(_client.Object);
            container.Setup(e => e.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()));

            _storage = new BlobsStorage(container.Object, jsonSerializerOptions: jsonSerializer);
        }
    }

    public class StoreItem : IStoreItem
    {
        public int Id { get; set; } = 0;

        public string Topic { get; set; } = "car";

        public string ETag { get; set; }
    }
}
