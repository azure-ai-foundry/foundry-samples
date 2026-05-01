// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint property pane slider properties object.
    /// </summary>
    public class PropertyPaneSliderProperties : IPropertyPaneFieldProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.PropertyPaneSliderProperties"/> class.
        /// </summary>
        public PropertyPaneSliderProperties()
        {
            this.Step = 1;
        }

        /// <summary>
        /// Gets or Sets the label of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the label of the slider.</value>
        public string Label { get; set; }

        /// <summary>
        /// Gets or Sets the value of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the value of the slider.</value>
        public string Value { get; set; }

        /// <summary>
        /// Gets or Sets optional ariaLabel flag. Text for screen-reader to announce regardless of toggle state. Of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the aria label of the slider.</value>
        public string AriaLabel { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether this control is enabled or not of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the slider is disabled.</value>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or Sets the max value of the Slider of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the max value of the slider.</value>
        public int Max { get; set; }

        /// <summary>
        /// Gets or Sets the min value of the Slider of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the min value of the slider.</value>
        public int Min { get; set; }

        /// <summary>
        /// Gets or Sets a value indicating whether to show the value on the right of the Slider of type <see cref="System.Boolean"/>.
        /// </summary>
        /// <value>This value indicates whether the value of the slider should be shown.</value>
        public bool ShowValue { get; set; }

        /// <summary>
        /// Gets or Sets the  difference between the two adjacent values of the Slider. Defaults to 1. of type <see cref="System.Int32"/>.
        /// </summary>
        /// <value>This value is the step amount of the slider.</value>
        public int Step { get; set; }
    }
}
