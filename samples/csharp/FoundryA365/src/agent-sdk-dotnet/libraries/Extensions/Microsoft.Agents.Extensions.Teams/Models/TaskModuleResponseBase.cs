// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Base class for Task Module responses.
    /// </summary>
    public class TaskModuleResponseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponseBase"/> class.
        /// </summary>
        public TaskModuleResponseBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponseBase"/> class.
        /// </summary>
        /// <param name="type">Choice of action options when responding to the task/submit message. Possible values include: 'message', 'continue'.</param>
        public TaskModuleResponseBase(string type = default)
        {
            Type = type;
        }

        /// <summary>
        /// Gets or sets choice of action options when responding to the
        /// task/submit message. Possible values include: 'message', 'continue'.
        /// </summary>
        /// <value>The choice of action options when responding to the task/submit message.</value>
        public string Type { get; set; }

        /// <summary>
        /// Gets properties that are not defined by the <see cref="Microsoft.Agents.Extensions.Teams.Models.TaskModuleResponseBase"/> type but that
        /// might appear in the serialized REST JSON object.  In this case, it would properties defined
        /// by derived types.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
