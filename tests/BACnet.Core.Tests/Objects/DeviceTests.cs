using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BACnet.Core.Objects;
using Xunit;

namespace BACnet.Core.Tests.Objects
{
    public class DeviceTests
    {
        [Fact]
        public void Constructor_InitializesAllProperties()
        {
            // Arrange & Act
            const uint instanceNumber = 12345;
            const string deviceName = "TestDevice";
            const string location = "TestLocation";
            const string vendorName = "TestVendor";
            const uint vendorId = 1000;
            const uint modelNumber = 2000;
            const uint firmwareRevision = 3000;

            var device = new Device(
                instanceNumber, 
                deviceName, 
                location, 
                vendorName, 
                vendorId, 
                modelNumber, 
                firmwareRevision);

            // Assert
            Assert.Equal(instanceNumber.ToString(), device.DeviceId);
            Assert.Equal(deviceName, device.DeviceName);
            Assert.Equal(location, device.Location);
            Assert.Equal(vendorName, device.VendorName);
            Assert.Equal(vendorId, device.VendorId);
            Assert.Equal(modelNumber, device.ModelNumber);
            Assert.Equal(firmwareRevision, device.FirmwareRevision);
            Assert.Equal(deviceName, device.ObjectName);
            Assert.Equal("Device", device.ObjectType);
            Assert.Equal(instanceNumber, device.ObjectIdentifier);
        }

        [Fact]
        public void DeviceId_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const string newDeviceId = "98765";
            
            // Act
            device.DeviceId = newDeviceId;
            
            // Assert
            Assert.Equal(newDeviceId, device.DeviceId);
        }

        [Fact]
        public void DeviceName_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const string newDeviceName = "New Test Device";
            
            // Act
            device.DeviceName = newDeviceName;
            
