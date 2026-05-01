// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Microsoft.Agents.Connector.RestClients
{
    /// <summary>
    /// Fluent builder for <see cref="System.Net.Http.HttpRequestMessage"/>.
    /// Replaces the Create*Request() helper methods scattered across REST clients.
    /// </summary>
    internal sealed class RestRequest
    {
        private readonly HttpMethod _method;
        private readonly string _path;
        private object _body;
        private readonly List<(string name, string value, bool escape)> _queryParams = [];

        private RestRequest(HttpMethod method, string path)
        {
            _method = method;
            _path = path;
        }

        public static RestRequest Get(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Get, path);
        }

        public static RestRequest Post(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Post, path);
        }

        public static RestRequest Put(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Put, path);
        }

        public static RestRequest Delete(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Delete, path);
        }

        /// <summary>Sets the JSON request body.</summary>
        public RestRequest WithBody<T>(T body)
        {
            _body = body;
            return this;
        }

        /// <summary>Appends a query parameter. Null values are omitted.</summary>
        public RestRequest WithQuery(string name, string value, bool escape = true)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));
            _queryParams.Add((name, value, escape));
            return this;
        }

        /// <summary>
        /// Builds the <see cref="System.Net.Http.HttpRequestMessage"/>.
        /// The caller is responsible for disposing the returned message.
        /// - URI is resolved relative to <paramref name="baseUri"/> (trailing slash is ensured).
        /// - Accept: application/json is added.
        /// - Body (if set) is serialized as application/json using ProtocolJsonSerializer.
        /// Auth headers are NOT added here; those come from IRestTransport.GetHttpClientAsync().
        /// </summary>
        public HttpRequestMessage Build(Uri baseUri)
        {
            AssertionHelpers.ThrowIfNull(baseUri, nameof(baseUri));

            var uri = new Uri(baseUri.EnsureTrailingSlash(), _path);
            foreach (var (name, value, escape) in _queryParams)
            {
                uri = uri.AppendQuery(name, value, escape);
            }

            var message = new HttpRequestMessage(_method, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (_body != null)
            {
                message.Content = new StringContent(
                    ProtocolJsonSerializer.ToJson(_body),
                    Encoding.UTF8,
                    "application/json");
            }

            return message;
        }
    }
}
