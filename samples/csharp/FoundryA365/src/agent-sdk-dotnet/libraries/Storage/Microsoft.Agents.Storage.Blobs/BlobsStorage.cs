// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage.Telemetry.Scopes;

namespace Microsoft.Agents.Storage.Blobs
{
    /// <summary>
    /// Implements <see cref="Microsoft.Agents.Storage.IStorage"/> using Azure Storage Blobs.
    /// </summary>
    /// <remarks>
    /// This class uses a single Azure Storage Blob Container.
    /// Each entity or <see cref="Microsoft.Agents.Storage.IStoreItem"/> is serialized into a JSON string and stored in an individual text blob.
    /// Each blob is named after the store item key,  which is encoded so that it conforms a valid blob name.
    /// If an entity is an <see cref="Microsoft.Agents.Storage.IStoreItem"/>, the storage object will set the entity's <see cref="Microsoft.Agents.Storage.IStoreItem.ETag"/>
    /// property value to the blob's ETag upon read. Afterward, an <see cref="Azure.Storage.Blobs.Models.BlobRequestConditions"/> with the ETag value
    /// will be generated during Write. New entities start with a null ETag.
    /// </remarks>
    public class BlobsStorage : IStorage
    {
        private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = ProtocolJsonSerializer.SerializationOptions;
        private readonly JsonSerializerOptions _serializerOptions;

