// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Microsoft.Agents.Core.Serialization.Converters
{
    internal class Array2DConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsArray && typeToConvert.GetArrayRank() == 2;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(
                typeof(Array2DConverterInner<>).MakeGenericType(new[] { type.GetElementType() }),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { options },
                culture: null);

        class Array2DConverterInner<T> : JsonConverter<T[,]>
        {
            readonly JsonConverter<T> _valueConverter;

            public Array2DConverterInner(JsonSerializerOptions options) =>
                this._valueConverter = (typeof(T) == typeof(object) ? null : (JsonConverter<T>)options.GetConverter(typeof(T))); // Encountered a bug using the builtin ObjectConverter 

            public override void Write(Utf8JsonWriter writer, T[,] array, JsonSerializerOptions options)
            {
                // Adapted from this answer https://stackoverflow.com/a/25995025/3744182
                // By https://stackoverflow.com/users/3258160/pedro
                // To https://stackoverflow.com/questions/21986909/convert-multidimensional-array-to-jagged-array-in-c-sharp
                var rowsFirstIndex = array.GetLowerBound(0);
                var rowsLastIndex = array.GetUpperBound(0);
                var columnsFirstIndex = array.GetLowerBound(1);
                var columnsLastIndex = array.GetUpperBound(1);

                writer.WriteStartArray();
                for (var i = rowsFirstIndex; i <= rowsLastIndex; i++)
                {
                    writer.WriteStartArray();
                    for (var j = columnsFirstIndex; j <= columnsLastIndex; j++)
                        _valueConverter.WriteOrSerialize(writer, array[i, j], options);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
            }

            public override T[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                JsonSerializer.Deserialize<List<List<T>>>(ref reader, options)?.To2D();
        }
    }

    static class JsonSerializerExtensions
    {
        public static void WriteOrSerialize<T>(this JsonConverter<T> converter, Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (converter != null)
            {
                converter.Write(writer, value, options);
            }
            else
            {
                // Use the generic overload of JsonSerializer.Serialize to fix CA2263
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}
