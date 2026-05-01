// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// Adaptive Card Extension Client-side action response to render quick view.
    /// </summary>
    public class QuickViewHandleActionResponse : BaseHandleActionResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.QuickViewHandleActionResponse"/> class.
        /// </summary>
        public QuickViewHandleActionResponse() 
            : base(ViewResponseType.QuickView)
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or sets card view render arguments.
        /// </summary>
        /// <value>Card view render arguments.</value>
        public new QuickViewResponse RenderArguments { get; set; }
    }
}
