// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.SharePoint.Models.Actions
{
    /// <summary>
    /// Action.Execute.
    /// </summary>
    public class ExecuteAction : BaseAction, IAction
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.Actions.ExecuteAction"/> class.
        /// </summary>
        public ExecuteAction() 
            : base("Execute")
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the action parameters of type <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
        /// </summary>
        /// <value>This value is the parameters of the action.</value>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// Gets or Sets the verb associated with this action of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the verb associated with the action.</value>
        public string Verb { get; set; }
    }
}
