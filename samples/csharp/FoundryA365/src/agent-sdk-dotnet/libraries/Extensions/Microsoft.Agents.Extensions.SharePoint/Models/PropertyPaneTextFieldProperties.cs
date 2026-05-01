// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane text field properties object.
    /// </summary>
    public class PropertyPaneTextFieldProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneTextFieldProperties"/> class.
        /// </summary>
        public PropertyPaneTextFieldProperties()
        {
            // Do nothing
        }

        /// <summary>
        /// Gets or Sets the label of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the text field.</value>
        public string Label { get; set; }

        /// <summary>
        /// Gets or Sets the value of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the value of the text field.</value>
        public string Value { get; set; }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the text field.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets the description of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the description of the text field.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the text field is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets the error message of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the error message of the text field.</value>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or Sets the name used to log PropertyPaneTextField value changes for engagement tracking of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the log name of the text field.</value>
        public string LogName { get; set; }

        /// <summary>
        /// Gets or Sets the maximum number of characters that the PropertyPaneTextField can have of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the max length of the text field.</value>
        public int MaxLength { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether or not the text field is a multiline text field of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the text field is multiline.</value>
        public bool Multiline { get; set; }

        /// <summary>
        /// Gets or Sets the placeholder text to be displayed in the text field of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the place holder of the text field.</value>
        public string Placeholder { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether or not the multiline text field is resizable of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the text field is resiable.</value>
        public bool Resizable { get; set; }

        /// <summary>
        /// Gets or Sets the value that specifies the visible height of a text area(multiline text TextField), in lines.maximum number of characters that the PropertyPaneTextField can have of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the number of rows of the text field.</value>
        public int Rows { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether or not the text field is underlined of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the text field is underlined.</value>
        public bool Underlined { get; set; }
    }
}
