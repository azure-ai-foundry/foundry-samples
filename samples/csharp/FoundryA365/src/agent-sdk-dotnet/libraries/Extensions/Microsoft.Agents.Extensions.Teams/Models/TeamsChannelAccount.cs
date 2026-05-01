// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Teams channel account detailing user Azure Active Directory details.
    /// </summary>
    public class TeamsChannelAccount : ChannelAccount
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelAccount"/> class.
        /// </summary>
        public TeamsChannelAccount()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelAccount"/> class.
        /// </summary>
        /// <param name="id">Channel id for the user or bot on this channel.
        /// (Example: joe@smith.com, or @joesmith or 123456).</param>
        /// <param name="name">Display friendly name.</param>
        /// <param name="givenName">Given name part of the user name.</param>
        /// <param name="surname">Surname part of the user name.</param>
        /// <param name="email">Email Id of the user.</param>
        /// <param name="userPrincipalName">Unique user principal name.</param>
        /// <param name="tenantId">TenantId of the user.</param>
        /// <param name="userRole">UserRole of the user.</param>
        /// <param name="agenticUserId"></param>
        /// <param name="agenticAppId"></param>
        public TeamsChannelAccount(
            string id = default, 
            string name = default, 
            string givenName = default, 
            string surname = default, 
            string email = default, 
            string userPrincipalName = default, 
            string tenantId = default, 
            string userRole = default, 
            string agenticUserId = default, 
            string agenticAppId = default)
            : base(id, name, tenantId: tenantId, agenticUserId: agenticUserId, agenticAppId: agenticAppId)
        {
            GivenName = givenName;
            Surname = surname;
            Email = email;
            UserPrincipalName = userPrincipalName;
            UserRole = userRole;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TeamsChannelAccount"/> class.
        /// </summary>
        /// <param name="id">Channel id for the user or bot on this channel.
        /// (Example: joe@smith.com, or @joesmith or 123456).</param>
        /// <param name="name">Display friendly name.</param>
        /// <param name="givenName">Given name part of the user name.</param>
        /// <param name="surname">Surname part of the user name.</param>
        /// <param name="email">Email Id of the user.</param>
        /// <param name="userPrincipalName">Unique user principal name.</param>
        public TeamsChannelAccount(string id = default, string name = default, string givenName = default, string surname = default, string email = default, string userPrincipalName = default)
            : this(id, name, givenName, surname, email, userPrincipalName, string.Empty, string.Empty)
        {
        }

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

        /// <summary>
        ///  List of membership sources associated with the channel account.
        /// </summary>
        public List<MembershipSource>? MembershipSource { get; set; }

        /// <summary>
        /// Gets or sets the AAD Object Id.
        /// </summary>
        private string ObjectId
        {
            get => AadObjectId;

            set => AadObjectId = value;
        }
    }
}
