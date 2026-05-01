// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Represents a private suggestion to the <see cref="Microsoft.Agents.Core.Models.Activity.Recipient"/> about another activity.
    /// </summary>
    /// <remarks>
    /// The activity's <see cref="Microsoft.Agents.Core.Models.Activity.ReplyToId"/> property identifies the activity being referenced.
    /// The activity's <see cref="Microsoft.Agents.Core.Models.Activity.Recipient"/> property indicates which user the suggestion is for.
    /// </remarks>
    public interface ISuggestionActivity : IMessageActivity
    {
    }
}
