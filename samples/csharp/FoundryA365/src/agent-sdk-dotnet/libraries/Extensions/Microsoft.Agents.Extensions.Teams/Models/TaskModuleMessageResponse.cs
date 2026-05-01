// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Task Module response with message action.
    /// </summary>
    public class TaskModuleMessageResponse : TaskModuleResponseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleMessageResponse"/> class.
        /// </summary>
        public TaskModuleMessageResponse()
            : base("message")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleMessageResponse"/> class.
        /// </summary>
        /// <param name="value">Teams will display the value of value in a popup message box.</param>
        public TaskModuleMessageResponse(string value = default)
            : base("message")
        {
            Value = value;
        }

        /// <summary>
        /// Gets or sets Teams will display the value of value in a popup
        /// message box.
        /// </summary>
        /// <value>The value Teams will display in a pop-up message box.</value>
        public string Value { get; set; }
    }
}
