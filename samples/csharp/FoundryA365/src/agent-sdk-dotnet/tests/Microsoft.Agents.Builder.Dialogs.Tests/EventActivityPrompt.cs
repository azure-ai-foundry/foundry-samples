// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Dialogs.Prompts;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class EventActivityPrompt : ActivityPrompt
    {
        public EventActivityPrompt(string dialogId, PromptValidator<IActivity> validator)
            : base(dialogId, validator)
        {
        }

        public async Task OnPromptNullContext(object options, CancellationToken cancellationToken = default)
        {
            var opt = (PromptOptions)options;

            // should throw ArgumentNullException
            await OnPromptAsync(turnContext: null, state: null, options: opt, isRetry: false);
        }

        public async Task OnPromptNullOptions(DialogContext dc, CancellationToken cancellationToken = default)
        {
            // should throw ArgumentNullException
            await OnPromptAsync(dc.Context, state: null, options: null, isRetry: false);
        }
    }
}