        // If a JsonSerializer is not provided during construction, this will be the default JsonSerializer.
        private readonly BlobContainerClient _containerClient;
        private int _checkForContainerExistence;
        private readonly StorageTransferOptions _storageTransferOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Blobs.BlobsStorage"/> class.
        /// </summary>
        /// <param name="dataConnectionString">Azure Storage connection string.</param>
        /// <param name="containerName">Name of the Blob container where entities will be stored.</param>
        /// <param name="storageTransferOptions">Used for providing options for parallel transfers <see cref="Azure.Storage.StorageTransferOptions"/>.</param>
        /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
        public BlobsStorage(string dataConnectionString, string containerName, StorageTransferOptions storageTransferOptions = default, JsonSerializerOptions jsonSerializerOptions = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(dataConnectionString, nameof(dataConnectionString));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(containerName, nameof(containerName));
            _storageTransferOptions = storageTransferOptions;

            _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;

            // Triggers a check for the existence of the container
            _checkForContainerExistence = 1;

            _containerClient = new BlobContainerClient(dataConnectionString, containerName);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Blobs.BlobsStorage"/> class.
        /// </summary>
        /// <param name="blobContainerUri">Azure blob storage container Uri.</param>
        /// <param name="tokenCredential">The token credential to authenticate to the Azure storage.</param>
        /// <param name="storageTransferOptions">Used for providing options for parallel transfers <see cref="Azure.Storage.StorageTransferOptions"/>.</param>
        /// <param name="options">Client options that define the transport pipeline policies for authentication, retries, etc., that are applied to every request.</param>
        /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
        public BlobsStorage(Uri blobContainerUri, TokenCredential tokenCredential, StorageTransferOptions storageTransferOptions = default, BlobClientOptions options = default, JsonSerializerOptions jsonSerializerOptions = null)
        {
            AssertionHelpers.ThrowIfNull(blobContainerUri, nameof(blobContainerUri));
            AssertionHelpers.ThrowIfNull(tokenCredential, nameof(tokenCredential));

            _storageTransferOptions = storageTransferOptions;

            _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;

            // Triggers a check for the existence of the container
            _checkForContainerExistence = 1;

            _containerClient = new BlobContainerClient(blobContainerUri, tokenCredential, options);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.Blobs.BlobsStorage"/> class.
        /// </summary>
        /// <param name="containerClient">The custom implementation of BlobContainerClient.</param>
        /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
        /// <param name="storageTransferOptions">Used for providing options for parallel transfers <see cref="Azure.Storage.StorageTransferOptions"/>.</param>
        public BlobsStorage(BlobContainerClient containerClient, StorageTransferOptions storageTransferOptions = default, JsonSerializerOptions jsonSerializerOptions = null)
        {
            AssertionHelpers.ThrowIfNull(containerClient, nameof(containerClient));

            _containerClient = containerClient;
            _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
            _storageTransferOptions = storageTransferOptions;
            _checkForContainerExistence = 1;
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            using var telemetryScope = new ScopeDelete(keys.Length);

            foreach (var key in keys)
            {
                var blobName = GetBlobName(key);
                var blobClient = _containerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            using var telemetryScope = new ScopeRead(keys.Length);

            // this should only happen once - assuming this is a singleton
            if (Interlocked.CompareExchange(ref _checkForContainerExistence, 0, 1) == 1)
            {
                await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            var items = new Dictionary<string, object>();

            foreach (var key in keys)
            {
                var blobName = GetBlobName(key);
                var blobClient = _containerClient.GetBlobClient(blobName);
                try
                {
                    items.Add(key, await InnerReadBlobAsync(blobClient, cancellationToken).ConfigureAwait(false));
                }
                catch (RequestFailedException ex)
                    when ((HttpStatusCode)ex.Status == HttpStatusCode.NotFound)
                {
                    continue;
                }
                catch (AggregateException ex)
                    when (ex.InnerException is RequestFailedException iex
                    && (HttpStatusCode)iex.Status == HttpStatusCode.NotFound)
                {
                    continue;
                }
            }

            return items;
        }

        //<inheritdoc/>
        public async Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            var storeItems = await ReadAsync(keys, cancellationToken).ConfigureAwait(false);
            var values = new Dictionary<string, TStoreItem>(keys.Length);
            foreach (var entry in storeItems)
            {
                if (entry.Value is TStoreItem valueAsType)
                {
                    values.Add(entry.Key, valueAsType);
                }
            }
            return values;
        }

        /// <inheritdoc/>
        public async Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            if (changes.Count == 0)
            {
                // No-op for no changes.
                return;
            }

            using var telemetryScope = new ScopeWrite(changes.Count);

            // this should only happen once - assuming this is a singleton
            if (Interlocked.CompareExchange(ref _checkForContainerExistence, 0, 1) == 1)
            {
                await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            foreach (var keyValuePair in changes)
            {
                var newValue = keyValuePair.Value;
                var storeItem = newValue as IStoreItem;

                // "*" eTag in IStoreItem converts to null condition for AccessCondition
                var accessCondition = (!string.IsNullOrEmpty(storeItem?.ETag) && storeItem?.ETag != "*")
                    ? new BlobRequestConditions() { IfMatch = new ETag(storeItem?.ETag) }
                    : null;

                var blobName = GetBlobName(keyValuePair.Key);
                var blobReference = _containerClient.GetBlobClient(blobName);
                try
                {
                    var newState = newValue != null ? JsonObject.Create(JsonSerializer.SerializeToElement(newValue, _serializerOptions)) : null;

                    if (newState != null)
                    {
                        using var memoryStream = new MemoryStream();

                        // Retain type info
                        newState.AddTypeInfo(newValue);

                        JsonSerializer.Serialize(memoryStream, newState, _serializerOptions);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        var blobHttpHeaders = new BlobHttpHeaders();
                        blobHttpHeaders.ContentType = "application/json";

                        await blobReference.UploadAsync(memoryStream, conditions: accessCondition, transferOptions: _storageTransferOptions, cancellationToken: cancellationToken, httpHeaders: blobHttpHeaders).ConfigureAwait(false);
                    }
                }
                catch (RequestFailedException ex)
                when (ex.Status == (int)HttpStatusCode.BadRequest
                && ex.ErrorCode == BlobErrorCode.InvalidBlockList)
                {
                    throw new InvalidOperationException(
                        $"An error occurred while trying to write an object. The underlying '{BlobErrorCode.InvalidBlockList}' error is commonly caused due to concurrently uploading an object larger than 128MB in size.",
                        ex);
                }
                catch (RequestFailedException ex)
                when (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                {
                    throw new EtagException($"Etag conflict: {ex.Message}");
                }
            }
        }

        //<inheritdoc/>
        public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));
            
            Dictionary<string, object> changesAsObject = new(changes.Count);
            foreach (var change in changes)
            {
                changesAsObject.Add(change.Key, change.Value);
            }
            return WriteAsync(changesAsObject, cancellationToken);
        }

        private static string GetBlobName(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            return HttpUtility.UrlEncode(key);
        }

        private async Task<object> InnerReadBlobAsync(BlobClient blobReference, CancellationToken cancellationToken)
        {
            var i = 0;
            while (true)
            {
                try
                {
                    using BlobDownloadInfo download = await blobReference.DownloadAsync(cancellationToken).ConfigureAwait(false);
                    object item = null;

                    using (var sr = new StreamReader(download.Content))
                    {
                        var json = sr.ReadToEnd();
                        var jsonObject = (JsonObject)JsonObject.Parse(json);

                        if (jsonObject.GetTypeInfo(out var type))
                        {
                            var typeProps = jsonObject.RemoveTypeInfoProperties();
                            item = jsonObject.Deserialize(type, _serializerOptions);
                            jsonObject.SetTypeInfoProperties(typeProps);
                        }
                        else
                        {
                            item = jsonObject.Deserialize<object>(_serializerOptions);
                        }
                    }

                    // if item == null at this point, we received unexpected content
                    if (item == null)
                    {
                        throw new InvalidDataException("Unexpected response content.  Unable to deserialize.");
                    }

                    if (item is IStoreItem storeItem)
                    {
                        storeItem.ETag = (await blobReference.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))?.Value?.ETag.ToString();
                    }

                    return item;
                }
                catch (RequestFailedException ex)
                    when ((HttpStatusCode)ex.Status == HttpStatusCode.PreconditionFailed)
                {
                    // additional retry logic, even though this is a read operation blob storage can return 412 if there is contention
                    if (i++ < 8)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
