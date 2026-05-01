// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.CardView
{
    /// <summary>
    /// Base class for Adaptive Card Extensions card view components.
    /// </summary>
    public class BaseCardComponent
    {
        /// <summary>
        /// Component name.
        /// </summary>
        private readonly CardComponentName cardComponentName;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.CardView.BaseCardComponent"/> class.
        /// </summary>
        /// <param name="cardComponentName">Component name.</param>
        protected BaseCardComponent(CardComponentName cardComponentName)
        {
            this.cardComponentName = cardComponentName;
        }

        /// <summary>
        /// Gets or sets optional unique identifier of the component's instance.
        /// </summary>
        /// <value>The value is a unique string identifier of the component.</value>
        public string Id { get; set; }
    }
}
