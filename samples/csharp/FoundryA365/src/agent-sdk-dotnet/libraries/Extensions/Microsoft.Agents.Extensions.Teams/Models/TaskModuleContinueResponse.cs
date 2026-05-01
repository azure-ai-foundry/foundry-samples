// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Task Module Response with continue action.
    /// </summary>
    public class TaskModuleContinueResponse : TaskModuleResponseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleContinueResponse"/> class.
        /// </summary>
        public TaskModuleContinueResponse()
            : base("continue")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleContinueResponse"/> class.
        /// </summary>
        /// <param name="value">The JSON for the Adaptive card to appear in the task module.</param>
        public TaskModuleContinueResponse(TaskModuleTaskInfo value = default)
            : base("continue")
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets the JSON for the Adaptive card to appear in the task module.
        /// </summary>
        /// <value>The JSON for the adaptive card to appear in the task module.</value>
        public TaskModuleTaskInfo Value { get; set; }
    }
}
