// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Microsoft.Agents.Authentication.Model
{
    internal class ConnectionDefinition
    {
        public string Assembly { get; set; }
        public string Type { get; set; }
        public IConfigurationSection Settings { get; set; }
        public ConstructorInfo Constructor { get; set; }
        public IAccessTokenProvider Instance { get; set; }
    }
}
