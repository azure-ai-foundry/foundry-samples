// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using System;

namespace Microsoft.Agents.Builder.Dialogs.Debugging
{
    /// <summary>
    /// SourcePoint represents the line and character index into the source code or declarative object backing an object in memory.
    /// </summary>
    public class SourcePoint : IEquatable<SourcePoint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SourcePoint"/> class.
        /// </summary>
        public SourcePoint()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourcePoint"/> class.
        /// </summary>
        /// <param name="lineIndex">line index.</param>
        /// <param name="charIndex">char index.</param>
        public SourcePoint(int lineIndex, int charIndex)
        {
            this.LineIndex = lineIndex;
            this.CharIndex = charIndex;
        }

        /// <summary>
        /// Gets or sets line number into the source file.
        /// </summary>
        /// <value>
        /// Line number into the source file.
        /// </value>
        public int LineIndex { get; set; }

        /// <summary>
        /// Gets or sets char index on the line from lineindex.
        /// </summary>
        /// <value>
        /// Char index on the line from lineindex.
        /// </value>
        public int CharIndex { get; set; }

        /// <summary>
        /// Returns a string that represents the current <see cref="SourcePoint"/>.
        /// </summary>
        /// <returns>A string that represents the current <see cref="SourcePoint"/>.</returns>
        public override string ToString() => $"{LineIndex}:{CharIndex}";

        /// <summary>
        /// Creates a new instance of the <see cref="SourcePoint"/>. All properties are recursively cloned.
        /// </summary>
        /// <returns>A new instance of the <see cref="SourcePoint"/>.</returns>
        public SourcePoint DeepClone() => new SourcePoint() { LineIndex = LineIndex, CharIndex = CharIndex };

        /// <summary>
        /// Indicates whether the current <see cref="SourcePoint"/> is equal to another object.
        /// </summary>
        /// <param name="obj">An object to compare with this <see cref="SourcePoint"/>.</param>
        /// <returns><c>true</c> if the current <see cref="SourcePoint"/> is equal to the object parameter; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            // Auto-generated
            return Equals(obj as SourcePoint);
        }

        /// <summary>
        /// Indicates whether the current <see cref="SourcePoint"/> is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this <see cref="SourcePoint"/>.</param>
        /// <returns><c>true</c> if the current <see cref="SourcePoint"/> is equal to the other parameter; otherwise, <c>false</c>.</returns>
        public bool Equals(SourcePoint other)
        {
            // Auto-generated
            return other != null &&
                   LineIndex == other.LineIndex &&
                   CharIndex == other.CharIndex;
        }

        /// <summary>
        /// Creates a hash code for the current <see cref="SourcePoint"/>.
        /// </summary>
        /// <returns>A hash code for the current <see cref="SourcePoint"/>.</returns>
        public override int GetHashCode()
        {
            // Auto-generated
            var hashCode = 1680727000;
            hashCode = (hashCode * -1521134295) + LineIndex.GetHashCode();
            hashCode = (hashCode * -1521134295) + CharIndex.GetHashCode();
            return hashCode;
        }
    }
}
