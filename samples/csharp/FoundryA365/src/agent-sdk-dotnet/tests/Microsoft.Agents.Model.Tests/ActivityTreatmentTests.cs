// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ActivityTreatmentTests
    {
        [Fact]
        public void ActivityTreatmentRoundTrip()
        {
            var outActivity = new Activity()
            {
                Type = ActivityTypes.Message,
                Entities = [new ActivityTreatment() { Treatment = ActivityTreatmentTypes.Targeted }]
            };

            var json = ProtocolJsonSerializer.ToJson(outActivity);
            var inActivity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            Assert.Single(inActivity.Entities);
            Assert.IsAssignableFrom<ActivityTreatment>(inActivity.Entities[0]);
            Assert.Equal(ActivityTreatmentTypes.Targeted, ((ActivityTreatment) inActivity.Entities[0]).Treatment);
            Assert.NotNull(inActivity.GetActivityTreatmentEntity());
        }
    }
}
