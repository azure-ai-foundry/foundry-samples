// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// The settings for OAuthAuthentication.
    /// </summary>
    public class OAuthSettings : OBOSettings
    {
        /// <summary>
        /// The default timeout for the exchange.
        /// </summary>
        public static readonly TimeSpan DefaultTimeoutValue = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the name of the OAuth Connection defined on the Azure Bot.
        /// </summary>
        /// <value>The name of the OAuth connection.</value>
        public string AzureBotOAuthConnectionName { get; set; }

        /// <summary>
        /// Gets or sets the title of the sign-in card.
        /// </summary>
        /// <value>The title of the sign-in card.</value>
        public string Title { get; set; } = "Sign In";

        /// <summary>
        /// Gets or sets any additional text to include in the sign-in card.
        /// </summary>
        /// <value>Any additional text to include in the sign-in card.</value>
        public string Text { get; set; } = "Please sign in";

        public string InvalidSignInRetryMessage { get; set; } = "Invalid sign in code. Please enter the 6-digit code.";
        public int InvalidSignInRetryMax { get; set; } = 2;

        /// <summary>
        /// Gets or sets the number of milliseconds the prompt waits for the user to authenticate.
        /// Default is <see cref="DefaultTimeoutValue"/>.
        /// </summary>
        /// <value>The number of milliseconds the prompt waits for the user to authenticate.</value>
        public int? Timeout { get; set; } = (int)DefaultTimeoutValue.TotalMilliseconds;

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="OAuthPrompt"/> should end upon
        /// receiving an invalid message.  Generally the <see cref="OAuthPrompt"/> will ignore
        /// incoming messages from the user during the auth flow, if they are not related to the
        /// auth flow.  This flag enables ending the <see cref="OAuthPrompt"/> rather than
        /// ignoring the user's message.  Typically, this flag will be set to 'true', but is 'false'
        /// by default for backwards compatibility.
        /// </summary>
        /// <value>True if the <see cref="OAuthPrompt"/> should automatically end upon receiving
        /// an invalid message.</value>
        public bool EndOnInvalidMessage { get; set; }

        /// <summary>
        /// Gets or sets an optional boolean value to force the display of a Sign In link overriding
        /// the default behavior.
        /// </summary>
        /// <value>True to display the SignInLink.</value>
        public bool? ShowSignInLink { get; set; }

        /// <summary>
        /// Set to `true` to enable SSO when authenticating using Azure Active Directory (AAD).
        /// </summary>
        public bool EnableSso { get; set; } = true;
    }
}
