// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// This allows you to mark a class with the name of the Entity it represents.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EntityNameAttribute : Attribute
    {
        /// <summary>
        /// Gets the Entity name for the entity class
        /// </summary>
        public string EntityName { get; private set; }

        public EntityNameAttribute(string name)
            : base()
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            this.EntityName = name;
        }
    }
}
