using System;
using System.Collections.Generic;
using BACnet.Core.Objects;
using Xunit;

namespace BACnet.Core.Tests.Objects
{
    public class BACnetObjectTests
    {
        private class TestBACnetObject : BACnetObject
        {
            public TestBACnetObject(uint objectIdentifier, string objectType) 
                : base(objectIdentifier, objectType)
            {
            }

            public override void ReadProperty(string propertyName)
            {
                // Test implementation
            }

            public override void WriteProperty(string propertyName, object value)
            {
                // Test implementation
            }
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            // Arrange & Act
            const uint objectId = 12345;
            const string objectType = "TestObject";
            var bacnetObject = new TestBACnetObject(objectId, objectType);

            // Assert
            Assert.Equal(objectId, bacnetObject.ObjectIdentifier);
            Assert.Equal(objectType, bacnetObject.ObjectType);
        }

        [Fact]
        public void ObjectName_GetSet_Success()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            
            // Act
            bacnetObject.ObjectName = "TestName";
            
            // Assert
            Assert.Equal("TestName", bacnetObject.ObjectName);
        }

        [Fact]
        public void GetProperty_WhenPropertyExists_ReturnsValue()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            bacnetObject.SetProperty("TestProp", "TestValue");

            // Act
            var result = bacnetObject.GetProperty<string>("TestProp");

            // Assert
            Assert.Equal("TestValue", result);
        }

        [Fact]
        public void GetProperty_WhenPropertyExistsWithDifferentType_ConvertsType()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            bacnetObject.SetProperty("TestProp", 12345);

            // Act
            var result = bacnetObject.GetProperty<string>("TestProp");

            // Assert
            Assert.Equal("12345", result);
        }

        [Fact]
        public void GetProperty_WhenPropertyDoesNotExist_ThrowsKeyNotFoundException()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => bacnetObject.GetProperty<string>("NonExistentProp"));
        }

        [Fact]
        public void GetProperty_WithNullPropertyName_ThrowsArgumentNullException()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => bacnetObject.GetProperty<string>(null));
        }

        [Fact]
        public void GetProperty_WithIncompatibleTypes_ThrowsInvalidCastException()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            bacnetObject.SetProperty("TestProp", new Dictionary<string, string>());

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => bacnetObject.GetProperty<int>("TestProp"));
        }

        [Fact]
        public void SetProperty_WithValidInput_SetsProperty()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            
            // Act
            bacnetObject.SetProperty("TestProp", "TestValue");
            
            // Assert
            Assert.Equal("TestValue", bacnetObject.GetProperty<string>("TestProp"));
        }

        [Fact]
        public void SetProperty_WithNullPropertyName_ThrowsArgumentNullException()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => bacnetObject.SetProperty(null, "value"));
        }

        [Fact]
        public void SetProperty_UpdatesExistingProperty()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            bacnetObject.SetProperty("TestProp", "InitialValue");
            
            // Act
            bacnetObject.SetProperty("TestProp", "UpdatedValue");
            
            // Assert
            Assert.Equal("UpdatedValue", bacnetObject.GetProperty<string>("TestProp"));
        }

        [Fact]
        public void HasProperty_WithExistingProperty_ReturnsTrue()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            bacnetObject.SetProperty("TestProp", "Value");
            
            // Act
            var result = bacnetObject.HasProperty("TestProp");
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void HasProperty_WithNonExistentProperty_ReturnsFalse()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            
            // Act
            var result = bacnetObject.HasProperty("NonExistentProp");
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HasProperty_WithNullPropertyName_ReturnsFalse()
        {
            // Arrange
            var bacnetObject = new TestBACnetObject(1, "Test");
            
            // Act
            var result = bacnetObject.HasProperty(null);
            
            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            // Arrange
            const uint objectId = 12345;
            const string objectType = "TestObject";
            var bacnetObject = new TestBACnetObject(objectId, objectType);
            bacnetObject.ObjectName = "TestName";
            
            // Act
            var result = bacnetObject.ToString();
            
            // Assert
            Assert.Equal($"{objectType}:{objectId} 'TestName'", result);
        }
    }
}