// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Treatment types for <see cref="Microsoft.Agents.Core.Models.ActivityTreatment"/>.
    /// </summary>
    public class ActivityTreatmentTypes
    {
        /// <summary>
        /// Indicates that only the recipient should be able to see the message even if the activity
        /// is being sent to a group of people.
        /// </summary>
        public const string Targeted = "targeted";
    }
}
