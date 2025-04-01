using System;
using System.Collections.Generic;
using System.Net;
using BACnet.Core.Objects;
using BACnet.Core.Services;

namespace BACnet.Device
{
    /// <summary>
    /// Represents a BACnet physical or virtual device 
    /// that can be communicated with over a BACnet network
    /// </summary>
    public class BACnetDevice
    {
        private readonly Dictionary<string, Core.Objects.BACnetObject> _objects = new Dictionary<string, Core.Objects.BACnetObject>();
        private readonly Core.Objects.Device _deviceObject;
        
        /// <summary>
        /// Gets the unique device identifier
        /// </summary>
        public uint DeviceId => _deviceObject.ObjectIdentifier;
        
        /// <summary>
        /// Gets or sets the device name
        /// </summary>
        public string DeviceName 
        { 
            get => _deviceObject.DeviceName;
            set => _deviceObject.DeviceName = value;
        }
        
        /// <summary>
        /// Gets or sets the device location
        /// </summary>
        public string Location
        {
            get => _deviceObject.Location;
            set => _deviceObject.Location = value;
        }
        
        /// <summary>
        /// Gets the device vendor name
        /// </summary>
        public string VendorName => _deviceObject.VendorName;
        
        /// <summary>
        /// Gets the device vendor ID
        /// </summary>
        public uint VendorId => _deviceObject.VendorId;
        
        /// <summary>
        /// Gets the model number
        /// </summary>
        public uint ModelNumber => _deviceObject.ModelNumber;
        
        /// <summary>
        /// Gets the firmware revision
        /// </summary>
        public uint FirmwareRevision => _deviceObject.FirmwareRevision;
        
        /// <summary>
        /// Gets or sets the device's IP address
        /// </summary>
        public IPAddress IPAddress { get; set; }
        
        /// <summary>
        /// Gets or sets the device's BACnet port
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// Gets the device's network number (0 for local devices)
        /// </summary>
        public ushort NetworkNumber { get; set; }
        
        /// <summary>
        /// Gets the device's MAC address
        /// </summary>
        public byte[] MacAddress { get; set; }

        /// <summary>
        /// Initializes a new instance of the BACnetDevice class
        /// </summary>
        /// <param name="deviceId">Unique identifier for the device</param>
        /// <param name="deviceName">Name of the device</param>
        /// <param name="location">Location of the device</param>
        /// <param name="vendorName">Name of the device vendor</param>
        /// <param name="vendorId">Vendor identifier</param>
        /// <param name="modelNumber">Model number of the device</param>
        /// <param name="firmwareRevision">Firmware revision of the device</param>
        public BACnetDevice(uint deviceId, string deviceName, string location, string vendorName, uint vendorId, uint modelNumber, uint firmwareRevision)
        {
            _deviceObject = new Core.Objects.Device(deviceId, deviceName, location, vendorName, vendorId, modelNumber, firmwareRevision);
            NetworkNumber = 0; // Default to local network
            MacAddress = new byte[0]; // Default to empty MAC
            Port = 47808; // Default BACnet port
        }
        
        /// <summary>
        /// Adds an object to the device
        /// </summary>
        /// <param name="bacnetObject">The object to add</param>
        public void AddObject(Core.Objects.BACnetObject bacnetObject)
        {
            if (bacnetObject == null)
                throw new ArgumentNullException(nameof(bacnetObject));
                
            string objectKey = $"{bacnetObject.ObjectType}:{bacnetObject.ObjectIdentifier}";
            _objects[objectKey] = bacnetObject;
            _deviceObject.AddObject(bacnetObject);
        }
        
        /// <summary>
        /// Removes an object from the device
        /// </summary>
        /// <param name="bacnetObject">The object to remove</param>
        /// <returns>True if the object was removed, false otherwise</returns>
        public bool RemoveObject(Core.Objects.BACnetObject bacnetObject)
        {
            if (bacnetObject == null)
                throw new ArgumentNullException(nameof(bacnetObject));
                
            string objectKey = $"{bacnetObject.ObjectType}:{bacnetObject.ObjectIdentifier}";
            bool removed = _objects.Remove(objectKey);
            if (removed)
            {
                _deviceObject.RemoveObject(bacnetObject);
            }
            return removed;
        }
        
        /// <summary>
        /// Gets an object by its type and identifier
        /// </summary>
        /// <param name="objectType">The object type</param>
        /// <param name="objectId">The object identifier</param>
        /// <returns>The object, or null if not found</returns>
        public Core.Objects.BACnetObject GetObject(string objectType, uint objectId)
        {
            string objectKey = $"{objectType}:{objectId}";
            return _objects.TryGetValue(objectKey, out var obj) ? obj : null;
        }
        
        /// <summary>
        /// Gets all objects in the device
        /// </summary>
        /// <returns>A read-only list of all objects</returns>
        public IReadOnlyList<Core.Objects.BACnetObject> GetObjects()
        {
            return _deviceObject.GetObjects();
        }
        
        /// <summary>
        /// Processes a read property request
        /// </summary>
        /// <param name="objectType">The object type</param>
        /// <param name="objectId">The object identifier</param>
        /// <param name="propertyId">The property identifier</param>
        /// <returns>The property value, or null if not found</returns>
        public object ReadProperty(string objectType, uint objectId, string propertyId)
        {
            var obj = GetObject(objectType, objectId);
            if (obj == null)
            {
                throw new KeyNotFoundException($"Object {objectType}:{objectId} not found");
            }
            
            var readProperty = new ReadProperty(obj, propertyId);
            return readProperty.Execute();
        }
        
        /// <summary>
        /// Processes a write property request
        /// </summary>
        /// <param name="objectType">The object type</param>
        /// <param name="objectId">The object identifier</param>
        /// <param name="propertyId">The property identifier</param>
        /// <param name="value">The value to write</param>
        public void WriteProperty(string objectType, uint objectId, string propertyId, object value)
        {
            var obj = GetObject(objectType, objectId);
            if (obj == null)
            {
                throw new KeyNotFoundException($"Object {objectType}:{objectId} not found");
            }
            
            var writeProperty = new WriteProperty(obj, propertyId, value);
            writeProperty.Execute();
        }
        
        /// <summary>
        /// Displays information about the device
        /// </summary>
        public void DisplayDeviceInfo()
        {
            Console.WriteLine($"Device ID: {DeviceId}");
            Console.WriteLine($"Device Name: {DeviceName}");
            Console.WriteLine($"Location: {Location}");
            Console.WriteLine($"Vendor Name: {VendorName}");
            Console.WriteLine($"Vendor ID: {VendorId}");
            Console.WriteLine($"Model Number: {ModelNumber}");
            Console.WriteLine($"Firmware Revision: {FirmwareRevision}");
            Console.WriteLine($"Network: {NetworkNumber}");
            
            if (IPAddress != null)
            {
                Console.WriteLine($"IP Address: {IPAddress}:{Port}");
            }
            
            if (MacAddress != null && MacAddress.Length > 0)
            {
                Console.WriteLine($"MAC Address: {BitConverter.ToString(MacAddress)}");
            }
            
            Console.WriteLine($"Object Count: {_objects.Count}");
        }
        
        /// <summary>
        /// Returns a string representation of the device
        /// </summary>
        /// <returns>A string representation of the device</returns>
        public override string ToString()
        {
            return $"BACnet Device {DeviceId}: {DeviceName} ({VendorName}, {ModelNumber})";
        }
    }
}