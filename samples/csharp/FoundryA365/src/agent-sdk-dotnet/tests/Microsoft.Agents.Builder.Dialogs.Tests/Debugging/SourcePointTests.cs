// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Agents.Builder.Dialogs.Debugging;
using Xunit;

namespace Microsoft.Agents.Builder.Dialogs.Tests.Debugging
{
    public class SourcePointTests
    {
        private const int LineIndex = 5;
        private const int CharIndex = 1;

        [Fact]
        public void Constructor_ShouldSetPropertiesWithProvidedValues()
        {
            var sourcePoint = new SourcePoint(LineIndex, CharIndex);

            Assert.Equal(LineIndex, sourcePoint.LineIndex);
            Assert.Equal(CharIndex, sourcePoint.CharIndex);
        }

        [Fact]
        public void ToString_ShouldSetValueCorrectly()
        {
            var expectedResult = "5:1";

            var sourcePoint = new SourcePoint(LineIndex, CharIndex);

            var strSource = sourcePoint.ToString();

            Assert.Equal(expectedResult, strSource);
        }

        [Fact]
        public void DeepClone_ShouldSetValueCorrectly()
        {
            var sourcePoint = new SourcePoint(LineIndex, CharIndex);

            var clonedSource = sourcePoint.DeepClone();

            Assert.Equal(sourcePoint, clonedSource);
        }

        [Fact]
        public void Equals_ShouldReturnTrueOnEqualObject()
        {
            object other = new SourcePoint(LineIndex, CharIndex);

            var sourcePoint = new SourcePoint(LineIndex, CharIndex);

            Assert.True(sourcePoint.Equals(other));
        }

        [Fact]
        public void Equals_ShouldReturnFalseOnDifferentObject()
        {
            object other = new SourcePoint(12, 4);

            var sourcePoint = new SourcePoint(LineIndex, CharIndex);

            Assert.False(sourcePoint.Equals(other));
        }

        [Fact]
        public void GetHashCode_ShouldReturnSameHashForSameSourcePoint()
        {
            var sourcePoint1 = new SourcePoint(LineIndex, CharIndex);
            var sourcePoint2 = new SourcePoint(LineIndex, CharIndex);

            var hashCode1 = sourcePoint1.GetHashCode();
            var hashCode2 = sourcePoint2.GetHashCode();

            Assert.Equal(hashCode1, hashCode2);
        }

        [Fact]
        public void GetHashCode_ShouldReturnDifferentHashForDifferentSourcePoint()
        {
            var sourcePoint1 = new SourcePoint(LineIndex, CharIndex);
            var sourcePoint2 = new SourcePoint(2, 6);

            var hashCode1 = sourcePoint1.GetHashCode();
            var hashCode2 = sourcePoint2.GetHashCode();

            Assert.NotEqual(hashCode1, hashCode2);
        }
    }
}
