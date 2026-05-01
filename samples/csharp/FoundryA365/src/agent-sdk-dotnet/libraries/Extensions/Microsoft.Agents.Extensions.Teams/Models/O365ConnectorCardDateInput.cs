// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// O365 connector card date input.
    /// </summary>
    public class O365ConnectorCardDateInput : O365ConnectorCardInputBase
    {
        /// <summary>
        /// Content type to be used in the @type property.
        /// </summary>
        public new const string Type = "DateInput";

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardDateInput"/> class.
        /// </summary>
        public O365ConnectorCardDateInput()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.Teams.Models.O365ConnectorCardDateInput"/> class.
        /// </summary>
        /// <param name="type">Input type name. Possible values include:
        /// 'textInput', 'dateInput', 'multichoiceInput'.</param>
        /// <param name="id">Input Id. It must be unique per entire O365
        /// connector card.</param>
        /// <param name="isRequired">Define if this input is a required field.
        /// Default value is false.</param>
        /// <param name="title">Input title that will be shown as the
        /// placeholder.</param>
        /// <param name="value">Default value for this input field.</param>
        /// <param name="includeTime">Include time input field. Default value
        /// is false (date only).</param>
        public O365ConnectorCardDateInput(string type = default, string id = default, bool? isRequired = default, string title = default, string value = default, bool? includeTime = default)
            : base(type, id, isRequired, title, value)
        {
            IncludeTime = includeTime;
        }

        /// <summary>
        /// Gets or sets include time input field. Default value  is false
        /// (date only).
        /// </summary>
        /// <value>Boolean indicating whether to include time.</value>
        public bool? IncludeTime { get; set; }
    }
}
