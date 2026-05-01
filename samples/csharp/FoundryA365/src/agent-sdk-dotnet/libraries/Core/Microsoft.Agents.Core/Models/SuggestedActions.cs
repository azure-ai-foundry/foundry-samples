// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> SuggestedActions that can be performed. </summary>
    public class SuggestedActions
    {
        public SuggestedActions()
        {
            To = [];
            Actions = [];
        }

        /// <summary> Initializes a new instance of SuggestedActions. </summary>
        /// <param name="to"> Ids of the recipients that the actions should be shown to.  These Ids are relative to the channelId and a subset of all recipients of the activity. </param>
        /// <param name="actions"> Actions that can be shown to the user. </param>
        public SuggestedActions(IList<string> to = default, IList<CardAction> actions = default)
        {
            To = to ?? [];
            Actions = actions ?? [];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Core.Models.SuggestedActions"/> class.
        /// </summary>
        /// <param name="to">Ids of the recipients that the actions should be
        /// shown to. These Ids are relative to the channelId and a subset of
        /// all recipients of the activity.</param>
        /// <param name="actions">Actions that can be shown to the user.</param>
        /// <exception cref="System.ArgumentNullException">ArgumentNullException.</exception>
        public SuggestedActions(IEnumerable<string> to, IEnumerable<CardAction> actions)
            : this([.. to], [.. actions])
        {
        }

        /// <summary> Ids of the recipients that the actions should be shown to.  These Ids are relative to the channelId and a subset of all recipients of the activity. </summary>
        public IList<string> To { get; set; }
        /// <summary> Actions that can be shown to the user. </summary>
        public IList<CardAction> Actions { get; set; }
    }
}
