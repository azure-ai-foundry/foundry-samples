// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Task module invoke request value payload.
    /// </summary>
    public class TaskModuleRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleRequest"/> class.
        /// </summary>
        public TaskModuleRequest()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleRequest"/> class.
        /// </summary>
        /// <param name="data">User input data. Free payload with key-value pairs.</param>
        /// <param name="context">Current user context, i.e., the current theme.</param>
        public TaskModuleRequest(object data = default, TaskModuleRequestContext context = default)
        {
            Data = data;
            Context = context;
        }

        /// <summary>
        /// Gets or sets user input data. Free payload with key-value pairs.
        /// </summary>
        /// <value>The user input data.</value>
        public object Data { get; set; }

        /// <summary>
        /// Gets or sets current user context, i.e., the current theme.
        /// </summary>
        /// <value>The current user context.</value>
        public TaskModuleRequestContext Context { get; set; }

        /// <summary>
        /// Gets or sets current tab request context.
        /// </summary>
        /// <value>
        /// Tab request context.
        /// </value>
        public TabEntityContext TabEntityContext { get; set; }
    }
}
