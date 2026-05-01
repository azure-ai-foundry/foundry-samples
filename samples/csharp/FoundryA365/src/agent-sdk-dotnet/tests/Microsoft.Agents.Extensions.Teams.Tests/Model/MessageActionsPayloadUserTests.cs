// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Teams.Models;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.Model
{
    public class MessageActionsPayloadUserTests
    {
        [Fact]
        public void MessageActionsPayloadUserInits()
        {
            var userIdentityType = "onPremiseAadUser";
            var id = "userId1234";
            var displayName = "FName LName";

            var msgActionsPayloadUser = new MessageActionsPayloadUser(userIdentityType, id, displayName);

            Assert.NotNull(msgActionsPayloadUser);
            Assert.IsType<MessageActionsPayloadUser>(msgActionsPayloadUser);
            Assert.Equal(userIdentityType, msgActionsPayloadUser.UserIdentityType);
            Assert.Equal(id, msgActionsPayloadUser.Id);
            Assert.Equal(displayName, msgActionsPayloadUser.DisplayName);
        }

        [Fact]
        public void MessageActionsPayloadUserInitsWithNoArgs()
        {
            var msgActionsPayloadUser = new MessageActionsPayloadUser();

            Assert.NotNull(msgActionsPayloadUser);
            Assert.IsType<MessageActionsPayloadUser>(msgActionsPayloadUser);
        }
    }
}
