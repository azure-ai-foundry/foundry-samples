// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Client.Errors;
using System;

namespace Microsoft.Agents.Client
{
    public class AgentClientSettings() : IAgentInfo
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public virtual void ValidateClientSettings() 
        { 
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AgentMissingProperty, null, nameof(Name));
            }
        }
    }
}
