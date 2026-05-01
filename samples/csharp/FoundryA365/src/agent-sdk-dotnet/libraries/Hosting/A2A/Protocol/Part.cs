// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// Represents a distinct piece of content within a Message or Artifact. A Part is a union type 
/// representing exportable content as either TextPart, FilePart, or DataPart. All Part types 
/// also include an optional metadata field for part-specific metadata.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextPart), typeDiscriminator: "text")]
[JsonDerivedType(typeof(FilePart), typeDiscriminator: "file")]
[JsonDerivedType(typeof(DataPart), typeDiscriminator: "data")]
public abstract class Part
{
    /// <summary>
    /// Optional metadata specific to this text part.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object>? Metadata { get; set; }
}
