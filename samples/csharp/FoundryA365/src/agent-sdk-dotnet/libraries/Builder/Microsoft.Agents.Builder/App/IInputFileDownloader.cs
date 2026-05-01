// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// A plugin responsible for downloading files relative to the current user's input.
    /// </summary>
    public interface IInputFileDownloader
    {
        /// <summary>
        /// Download any files relative to the current user's input.
        /// </summary>
        /// <param name="turnContext">The turn context.</param>
        /// <param name="turnState">The turn state.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A list of input files</returns>
        public Task<IList<InputFile>> DownloadFilesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken = default);
    }
}
