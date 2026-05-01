// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// Adaptive Card Extension View response type.
    /// </summary>
    public enum ViewResponseType
    {
        /// <summary>
        /// Render card view.
        /// </summary>
        Card,

        /// <summary>
        /// Render quick view.
        /// </summary>
        QuickView,

        /// <summary>
        /// No operation.
        /// </summary>
        NoOp
    }

    /// <summary>
    /// Response returned when handling a client-side action on an Adaptive Card Extension.
    /// </summary>
    public class BaseHandleActionResponse
    {
        /// <summary>
        /// Gets the response type.
        /// </summary>
        /// <value>Response type.</value>
        private readonly ViewResponseType responseType;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.BaseHandleActionResponse"/> class.
        /// </summary>
        /// <param name="responseType">Response type.</param>
        protected BaseHandleActionResponse(ViewResponseType responseType)
        {
            this.responseType = responseType;
        }

        /// <summary>
        /// Gets or sets render arguments.
        /// </summary>
        /// <value>Render arguments.</value>
        public object RenderArguments { get; set; }
    }
}
