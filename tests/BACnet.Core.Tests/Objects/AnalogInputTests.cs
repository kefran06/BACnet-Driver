using System;
using System.IO;
using BACnet.Core.Objects;
using Xunit;

namespace BACnet.Core.Tests.Objects
{
    public class AnalogInputTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            // Arrange & Act
            const uint instanceNumber = 123;
            var analogInput = new AnalogInput(instanceNumber);
            
            // Assert
            Assert.Equal(instanceNumber, analogInput.ObjectIdentifier);
            Assert.Equal("AnalogInput", analogInput.ObjectType);
            Assert.Equal($"AI_{instanceNumber}", analogInput.ObjectName);
            Assert.Equal(0.0f, analogInput.PresentValue);
            Assert.Equal(float.MinValue, analogInput.MinPresentValue);
            Assert.Equal(float.MaxValue, analogInput.MaxPresentValue);
            Assert.Equal("units", analogInput.Units);
        }

        [Fact]
        public void PresentValue_GetSet_Success()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const float newValue = 42.5f;
            
            // Act
            analogInput.PresentValue = newValue;
            
            // Assert
            Assert.Equal(newValue, analogInput.PresentValue);
        }

        [Fact]
        public void MinPresentValue_GetSet_Success()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const float newMin = -100.0f;
            
            // Act
            analogInput.MinPresentValue = newMin;
            
            // Assert
            Assert.Equal(newMin, analogInput.MinPresentValue);
        }

        [Fact]
        public void MaxPresentValue_GetSet_Success()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const float newMax = 100.0f;
            
            // Act
            analogInput.MaxPresentValue = newMax;
            
            // Assert
            Assert.Equal(newMax, analogInput.MaxPresentValue);
        }

        [Fact]
        public void Units_GetSet_Success()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const string newUnits = "celsius";
            
            // Act
            analogInput.Units = newUnits;
            
            // Assert
            Assert.Equal(newUnits, analogInput.Units);
        }

        [Fact]
        public void UpdateValue_ValidValue_UpdatesPresentValue()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            analogInput.MinPresentValue = -100.0f;
            analogInput.MaxPresentValue = 100.0f;
            const float newValue = 75.5f;
            
            // Act
            analogInput.UpdateValue(newValue);
            
            // Assert
            Assert.Equal(newValue, analogInput.PresentValue);
        }

        [Fact]
        public void UpdateValue_ValueBelowMin_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            analogInput.MinPresentValue = -100.0f;
            analogInput.MaxPresentValue = 100.0f;
            const float invalidValue = -150.0f;
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => analogInput.UpdateValue(invalidValue));
        }

        [Fact]
        public void UpdateValue_ValueAboveMax_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            analogInput.MinPresentValue = -100.0f;
            analogInput.MaxPresentValue = 100.0f;
            const float invalidValue = 150.0f;
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => analogInput.UpdateValue(invalidValue));
        }

        [Fact]
        public void ReadProperty_OutputsReadingMessage()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Act
                analogInput.ReadProperty("PresentValue");
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Contains($"Reading property PresentValue from AnalogInput {analogInput.ObjectIdentifier}", output);
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }

        [Fact]
        public void WriteProperty_ValidPresentValue_UpdatesValueAndOutputsMessage()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            analogInput.MinPresentValue = -100.0f;
            analogInput.MaxPresentValue = 100.0f;
            const float newValue = 50.0f;
            
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Act
                analogInput.WriteProperty("PresentValue", newValue);
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Equal(newValue, analogInput.PresentValue);
                Assert.Contains($"Writing {newValue} to property PresentValue of AnalogInput {analogInput.ObjectIdentifier}", output);
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }

        [Fact]
        public void WriteProperty_InvalidPresentValueType_ThrowsArgumentException()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const string invalidValue = "not a float";
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => analogInput.WriteProperty("PresentValue", invalidValue));
        }

        [Fact]
        public void WriteProperty_NonPresentValueProperty_SetsPropertyAndOutputsMessage()
        {
            // Arrange
            var analogInput = new AnalogInput(123);
            const string propertyName = "Units";
            const string newValue = "celsius";
            
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Act
                analogInput.WriteProperty(propertyName, newValue);
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Equal(newValue, analogInput.Units);
                Assert.Contains($"Writing {newValue} to property {propertyName} of AnalogInput {analogInput.ObjectIdentifier}", output);
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }
    }
}