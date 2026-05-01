// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.



namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies Invoke response base including response type.
    /// </summary>
    public class ConfigResponseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ConfigResponseBase"/> class.
        /// </summary>
        protected ConfigResponseBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.ConfigResponseBase"/> class.
        /// </summary>
        /// <param name="responseType"> response type for invoke.</param>
        protected ConfigResponseBase(string responseType)
        {
            ResponseType = responseType;
        }

        /// <summary>
        /// Gets or sets response type invoke request.
        /// </summary>
        /// <value> Invoke request response type.</value>
        public string ResponseType { get; set; }
    }
}
