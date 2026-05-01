// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// The type ActivityTreatment indicates that this object will contain metadata to indicate to clients how 
    /// the activity should be handled.
    /// </summary>
    public class ActivityTreatment : Entity
    {
        public ActivityTreatment() : base(EntityTypes.ActivityTreatment)
        {
        }

        public string Treatment { get; set; }
    }
}