            // Assert
            Assert.Equal(newDeviceName, device.DeviceName);
        }

        [Fact]
        public void Location_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const string newLocation = "New Test Location";
            
            // Act
            device.Location = newLocation;
            
            // Assert
            Assert.Equal(newLocation, device.Location);
        }

        [Fact]
        public void VendorName_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const string newVendorName = "New Test Vendor";
            
            // Act
            device.VendorName = newVendorName;
            
            // Assert
            Assert.Equal(newVendorName, device.VendorName);
        }

        [Fact]
        public void VendorId_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const uint newVendorId = 5000;
            
            // Act
            device.VendorId = newVendorId;
            
            // Assert
            Assert.Equal(newVendorId, device.VendorId);
        }

        [Fact]
        public void ModelNumber_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const uint newModelNumber = 6000;
            
            // Act
            device.ModelNumber = newModelNumber;
            
            // Assert
            Assert.Equal(newModelNumber, device.ModelNumber);
        }

        [Fact]
        public void FirmwareRevision_GetSet_Success()
        {
            // Arrange
            var device = CreateTestDevice();
            const uint newFirmwareRevision = 7000;
            
            // Act
            device.FirmwareRevision = newFirmwareRevision;
            
            // Assert
            Assert.Equal(newFirmwareRevision, device.FirmwareRevision);
        }

        [Fact]
        public void AddObject_AddsObjectToCollection()
        {
            // Arrange
            var device = CreateTestDevice();
            var testObject = new TestBACnetObject(1, "TestObject");
            
            // Act
            device.AddObject(testObject);
            var objects = device.GetObjects();
            
            // Assert
            Assert.Single(objects);
            Assert.Same(testObject, objects[0]);
        }

        [Fact]
        public void RemoveObject_RemovesObjectFromCollection()
        {
            // Arrange
            var device = CreateTestDevice();
            var testObject = new TestBACnetObject(1, "TestObject");
            device.AddObject(testObject);
            
            // Act
            device.RemoveObject(testObject);
            var objects = device.GetObjects();
            
            // Assert
            Assert.Empty(objects);
        }

        [Fact]
        public void RemoveObject_WithNonExistingObject_DoesNotThrowException()
        {
            // Arrange
            var device = CreateTestDevice();
            var testObject1 = new TestBACnetObject(1, "TestObject1");
            var testObject2 = new TestBACnetObject(2, "TestObject2");
            device.AddObject(testObject1);
            
            // Act & Assert (should not throw)
            device.RemoveObject(testObject2);
            var objects = device.GetObjects();
            
            // Assert
            Assert.Single(objects);
            Assert.Same(testObject1, objects[0]);
        }

        [Fact]
        public void GetObjects_ReturnsReadOnlyList()
        {
            // Arrange
            var device = CreateTestDevice();
            var testObject = new TestBACnetObject(1, "TestObject");
            device.AddObject(testObject);
            
            // Act
            var objects = device.GetObjects();
            
            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<BACnetObject>>(objects);
            Assert.Single(objects);
            Assert.Same(testObject, objects[0]);
            
            // Verify we cannot modify the returned list
            Assert.Throws<NotSupportedException>(() => 
            {
                var list = objects as IList<BACnetObject>;
                list.Add(new TestBACnetObject(2, "AnotherTestObject"));
            });
        }

        [Fact]
        public void UpdateDeviceInfo_UpdatesNameAndLocation()
        {
            // Arrange
            var device = CreateTestDevice();
            const string newDeviceName = "Updated Device Name";
            const string newLocation = "Updated Location";
            
            // Act
            device.UpdateDeviceInfo(newDeviceName, newLocation);
            
            // Assert
            Assert.Equal(newDeviceName, device.DeviceName);
            Assert.Equal(newLocation, device.Location);
        }

        [Fact]
        public void DisplayDeviceInfo_OutputsAllDeviceInformation()
        {
            // Arrange
            var device = CreateTestDevice();
            var testObject = new TestBACnetObject(1, "TestObject");
            device.AddObject(testObject);
            
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                // Act
                device.DisplayDeviceInfo();
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Contains($"Device ID: {device.DeviceId}", output);
                Assert.Contains($"Device Name: {device.DeviceName}", output);
                Assert.Contains($"Location: {device.Location}", output);
                Assert.Contains($"Vendor Name: {device.VendorName}", output);
                Assert.Contains($"Vendor ID: {device.VendorId}", output);
                Assert.Contains($"Model Number: {device.ModelNumber}", output);
                Assert.Contains($"Firmware Revision: {device.FirmwareRevision}", output);
                Assert.Contains($"Object Count: 1", output);
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }

        [Fact]
        public void ReadProperty_OutputsReadingMessage()
        {
            // Arrange
            var device = CreateTestDevice();
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            try
            {
                // Act
                device.ReadProperty("TestProperty");
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Contains($"Reading property TestProperty from Device {device.ObjectIdentifier}", output);
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }

        [Fact]
        public void WriteProperty_SetsPropertyAndOutputsMessage()
        {
            // Arrange
            var device = CreateTestDevice();
            var originalOutput = Console.Out;
            using var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            try
            {
                // Act
                device.WriteProperty("TestProperty", "TestValue");
                var output = stringWriter.ToString();
                
                // Assert
                Assert.Contains($"Writing TestValue to property TestProperty of Device {device.ObjectIdentifier}", output);
                Assert.Equal("TestValue", device.GetProperty<string>("TestProperty"));
            }
            finally
            {
                // Restore standard output
                Console.SetOut(originalOutput);
            }
        }

        private Device CreateTestDevice()
        {
            return new Device(
                12345,
                "TestDevice",
                "TestLocation",
                "TestVendor",
                1000,
                2000,
                3000);
        }
        
        private class TestBACnetObject : BACnetObject
        {
            public TestBACnetObject(uint objectIdentifier, string objectType) 
                : base(objectIdentifier, objectType)
            {
            }

            public override void ReadProperty(string propertyName) { }

            public override void WriteProperty(string propertyName, object value) { }
        }
    }
}