// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Connector
{
    /// <summary>
    /// TeamsOperations operations.
    /// </summary>
    public interface ITeamsOperations
    {
        /// <summary>
        /// Fetches channel list for a given team.
        /// </summary>
        /// <param name='teamId'>
        /// Team Id.
        /// </param>
        /// <param name='customHeaders'>
        /// The headers that will be added to request.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <returns>The channel list for a given team.</returns>
        Task<ConversationList> FetchChannelListAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches details related to a team.
        /// </summary>
        /// <param name='teamId'>
        /// Team Id.
        /// </param>
        /// <param name='customHeaders'>
        /// The headers that will be added to request.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <returns>The details related to a team.</returns>
        Task<TeamDetails> FetchTeamDetailsAsync(string teamId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches Teams meeting participant details.
        /// </summary>
        /// <remarks>
        /// Fetches details for a meeting participant.
        /// </remarks>
        /// <param name='meetingId'>
        /// Teams meeting id.
        /// </param>
        /// <param name='participantId'>
        /// Teams meeting participant id.
        /// </param>
        /// <param name='tenantId'>
        /// Teams meeting tenant id.
        /// </param>
        /// <param name='customHeaders'>
        /// Headers that will be added to request.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when a required parameter is null.
        /// </exception>
        /// <returns>
        /// A response object containing the response body and response headers.
        /// </returns>
        Task<TeamsMeetingParticipant> FetchParticipantAsync(string meetingId, string participantId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches details related to a meeting.
        /// </summary>
        /// <param name='meetingId'>
        /// Meeting Id, encoded as a BASE64 string.
        /// </param>
        /// <param name='customHeaders'>
        /// Headers that will be added to request.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when a required parameter is null.
        /// </exception>
        /// <returns>
        /// A response object containing the response body and response headers.
        /// </returns>
        Task<MeetingInfo> FetchMeetingInfoAsync(string meetingId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a teams meeting notification.
        /// </summary>
        /// <remarks>
        /// Send a notification to teams meeting participants.
        /// </remarks>
        /// <param name='meetingId'>
        /// Teams meeting id.
        /// </param>
        /// <param name='notification'>
        /// Teams notification object.
        /// </param>
        /// <param name='customHeaders'>
        /// Headers that will be added to request.
        /// </param>
        /// <param name='cancellationToken'>
        /// The cancellation token.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown when a required parameter is null.
        /// </exception>
        /// <returns>
        /// A response object containing the response body and response headers.
        /// </returns>
        Task<MeetingNotificationResponse> SendMeetingNotificationAsync(string meetingId, MeetingNotificationBase notification, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a message to a list of Teams members.
        /// </summary>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="teamsMembers"> The list of members. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token.  </param>
        /// <returns>
        /// A response object containing the operation id.
        /// </returns>
        Task<string> SendMessageToListOfUsersAsync(IActivity activity, List<TeamMember> teamsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a message to all the users in a tenant.
        /// </summary>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token.  </param>
        /// <returns>
        /// A response object containing the operation id.
        /// </returns>
        Task<string> SendMessageToAllUsersInTenantAsync(IActivity activity, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a message to all the users in a team.
        /// </summary>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="teamId"> The team ID. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token.  </param>
        /// <returns>
        /// A response object containing the operation id.
        /// </returns>
        Task<string> SendMessageToAllUsersInTeamAsync(IActivity activity, string teamId, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a message to a list of Teams channels.
        /// </summary>
        /// <param name="activity"> The activity to send. </param>
        /// <param name="channelsMembers"> The list of channels. </param>
        /// <param name="tenantId"> The tenant ID. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token.  </param>
        /// <returns>
        /// A response object containing the operation id.
        /// </returns>
        Task<string> SendMessageToListOfChannelsAsync(IActivity activity, List<TeamMember> channelsMembers, string tenantId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the state of an operation.
        /// </summary>
        /// <param name="operationId"> The operationId to get the state of. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        /// <returns>
        /// A response object containing the state and responses of the operation.
        /// </returns>
        Task<BatchOperationState> GetOperationStateAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the failed entries of a batch operation with error code and message.
        /// </summary>
        /// <param name="operationId"> The operationId to get the failed entries of. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name="continuationToken"> The continuation token. </param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        /// <returns>
        /// A response object containing the state and responses of the operation.
        /// </returns>
        Task<BatchFailedEntriesResponse> GetPagedFailedEntriesAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, string continuationToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels a batch operation by its id.
        /// </summary>
        /// <param name="operationId"> The id of the operation to cancel. </param>
        /// <param name="customHeaders"> Headers that will be added to request. </param>
        /// <param name='cancellationToken'> The cancellation token. </param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.
        /// </returns>
        Task CancelOperationAsync(string operationId, Dictionary<string, List<string>> customHeaders = null, CancellationToken cancellationToken = default);
    }
}
