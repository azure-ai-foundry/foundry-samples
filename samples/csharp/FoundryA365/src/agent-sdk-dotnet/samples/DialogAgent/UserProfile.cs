// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace DialogAgent;

/// <summary>
/// This is our application state. Just a regular serializable .NET class.
/// </summary>
public class UserProfile
{
    public string? Transport { get; set; }

    public string? Name { get; set; }

    public int? Age { get; set; }

    public Attachment? Picture { get; set; }
}
