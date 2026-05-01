// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;
using Microsoft.Agents.Core.Models;
using System;
using System.Text;
using System.Security.Cryptography;

namespace Microsoft.Agents.Builder.Dialogs.Tests
{
    public class StringUtilsTests
    {
        private readonly string _text = "Lorem ipsum dolor sit amet.";

        [Fact]
        public void Ellipsis_ShouldNotTruncateText()
        {
            var result = StringUtils.Ellipsis(_text, _text.Length);

            Assert.Equal(_text, result);
        }

        [Fact]
        public void Ellipsis_ShouldTruncateText()
        {
            var length = _text.Length / 2;
            var split = $"{_text[..length]}...";
            var result = StringUtils.Ellipsis(_text, length);

            Assert.Equal(split, result);
        }

        [Fact]
        public void Ellipsis_ShouldNotTruncateTextWithStringBuilder()
        {
            var builder = new StringBuilder(_text);
            var result = StringUtils.Ellipsis(builder, _text.Length);

            Assert.Equal(_text, result.ToString());
        }

        [Fact]
        public void Ellipsis_ShouldTruncateTextWithStringBuilder()
        {
            var length = _text.Length / 2;
            var split = $"{_text[..length]}...";
            var builder = new StringBuilder(_text);
            var result = StringUtils.Ellipsis(builder, length);

            Assert.Equal(split, result.ToString());
        }

        [Fact]
        public void EllipsisHash_ShouldNotTruncateText()
        {
            var result = StringUtils.EllipsisHash(_text, _text.Length);

            Assert.Equal(_text, result);
        }

        [Fact]
        public void EllipsisHash_ShouldTruncateText()
        {
            var length = _text.Length / 2;
            var split = $"{_text[..length]}...{StringUtils.Hash(_text)}";
            var result = StringUtils.EllipsisHash(_text, length);

            Assert.Equal(split, result);
        }

        [Fact]
        public void EllipsisHash_ShouldNotTruncateTextWithStringBuilder()
        {
            var builder = new StringBuilder(_text);
            var result = StringUtils.EllipsisHash(builder, _text.Length);

            Assert.Equal(_text, result.ToString());
        }

        [Fact]
        public void EllipsisHash_ShouldTruncateTextWithStringBuilder()
        {
            var length = _text.Length / 2;
            var split = $"{_text[..length]}...{StringUtils.Hash(_text)}";
            var builder = new StringBuilder(_text);
            var result = StringUtils.EllipsisHash(builder, length);

            Assert.Equal(split, result.ToString());
        }

        [Fact]
        public void Hash_ShouldReturnBase64String()
        {
            var result = StringUtils.Hash(_text);
#if !NETFRAMEWORK
            var expected = SHA256.HashData(Encoding.UTF8.GetBytes(_text));
#else
            using var sha256 = SHA256.Create();
            var expected = sha256.ComputeHash(Encoding.UTF8.GetBytes(_text));
#endif
            var actual = Convert.FromBase64String(result);

            Assert.Equal(expected, actual);
        }
    }
}
