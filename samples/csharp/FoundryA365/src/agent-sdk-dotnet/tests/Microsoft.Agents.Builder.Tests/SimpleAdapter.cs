// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class SimpleAdapter : ChannelAdapter
    {
        private readonly Action<IActivity[]> _callOnSend = null;
        private readonly Action<IActivity> _callOnUpdate = null;
        private readonly Action<ConversationReference> _callOnDelete = null;

        public SimpleAdapter()
        {
        }

        public SimpleAdapter(Action<IActivity[]> callOnSend)
        {
            _callOnSend = callOnSend;
        }

        public SimpleAdapter(Action<IActivity> callOnUpdate)
        {
            _callOnUpdate = callOnUpdate;
        }

        public SimpleAdapter(Action<ConversationReference> callOnDelete)
        {
            _callOnDelete = callOnDelete;
        }

        public override Task DeleteActivityAsync(ITurnContext turnContext, ConversationReference reference, CancellationToken cancellationToken)
        {
            Assert.NotNull(reference); // SimpleAdapter.deleteActivity: missing reference
            _callOnDelete?.Invoke(reference);
            return Task.CompletedTask;
        }

        public override Task<ResourceResponse[]> SendActivitiesAsync(ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            Assert.NotNull(activities); // SimpleAdapter.deleteActivity: missing reference
            Assert.True(activities.Count() > 0, "SimpleAdapter.sendActivities: empty activities array.");

            _callOnSend?.Invoke(activities);
            List<ResourceResponse> responses = new List<ResourceResponse>();

            foreach (var activity in activities)
            {
                responses.Add(new ResourceResponse(activity.Id));
            }

            return Task.FromResult(responses.ToArray());
        }

        public override Task<ResourceResponse> UpdateActivityAsync(ITurnContext turnContext, IActivity activity, CancellationToken cancellationToken)
        {
            Assert.NotNull(activity); //SimpleAdapter.updateActivity: missing activity
            _callOnUpdate?.Invoke(activity);
            return Task.FromResult(new ResourceResponse(activity.Id)); // echo back the Id
        }

        public async Task ProcessRequest(Activity activity, AgentCallbackHandler callback, CancellationToken cancellationToken)
        {
            using (var ctx = new TurnContext(this, activity))
            {
                await this.RunPipelineAsync(ctx, callback, cancellationToken);
            }
        }
    }
}
