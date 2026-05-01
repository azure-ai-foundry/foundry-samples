// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> An attachment within an activity. </summary>
    public class Attachment
    {
        /// <summary> Initializes a new instance of Attachment. </summary>
        public Attachment()
        {
        }

        /// <summary> Initializes a new instance of Attachment. </summary>
        /// <param name="contentType"> mimetype/Contenttype for the file. </param>
        /// <param name="contentUrl"> Content Url. </param>
        /// <param name="content"> Embedded content. </param>
        /// <param name="name"> (OPTIONAL) The name of the attachment. </param>
        /// <param name="thumbnailUrl"> (OPTIONAL) Thumbnail associated with attachment. </param>
        public Attachment(string contentType = default, string contentUrl = default, object content = default, string name = default, string thumbnailUrl = default)
        {
            ContentType = contentType;
            ContentUrl = contentUrl;
            Content = content;
            Name = name;
            ThumbnailUrl = thumbnailUrl;
        }

        /// <summary> mimetype/Contenttype for the file. </summary>
        public string ContentType { get; set; }
        /// <summary> Content Url. </summary>
        public string ContentUrl { get; set; }
        /// <summary> Embedded content. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Content { get; set; }
        /// <summary> (OPTIONAL) The name of the attachment. </summary>
        public string Name { get; set; }
        /// <summary> (OPTIONAL) Thumbnail associated with attachment. </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Microsoft.Agents.Core.Models.Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
