// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Contents of the reply to an operation which returns expected Activity replies. </summary>
    public class ExpectedReplies
    {
        /// <summary> Initializes a new instance of ExpectedReplies. </summary>
        public ExpectedReplies()
        {
            Activities = [];
        }

        /// <summary> Initializes a new instance of ExpectedReplies. </summary>
        /// <param name="activities"> A list of Activities included in the response. </param>
        public ExpectedReplies(IList<IActivity> activities)
        {
            Activities = activities ?? [];
        }

        /// <summary> A list of Activities included in the response. </summary>
        public IList<IActivity> Activities { get; set; }

        /// <summary>
        /// If Activity.Type is "Invoke", this property contains the body of the InvokeResponse.
        /// </summary>
        public object Body { get; set; }
    }
}
