// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


namespace Microsoft.Agents.Extensions.SharePoint.Models
{
    /// <summary>
    /// SharePoint Ace Data object.
    /// </summary>
    public class AceData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Extensions.SharePoint.Models.AceData"/> class.
        /// </summary>
        public AceData()
        {
            // Do nothing
        }

        /// <summary>
        /// This enum contains the different types of card templates available in the SPFx framework.
        /// </summary>
        public enum AceCardSize
        {
            /// <summary>
            /// Medium
            /// </summary>
            Medium,

            /// <summary>
            /// Large
            /// </summary>
            Large
        }

        /// <summary>
        /// Gets or Sets the card size of the adaptive card extension of type <see cref="Microsoft.Agents.Extensions.SharePoint.Models.AceData.AceCardSize"/> enum.
        /// </summary>
        /// <value>This value is the size of the adaptive card extension.</value>
        public AceCardSize CardSize { get; set; }

        /// <summary>
        /// Gets or Sets the version of the data of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the version of the adaptive card extension.</value>
        /// <remarks>Although there is no restriction on the format of this property, it is recommended to use semantic versioning.</remarks>
        public string DataVersion { get; set; }

        /// <summary>
        /// Gets or Sets the unique id (Guid) of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the ID of the adaptive card extension.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or Sets the title of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the title of the adaptive card extension.</value>
        public string Title { get; set; }

        /// <summary>
        /// Gets or Sets the description of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the description of the adaptive card extension.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or Sets the icon property of type <see cref="System.String"/>.
        /// </summary>
        /// <value>This value is the icon of the adaptive card extension.</value>
        public string IconProperty { get; set; }

        /// <summary>
        /// Gets or sets the visibility of the Adaptive Card Extension.
        /// </summary>
        /// <value>The value is the flag that indicates if the Adaptive Card Extension is visible. Default to true.</value>
        public bool? IsVisible { get; set; }

        /// <summary>
        /// Gets or sets ACE properties data. Free payload with key-value pairs.
        /// </summary>
        /// <value>ACE Properties object.</value>
        public object Properties { get; set; }
    }
}
