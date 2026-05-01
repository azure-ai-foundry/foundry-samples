// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

internal class A2AResponseHandler : IChannelResponseHandler
{
    private const string SseTemplate = "event: {0}\r\ndata: {1}\r\n\r\n";
    private readonly ITaskStore _taskStore;
    private readonly JsonRpcId _requestId;
    private readonly ILogger _logger;
    private readonly bool _sse;
    private readonly bool _isNewTask;
    private readonly MessageSendParams _sendParams;
    private readonly AgentTask _incomingTask;

    public A2AResponseHandler(ITaskStore taskStore, JsonRpcId requestId, AgentTask incomingTask, MessageSendParams sendParams, bool sse, bool isNewTask, ILogger logger)
    {
        AssertionHelpers.ThrowIfNull(requestId, nameof(requestId));
        AssertionHelpers.ThrowIfNull(taskStore, nameof(taskStore));
        AssertionHelpers.ThrowIfNull(incomingTask, nameof(incomingTask));
        AssertionHelpers.ThrowIfNull(sendParams, nameof(sendParams));

        _taskStore = taskStore;
        _requestId = requestId;
        _incomingTask = incomingTask;
        _sendParams = sendParams;
        _logger = logger ?? NullLogger.Instance;
        _sse = sse;
        _isNewTask = isNewTask;
    }

