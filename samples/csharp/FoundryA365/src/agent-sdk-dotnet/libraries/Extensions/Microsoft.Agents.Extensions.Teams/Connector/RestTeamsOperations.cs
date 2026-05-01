// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Connector.RestClients;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// TeamsOperations operations.
    /// </summary>
    internal class RestTeamsOperations(IRestTransport transport) : ITeamsOperations
    {
        private static volatile RetryParams currentRetryPolicy;
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        /// <summary>
        /// Gets a reference to the TeamsConnectorClient.
        /// </summary>
        /// <value>The TeamsConnectorClient.</value>
        public RestTeamsConnectorClient Client { get; internal set; }

        /// <inheritdoc/>
        public async Task<ConversationList> FetchChannelListAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(teamId, nameof(teamId));

            // Construct URL
            var url = "v3/teams/{teamId}/conversations";
            url = url.Replace("{teamId}", Uri.EscapeDataString(teamId));

            return await GetResponseAsync<ConversationList>("FetchChannelList", url, HttpMethod.Get, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TeamDetails> FetchTeamDetailsAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(teamId, nameof(teamId));

            // Construct URL
            var url = "v3/teams/{teamId}";
            url = url.Replace("{teamId}", Uri.EscapeDataString(teamId));

            return await GetResponseAsync<TeamDetails>("FetchTeamDetails", url, HttpMethod.Get, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MeetingInfo> FetchMeetingInfoAsync(string meetingId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(meetingId, nameof(meetingId));

            // Construct URL
            var url = "v1/meetings/{meetingId}";
            url = url.Replace("{meetingId}", System.Uri.EscapeDataString(meetingId));

            return await GetResponseAsync<MeetingInfo>("FetchMeetingInfo", url, HttpMethod.Get, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<TeamsMeetingParticipant> FetchParticipantAsync(string meetingId, string participantId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(meetingId, nameof(meetingId));
            AssertionHelpers.ThrowIfNull(participantId, nameof(participantId));
            AssertionHelpers.ThrowIfNull(tenantId, nameof(tenantId));

            // Construct URL
            var url = "v1/meetings/{meetingId}/participants/{participantId}?tenantId={tenantId}";
            url = url.Replace("{meetingId}", System.Uri.EscapeDataString(meetingId));
            url = url.Replace("{participantId}", System.Uri.EscapeDataString(participantId));
            url = url.Replace("{tenantId}", System.Uri.EscapeDataString(tenantId));

            return await GetResponseAsync<TeamsMeetingParticipant>("FetchParticipant", url, HttpMethod.Get, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false); 
        }

        /// <inheritdoc/>
        public async Task<MeetingNotificationResponse> SendMeetingNotificationAsync(string meetingId, MeetingNotificationBase notification, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(meetingId, nameof(meetingId));

            // Construct URL
            var url = "v1/meetings/{meetingId}/notification";
            url = url.Replace("{meetingId}", Uri.EscapeDataString(meetingId));

            return await GetResponseAsync<MeetingNotificationResponse>("SendMeetingNotification", url, HttpMethod.Post, body: notification, customHeaders: customHeaders, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToListOfUsersAsync(IActivity activity, List<TeamMember> teamsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(tenantId, nameof(tenantId));

            if (teamsMembers.Count == 0)
            {
                throw new ArgumentNullException(nameof(teamsMembers));
            }

            var content = new
            {
                Members = teamsMembers,
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/users/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToListOfUsers", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToAllUsersInTenantAsync(IActivity activity, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNullOrEmpty(tenantId, nameof(tenantId));

            var content = new
            {
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/tenant/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 miliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToAllUsersInTenant", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToAllUsersInTeamAsync(IActivity activity, string teamId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNullOrEmpty(teamId, nameof(teamId));
            AssertionHelpers.ThrowIfNullOrEmpty(tenantId, nameof(tenantId));

            var content = new
            {
                Activity = activity,
                TeamId = teamId,
                TenantId = tenantId,
            };
     
            var apiUrl = "v3/batch/conversation/team/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 milliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToAllUsersInTeam", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<string> SendMessageToListOfChannelsAsync(IActivity activity, List<TeamMember> channelsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(tenantId, nameof(tenantId));

            if (channelsMembers?.Count == 0)
            {
                throw new ArgumentNullException(nameof(channelsMembers));
            }

            var content = new
            {
                Members = channelsMembers,
                Activity = activity,
                TenantId = tenantId,
            };

            var apiUrl = "v3/batch/conversation/channels/";

            // In case of throttling, it will retry the operation with default values (10 retries every 50 milliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<string>("SendMessageToListOfChannels", apiUrl, HttpMethod.Post, body: content, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<BatchOperationState> GetOperationStateAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(operationId, nameof(operationId));

            var apiUrl = "v3/batch/conversation/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 milliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchOperationState>("GetOperationState", apiUrl, HttpMethod.Get, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task<BatchFailedEntriesResponse> GetPagedFailedEntriesAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(operationId, nameof(operationId));

            var apiUrl = "v3/batch/conversation/failedentries/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 milliseconds).
            var result = await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchFailedEntriesResponse>("GetPagedFailedEntries", apiUrl, HttpMethod.Get, continuationToken: continuationToken, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);

            return result;
        }

        /// <inheritdoc/>
        public async Task CancelOperationAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(operationId, nameof(operationId));

            var apiUrl = "v3/batch/conversation/{operationId}";
            apiUrl = apiUrl.Replace("{operationId}", Uri.EscapeDataString(operationId));

            // In case of throttling, it will retry the operation with default values (10 retries every 50 milliseconds).
            await RetryAction.RunAsync(
                task: () => GetResponseAsync<BatchOperationState>("CancelOperation", apiUrl, HttpMethod.Delete, customHeaders: customHeaders, cancellationToken: cancellationToken),
                retryExceptionHandler: (ex, ct) => HandleThrottlingException(ex, ct)).ConfigureAwait(false);
        }

        private static RetryParams HandleThrottlingException(Exception ex, int currentRetryCount)
        {
            if (ex is ThrottleException throttleException)
            {
                return throttleException.RetryParams ?? RetryParams.DefaultBackOff(currentRetryCount);
            }
            else
            {
                return RetryParams.StopRetrying;
            }
        }

        private async Task<T> GetResponseAsync<T>(string operationName, string apiUrl, HttpMethod httpMethod, object body = null, string continuationToken = null, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage
            {
                Method = httpMethod,
                RequestUri = new Uri(_transport.Endpoint, apiUrl)
                    .AppendQuery("continuationToken", continuationToken)
            };
            request.Headers.Add("Accept", "application/json");

            if (body != null)
            {
                var json = ProtocolJsonSerializer.ToJson(body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            if (customHeaders != null)
            {
                foreach (var customHeader in customHeaders)
                {
                    if (request.Headers.Contains(customHeader.Key))
                    {
                        request.Headers.Remove(customHeader.Key);
                    }

                    request.Headers.Add(customHeader.Key, customHeader.ToString());
                }
            }

            try
            {
                using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
                using var httpResponse = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                switch ((int)httpResponse.StatusCode)
                {
                    case 200:
                    case 201:
                    case 202:
                    case 207:
                        {
                            if (httpResponse.Content != null)
                            {
#if !NETSTANDARD
                                var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                                var responseContent = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                                if (!string.IsNullOrEmpty(responseContent))
                                {
                                    if (typeof(T) == typeof(string))
                                    {
                                        return ProtocolJsonSerializer.ToObject<T>(responseContent);
                                    }
#if !NETSTANDARD
                                    return ProtocolJsonSerializer.ToObject<T>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                                    return ProtocolJsonSerializer.ToObject<T>(responseContent);
#endif
                                }
                            }
                            return default;
                        }
                    case 429:
                        {
                            throw new ThrottleException() { RetryParams = currentRetryPolicy };
                        }
                    default:
                        {
                            var ex = new ErrorResponseException($"{operationName} operation returned an invalid status code '{httpResponse.StatusCode}'");
                            try
                            {
#if !NETSTANDARD
                                ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                                ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                                if (errorBody != null)
                                {
                                    ex.Body = errorBody;
                                }
                            }
                            catch (JsonException)
                            {
                                // Ignore the exception
                            }
                            throw ex;
                        }
                }
            }
            finally
            {
                // This means the request was successful. We can make our retry policy null.
                if (currentRetryPolicy != null)
                {
                    currentRetryPolicy = null;
                }
            }
        }
    }
}
