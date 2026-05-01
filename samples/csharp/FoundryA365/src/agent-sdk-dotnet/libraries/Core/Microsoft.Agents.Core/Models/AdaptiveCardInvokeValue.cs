// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Defines the structure that arrives in the Activity.Value for Invoke activity with Name of 'adaptiveCard/action'. </summary>
    public class AdaptiveCardInvokeValue
    {
        /// <summary> Initializes a new instance of AdaptiveCardInvokeValue. </summary>
        public AdaptiveCardInvokeValue()
        {
        }

        /// <summary> Initializes a new instance of AdaptiveCardInvokeValue. </summary>
        /// <param name="action"> Defines the structure that arrives in the Activity.Value.Action for Invoke activity with Name of 'adaptiveCard/action'. </param>
        /// <param name="authentication"> A request to exchange a token. </param>
        /// <param name="state"> The 'state' or magic code for an OAuth flow. </param>
        internal AdaptiveCardInvokeValue(AdaptiveCardInvokeAction action, TokenExchangeInvokeRequest authentication, string state)
        {
            Action = action;
            Authentication = authentication;
            State = state;
        }

        /// <summary> Defines the structure that arrives in the Activity.Value.Action for Invoke activity with Name of 'adaptiveCard/action'. </summary>
        public AdaptiveCardInvokeAction Action { get; set; }
        /// <summary> A request to exchange a token. </summary>
        public TokenExchangeInvokeRequest Authentication { get; set; }
        /// <summary> The 'state' or magic code for an OAuth flow. </summary>
        public string State { get; set; }
        /// <summary> What triggered the action.</summary>
        public string Trigger { get; set; }
    }
}
