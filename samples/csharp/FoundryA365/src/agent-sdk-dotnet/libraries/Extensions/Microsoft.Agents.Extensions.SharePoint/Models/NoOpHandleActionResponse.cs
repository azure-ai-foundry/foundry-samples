// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// Adaptive Card Extension Client-side action no-op response.
    /// </summary>
    public class NoOpHandleActionResponse : BaseHandleActionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.NoOpHandleActionResponse"/> class.
        /// </summary>
        public NoOpHandleActionResponse() 
            : base(ViewResponseType.NoOp)
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or sets card view render arguments.
        /// </summary>
        /// <value>Card view render arguments.</value>
        public static new object RenderArguments
        { 
            get => null; 
            set { } 
        }
    }
}
