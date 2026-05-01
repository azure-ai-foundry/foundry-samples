// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class AgentInterfaceAttributeTests
    {
        [Fact]
        public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var protocol = "ActivityProtocol";
            var path = "/api/messages";
            var processDelegate = "MyProcessDelegate";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path, processDelegate);

            // Assert
            Assert.Equal(protocol, attribute.Protocol);
            Assert.Equal(path, attribute.Path);
            Assert.Equal(processDelegate, attribute.ProcessDelegate);
        }

        [Fact]
        public void Constructor_WithMinimalParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var protocol = "ActivityProtocol";
            var path = "/api/messages";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path);

            // Assert
            Assert.Equal(protocol, attribute.Protocol);
            Assert.Equal(path, attribute.Path);
            Assert.Null(attribute.ProcessDelegate);
        }

        [Fact]
        public void Constructor_WithNullProcessDelegate_SetsPropertiesCorrectly()
        {
            // Arrange
            var protocol = "ActivityProtocol";
            var path = "/api/messages";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path, null);

            // Assert
            Assert.Equal(protocol, attribute.Protocol);
            Assert.Equal(path, attribute.Path);
            Assert.Null(attribute.ProcessDelegate);
        }

        [Fact]
        public void Protocol_ReturnsValuePassedToConstructor()
        {
            // Arrange
            var protocol = "CustomProtocol";
            var path = "/custom/path";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path);

            // Assert
            Assert.Equal(protocol, attribute.Protocol);
        }

        [Fact]
        public void Path_ReturnsValuePassedToConstructor()
        {
            // Arrange
            var protocol = "ActivityProtocol";
            var path = "/api/custom";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path);

            // Assert
            Assert.Equal(path, attribute.Path);
        }

        [Fact]
        public void ProcessDelegate_ReturnsValuePassedToConstructor()
        {
            // Arrange
            var protocol = "ActivityProtocol";
            var path = "/api/messages";
            var processDelegate = "CustomProcessDelegate";

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path, processDelegate);

            // Assert
            Assert.Equal(processDelegate, attribute.ProcessDelegate);
        }

        [Fact]
        public void AttributeUsage_AllowsMultipleInstances()
        {
            // Arrange & Act
            var attributeUsage = typeof(AgentInterfaceAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0] as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.True(attributeUsage.AllowMultiple);
        }

        [Fact]
        public void AttributeUsage_TargetsClass()
        {
            // Arrange & Act
            var attributeUsage = typeof(AgentInterfaceAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0] as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.Equal(AttributeTargets.Class, attributeUsage.ValidOn);
        }

        [Fact]
        public void AttributeUsage_IsInherited()
        {
            // Arrange & Act
            var attributeUsage = typeof(AgentInterfaceAttribute)
                .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0] as AttributeUsageAttribute;

            // Assert
            Assert.NotNull(attributeUsage);
            Assert.True(attributeUsage.Inherited);
        }

        [Fact]
        public void Constructor_WithEmptyStrings_AcceptsValues()
        {
            // Arrange
            var protocol = string.Empty;
            var path = string.Empty;
            var processDelegate = string.Empty;

            // Act
            var attribute = new AgentInterfaceAttribute(protocol, path, processDelegate);

            // Assert
            Assert.Equal(protocol, attribute.Protocol);
            Assert.Equal(path, attribute.Path);
            Assert.Equal(processDelegate, attribute.ProcessDelegate);
        }

        [Fact]
        public void MultipleAttributes_CanBeAppliedToClass()
        {
            // Arrange & Act
            var attributes = typeof(MultiInterfaceTestClass).GetCustomAttributes(typeof(AgentInterfaceAttribute), false);

            // Assert
            Assert.NotNull(attributes);
            Assert.Equal(2, attributes.Length);

            var attr1 = attributes[0] as AgentInterfaceAttribute;
            var attr2 = attributes[1] as AgentInterfaceAttribute;

            Assert.NotNull(attr1);
            Assert.NotNull(attr2);
            Assert.Equal("Protocol1", attr1.Protocol);
            Assert.Equal("/path1", attr1.Path);
            Assert.Equal("Protocol2", attr2.Protocol);
            Assert.Equal("/path2", attr2.Path);
        }

        [Fact]
        public void InheritedAttribute_IsAppliedToSubclass()
        {
            // Arrange & Act
            var attributes = typeof(InheritedTestClass).GetCustomAttributes(typeof(AgentInterfaceAttribute), true);

            // Assert
            Assert.NotNull(attributes);
            Assert.Single(attributes);

            var attr = attributes[0] as AgentInterfaceAttribute;
            Assert.NotNull(attr);
            Assert.Equal("BaseProtocol", attr.Protocol);
            Assert.Equal("/base", attr.Path);
        }
    }

    [AgentInterface("Protocol1", "/path1", "Delegate1")]
    [AgentInterface("Protocol2", "/path2")]
    internal class MultiInterfaceTestClass
    {
    }

    [AgentInterface("BaseProtocol", "/base")]
    internal class BaseTestClass
    {
    }

    internal class InheritedTestClass : BaseTestClass
    {
    }
}