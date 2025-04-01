using System.Net;
using BACnet.Core.Objects;
using Xunit;

namespace BACnet.Device.Tests
{
    public class BACnetDeviceTests
    {
        private readonly uint _deviceId = 1234;
        private readonly string _deviceName = "Test Device";
        private readonly string _location = "Test Location";
        private readonly string _vendorName = "Test Vendor";
        private readonly uint _vendorId = 42;
        private readonly uint _modelNumber = 101;
        private readonly uint _firmwareRevision = 1;

        [Fact]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var device = new BACnetDevice(
                _deviceId,
                _deviceName,
                _location,
                _vendorName,
                _vendorId,
                _modelNumber,
                _firmwareRevision
            );

            // Assert
            Assert.Equal(_deviceId, device.DeviceId);
            Assert.Equal(_deviceName, device.DeviceName);
            Assert.Equal(_location, device.Location);
            Assert.Equal(_vendorName, device.VendorName);
            Assert.Equal(_vendorId, device.VendorId);
            Assert.Equal(_modelNumber, device.ModelNumber);
            Assert.Equal(_firmwareRevision, device.FirmwareRevision);
            Assert.Equal(0, device.NetworkNumber); // Default value
            Assert.Empty(device.MacAddress); // Default value
            Assert.Equal(47808, device.Port); // Default BACnet port
        }

        [Fact]
        public void DeviceProperties_CanBeModified()
        {
            // Arrange
            var device = CreateTestDevice();
            string newName = "New Device Name";
            string newLocation = "New Location";
            IPAddress newIp = IPAddress.Parse("192.168.1.100");
            int newPort = 47809;
            ushort newNetworkNumber = 1;
            byte[] newMacAddress = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

            // Act
            device.DeviceName = newName;
            device.Location = newLocation;
            device.IPAddress = newIp;
            device.Port = newPort;
            device.NetworkNumber = newNetworkNumber;
            device.MacAddress = newMacAddress;

            // Assert
            Assert.Equal(newName, device.DeviceName);
            Assert.Equal(newLocation, device.Location);
            Assert.Equal(newIp, device.IPAddress);
            Assert.Equal(newPort, device.Port);
            Assert.Equal(newNetworkNumber, device.NetworkNumber);
            Assert.Equal(newMacAddress, device.MacAddress);
        }

        [Fact]
        public void AddObject_AddsObjectToDevice()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogInput = new AnalogInput(1);

            // Act
            device.AddObject(analogInput);
            var objects = device.GetObjects();

            // Assert
            Assert.Contains(objects, obj => 
                obj.ObjectType == "analog-input" && 
                obj.ObjectIdentifier == 1);
        }

        [Fact]
        public void AddObject_WithNullObject_ThrowsArgumentNullException()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => device.AddObject(null));
        }

        [Fact]
        public void RemoveObject_RemovesObjectFromDevice()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogInput = new AnalogInput(1);
            device.AddObject(analogInput);

            // Act
            bool result = device.RemoveObject(analogInput);
            var objects = device.GetObjects();

            // Assert
            Assert.True(result);
            Assert.DoesNotContain(objects, obj => 
                obj.ObjectType == "analog-input" && 
                obj.ObjectIdentifier == 1);
        }

        [Fact]
        public void RemoveObject_WithNonexistentObject_ReturnsFalse()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogInput = new AnalogInput(1);

            // Act
            bool result = device.RemoveObject(analogInput);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveObject_WithNullObject_ThrowsArgumentNullException()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => device.RemoveObject(null));
        }

        [Fact]
        public void GetObject_ReturnsCorrectObject()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogInput = new AnalogInput(1 );
            var analogOutput = new AnalogOutput(2);
            device.AddObject(analogInput);
            device.AddObject(analogOutput);

            // Act
            var retrievedObject = device.GetObject("analog-input", 1);

            // Assert
            Assert.NotNull(retrievedObject);
            Assert.Equal("analog-input", retrievedObject.ObjectType);
            Assert.Equal((uint)1, retrievedObject.ObjectIdentifier);
        }

        [Fact]
        public void GetObject_WithNonexistentObject_ReturnsNull()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act
            var retrievedObject = device.GetObject("analog-input", 999);

            // Assert
            Assert.Null(retrievedObject);
        }

        [Fact]
        public void ReadProperty_ReturnsCorrectValue()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogInput = new AnalogInput(1);
            analogInput.PresentValue = (float)42.5;
            device.AddObject(analogInput);

            // Act
            var result = device.ReadProperty("analog-input", 1, "present-value");

            // Assert
            Assert.Equal(42.5, result);
        }

        [Fact]
        public void ReadProperty_WithNonexistentObject_ThrowsKeyNotFoundException()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => 
                device.ReadProperty("analog-input", 999, "present-value"));
        }

        [Fact]
        public void WriteProperty_ChangesPropertyValue()
        {
            // Arrange
            var device = CreateTestDevice();
            var analogOutput = new AnalogOutput(1);
            analogOutput.PresentValue = 0;
            device.AddObject(analogOutput);
            double newValue = 75.5;

            // Act
            device.WriteProperty("analog-output", 1, "present-value", newValue);
            var result = device.ReadProperty("analog-output", 1, "present-value");

            // Assert
            Assert.Equal(newValue, result);
        }

        [Fact]
        public void WriteProperty_WithNonexistentObject_ThrowsKeyNotFoundException()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => 
                device.WriteProperty("analog-output", 999, "present-value", 42.5));
        }

        [Fact]
        public void ToString_ReturnsExpectedFormat()
        {
            // Arrange
            var device = CreateTestDevice();

            // Act
            var result = device.ToString();

            // Assert
            Assert.Equal($"BACnet Device {_deviceId}: {_deviceName} ({_vendorName}, {_modelNumber})", result);
        }

        private BACnetDevice CreateTestDevice()
        {
            return new BACnetDevice(
                _deviceId,
                _deviceName,
                _location,
                _vendorName,
                _vendorId,
                _modelNumber,
                _firmwareRevision
            );
        }
    }
}