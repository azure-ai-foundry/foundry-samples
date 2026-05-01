// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ChannelAccountTests
    {
        [Fact]
        public void ChannelAccount_RoundTrip()
        {
            var channelAccount = new ChannelAccount
            {
                Id = "id",
                Name = "name",
                Role = "role"
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(channelAccount);

            // Out
            var outJson = ProtocolJsonSerializer.ToJson(channelAccount);

            Assert.Equal(goodJson, outJson);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<ChannelAccount>(outJson);
            Assert.Equal(channelAccount.Id, inObj.Id);
        }

        [Fact]
        public void ChannelAccount_RoundTripSub()
        {
            var channelAccount = new SubChannelAccount
            {
                Id = "id",
                Name = "name",
                Role = "role",
                GivenName = "givenName",
                Surname = "surname",
                Email = "email",
                UserPrincipalName = "userPrincipalName",
                UserRole = "userRole"
            };

            // Known good
            var goodJson = LoadTestJson.LoadJson(channelAccount);

            // Out
            var outJson = ProtocolJsonSerializer.ToJson(channelAccount);

            Assert.Equal(goodJson, outJson);

            // In
            var inObj = ProtocolJsonSerializer.ToObject<ChannelAccount>(outJson);
            Assert.Equal(channelAccount.Id, inObj.Id);

            var inSubObj = ProtocolJsonSerializer.ToObject<SubChannelAccount>(outJson);
            Assert.Equal(channelAccount.GivenName, inSubObj.GivenName);
        }
    }

    class SubChannelAccount : ChannelAccount
    {
        /// <summary>
        /// Gets or sets given name part of the user name.
        /// </summary>
        /// <value>The given name part of the user name.</value>
        public string GivenName { get; set; }

        /// <summary>
        /// Gets or sets surname part of the user name.
        /// </summary>
        /// <value>The surname part of the user name.</value>
        public string Surname { get; set; }

        /// <summary>
        /// Gets or sets email Id of the user.
        /// </summary>
        /// <value>The email ID of the user.</value>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets unique user principal name.
        /// </summary>
        /// <value>The unique user principal name.</value>
        public string UserPrincipalName { get; set; }

        /// <summary>
        /// Gets or sets the UserRole.
        /// </summary>
        /// <value>The user role.</value>
        public string UserRole { get; set; }
    }
}
