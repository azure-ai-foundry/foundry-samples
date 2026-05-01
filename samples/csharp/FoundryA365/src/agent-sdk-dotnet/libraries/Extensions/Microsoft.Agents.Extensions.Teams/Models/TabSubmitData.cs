// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Invoke ('tab/submit') request value payload data.
    /// </summary>
    public class TabSubmitData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.TabSubmitData"/> class.
        /// </summary>
        public TabSubmitData()
        {
        }

        /// <summary>
        /// Gets or sets the type for this TabSubmitData.
        /// </summary>
        /// <value>
        /// Currently, 'tab/submit'.
        /// </value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets properties that are not otherwise defined by the <see cref="TabSubmit"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
