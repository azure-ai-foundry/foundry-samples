// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Defines values for InstallationUpdateActionTypes.
    /// </summary>
    public static class InstallationUpdateActionTypes
    {
        /// <summary>
        /// The type for add installation update actions.
        /// </summary>
        public const string Add = "add";

        /// <summary>
        /// The type for add-upgrade installation update actions.
        /// </summary>
        public const string AddUpgrade = "add-upgrade";

        /// <summary>
        /// The type for remove installation update actions.
        /// </summary>
        public const string Remove = "remove";

        /// <summary>
        /// The type for remove-upgrade installation update actions.
        /// </summary>
        public const string RemoveUpgrade = "remove-upgrade";
    }
}
