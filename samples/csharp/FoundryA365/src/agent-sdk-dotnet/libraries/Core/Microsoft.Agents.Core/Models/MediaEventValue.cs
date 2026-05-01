// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Supplementary parameter for media events. </summary>
    public class MediaEventValue
    {
        public MediaEventValue() { }

        /// <summary> Initializes a new instance of MediaEventValue. </summary>
        public MediaEventValue(object cardValue = default)
        {
            CardValue = cardValue;
        }

        /// <summary> Callback parameter specified in the Value field of the MediaCard that originated this event. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object CardValue { get; }
    }
}
