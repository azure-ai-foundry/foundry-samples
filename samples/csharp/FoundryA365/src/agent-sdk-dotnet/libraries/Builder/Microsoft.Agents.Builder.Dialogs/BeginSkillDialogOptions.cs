// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.Dialogs
{
    /// <summary>
    /// A class with dialog arguments for a <see cref="SkillDialog"/>.
    /// </summary>
    public class BeginSkillDialogOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="Activity"/> to send to the skill.
        /// </summary>
        /// <value>
        /// The <see cref="Activity"/> to send to the skill.
        /// </value>
        public IActivity Activity { get; set; }
    }
}
