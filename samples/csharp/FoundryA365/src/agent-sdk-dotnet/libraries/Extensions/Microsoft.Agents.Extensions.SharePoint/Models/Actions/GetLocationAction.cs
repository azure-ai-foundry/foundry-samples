// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// SharePoint get location action.
    /// </summary>
    public class GetLocationAction : BaseAction, IAction, IOnCardSelectionAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.GetLocationAction"/> class.
        /// </summary>
        public GetLocationAction()
            : base("VivaAction.GetLocation")
        {
            // Do nothing
        }
        
        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.GetLocationActionParameters"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public GetLocationActionParameters Parameters { get; set; }
    }
}
