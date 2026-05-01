// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A response containing a resource ID. </summary>
    public class ResourceResponse
    {
        public ResourceResponse() { }

        /// <summary> Initializes a new instance of ResourceResponse. </summary>
        /// <param name="id"> Id of the resource. </param>
        public ResourceResponse(string id = default)
        {
            Id = id;
        }

        /// <summary> Id of the resource. </summary>
        public string Id { get; set; }
    }
}
