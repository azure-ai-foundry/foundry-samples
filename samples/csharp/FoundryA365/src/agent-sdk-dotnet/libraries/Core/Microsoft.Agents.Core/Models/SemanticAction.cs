// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Represents a reference to a programmatic action. </summary>
    public class SemanticAction
    {
        public SemanticAction() 
        {
            Entities = new Dictionary<string, Entity>();
        }

        /// <summary> Initializes a new instance of SemanticAction. </summary>
        /// <param name="state"> Indicates whether the semantic action is starting, continuing, or done. </param>
        /// <param name="id"> ID of this action. </param>
        /// <param name="entities"> Entities associated with this action. </param>
        public SemanticAction(string id = default, IDictionary<string, Entity> entities = default, string state = default)
        {
            State = state;
            Id = id;
            Entities = entities ?? new Dictionary<string, Entity>();
        }

        /// <summary> Indicates whether the semantic action is starting, continuing, or done. </summary>
        public string State { get; set; }
        /// <summary> ID of this action. </summary>
        public string Id { get; set; }
        /// <summary> Entities associated with this action. </summary>
        public IDictionary<string, Entity> Entities { get; }
    }
}
