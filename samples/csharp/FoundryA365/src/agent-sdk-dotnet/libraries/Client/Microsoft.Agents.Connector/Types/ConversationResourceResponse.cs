// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Connector.Types
{
    /// <summary> A response containing a resource. </summary>
    public class ConversationResourceResponse
    {
        /// <summary> Initializes a new instance of ConversationResourceResponse. </summary>
        public ConversationResourceResponse()
        {
        }

        /// <summary> Initializes a new instance of ConversationResourceResponse. </summary>
        /// <param name="activityId"> ID of the Activity (if sent). </param>
        /// <param name="serviceUrl"> Service endpoint where operations concerning the conversation may be performed. </param>
        /// <param name="id"> Id of the resource. </param>
        public ConversationResourceResponse(string activityId = default, string serviceUrl = default, string id = default)
        {
            ActivityId = activityId;
            ServiceUrl = serviceUrl;
            Id = id;
        }

        /// <summary> ID of the Activity (if sent). </summary>
        public string ActivityId { get; set; }
        /// <summary> Service endpoint where operations concerning the conversation may be performed. </summary>
        /// 
        public string ServiceUrl { get; set; }

        /// <summary> Id of the resource. </summary>
        public string Id { get; set; }
    }
}
