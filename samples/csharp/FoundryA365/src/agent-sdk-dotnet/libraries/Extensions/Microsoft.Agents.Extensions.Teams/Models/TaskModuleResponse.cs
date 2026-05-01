// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Envelope for Task Module Response.
    /// </summary>
    public class TaskModuleResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponse"/> class.
        /// </summary>
        public TaskModuleResponse()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponse"/> class.
        /// </summary>
        /// <param name="task">The JSON for the Adaptive card to appear in the task module.</param>
        public TaskModuleResponse(TaskModuleResponseBase task = default)
        {
            Task = task;
        }

        /// <summary>
        /// Gets or sets the JSON for the Adaptive card to appear in the task
        /// module.
        /// </summary>
        /// <value>The JSON for the Adaptive card to appear in the task module.</value>
        public TaskModuleResponseBase Task { get; set; }

        /// <summary>
        /// Gets or sets the CacheInfo for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponse"/> module.
        /// </summary>
        /// <value>The CacheInfo for this <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponse"/>.</value>
        public CacheInfo CacheInfo { get; set; }
    }
}
