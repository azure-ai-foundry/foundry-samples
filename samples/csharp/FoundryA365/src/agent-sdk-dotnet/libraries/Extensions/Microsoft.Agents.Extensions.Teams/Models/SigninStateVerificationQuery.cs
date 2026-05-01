// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Signin state (part of signin action auth flow) verification invoke query.
    /// </summary>
    public class SigninStateVerificationQuery
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.SigninStateVerificationQuery"/> class.
        /// </summary>
        public SigninStateVerificationQuery()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.SigninStateVerificationQuery"/> class.
        /// </summary>
        /// <param name="state"> The state string originally received when the
        /// signin web flow is finished with a state posted back to client via
        /// tab SDK microsoftTeams.authentication.notifySuccess(state).</param>
        public SigninStateVerificationQuery(string state = default)
        {
            State = state;
        }

        /// <summary>
        /// Gets or sets  The state string originally received when the signin
        /// web flow is finished with a state posted back to client via tab SDK
        /// microsoftTeams.authentication.notifySuccess(state).
        /// </summary>
        /// <value>The state.</value>
        public string State { get; set; }
    }
}
