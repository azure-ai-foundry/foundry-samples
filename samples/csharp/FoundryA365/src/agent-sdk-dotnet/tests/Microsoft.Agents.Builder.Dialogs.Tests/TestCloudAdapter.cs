// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class TestCloudAdapter : ChannelServiceAdapterBase
    {
        public TestCloudAdapter(IChannelServiceClientFactory channelServiceClientFactory)
            : base(channelServiceClientFactory)
        {
        }

        public List<Activity> SentActivities { get; } = new List<Activity>();

        public Task<InvokeResponse> ProcessAsync(ClaimsIdentity claimsIdentity, Activity activity, AgentCallbackHandler callback, CancellationToken cancellationToken = default)
        {
            return ProcessActivityAsync(claimsIdentity, activity, (tc, c) => callback(new InterceptTurnContext(this, tc), c), cancellationToken);
        }

        private class InterceptTurnContext : ITurnContext
        {
            private TestCloudAdapter _testAdapter;
            private ITurnContext _innerTurnContext;

            public InterceptTurnContext(TestCloudAdapter testAdapter, ITurnContext innerTurnContext)
            {
                _testAdapter = testAdapter;
                _innerTurnContext = innerTurnContext;
            }

            public IChannelAdapter Adapter => _innerTurnContext.Adapter;

            public TurnContextStateCollection StackState => _innerTurnContext.StackState;
            public TurnContextStateCollection Services => _innerTurnContext.Services;

            public IActivity Activity => _innerTurnContext.Activity;

            public IStreamingResponse StreamingResponse => _innerTurnContext.StreamingResponse;

            public bool Responded => _innerTurnContext.Responded;

            public ClaimsIdentity Identity => _innerTurnContext.Identity;

            public IConnectorClient Connector => throw new System.NotImplementedException();

            public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
            {
                return _innerTurnContext.OnDeleteActivity(handler);
            }

            public ITurnContext OnSendActivities(SendActivitiesHandler handler)
            {
                return _innerTurnContext.OnSendActivities(handler);
            }

            public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
            {
                return _innerTurnContext.OnUpdateActivity(handler);
            }

            public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
            {
                if (activity.Type != ActivityTypes.InvokeResponse)
                {
                    _testAdapter.SentActivities.Add((Activity)activity);
                    return Task.FromResult(new ResourceResponse());
                }
                else
                {
                    return _innerTurnContext.SendActivityAsync(activity, cancellationToken);
                }
            }

            public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
            {
                _testAdapter.SentActivities.AddRange(activities.Cast<Activity>());
                return Task.FromResult(Enumerable.Repeat(new ResourceResponse(), activities.Length).ToArray());
            }

            public Task<ResourceResponse> SendActivityAsync(string textReplyToSend, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
            {
                return _innerTurnContext.SendActivityAsync(textReplyToSend, speak, inputHint, cancellationToken);
            }

            public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
            {
                return _innerTurnContext.UpdateActivityAsync(activity, cancellationToken);
            }

            public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
            {
                return _innerTurnContext.DeleteActivityAsync(activityId, cancellationToken);
            }

            public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
            {
                return _innerTurnContext.DeleteActivityAsync(conversationReference, cancellationToken);
            }

            public Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
            {
                throw new System.NotImplementedException();
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}
