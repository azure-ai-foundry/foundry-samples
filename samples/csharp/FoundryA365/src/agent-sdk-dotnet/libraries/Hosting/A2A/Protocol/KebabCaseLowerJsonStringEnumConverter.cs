using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Hosting.A2A.Protocol;

/// <summary>
/// A JSON string enum converter that converts enum values to kebab-case lower strings.
/// </summary>
/// <typeparam name="TEnum">The type of the enum to convert.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class KebabCaseLowerJsonStringEnumConverter<TEnum>() :
    JsonStringEnumConverter<TEnum>(JsonNamingPolicy.KebabCaseLower)
    where TEnum : struct, Enum;
