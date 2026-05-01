// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Current user context, i.e., the current theme.
    /// </summary>
    public class TaskModuleRequestContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleRequestContext"/> class.
        /// </summary>
        public TaskModuleRequestContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleRequestContext"/> class.
        /// </summary>
        /// <param name="theme">The theme.</param>
        public TaskModuleRequestContext(string theme = default)
        {
            Theme = theme;
        }

        /// <summary>
        /// Gets or sets the theme.
        /// </summary>
        /// <value>The theme.</value>
        public string Theme { get; set; }
    }
}
