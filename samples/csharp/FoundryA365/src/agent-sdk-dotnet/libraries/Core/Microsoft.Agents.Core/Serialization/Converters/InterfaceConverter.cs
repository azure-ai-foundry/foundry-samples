// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    public class InterfaceConverter<M, I> : JsonConverter<I> where M : class, I
    {
        public override I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<M>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, I value, JsonSerializerOptions options) 
        { 
            JsonSerializer.Serialize(writer, (M) value, options);
        }
    }
}
