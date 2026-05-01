// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace TeamsConversationSsoQuickstart
{
    // This class is a wrapper for the Microsoft Graph API
    // See: https://developer.microsoft.com/en-us/graph
    public class SimpleGraphClient
    {
        private readonly string _token;

        public SimpleGraphClient(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            _token = token;
        }

        // Sends an email on the users behalf using the Microsoft Graph API
        public async Task SendMailAsync(string toAddress, string subject, string content)
        {
            if (string.IsNullOrWhiteSpace(toAddress))
            {
                throw new ArgumentNullException(nameof(toAddress));
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentNullException(nameof(content));
            }

            var graphClient = GetAuthenticatedClient();
            var recipients = new List<Recipient>
            {
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = toAddress,
                    },
                },
            };

            // Create the message.
            var email = new Message
            {
                Body = new ItemBody
                {
                    Content = content,
                    ContentType = BodyType.Text,
                },
                Subject = subject,
                ToRecipients = recipients,
            };

            // Send the message.
            await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody() { Message = email, SaveToSentItems = true });
        }

        // Gets mail for the user using the Microsoft Graph API
        public async Task<Message[]> GetRecentMailAsync()
        {
            var graphClient = GetAuthenticatedClient();
            var messages = await graphClient.Me.Messages.GetAsync();
            return [.. messages.Value.Take(5)];
        }

        // Get information about the user.
        public async Task<User> GetMeAsync()
        {
            var graphClient = GetAuthenticatedClient();
            var me = await graphClient.Me.GetAsync();
            return me;
        }

        // Gets the user's photo
        public async Task<string> GetPhotoAsync()
        {
            var graphClient = GetAuthenticatedClient();
            var photo = await graphClient.Me.Photo.Content.GetAsync();
            if (photo != null)
            {
                MemoryStream ms = new MemoryStream();
                photo.CopyTo(ms);
                byte[] buffers = ms.ToArray();
                string imgDataURL = string.Format("data:image/png;base64,{0}", Convert.ToBase64String(buffers));
                return imgDataURL;
            }
            else
            {
                return "";
            }
        }

        // Get an Authenticated Microsoft Graph client using the token issued to the user.
        private GraphServiceClient GetAuthenticatedClient()
        {
            var authProvider = new ManualTokenAuthenticationProvider(() =>
            {
                return Task.FromResult(_token);
            });

            var graphClient = new GraphServiceClient(authProvider);

            return graphClient;
        }
    }

    class ManualTokenAuthenticationProvider(Func<Task<string>> AccessTokenProvider) : IAuthenticationProvider
    {
        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            var accessToken = await AccessTokenProvider();
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }
    }
}
