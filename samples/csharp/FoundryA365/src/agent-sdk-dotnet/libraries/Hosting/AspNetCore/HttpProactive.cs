// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class HttpProactive
    {
        public static async Task SendActivityWithConversationIdAsync<TAgent>(HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, string conversationId, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            using (logger.BeginScope("Proactive request `{Request}` with agent '{Agent}'", "SendActivityWithConversationIdAsync", typeof(TAgent).Name))
            {
                await Execute<TAgent>(
                    httpResponse,
                    async () =>
                    {
                        var activity = await HttpHelper.ReadRequestAsync<IActivity>(httpRequest).ConfigureAwait(false)
                            ?? throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.HttpProactiveMissingActivityBody, null);

                        var conversation = await agent.Proactive.GetConversationWithThrowAsync(conversationId, cancellationToken).ConfigureAwait(false);
                        conversation.Reference.RequestId = httpRequest.HttpContext.TraceIdentifier;

                        Log.WithConversationIdAndBody(logger, conversationId, ProtocolJsonSerializer.ToJson(activity));

                        return new Result(StatusCodes.Status200OK, await Proactive.SendActivityAsync(adapter, conversation, activity, cancellationToken).ConfigureAwait(false));
                    },
                    logger,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task SendActivityWithConversationAsync<TAgent>(HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            using (logger.BeginScope("Proactive request `{Request}` with agent '{Agent}'", "SendActivityWithConversationAsync", typeof(TAgent).Name))
            {
                await Execute<TAgent>(
                    httpResponse,
                    async () =>
                    {
                        var body = await HttpHelper.ReadRequestAsync<SendToConversationBody>(httpRequest).ConfigureAwait(false)
                            ?? throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.HttpProactiveMissingSendBody, null);
                        body.Conversation.Reference.RequestId = httpRequest.HttpContext.TraceIdentifier;

                        Log.WithBody(logger, ProtocolJsonSerializer.ToJson(body));

                        return new Result(StatusCodes.Status200OK, await Proactive.SendActivityAsync(adapter, body.Conversation, body.Activity, cancellationToken).ConfigureAwait(false));
                    },
                    logger,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task ContinueConversationWithConversationIdAsync<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, string conversationId, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            using (logger.BeginScope("Proactive request `{Request}` with agent '{Agent}' using `{Route}`", "ContinueConversationWithConversationIdAsync", typeof(TAgent).Name, continueRoute.ToString()))
            {
                await Execute<TAgent>(
                    httpResponse,
                    async () =>
                    {
                        Log.WithConversationId(logger, conversationId);

                        var conversation = await agent.Proactive.GetConversationWithThrowAsync(conversationId, cancellationToken).ConfigureAwait(false);
                        conversation.Reference.RequestId = httpRequest.HttpContext.TraceIdentifier;

                        // Creating a continuation activity with Value containing Query args.
                        var continuationActivity = conversation.Reference.GetContinuationActivity();
                        var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                        if (eventValue.Count > 0)
                        {
                            continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                            continuationActivity.Value = eventValue;
                        }

                        await agent.Proactive.ContinueConversationAsync(
                            adapter,
                            conversation,
                            continueRoute.RouteHandler(agent),
                            continueRoute.TokenHandlers,
                            continuationActivity,
                            cancellationToken).ConfigureAwait(false);

                        return new Result(StatusCodes.Status200OK);
                    },
                    logger,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task ContinueConversationWithConversationAsync<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            using (logger.BeginScope("Proactive request `{Request}` with agent '{Agent}' using `{Route}`", "ContinueConversationWithConversationAsync", typeof(TAgent).Name, continueRoute.ToString()))
            {
                await Execute<TAgent>(
                    httpResponse,
                    async () =>
                    {
                        var conversation = await HttpHelper.ReadRequestAsync<Conversation>(httpRequest).ConfigureAwait(false)
                            ?? throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.HttpProactiveMissingConversationBody, null);

                        Log.WithBody(logger, ProtocolJsonSerializer.ToJson(conversation));

                        // Creating a continuation activity with Value containing Query args.
                        var continuationActivity = conversation.Reference.GetContinuationActivity();
                        continuationActivity.RequestId = httpRequest.HttpContext.TraceIdentifier;

                        var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                        if (eventValue.Count > 0)
                        {
                            continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                            continuationActivity.Value = eventValue;
                        }

                        await agent.Proactive.ContinueConversationAsync(
                            adapter,
                            conversation,
                            continueRoute.RouteHandler(agent),
                            continueRoute.TokenHandlers,
                            continuationActivity,
                            cancellationToken).ConfigureAwait(false);

                        return new Result(StatusCodes.Status200OK);
                    },
                    logger,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public static async Task CreateConversationAsync<TAgent>(ContinueConversationRoute<TAgent> continueRoute, HttpRequest httpRequest, HttpResponse httpResponse, IChannelAdapter adapter, TAgent agent, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            using (logger.BeginScope("Proactive request `{Request}` with agent '{Agent}' using `{Route}`", "CreateConversationAsync", typeof(TAgent).Name, continueRoute.ToString()))
            {
                await Execute<TAgent>(
                    httpResponse,
                    async () =>
                    {
                        var body = await HttpHelper.ReadRequestAsync<CreateConversationBody>(httpRequest).ConfigureAwait(false)
                            ?? throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.HttpProactiveMissingCreateBody, null);

                        Log.WithBody(logger, ProtocolJsonSerializer.ToJson(body));

                        // Create the CreateConversation instance from the request body.
                        IDictionary<string, string> claims = null;
                        if (string.IsNullOrWhiteSpace(body.AgentClientId))
                        {
                            claims = Conversation.ClaimsFromIdentity(HttpHelper.GetClaimsIdentity(httpRequest));
                        }
                        else
                        {
                            claims = new Dictionary<string, string>
                            {
                                { "aud", body.AgentClientId },
                            };
                        }

                        var createOptionsBuilder = CreateConversationOptionsBuilder.Create(claims, body.ChannelId)
                            .WithActivity(body.Activity)
                            .WithTopicName(body.TopicName)
                            .WithUser(body.User)
                            .WithChannelData(body.ChannelData)
                            .WithTeamsChannelId(body.TeamsChannelId)
                            .WithTenantId(body.TenantId)
                            .WithStoreConversation(body.StoreConversation);

                        if ((bool)(body.IsGroup.HasValue))
                        {
                            createOptionsBuilder.IsGroup((bool)(body.IsGroup.Value));
                        }

                        var createOptions = createOptionsBuilder.Build();

                        // Execute the conversation creation
                        var newConversation = await agent.Proactive.CreateConversationAsync(
                            adapter,
                            createOptions,
                            body.ContinueConversation ? continueRoute.RouteHandler(agent) : null,
                            continueRoute.TokenHandlers,
                            (reference) =>
                            {
                                // Creating a continuation activity with Value containing Query args.
                                var continuationActivity = reference.GetCreateContinuationActivity();
                                var eventValue = httpRequest.Query.Select(p => KeyValuePair.Create(p.Key, p.Value.ToString())).ToDictionary();
                                if (eventValue.Count > 0)
                                {
                                    continuationActivity.ValueType = Proactive.ContinueConversationValueType;
                                    continuationActivity.Value = eventValue;
                                }
                                continuationActivity.RequestId = httpRequest.HttpContext.TraceIdentifier;
                                return continuationActivity;
                            },
                            cancellationToken).ConfigureAwait(false);

                        return new Result(StatusCodes.Status200OK, newConversation);
                    },
                    logger,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task Execute<TAgent>(HttpResponse httpResponse, Func<Task<Result>> action, ILogger<HttpProactive> logger, CancellationToken cancellationToken) where TAgent : AgentApplication
        {
            try 
            {
                var result = await action().ConfigureAwait(false);

                Log.Result(logger, ProtocolJsonSerializer.ToJson(result));

                httpResponse.StatusCode = result.StatusCode;
                if (result.Body != null)
                {
                    using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(ProtocolJsonSerializer.ToJson(result.Body)));
                    httpResponse.Headers.ContentType = "application/json";
                    await memoryStream.CopyToAsync(httpResponse.Body, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ErrorResponseException errorResponse)
            {
                var body = ProtocolJsonSerializer.ToJson(errorResponse.Body);
                Log.Error(logger, ProtocolJsonSerializer.ToJson(body));

                httpResponse.StatusCode = (int)errorResponse.StatusCode.GetValueOrDefault(StatusCodes.Status500InternalServerError);
                if (errorResponse.Body != null)
                {
                    using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
                    httpResponse.Headers.ContentType = "application/json";
                    await memoryStream.CopyToAsync(httpResponse.Body, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (KeyNotFoundException knf)
            {
                var body = ErrorBody(knf.Message, knf.HResult, knf.HelpLink);
                Log.Error(logger, ProtocolJsonSerializer.ToJson(body));

                httpResponse.StatusCode = StatusCodes.Status404NotFound;
                await httpResponse.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when(ex is InvalidOperationException || ex is ArgumentException || ex is ArgumentNullException)
            {
                var body = ErrorBody(ex.Message, ex.HResult, ex.HelpLink);
                Log.Error(logger, ProtocolJsonSerializer.ToJson(body));

                httpResponse.StatusCode = StatusCodes.Status400BadRequest;
                await httpResponse.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception requestFailed)
            {
                var body = ErrorBody(requestFailed.Message, requestFailed.HResult, requestFailed.HelpLink);
                Log.Error(logger, ProtocolJsonSerializer.ToJson(body));

                httpResponse.StatusCode = StatusCodes.Status500InternalServerError;
                await httpResponse.WriteAsJsonAsync(body, cancellationToken).ConfigureAwait(false);
            }
        }

        private static object ErrorBody(string message, int? hresult = null, string helpLink = null)
        {
            if (!hresult.HasValue || hresult.Value == 0)
            {
                return new { error = new { message, helpLink } };
            }
            
            return new { error = new { code = hresult.Value.ToString(), message, helpLink } };
        }
    }

    static partial class Log
    {
        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Debug,
            Message = "Proactive with conversationId '{ConversationId}' and body: {Body}")]
        public static partial void WithConversationIdAndBody(ILogger logger, string conversationId, string body);

        [LoggerMessage(
            EventId = 2,
            Level = LogLevel.Debug,
            Message = "Proactive with conversationId: {ConversationId}")]
        public static partial void WithConversationId(ILogger logger, string conversationId);

        [LoggerMessage(
            EventId = 3,
            Level = LogLevel.Debug,
            Message = "Proactive with body: {Body}")]
        public static partial void WithBody(ILogger logger, string body);

        [LoggerMessage(
            EventId = 4,
            Level = LogLevel.Debug,
            Message = "Proactive result: {Body}")]
        public static partial void Result(ILogger logger, string body);

        [LoggerMessage(
            EventId = 5,
            Level = LogLevel.Debug,
            Message = "Proactive error: {Body}")]
        public static partial void Error(ILogger logger, string body);
    }

    record Result(int StatusCode, object Body = null) {}

    class SendToConversationBody
    {
        public Conversation Conversation { get; set; }
        public IActivity Activity { get; set; } = default!;
    }

    class CreateConversationBody
    {
        public string AgentClientId { get; set; }
        public string ChannelId { get; set; }
        public bool? IsGroup { get; set; }
        public ChannelAccount User { get; set; }
        public string TopicName { get; set; }
        public string TenantId { get; set; }
        public IActivity Activity { get; set; }
        public string TeamsChannelId { get; set; }
        public object ChannelData { get; set; }
        public bool StoreConversation { get; set; } = false;
        public bool ContinueConversation { get; set; } = false;
    }
}
