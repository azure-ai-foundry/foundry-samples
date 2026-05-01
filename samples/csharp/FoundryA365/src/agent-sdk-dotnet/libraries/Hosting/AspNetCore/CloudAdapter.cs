// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Telemetry.Adapter.Scopes;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Validation;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// The <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/> will queue the incoming request to be
    /// processed by the configured background service if possible.
    /// </summary>
    /// <remarks>
    /// Invoke and ExpectReplies are always handled synchronously.
    /// </remarks>
    public class CloudAdapter
        : ChannelServiceAdapterBase, IAgentHttpAdapter
    {
        private readonly IActivityTaskQueue _activityTaskQueue;
        private readonly AdapterOptions _adapterOptions;
        private readonly ChannelResponseQueue _responseQueue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Hosting.AspNetCore.CloudAdapter"/> class.
        /// </summary>
        /// <param name="channelServiceClientFactory"></param>
        /// <param name="activityTaskQueue"></param>
        /// <param name="logger"></param>
        /// <param name="options">Defaults to Async enabled and 60 second shutdown delay timeout</param>
        /// <param name="middlewares"></param>
        /// <param name="config"></param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public CloudAdapter(
            IChannelServiceClientFactory channelServiceClientFactory,
            IActivityTaskQueue activityTaskQueue,
            ILogger<CloudAdapter> logger = null,
            AdapterOptions options = null,
            Builder.IMiddleware[] middlewares = null,
            IConfiguration config = null)
            : base(channelServiceClientFactory, logger)
        {
            _activityTaskQueue = activityTaskQueue ?? throw new ArgumentNullException(nameof(activityTaskQueue));
            _adapterOptions = options ?? new AdapterOptions();
            _responseQueue = new ChannelResponseQueue(Logger);

            if (middlewares != null)
            {
                foreach (var middleware in middlewares)
                {
                    Use(middleware);
                }
            }

            OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                StringBuilder sbError = new StringBuilder(1024);
                int iLevel = 0;
                bool emitStackTrace = true;
                if (config != null && config["EmitStackTrace"] != null)
                {
                    if (!bool.TryParse(config["EmitStackTrace"], out emitStackTrace))
                    {
                        emitStackTrace = true; // Default to true if parsing fails
                    }
                }
                StringBuilder lastErrorMessage = new(1024);
                exception.GetExceptionDetail(sbError, iLevel, lastErrorMsg: lastErrorMessage, includeStackTrace: emitStackTrace); // ExceptionParser
                if (exception is ErrorResponseException errorResponse && errorResponse.Body != null)
                {
                    sbError.Append(Environment.NewLine);
                    sbError.Append(errorResponse.Body.ToString());
                }
                string resolvedErrorMessage = sbError.ToString();

                // Writing formatted exception message to log with error codes and help links. 
#pragma warning disable CA2254 // Template should be a static expression
                Logger.LogError(resolvedErrorMessage);
#pragma warning restore CA2254 // Template should be a static expression

                if (exception is not OperationCanceledException) // Do not try to send another message if the response has been canceled.
                {
                    try
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(lastErrorMessage.ToString()), CancellationToken.None);
                        await turnContext.TraceActivityAsync("OnTurnError Trace", resolvedErrorMessage, "https://www.botframework.com/schemas/error", "TurnError");
                    }
                    catch
                    {
                        System.Diagnostics.Trace.WriteLine($"Unable to send error Activity for: {lastErrorMessage}");
                    }
                }
                sbError.Clear();
            };
        }

        /// <summary>
        /// This method can be called from inside a POST method on any Controller implementation.  If the activity is Not an Invoke, and
        /// DeliveryMode is Not ExpectReplies, and this is not a GET request to upgrade to WebSockets, then the activity will be enqueued
        /// for processing on a background thread.
        /// </summary>
        /// <remarks>
        /// Note, this is an ImmediateAccept and BackgroundProcessing override of: 
        /// Task IAgentHttpAdapter.ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default);
        /// </remarks>
        /// <param name="httpRequest">The HTTP request object, typically in a POST handler by a Controller.</param>
        /// <param name="httpResponse">The HTTP response object.</param>
        /// <param name="agent">The bot implementation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive
        ///     notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task ProcessAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            ArgumentNullException.ThrowIfNull(httpResponse);
            ArgumentNullException.ThrowIfNull(agent);

            using var telemetryScope = new ScopeProcess();

            if (httpRequest.Method != HttpMethods.Post)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            }
            else
            {
                var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest).ConfigureAwait(false);
                if (activity == null || !activity.Validate(ValidationContext.Channel | ValidationContext.Receiver))
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
                activity.RequestId ??= httpRequest.HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();
                telemetryScope.Share(activity);

                var claimsIdentity = HttpHelper.GetClaimsIdentity(httpRequest);

                try
                {
                    if (activity.IsType(ActivityTypes.Invoke) || activity.DeliveryMode == DeliveryModes.Stream || activity.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        InvokeResponse invokeResponse = null;

                        IChannelResponseHandler writer = activity.DeliveryMode == DeliveryModes.Stream
                            ? new ActivityResponseHandler()
                            : new ExpectRepliesResponseWriter(activity);

                        // Turn Begin
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Turn Begin (blocking): RequestId={RequestId}", activity.RequestId);
                        }

                        _responseQueue.StartHandlerForRequest(activity.RequestId);
                        await writer.ResponseBegin(httpResponse, cancellationToken).ConfigureAwait(false);

                        // Start the processing without waiting for it to complete. The HandleResponsesAsync will block until the CompleteHandlerForRequest is called.
                        _ = ProcessActivityAsync(claimsIdentity, activity, agent.OnTurnAsync, cancellationToken).ContinueWith(t =>
                        {
                            try
                            {
                                invokeResponse = t.Result;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Exception processing activity for RequestId={RequestId}", activity.RequestId);
                            }
                            finally
                            {
                                // Ensure the handler is always completed so HandleResponsesAsync can finish.
                                _responseQueue.CompleteHandlerForRequest(activity.RequestId);
                            }
                        }, cancellationToken).ConfigureAwait(false);

                        // Block until turn is complete. This is triggered by CompleteHandlerForRequest and all responses read.
                        await _responseQueue.HandleResponsesAsync(activity.RequestId, async (response) =>
                        {
                            if (Logger.IsEnabled(LogLevel.Debug))
                            {
                                Logger.LogDebug("Turn Response (blocking): RequestId={RequestId}, Activity='{Activity}'", activity.RequestId, ProtocolJsonSerializer.ToJson(response));
                            }

                            await writer.OnResponse(httpResponse, response, cancellationToken).ConfigureAwait(false);
                        }, cancellationToken).ConfigureAwait(false);

                        // Turn done
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Turn End (blocking): RequestId={RequestId}, InvokeResponse='{InvokeResponse}'", activity.RequestId, invokeResponse == null ? null : ProtocolJsonSerializer.ToJson(invokeResponse));
                        }

                        await writer.ResponseEnd(httpResponse, invokeResponse, cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        if (Logger.IsEnabled(LogLevel.Debug))
                        {
                            Logger.LogDebug("Activity Accepted: RequestId={RequestId}, Activity='{Activity}'", activity.RequestId, ProtocolJsonSerializer.ToJson(activity));
                        }

                        // Queue the activity to be processed by the ActivityBackgroundService.  There is no response body in
                        // this case and the request is handled in the background.
                        _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, this, activity, agentType: agent.GetType(), headers: httpRequest.Headers);

                        // Activity has been queued to process, so return immediately
                        httpResponse.StatusCode = (int)HttpStatusCode.Accepted;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Request was cancelled (e.g. client disconnected). This is expected and not an error.
                    Logger.LogWarning("CloudAdapter.ProcessAsync cancelled for RequestId={RequestId}", activity.RequestId);
                    _responseQueue.CompleteHandlerForRequest(activity.RequestId);
                }
                catch (Exception ex)
                {
                    // OnTurnError should be catching these.  
                    Logger.LogError(ex, "Unexpected exception in CloudAdapter.ProcessAsync");
                    httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                    _responseQueue.CompleteHandlerForRequest(activity.RequestId);
                    telemetryScope.SetError(ex);
                }
            }
        }

        /// <summary>
        /// CloudAdapter handles this override asynchronously if the Activity uses DeliverModes.Normal.  Otherwise
        /// as <see cref="Microsoft.Agents.Builder.ChannelServiceAdapterBase.ProcessActivityAsync(System.Security.Claims.ClaimsIdentity, Microsoft.Agents.Core.Models.IActivity, Microsoft.Agents.Builder.AgentCallbackHandler, System.Threading.CancellationToken)"/> using
        /// `agent.OnTurnAsync`.
        /// </summary>
        /// <param name="claimsIdentity"></param>
        /// <param name="continuationActivity"></param>
        /// <param name="agent"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="audience"></param>
        public override Task ProcessProactiveAsync(ClaimsIdentity claimsIdentity, IActivity continuationActivity, IAgent agent, CancellationToken cancellationToken, string audience = null)
        {
            // DeliveryModes.Normal can be pushed to the Queue which allows the calling request to continue without blocking.
            if (continuationActivity.DeliveryMode == null || continuationActivity.DeliveryMode == DeliveryModes.Normal)
            {
                // Queue the activity to be processed by the ActivityBackgroundService
                _activityTaskQueue.QueueBackgroundActivity(claimsIdentity, this, continuationActivity, proactive: true, proactiveAudience: audience);
                return Task.CompletedTask;
            }

            return base.ProcessProactiveAsync(claimsIdentity, continuationActivity, agent, cancellationToken, audience);
        }

        /// <summary>
        /// Processes an outgoing activity in response to an incoming activity, handling delivery modes that require
        /// immediate response from the host.
        /// </summary>
        /// <remarks>This method handles activities with delivery modes <see cref="Microsoft.Agents.Core.Models.DeliveryModes.Stream"/>
        /// and <see cref="Microsoft.Agents.Core.Models.DeliveryModes.ExpectReplies"/> by sending the response activity through the response queue.
        /// For other delivery modes, the method returns <see langword="false"/> and does not process the
        /// response.</remarks>
        /// <param name="incomingActivity">The incoming activity that triggered the response. The delivery mode of this activity determines how the
        /// response is handled.</param>
        /// <param name="outActivity">The activity to be sent as a response to the incoming activity.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the response
        /// was handled by the host; otherwise, <see langword="false"/>.</returns>
        protected override async Task<bool> HostResponseAsync(IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            // CloudAdapter handles Stream and ExpectReplies.  According to spec, any other values are treated as Normal and
            // ChannelServiceAdapterBase will handle that.
            if (incomingActivity.DeliveryMode != DeliveryModes.Stream && incomingActivity.DeliveryMode != DeliveryModes.ExpectReplies)
            {
                return false;
            }

            await _responseQueue.SendActivitiesAsync(incomingActivity.RequestId, [outActivity], cancellationToken).ConfigureAwait(false);

            return true;
        }
    }
}
