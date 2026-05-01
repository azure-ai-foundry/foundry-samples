// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder
{
    public static class TurnContextExtensions
    {
        public static bool IsAgenticRequest(this ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return AgenticAuthorization.IsAgenticRequest(turnContext);
        }
    }
}
