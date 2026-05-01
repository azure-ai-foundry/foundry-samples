// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.A2A.JsonRpc;
using Microsoft.Agents.Hosting.A2A.Protocol;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.A2A;

/// <summary>
/// Adapter for handling A2A requests.
/// </summary>
/// <remarks>
/// Register Adapter and map endpoints in startup using:
/// <code>
///    builder.Services.AddA2AAdapter();
/// 
///    app.MapA2A();
/// </code>
/// <see cref="A2AServiceExtensions.AddA2AAdapter(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// <see cref="A2AServiceExtensions.MapA2AEndpoints(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder, bool, string)"/>
/// </remarks>
public class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
{
    private readonly TaskStore _taskStore;
    private readonly IActivityTaskQueue _activityTaskQueue;
    private readonly ChannelResponseQueue _responseQueue;
    private readonly ILogger<A2AAdapter> _logger;

    public A2AAdapter(IActivityTaskQueue activityTaskQueue, IStorage storage, ILogger<A2AAdapter> logger = null) : base(logger)
    {
        _logger = logger ?? NullLogger<A2AAdapter>.Instance;
        _activityTaskQueue = activityTaskQueue;
        _taskStore = new TaskStore(storage);
        _responseQueue = new ChannelResponseQueue(_logger);
    }

    /// <inheritdoc/>
    public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonRpcRequest = await A2AModel.ReadRequestAsync<JsonRpcRequest>(httpRequest);
            var identity = HttpHelper.GetClaimsIdentity(httpRequest);

