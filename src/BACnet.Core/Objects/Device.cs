using System;
using System.Collections.Generic;

namespace BACnet.Core.Objects
{
    public class Device : BACnetObject
    {
        public string DeviceId 
        { 
            get => GetProperty<string>("DeviceId");
            set => SetProperty("DeviceId", value); 
        }
        
        public string DeviceName 
        { 
            get => GetProperty<string>("DeviceName");
            set => SetProperty("DeviceName", value); 
        }
        
        public string Location 
        { 
            get => GetProperty<string>("Location");
            set => SetProperty("Location", value); 
        }
        
        public string VendorName 
        { 
            get => GetProperty<string>("VendorName");
            set => SetProperty("VendorName", value); 
        }
        
        public uint VendorId 
        { 
            get => GetProperty<uint>("VendorId");
            set => SetProperty("VendorId", value); 
        }
        
        public uint ModelNumber 
        { 
            get => GetProperty<uint>("ModelNumber");
            set => SetProperty("ModelNumber", value); 
        }
        
        public uint FirmwareRevision 
        { 
            get => GetProperty<uint>("FirmwareRevision");
            set => SetProperty("FirmwareRevision", value); 
        }

        private List<BACnetObject> _objects = new List<BACnetObject>();

        public Device(uint instanceNumber, string deviceName, string location, string vendorName, uint vendorId, uint modelNumber, uint firmwareRevision) : base(instanceNumber, "Device")
        {
            ObjectName = deviceName;
            
            SetProperty("DeviceId", instanceNumber.ToString());
            SetProperty("DeviceName", deviceName);
            SetProperty("Location", location);
            SetProperty("VendorName", vendorName);
            SetProperty("VendorId", vendorId);
            SetProperty("ModelNumber", modelNumber);
            SetProperty("FirmwareRevision", firmwareRevision);
        }

        public void AddObject(BACnetObject bacnetObject)
        {
            _objects.Add(bacnetObject);
        }

        public void RemoveObject(BACnetObject bacnetObject)
        {
            _objects.Remove(bacnetObject);
        }

        public IReadOnlyList<BACnetObject> GetObjects()
        {
            return _objects.AsReadOnly();
        }

        public void UpdateDeviceInfo(string deviceName, string location)
        {
            DeviceName = deviceName;
            Location = location;
        }

        public void DisplayDeviceInfo()
        {
            Console.WriteLine($"Device ID: {DeviceId}");
            Console.WriteLine($"Device Name: {DeviceName}");
            Console.WriteLine($"Location: {Location}");
            Console.WriteLine($"Vendor Name: {VendorName}");
            Console.WriteLine($"Vendor ID: {VendorId}");
            Console.WriteLine($"Model Number: {ModelNumber}");
            Console.WriteLine($"Firmware Revision: {FirmwareRevision}");
            Console.WriteLine($"Object Count: {_objects.Count}");
        }
        
        public override void ReadProperty(string propertyName)
        {
            // In a real implementation, this would read from device-specific sources
            Console.WriteLine($"Reading property {propertyName} from Device {ObjectIdentifier}");
        }

        public override void WriteProperty(string propertyName, object value)
        {
            // In a real implementation, this would write to device-specific destinations
            SetProperty(propertyName, value);
            Console.WriteLine($"Writing {value} to property {propertyName} of Device {ObjectIdentifier}");
        }
    }
}