// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// A card action represents a clickable or interactive button for use within cards or as suggested actions.
    /// They are used to solicit input from users.Despite their name, card actions are not limited to use solely on cards.
    ///
    /// Card actions are meaningful only when sent to channels.
    ///
    /// Channels decide how each action manifests in their user experience.In most cases, the cards are clickable. In 
    /// others, they may be selected by speech input.In cases where the channel does not offer an interactive activation 
    /// experience (e.g., when interacting over SMS), the channel may not support activation whatsoever.The decision about 
    /// how to render actions is controlled by normative requirements elsewhere in this document (e.g.within the card 
    /// format, or within the suggested actions definition).    
    /// </summary>
    public class CardAction
    {
        /// <summary> Initializes a new instance of CardAction. </summary>
        public CardAction()
        {
        }

        /// <summary> Initializes a new instance of CardAction. </summary>
        /// <param name="type"> Defines action types for clickable buttons. See <see cref="Microsoft.Agents.Core.Models.ActionTypes"/>.</param>
        /// <param name="title"> Text description which appears on the button. </param>
        /// <param name="image"> Image URL which will appear on the button, next to text label. </param>
        /// <param name="imageAltText"> Alternate text to be used for the Image property. </param>
        /// <param name="text"> Text for this action. </param>
        /// <param name="displayText"> (Optional) text to display in the chat feed if the button is clicked. </param>
        /// <param name="value"> Supplementary parameter for action. Content of this property depends on the ActionType. </param>
        /// <param name="channelData"> Channel-specific data associated with this action. </param>
        public CardAction(string type = default, string title = default, string image = default, string text = default, string displayText = default, object value = default, object channelData = default, string imageAltText = default)
        {
            Type = type;
            Title = title;
            Image = image;
            ImageAltText = imageAltText;
            Text = text;
            DisplayText = displayText;
            Value = value;
            ChannelData = channelData;
        }

        /// <summary> Defines action types for clickable buttons. See <see cref="Microsoft.Agents.Core.Models.ActionTypes"/>.</summary>
        public string Type { get; set; }
        /// <summary> Text description which appears on the button. </summary>
        public string Title { get; set; }
        /// <summary> Image URL which will appear on the button, next to text label. </summary>
        public string Image { get; set; }
        /// <summary> Alternate text to be used for the Image property. </summary>
        public string ImageAltText { get; set; }
        /// <summary> Text for this action. </summary>
        public string Text { get; set; }
        /// <summary> (Optional) text to display in the chat feed if the button is clicked. </summary>
        public string DisplayText { get; set; }
        /// <summary> Supplementary parameter for action. Content of this property depends on the ActionType. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
        /// <summary> Channel-specific data associated with this action. </summary>
        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object ChannelData { get; set; }

        /// <summary>
        /// Implicit conversion of string to CardAction to simplify creation of
        /// CardActions with string values.
        /// </summary>
        /// <param name="input">input.</param>
        public static implicit operator CardAction(string input) => new(title: input, value: input);

        /// <summary>
        /// Creates a <see cref="Microsoft.Agents.Core.Models.CardAction"/> from the given input.
        /// </summary>
        /// <param name="input">Represents the title and value for the <see cref="Microsoft.Agents.Core.Models.CardAction"/>.</param>
        /// <returns>A new <see cref="Microsoft.Agents.Core.Models.CardAction"/> instance.</returns>
        public static CardAction FromString(string input)
        {
            return new CardAction(title: input, value: input);
        }
    }
}
