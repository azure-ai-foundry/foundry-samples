// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Defines the structure that arrives in the Activity.Value.Action for Invoke activity with Name of 'adaptiveCard/action'. </summary>
    public class AdaptiveCardInvokeAction
    {
        /// <summary> Initializes a new instance of AdaptiveCardInvokeAction. </summary>
        public AdaptiveCardInvokeAction()
        {
        }

        /// <summary> Initializes a new instance of AdaptiveCardInvokeAction. </summary>
        /// <param name="type"> The Type of this adaptive card action invoke. </param>
        /// <param name="id"> The Id of this adaptive card action invoke. </param>
        /// <param name="verb"> The Verb of this adaptive card action invoke. </param>
        /// <param name="data"> The Data of this adaptive card action invoke. </param>
        internal AdaptiveCardInvokeAction(string type, string id, string verb, object data)
        {
            Type = type;
            Id = id;
            Verb = verb;
            Data = data;
        }

        /// <summary> The Type of this adaptive card action invoke. </summary>
        public string Type { get; set; }
        /// <summary> The Id of this adaptive card action invoke. </summary>
        public string Id { get; set; }
        /// <summary> The Verb of this adaptive card action invoke. </summary>
        public string Verb { get; set; }
        /// <summary> The Data of this adaptive card action invoke. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Data { get; set; }
    }
}