            // Turn Begin
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn Begin: RequestId={RequestId}, Body={RequestBody}", jsonRpcRequest.Id, ProtocolJsonSerializer.ToJson(jsonRpcRequest));
            }

            if (httpRequest.Method == HttpMethods.Post)
            {
                if (   jsonRpcRequest.Method.Equals(A2AMethods.MessageStream) 
                    || jsonRpcRequest.Method.Equals(A2AMethods.MessageSend))
                {
                    await OnMessageAsync(
                        jsonRpcRequest,
                        httpResponse,
                        identity,
                        agent,
                        cancellationToken).ConfigureAwait(false);
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksResubscribe))
                {
                    await OnTasksResubscribeAsync(jsonRpcRequest, httpResponse, cancellationToken).ConfigureAwait(false);
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksGet))
                {
                    await OnTasksGetAsync(jsonRpcRequest, httpResponse, false, cancellationToken).ConfigureAwait(false);
                }
                else if (jsonRpcRequest.Method.Equals(A2AMethods.TasksCancel))
                {
                    await OnTasksCancelAsync(jsonRpcRequest, httpResponse, identity, agent, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    JsonRpcResponse response = JsonRpcResponse.MethodNotFoundResponse(jsonRpcRequest.Id, $"{jsonRpcRequest.Method} not supported");
                    await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }

            // Turn done
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Turn End: RequestId={RequestId}", jsonRpcRequest.Id);
            }
        }
        catch(OperationCanceledException)
        {
            // TODO: probably need to cleanup to ChannelResponseQueue?
            _logger.LogDebug("ProcessAsync: OperationCanceledException");
        }
        catch (A2AException a2aEx)
        {
            JsonRpcResponse response = JsonRpcResponse.CreateJsonRpcErrorResponse(a2aEx.GetRequestId(), a2aEx);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogError("Turn End: RequestId={RequestId}, Error={ErrorBody}", a2aEx.GetRequestId(), ProtocolJsonSerializer.ToJson(response));
            }
            else
            {
                _logger.LogError("Turn End: RequestId={RequestId}, Error={ErrorCode}/{Message}", a2aEx.GetRequestId(), a2aEx.ErrorCode, a2aEx.Message);
            }

            await A2AResponseHandler.WriteResponseAsync(httpResponse, a2aEx.GetRequestId(), response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessAsync: {Message}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string messagePrefix, CancellationToken cancellationToken = default)
    {
        var agentCard = new AgentCard()
        {
            Name = nameof(A2AAdapter),
            Description = "Agents SDK A2A",
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            ProtocolVersion = "0.3.0",
            Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/",
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                {
                    "jwt",
                    new HTTPAuthSecurityScheme() { Scheme = "bearer" }
                }
            },
            DefaultInputModes = ["application/json"],
            DefaultOutputModes = ["application/json"],
            Skills = [],
            Capabilities = new AgentCapabilities()
            {
                Streaming = true,
            },
            AdditionalInterfaces =
            [
                new AgentInterface()
                {
                    Transport = TransportProtocol.JsonRpc,
                    Url = $"{httpRequest.Scheme}://{httpRequest.Host.Value}{messagePrefix}/"
                }
            ],
            PreferredTransport = TransportProtocol.JsonRpc,
           
        };

        // AgentApplication should implement IAgentCardHandler to set agent specific values.  But if
        // it doesn't, the default card will be used.
        if (agent is IAgentCardHandler agentCardHandler)
        {
            agentCard = await agentCardHandler.GetAgentCard(agentCard);
        }

        httpResponse.ContentType = "application/json";
        var json = ProtocolJsonSerializer.ToJson(agentCard);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("AgentCard: {RequestId}", json);
        }

        await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
        await httpResponse.Body.FlushAsync(cancellationToken);
    }

    private async Task OnMessageAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, ClaimsIdentity identity, IAgent agent, CancellationToken cancellationToken = default)
    {
        // Convert to Activity 
        bool isStreaming = !jsonRpcRequest.Method.Equals(A2AMethods.MessageSend);
        var sendParams = A2AModel.MessageSendParamsFromRequest(jsonRpcRequest);
        var (activity, contextId, taskId, message) = A2AActivity.ActivityFromRequest(jsonRpcRequest, sendParams: sendParams, isStreaming: isStreaming);
        if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
        {
            httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Create/update Task
        var incoming = await _taskStore.CreateOrContinueTaskAsync(contextId, taskId, message: message, cancellationToken: cancellationToken).ConfigureAwait(false);
        //activity.ChannelData = incoming.Task;

        if (incoming.Task.IsTerminal())
        {
            JsonRpcResponse response = JsonRpcResponse.UnsupportedOperationResponse(jsonRpcRequest.Id, $"Task '{taskId}' is in a terminal state");
            await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
            return;
        }

        var writer = new A2AResponseHandler(_taskStore, jsonRpcRequest.Id, incoming.Task, sendParams, isStreaming, incoming.IsNewTask, _logger);

        InvokeResponse invokeResponse = null;
        
        _responseQueue.StartHandlerForRequest(activity.RequestId);
        await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

        // Queue the activity to be processed by the ActivityBackgroundService, and stop ChannelResponseQueue when the
        // turn is done.
        _activityTaskQueue.QueueBackgroundActivity(identity, this, activity, agentType: agent.GetType(), onComplete: async (response) =>
        {
            invokeResponse = response;

            // Stops response handling and waits for HandleResponsesAsync to finish
            await _responseQueue.CompleteHandlerForRequestAsync(activity.RequestId).ConfigureAwait(false);
        });

        // Block until turn is complete. This is triggered by CompleteHandlerForRequest and all responses read.
        // MessageSendParams.Blocking is ignored.
        await _responseQueue.HandleResponsesAsync(activity.RequestId, async (activity) =>
        {
            await writer.OnResponse(httpResponse, activity, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnTasksResubscribeAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, CancellationToken cancellationToken = default)
    {
        // TODO: tasks/resubscribe
        JsonRpcResponse response = JsonRpcResponse.MethodNotFoundResponse(jsonRpcRequest.Id, $"{jsonRpcRequest.Method} not supported");
        await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnTasksGetAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, bool streamed, CancellationToken cancellationToken)
    {
        object response;

        var queryParams = A2AModel.ReadParams<TaskQueryParams>(jsonRpcRequest);
        var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

        if (task == null)
        {
            response = JsonRpcResponse.TaskNotFoundResponse(jsonRpcRequest.Id, $"Task '{queryParams.Id}' not found.");
        }
        else 
        {
            if (queryParams?.HistoryLength != null && queryParams.HistoryLength.Value < 0)
            {
                response = JsonRpcResponse.InvalidParamsResponse(jsonRpcRequest.Id, $"TaskQueryParams.History contains an invalid value: {queryParams.HistoryLength}");
            }
            else
            {
                task = task.WithHistoryTrimmedTo(queryParams.HistoryLength);
                response = JsonRpcResponse.CreateJsonRpcResponse(jsonRpcRequest.Id, task);
            }
        }

        await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, streamed, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnTasksCancelAsync(JsonRpcRequest jsonRpcRequest, HttpResponse httpResponse, ClaimsIdentity identity, IAgent agent, CancellationToken cancellationToken)
    {
        object response;

        var queryParams = A2AModel.ReadParams<TaskIdParams>(jsonRpcRequest);
        var task = await _taskStore.GetTaskAsync(queryParams.Id, cancellationToken).ConfigureAwait(false);

        if (task == null)
        {
            response = JsonRpcResponse.TaskNotFoundResponse(jsonRpcRequest.Id, $"Task '{queryParams.Id}' not found.");
        }
        else if (task.IsTerminal())
        {
            response = JsonRpcResponse.TaskNotCancelableResponse(jsonRpcRequest.Id, $"Task '{queryParams.Id}' is in a terminal state.");
        }
        else
        {
            // Send EndOfConversation to agent
            var eoc = new Activity()
            {
                Type = ActivityTypes.EndOfConversation,
                ChannelId = Channels.A2A,
                Conversation = new ConversationAccount()
                {
                    Id = queryParams.Id
                },
                Recipient = new ChannelAccount
                {
                    Id = "assistant",
                    Role = RoleTypes.Agent,
                },
                From = new ChannelAccount
                {
                    Id = A2AActivity.DefaultUserId,
                    Role = RoleTypes.User,
                },
                Code = EndOfConversationCodes.UserCancelled
            };

            // Note that we're not setting up to handle responses.  The task will be in terminal state regardless.
            await ProcessActivityAsync(identity, eoc, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

            // Update task
            task.Status.State = TaskState.Canceled;
            task.Status.Timestamp = DateTimeOffset.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken).ConfigureAwait(false);

            response = JsonRpcResponse.CreateJsonRpcResponse(jsonRpcRequest.Id, task);
        }

        await A2AResponseHandler.WriteResponseAsync(httpResponse, jsonRpcRequest.Id, response, false, HttpStatusCode.OK, _logger, cancellationToken).ConfigureAwait(false);
    }

    #region ChannelAdapter
    public override async Task<InvokeResponse> ProcessActivityAsync(ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
    {
        var context = new TurnContext(this, activity, claimsIdentity);
        await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <inheritdoc/>
    public override async Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
    {
        await _responseQueue.SendActivitiesAsync(turnContext.Activity.RequestId, activities, cancellationToken);
        return [];
    }
    #endregion
}
