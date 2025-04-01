using BACnet.Core.Objects;
using Xunit;

namespace BACnet.Device.Tests
{
    public class BACnetDeviceIntegrationTests
    {
        [Fact]
        public void DeviceManager_TracksObjectsAddedToDevices()
        {
            // Arrange
            var deviceManager = new DeviceManager();
            var device = CreateTestDevice(1234);
            deviceManager.AddDevice(device);
            
            // Act
            var analogInput = new AnalogInput(1);
            device.AddObject(analogInput);
            
            // Assert - The device returned from manager should contain the object
            var retrievedDevice = deviceManager.GetDevice(1234);
            var objects = retrievedDevice.GetObjects();
            Assert.Contains(objects, obj => 
                obj.ObjectType == "analog-input" && 
                obj.ObjectIdentifier == 1);
        }
        
        [Fact]
        public void DeviceManagerUpdates_ReflectInMatchingDevice()
        {
            // Arrange
            var deviceManager = new DeviceManager();
            var device1 = CreateTestDevice(1234, "Original Name");
            deviceManager.AddDevice(device1);
            
            // Act
            var retrievedDevice = deviceManager.GetDevice(1234);
            retrievedDevice.DeviceName = "Updated Name";
            
            // Assert - Original device should reflect the change
            Assert.Equal("Updated Name", device1.DeviceName);
        }
        
        [Fact]
        public void ReadPropertyAcrossDevices_WorksCorrectly()
        {
            // Arrange
            var deviceManager = new DeviceManager();
            var device1 = CreateTestDevice(1234);
            var device2 = CreateTestDevice(5678);
            
            var ai1 = new AnalogInput(1);
            var ai2 = new AnalogInput(1);
            
            ai1.PresentValue = 42.5f;
            ai2.PresentValue = 99.9f;
            
            device1.AddObject(ai1);
            device2.AddObject(ai2);
            
            deviceManager.AddDevice(device1);
            deviceManager.AddDevice(device2);
            
            // Act
            var value1 = deviceManager.GetDevice(1234).ReadProperty("analog-input", 1, "present-value");
            var value2 = deviceManager.GetDevice(5678).ReadProperty("analog-input", 1, "present-value");
            
            // Assert
            Assert.Equal(42.5f, value1);
            Assert.Equal(99.9f, value2);
        }
        
        private BACnetDevice CreateTestDevice(uint deviceId, string deviceName = "Test Device")
        {
            return new BACnetDevice(
                deviceId,
                deviceName,
                "Test Location",
                "Test Vendor",
                42,
                101,
                1
            );
        }
    }
}