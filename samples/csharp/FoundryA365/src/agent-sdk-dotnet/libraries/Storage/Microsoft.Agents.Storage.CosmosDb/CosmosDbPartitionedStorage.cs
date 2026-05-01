// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage.Telemetry.Scopes;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage.CosmosDb
{
    /// <summary>
    /// Implements an CosmosDB based storage provider using partitioning for an Agent.
    /// </summary>
    public class CosmosDbPartitionedStorage : IStorage, IDisposable
    {
        private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = ProtocolJsonSerializer.SerializationOptions;
        private readonly JsonSerializerOptions _serializerOptions;

        private Container _container;
        private readonly CosmosDbPartitionedStorageOptions _cosmosDbStorageOptions;
        private CosmosClient _client;
        private bool _compatibilityModePartitionKey;

        // To detect redundant calls to dispose
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.CosmosDb.CosmosDbPartitionedStorage"/> class.
        /// using the provided CosmosDB credentials, database ID, and container ID.
        /// </summary>
        /// <param name="jsonSerializerOptions"></param>
        /// <param name="cosmosDbStorageOptions">Cosmos DB partitioned storage configuration options.</param>
        public CosmosDbPartitionedStorage(CosmosDbPartitionedStorageOptions cosmosDbStorageOptions, JsonSerializerOptions jsonSerializerOptions = null)
        {
            ValidateOptions(cosmosDbStorageOptions);
            _cosmosDbStorageOptions = cosmosDbStorageOptions;
            _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.CosmosDb.CosmosDbPartitionedStorage"/> class.
        /// using the provided CosmosDB credentials, database ID, and collection ID.
        /// </summary>
        /// <param name="client">The custom implementation of CosmosClient.</param>
        /// <param name="cosmosDbStorageOptions">Cosmos DB partitioned storage configuration options.</param>
        /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
        public CosmosDbPartitionedStorage(CosmosClient client, CosmosDbPartitionedStorageOptions cosmosDbStorageOptions, JsonSerializerOptions jsonSerializerOptions = default)
        {
            ValidateOptions(cosmosDbStorageOptions, validateClientRelated: client == null);
            _client = client;
            _cosmosDbStorageOptions = cosmosDbStorageOptions;
            _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            if (keys.Length == 0)
            {
                // No keys passed in, no result to return.
                return new Dictionary<string, object>();
            }

            // Ensure Initialization has been run
            await InitializeAsync().ConfigureAwait(false);

            var storeItems = new Dictionary<string, object>(keys.Length);

            using var telemetryScope = new ScopeRead(keys.Length);

            foreach (var key in keys)
            {
                try
                {
                    var escapedKey = CosmosDbKeyEscape.EscapeKey(key, _cosmosDbStorageOptions.KeySuffix, _cosmosDbStorageOptions.CompatibilityMode);

                    var readItemResponse = await _container.ReadItemAsync<DocumentStoreItem>(
                            escapedKey,
                            GetPartitionKey(escapedKey),
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    var documentStoreItem = readItemResponse.Resource;
                    var document = documentStoreItem.Document;

                    // deserialize to type
                    object item = null;
                    if (document.GetTypeInfo(out var type))
                    {
                        var typeProps = document.RemoveTypeInfoProperties();
                        item = document.Deserialize(type, _serializerOptions);
                        document.SetTypeInfoProperties(typeProps);
                    }
                    else
                    {
                        item = document.Deserialize<object>(_serializerOptions);
                    }

                    // if item == null at this point, we received unexpected content
                    if (item == null)
                    {
                        throw new InvalidDataException("Unexpected response content.  Unable to deserialize.");
                    }

                    if (item is IStoreItem storeItem)
                    {
                        storeItem.ETag = documentStoreItem.ETag;
                        storeItems.Add(documentStoreItem.RealId, storeItem);
                    }
                    else
                    {
                        storeItems.Add(documentStoreItem.RealId, item);
                    }
                }
                catch (CosmosException exception)
                {
                    // When an item is not found a CosmosException is thrown, but we want to
                    // return an empty collection so in this instance we catch and do not rethrow.
                    // Throw for any other exception.
                    if (exception.StatusCode == HttpStatusCode.NotFound)
                    {
                        break;
                    }

                    throw;
                }
            }

            return storeItems;
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
                // Nothing to write is a no-op.
                return;
            }

            using var telemetryScope = new ScopeWrite(changes.Count);

            // Ensure Initialization has been run
            await InitializeAsync().ConfigureAwait(false);

            foreach (var change in changes)
            {
                // This is the object being stored, as a JsonObject
                var document = JsonObject.Create(JsonSerializer.SerializeToElement(change.Value, _serializerOptions));

                // Remove etag from JSON object that was copied from IStoreItem.
                // The ETag information is updated as an _etag attribute in the document metadata.
                document.Remove("eTag");

                // Retain type info
                document.AddTypeInfo(change.Value);

                // The actual object is being wrapped in a DocumentStoreItem
                var documentChange = new DocumentStoreItem
                {
                    Id = CosmosDbKeyEscape.EscapeKey(change.Key, _cosmosDbStorageOptions.KeySuffix, _cosmosDbStorageOptions.CompatibilityMode),
                    RealId = change.Key,
                    Document = document,
                };

                var etag = (change.Value as IStoreItem)?.ETag;

                // Store per eTag rules
                if (etag == null || etag == "*")
                {
                    // if new item or * then insert or replace unconditionally
                    await _container.UpsertItemAsync(
                            documentChange,
                            GetPartitionKey(documentChange.PartitionKey),
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (etag.Length > 0)
                {
                    // if we have an etag, do opt. concurrency replace
                    await _container.UpsertItemAsync(
                            documentChange,
                            GetPartitionKey(documentChange.PartitionKey),
                            new ItemRequestOptions() { IfMatchEtag = etag, },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    throw new ArgumentException("etag empty");
                }
            }
        }

        //<inheritdoc/>
        public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            Dictionary<string, object> changesAsObject = new Dictionary<string, object>(changes.Count);
            foreach (var change in changes)
            {
                changesAsObject.Add(change.Key, change.Value);
            }
            return WriteAsync(changesAsObject, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            if (keys.Length == 0)
            {
                // Nothing to delete is a no-op.
                return;
            }

            using var telemetryScope = new ScopeDelete(keys.Length);

            await InitializeAsync().ConfigureAwait(false);

            foreach (var key in keys)
            {
                var escapedKey = CosmosDbKeyEscape.EscapeKey(key, _cosmosDbStorageOptions.KeySuffix, _cosmosDbStorageOptions.CompatibilityMode);

                try
                {
                    await _container.DeleteItemAsync<DocumentStoreItem>(
                            partitionKey: GetPartitionKey(escapedKey),
                            id: escapedKey,
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (CosmosException exception)
                {
                    // If we get a 404 status then the item we tried to delete was not found
                    // To maintain consistency with other storage providers, we ignore this and return.
                    // Any other exceptions are thrown.
                    if (exception.StatusCode == HttpStatusCode.NotFound)
                    {
                        return;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Disposes the object instance and releases any related objects owned by the class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes objects used by the class.
        /// </summary>
        /// <param name="disposing">A Boolean that indicates whether the method call comes from a Dispose method (its value is true) or from a finalizer (its value is false).</param>
        /// <remarks>
        /// The disposing parameter should be false when called from a finalizer, and true when called from the IDisposable.Dispose method.
        /// In other words, it is true when deterministically called and false when non-deterministically called.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed objects owned by the class here.
                _client?.Dispose();
            }

            _disposed = true;
        }

        private PartitionKey GetPartitionKey(string key)
        {
            if (_compatibilityModePartitionKey)
            {
                return PartitionKey.None;
            }

            return new PartitionKey(key);
        }

        /// <summary>
        /// Connects to the CosmosDB database and creates / gets the container.
        /// </summary>
        private async Task InitializeAsync()
        {
            if (_container == null)
            {
                var cosmosClientOptions = _cosmosDbStorageOptions.CosmosClientOptions ?? new CosmosClientOptions();
                cosmosClientOptions.Serializer = new CosmosJsonSerializer(_serializerOptions);

                if (_client == null)
                {
                    var assemblyName = this.GetType().Assembly.GetName();
                    cosmosClientOptions.ApplicationName = string.Concat(assemblyName.Name, " ", assemblyName.Version.ToString());

                    if (_cosmosDbStorageOptions.TokenCredential != null)
                    {
                        _client = new CosmosClient(
                            _cosmosDbStorageOptions.CosmosDbEndpoint,
                            _cosmosDbStorageOptions.TokenCredential,
                            cosmosClientOptions
                        );
                    }
                    else
                    {
                        _client = new CosmosClient(
                            _cosmosDbStorageOptions.CosmosDbEndpoint,
                            _cosmosDbStorageOptions.AuthKey,
                            cosmosClientOptions
                        );
                    }
                }

                if (_container == null)
                {
                    if (!_cosmosDbStorageOptions.CompatibilityMode)
                    {
                        await CreateContainerIfNotExistsAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        try
                        {
                            _container = _client.GetContainer(_cosmosDbStorageOptions.DatabaseId, _cosmosDbStorageOptions.ContainerId);

                            // This will throw if the container does not exist. 
                            var readContainer = await _container.ReadContainerAsync().ConfigureAwait(false);

                            // Containers created with CosmosDbStorage had no partition key set, so the default was '/_partitionKey'.
                            var partitionKeyPath = readContainer.Resource.PartitionKeyPath;
                            if (partitionKeyPath == "/_partitionKey")
                            {
                                _compatibilityModePartitionKey = true;
                            }
                            else if (partitionKeyPath != DocumentStoreItem.PartitionKeyPath)
                            {
                                // We are not supporting custom Partition Key Paths
                                throw new InvalidOperationException($"Custom Partition Key Paths are not supported. {_cosmosDbStorageOptions.ContainerId} has a custom Partition Key Path of {partitionKeyPath}.");
                            }
                        }
                        catch (CosmosException)
                        {
                            await CreateContainerIfNotExistsAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private static void ValidateOptions(CosmosDbPartitionedStorageOptions cosmosDbStorageOptions, bool validateClientRelated = true)
        {
            AssertionHelpers.ThrowIfNull(cosmosDbStorageOptions, nameof(cosmosDbStorageOptions));

            if (validateClientRelated && cosmosDbStorageOptions.CosmosDbEndpoint == null)
            {
                throw new ArgumentException($"Service EndPoint for CosmosDB is required.", nameof(cosmosDbStorageOptions));
            }

            if (validateClientRelated && string.IsNullOrEmpty(cosmosDbStorageOptions.AuthKey) && cosmosDbStorageOptions.TokenCredential == null)
            {
                throw new ArgumentException("AuthKey or TokenCredential for CosmosDB is required.", nameof(cosmosDbStorageOptions));
            }

            if (string.IsNullOrEmpty(cosmosDbStorageOptions.DatabaseId))
            {
                throw new ArgumentException("DatabaseId is required.", nameof(cosmosDbStorageOptions));
            }

            if (string.IsNullOrEmpty(cosmosDbStorageOptions.ContainerId))
            {
                throw new ArgumentException("ContainerId is required.", nameof(cosmosDbStorageOptions));
            }

            if (!string.IsNullOrWhiteSpace(cosmosDbStorageOptions.KeySuffix))
            {
                if (cosmosDbStorageOptions.CompatibilityMode)
                {
                    throw new ArgumentException($"CompatibilityMode cannot be 'true' while using a KeySuffix.", nameof(cosmosDbStorageOptions));
                }

                // In order to reduce key complexity, we do not allow invalid characters in a KeySuffix
                // If the KeySuffix has invalid characters, the EscapeKey will not match
                var suffixEscaped = CosmosDbKeyEscape.EscapeKey(cosmosDbStorageOptions.KeySuffix);
                if (!cosmosDbStorageOptions.KeySuffix.Equals(suffixEscaped, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Cannot use invalid Row Key characters: {cosmosDbStorageOptions.KeySuffix}", nameof(cosmosDbStorageOptions));
                }
            }
        }

        private async Task CreateContainerIfNotExistsAsync()
        {
            var containerResponse = await _client
                .GetDatabase(_cosmosDbStorageOptions.DatabaseId)
                .DefineContainer(_cosmosDbStorageOptions.ContainerId, DocumentStoreItem.PartitionKeyPath)
                .WithIndexingPolicy().WithAutomaticIndexing(false).WithIndexingMode(IndexingMode.None).Attach()
                .CreateIfNotExistsAsync(_cosmosDbStorageOptions.ContainerThroughput)
                .ConfigureAwait(false);

            _container = containerResponse.Container;
        }

        /// <summary>
        /// Azure Cosmos DB does not expose a default implementation of CosmosSerializer that is required to set the custom JSON serializer settings.
        /// To fix this, we have to create our own implementation.
        /// <remarks>
        /// See: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Serializer/CosmosJsonDotNetSerializer.cs
        /// </remarks>
        /// </summary>
        internal class CosmosJsonSerializer : CosmosSerializer
        {
            private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
            private readonly JsonSerializerOptions _serializerSettings;

            /// <summary>
            /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.CosmosDb.CosmosDbPartitionedStorage.CosmosJsonSerializer"/> class that uses the JSON.net serializer.
            /// </summary>
            /// <param name="jsonSerializerSettings">The JSON.net serializer.</param>
            public CosmosJsonSerializer(JsonSerializerOptions jsonSerializerSettings)
            {
                _serializerSettings = jsonSerializerSettings ??
                      throw new ArgumentNullException(nameof(jsonSerializerSettings));
            }

            /// <summary>
            /// Convert a Stream to the passed in type.
            /// </summary>
            /// <typeparam name="T">The type of object that should be deserialized.</typeparam>
            /// <param name="stream">An open stream that is readable that contains JSON.</param>
            /// <returns>The object representing the deserialized stream.</returns>
            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    using (var sr = new StreamReader(stream))
                    {
                        var json = sr.ReadToEnd();
                        var jsonObject = JsonObject.Parse(json);

                        return (T) jsonObject.Deserialize<T>(_serializerSettings);
                    }
                }
            }

            /// <summary>
            /// Converts an object to a open readable stream.
            /// </summary>
            /// <typeparam name="T">The type of object being serialized.</typeparam>
            /// <param name="input">The object to be serialized.</param>
            /// <returns>An open readable stream containing the JSON of the serialized object.</returns>
            public override Stream ToStream<T>(T input)
            {
                var streamPayload = new MemoryStream();
                
                JsonSerializer.Serialize(streamPayload, input, _serializerSettings);
                streamPayload.Seek(0, SeekOrigin.Begin);
                streamPayload.Position = 0;

                return streamPayload;
            }
        }

        /// <summary>
        /// Internal data structure for storing items in a CosmosDB Collection.
        /// </summary>
        internal class DocumentStoreItem : IStoreItem
        {
            /// <summary>
            /// Gets the PartitionKey path to be used for this document type.
            /// </summary>
            /// <value>
            /// The PartitionKey path to be used for this document type.
            /// </value>
            public static string PartitionKeyPath => "/id";

            /// <summary>
            /// Gets or sets the sanitized Id/Key used as PrimaryKey.
            /// </summary>
            /// <value>
            /// The sanitized Id/Key used as PrimaryKey.
            /// </value>
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets the un-sanitized Id/Key.
            /// </summary>
            /// <value>
            /// The un-sanitized Id/Key.
            /// </value>
            public string RealId { get; set; }

            /// <summary>
            /// Gets or sets the persisted object.
            /// </summary>
            /// <value>
            /// The persisted object.
            /// </value>
            public JsonObject Document { get; set; }

            /// <summary>
            /// Gets or sets the ETag information for handling optimistic concurrency updates.
            /// </summary>
            /// <value>
            /// The ETag information for handling optimistic concurrency updates.
            /// </value>
            [JsonPropertyName("_etag")]
            public string ETag { get; set; }

            /// <summary>
            /// Gets the PartitionKey value for the document.
            /// </summary>
            /// <value>
            /// The PartitionKey value for the document.
            /// </value>
            public string PartitionKey => this.Id;
        }
    }
}