    public async Task ResponseBegin(HttpResponse httpResponse, CancellationToken cancellationToken = default)
    {
        if (_sse)
        {
            httpResponse.ContentType = "text/event-stream";

            if (_isNewTask)
            {
                await WriteEvent(httpResponse, _incomingTask.Kind, _incomingTask, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// ITurnContext.SendActivity ultimately ends up here by way of A2AAdapter -> ChannelResponseQueue -> IChannelResponseHandler.OnResponse.
    /// </summary>
    /// <param name="httpResponse"></param>
    /// <param name="activity"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task OnResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        using var telemetryScope = new ScopeWriteResponse(activity);
        var entity = activity.GetStreamingEntity();
        if (entity != null)
        {
            await OnStreamingResponse(httpResponse, activity, entity, cancellationToken).ConfigureAwait(false);
        }
        else if (activity.IsType(ActivityTypes.Message))
        {
            await OnMessageResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }
        else if (activity.IsType(ActivityTypes.EndOfConversation))
        {
            await OnEndOfConversationResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("A2AResponseHandler.OnResponse: Unhandled Activity Type: {ActivityType}", activity.Type);
        }
    }

    public async Task ResponseEnd(HttpResponse httpResponse, object data, CancellationToken cancellationToken = default)
    {
        if (!_sse)
        {
            var task = await _taskStore.GetTaskAsync(_incomingTask.Id, cancellationToken).ConfigureAwait(false);
            task = task.WithHistoryTrimmedTo(_sendParams?.Configuration?.HistoryLength);

            var response = JsonRpcResponse.CreateJsonRpcResponse(_requestId, task);
            await WriteResponseAsync(httpResponse, _requestId, response, logger: _logger, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task OnMessageResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        // Once we have an A2A Task created (we do), everything is either an Artifact or TaskStatus.Message
        // Send a Message (from Activity)
        var statusUpdate = A2AActivity.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, activity.GetA2ATaskState(), activity: activity);
        var task = await _taskStore.UpdateStatusAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

        if (!task.IsTerminal())
        {
            await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("OnMessageResponse: Message ignored. Task '{TaskId}' is in a terminal state.", activity.Conversation.Id);
        }
    }

    private async Task OnStreamingResponse(HttpResponse httpResponse, IActivity activity, StreamInfo entity, CancellationToken cancellationToken = default)
    {
        var isLastChunk = entity.StreamType == StreamTypes.Final;
        var isInformative = entity.StreamType == StreamTypes.Informative;

        if (isInformative)
        {
            // Informative is a Status update with a Message
            var statusUpdate = A2AActivity.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, TaskState.Working, artifactId: entity.StreamId, activity: isInformative ? activity : activity);
            var task = await _taskStore.UpdateStatusAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

            if (!task.IsTerminal())
            {
                await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("OnStreamingResponse: Message ignored. Task '{TaskId}' is in a terminal state.", activity.Conversation.Id);
            }
        }
        else
        {
            // This is using entity.StreamId for the artifactId.  This will result in a single Artifact in the Task
            var artifactUpdate = A2AActivity.CreateArtifactUpdate(_incomingTask.ContextId, _incomingTask.Id, activity, artifactId: entity.StreamId, lastChunk: isLastChunk);
            var task = await _taskStore.UpdateArtifactAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);

            if (!task.IsTerminal())
            {
                await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("OnStreamingResponse: Message ignored. Task '{TaskId}' is in a terminal state.", activity.Conversation.Id);
            }
        }
    }

    private async Task OnEndOfConversationResponse(HttpResponse httpResponse, IActivity activity, CancellationToken cancellationToken = default)
    {
        var task = await _taskStore.GetTaskAsync(activity.Conversation.Id, cancellationToken).ConfigureAwait(false);
        if (task.IsTerminal())
        {
            _logger.LogWarning("OnEndOfConversationResponse: EndOfConversation ignored. Task '{TaskId}' is in a terminal state.", activity.Conversation.Id);
            return;
        }

        // Set optional EOC Value as an Artifact.
        if (activity.Value != null)
        {
            var artifactUpdate = new TaskArtifactUpdateEvent()
            {
                TaskId = _incomingTask.Id,
                ContextId = _incomingTask.ContextId,
                Artifact = A2AActivity.CreateArtifactFromObject(
                    activity.Value,
                    name: "Result",
                    description: "Task completion result",
                    mediaType: "application/json"),
                Append = false,
                LastChunk = true
            };

            System.Diagnostics.Trace.WriteLine($"OnEndOfConversationResponse BEFORE UPDATEARTIFACT");
            await _taskStore.UpdateArtifactAsync(artifactUpdate, cancellationToken).ConfigureAwait(false);
            System.Diagnostics.Trace.WriteLine($"OnEndOfConversationResponse AFTER UPDATEARTIFACT");

            await WriteEvent(httpResponse, artifactUpdate.Kind, artifactUpdate, cancellationToken).ConfigureAwait(false);
        }

        // Upate status to terminal.  Status event sent in ResponseEnd
        TaskState taskState = activity.Code switch
        {
            EndOfConversationCodes.Error => TaskState.Failed,
            EndOfConversationCodes.UserCancelled => TaskState.Canceled,
            _ => TaskState.Completed,
        };

        // ResponseEnd sends status
        IActivity statusMessage = null;
        if (activity.HasA2AMessageContent())
        {
            // Clone to avoid altering input Activity
            statusMessage = ProtocolJsonSerializer.CloneTo<IActivity>(activity);

            // Value was set as Artifact on Task
            statusMessage.Value = null;
        }

        var statusUpdate = A2AActivity.CreateStatusUpdate(_incomingTask.ContextId, _incomingTask.Id, taskState, activity: statusMessage);
        await _taskStore.UpdateStatusAsync(statusUpdate, cancellationToken).ConfigureAwait(false);

        await WriteEvent(httpResponse, statusUpdate.Kind, statusUpdate, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteEvent(HttpResponse httpResponse, string eventName, object payload, CancellationToken cancellationToken)
    {
        if (!_sse)
        {
            return;
        }

        System.Diagnostics.Trace.WriteLine($"EVENT NAME: {eventName}");

        var sse = string.Format(
            SseTemplate,
            eventName,
            ProtocolJsonSerializer.ToJson(
                A2AModel.StreamingMessageResponse(
                    _requestId,
                    payload)
                )
            );

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("SSE event: RequestId={RequestId},\r\n{Event}", _requestId, sse);
        }

        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(sse), cancellationToken).ConfigureAwait(false);
        await httpResponse.Body.FlushAsync(cancellationToken);
    }

    public static async Task WriteResponseAsync(HttpResponse response, JsonRpcId requestId, object payload, bool streamed = false, HttpStatusCode code = HttpStatusCode.OK, ILogger logger = null,  CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(payload);

        response.StatusCode = (int)code;

        var json = ProtocolJsonSerializer.ToJson(payload);
        if (!streamed)
        {
            response.ContentType = "application/json";
        }
        else
        {
            response.ContentType = "text/event-stream";
            json = $"data: {json}\r\n\r\n";
        }

        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("WriteResponseAsync: RequestId={RequestId}, Body={Payload}", requestId, json);
        }

        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
