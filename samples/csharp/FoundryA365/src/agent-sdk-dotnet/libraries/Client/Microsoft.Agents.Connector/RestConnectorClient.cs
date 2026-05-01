// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Core;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// The Connector REST API allows your Agent to send and receive messages to channels configured in the Azure Bot Service.
    /// The Connector service uses industry-standard REST and JSON over HTTPS.
    /// </summary>
    public class RestConnectorClient : RestClientBase, IConnectorClient
    {
        public IAttachments Attachments { get; }

        public IConversations Conversations { get; }

        public Uri BaseUri => base.Endpoint;

        public RestConnectorClient(Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestConnectorClient), int? maxApxConversationIdLength = null)
            : base(endpoint, httpClientFactory, namedClient, tokenProviderFunction)
        {
            AssertionHelpers.ThrowIfNull(endpoint, nameof(endpoint));
            AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));

            Conversations = new ConversationsRestClient(this);
            if (maxApxConversationIdLength.HasValue)
            {
                // Cast this here as I do not want to expose MaxApxConversationIdLength on the interface.
                ((ConversationsRestClient)Conversations).MaxApxConversationIdLength = maxApxConversationIdLength.Value;
            }

            Attachments = new AttachmentsRestClient(this);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
