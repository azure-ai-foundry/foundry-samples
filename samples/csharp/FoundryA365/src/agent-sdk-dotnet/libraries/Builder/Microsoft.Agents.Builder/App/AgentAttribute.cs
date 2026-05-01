// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class AgentAttribute : Attribute
    {
        public string? Name { get; }

        public string? Description { get; }

        public string? Version { get; }

        public AgentAttribute(string? name = null, string? description = null, string? version = null)
        {
            Name = name ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            Description = description;
            Version = version ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        }
    };
}
