// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// Base Action.
    /// </summary>
    public class BaseAction
    {
        private readonly string type;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.BaseAction"/> class.
        /// </summary>
        /// <param name="actionType">Type of the action.</param>
        protected BaseAction(string actionType)
        {
            this.type = actionType;
        }
    }
}
