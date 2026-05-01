// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Agents.Builder.Dialogs.Debugging;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests.Debugging
{
    public class SourceRangeTests
    {
        private const string Path = "BotBuilder.Dialogs.PromptClass";
        private const int StartLine = 5;
        private const int EndLine = 6;
        private const int StartChar = 1;
        private const int EndChar = 120;
        
        [Fact]
        public void Constructor_ShouldSetPropertiesWithProvidedValues()
        {
            var sourceRange = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            Assert.Equal(Path, sourceRange.Path);
            Assert.Equal(StartLine, sourceRange.StartPoint.LineIndex);
            Assert.Equal(StartChar, sourceRange.StartPoint.CharIndex);
            Assert.Equal(EndLine, sourceRange.EndPoint.LineIndex);
            Assert.Equal(EndChar, sourceRange.EndPoint.CharIndex);
        }

        [Fact]
        public void ToString_ShouldSetValueCorrectly()
        {
            var expectedResult = "BotBuilder.Dialogs.PromptClass:5:1->6:120";

            var sourceRange = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            var strSource = sourceRange.ToString();

            Assert.Equal(expectedResult, strSource);
        }

        [Fact]
        public void DeepClone_ShouldSetValueCorrectly()
        {
            var sourceRange = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            var clonedSource = sourceRange.DeepClone();

            Assert.Equal(sourceRange, clonedSource);
        }

        [Fact]
        public void Equals_ShouldReturnTrueOnEqualObject()
        {
            object other = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            var sourceRange = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            Assert.True(sourceRange.Equals(other));
        }

        [Fact]
        public void Equals_ShouldReturnFalseOnDifferentObject()
        {
            object other = new SourceRange(Path+".Method", StartLine, StartChar, EndLine, EndChar);

            var sourceRange = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            Assert.False(sourceRange.Equals(other));
        }

        [Fact]
        public void GetHashCode_ShouldReturnSameHashForSameSourceRange()
        {
            var sourceRange1 = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);
            var sourceRange2 = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);

            var hashCode1 = sourceRange1.GetHashCode();
            var hashCode2 = sourceRange2.GetHashCode();

            Assert.Equal(hashCode1, hashCode2);
        }

        [Fact]
        public void GetHashCode_ShouldReturnDifferentHashForDifferentSourceRange()
        {
            var sourceRange1 = new SourceRange(Path, StartLine, StartChar, EndLine, EndChar);
            var sourceRange2 = new SourceRange(Path, 1, 1, 2, 6);

            var hashCode1 = sourceRange1.GetHashCode();
            var hashCode2 = sourceRange2.GetHashCode();

            Assert.NotEqual(hashCode1, hashCode2);
        }
    }
}
