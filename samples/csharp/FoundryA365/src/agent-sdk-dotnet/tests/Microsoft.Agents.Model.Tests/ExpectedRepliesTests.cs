// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ExpectedRepliesTests
    {
        [Fact]
        public void ExpectedRepliesInits()
        {
            var activities = new List<IActivity>() { new Activity() };
            
            var expectedReplies = new ExpectedReplies(activities);

            Assert.NotNull(expectedReplies);
            Assert.IsType<ExpectedReplies>(expectedReplies);
            Assert.Equal(activities, expectedReplies.Activities);
        }

        [Fact]
        public void ExpectedRepliesInitsWithNoArgs()
        {
            var expectedReplies = new ExpectedReplies();

            Assert.NotNull(expectedReplies);
            Assert.IsType<ExpectedReplies>(expectedReplies);
        }
    }
}
