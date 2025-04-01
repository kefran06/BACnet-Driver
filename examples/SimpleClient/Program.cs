using System.Net;
using BACnet.Client;
using BACnet.Core.Objects;
using BACnet.Core.Protocol;
using BACnet.Device;

namespace SimpleClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("BACnet Simple Client");
            Console.WriteLine("=====================");
            
            try
            {
                // Create a device manager for tracking discovered devices
                var deviceManager = new DeviceManager();
                
                // Listen for device discovery events
                deviceManager.DeviceDiscovered += (sender, e) =>
                {
                    Console.WriteLine($"Discovered device: {e.Device.DeviceName} (ID: {e.Device.DeviceId})");
                    Console.WriteLine($"  IP Address: {e.Device.IPAddress}:{e.Device.Port}");
                    Console.WriteLine($"  Vendor: {e.Device.VendorName} (ID: {e.Device.VendorId})");
                    Console.WriteLine();
                };
                
                // Discover devices on the network (actual network broadcast)
                Console.WriteLine("Discovering BACnet devices on the network...");
                Console.WriteLine("This will send a WhoIs broadcast and listen for I-Am responses.");
                Console.WriteLine("Waiting for 8 seconds to collect responses...");
                
                // Create broadcast address based on local subnet or use subnet broadcast
                IPAddress broadcastAddress;
                
                // Try to use a broadcast address that will work in most environments
                try 
                {
                    // You can customize this to your specific network needs
                    broadcastAddress = IPAddress.Parse("255.255.255.255"); // Global broadcast
                    // Alternative: Use subnet broadcast like 192.168.1.255
                }
                catch 
                {
                    // Fallback to global broadcast
                    broadcastAddress = IPAddress.Broadcast;
                }
                
                // Start real network discovery with 8 second timeout
                var discoveredDevices = await deviceManager.DiscoverDevicesAsync(
                    broadcastAddress, 
                    47808,  // Standard BACnet port
                    8000);  // 8 second timeout
                
                // Show discovery summary
                Console.WriteLine();
                Console.WriteLine($"Discovery complete. Found {discoveredDevices.Count} device(s).");
                
                // If no devices found, show help message
                if (discoveredDevices.Count == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("No devices found. Possible reasons:");
                    Console.WriteLine("1. No BACnet devices are present on the network");
                    Console.WriteLine("2. Broadcast packets are being blocked by a firewall");
                    Console.WriteLine("3. The network doesn't support broadcast packets");
                    Console.WriteLine("4. BACnet devices are on a different subnet");
                }
                else
                {
                    // Create a BACnet client for the first discovered device
                    var firstDevice = discoveredDevices[0];
                    Console.WriteLine();
                    Console.WriteLine($"Connecting to device {firstDevice.DeviceId} at {firstDevice.IPAddress}:{firstDevice.Port}...");
                    
                    // Create cancellation token source and task for client initialization
                    var cancellationTokenSource = new CancellationTokenSource();
                    var messageHandlingTask = Task.CompletedTask;
                    
                    var client = new BACnetClient(firstDevice.IPAddress.ToString(), firstDevice.Port, cancellationTokenSource, messageHandlingTask);
                    
                    try
                    {
                        client.Connect();
                        
                        // Hook up event handler for received messages
                        client.MessageReceived += (sender, e) =>
                        {
                            // Cast the message to byte array which is expected by BACnet decoder
                            byte[] messageBytes = e.Message as byte[];
                            
                            if (messageBytes == null)
                            {
                                Console.WriteLine("Error: Received message is not a byte array");
                                return;
                            }
                            
                            Console.WriteLine($"Received message of {messageBytes.Length} bytes");
                            
                            // Parse the message
                            try
                            {
                                var bvlc = new BVLC();
                                bvlc.Decode(messageBytes);
                                
                                var npdu = new NPDU();
                                int consumed = npdu.Decode(bvlc.Data, 0, bvlc.Data.Length);
                                
                                var apdu = new APDU();
                                if (npdu.ApplicationData != null && npdu.ApplicationData.Length > 0)
                                {
                                    apdu.Decode(npdu.ApplicationData);
                                    Console.WriteLine($"  Message type: {GetMessageTypeName(apdu.PDUType)}, Service: {GetServiceName(apdu.ServiceChoice)}");
                                    
                                    // Handle different response types
                                    if (apdu.PDUType == APDU.ComplexAck && apdu.ServiceChoice == APDU.ReadProperty)
                                    {
                                        ProcessReadPropertyResponse(apdu);
                                    }
                                    else if (apdu.PDUType == APDU.UnconfirmedRequest && apdu.ServiceChoice == APDU.IAm)
                                    {
                                        ProcessIAmResponse(apdu);
                                    }
                                    else if (apdu.PDUType == APDU.Error)
                                    {
                                        ProcessErrorResponse(apdu);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error parsing message: {ex.Message}");
                            }
                        };
                        
                        // Send a targeted WhoIs to get device information again
                        Console.WriteLine("Sending targeted WhoIs request...");
                        client.DiscoverDevices();
                        
                        // Wait a bit for responses
                        await Task.Delay(2000);
                        
                        // Try to read properties from the device if it has objects
                        Console.WriteLine("\nReading device properties...");
                        
                        try
                        {
                            // Read the device object name (for demonstration)
                            Console.WriteLine("Reading device object name...");
                            client.SendReadPropertyRequest(8, firstDevice.DeviceId, 77); // Object type 8=Device, property 77=ObjectName
                            await Task.Delay(2000);
                            
                            // Try to read analog inputs if available
                            Console.WriteLine("\nAttempting to read analog input values...");
                            for (uint i = 1; i <= 5; i++) // Try first 5 analog inputs
                            {
                                Console.WriteLine($"Reading Analog Input {i} present value...");
                                client.SendReadPropertyRequest(0, i, 85); // Object type 0=AnalogInput, property 85=PresentValue
                                await Task.Delay(500);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading property: {ex.Message}");
                        }
                    }
                    finally
                    {
                        // Always disconnect properly
                        client.Disconnect();
                        Console.WriteLine("Client disconnected.");
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }
        
        // Helper methods for BACnet message processing
        
        private static string GetMessageTypeName(byte pduType)
        {
            switch (pduType)
            {
                case APDU.ConfirmedRequest: return "Confirmed Request";
                case APDU.UnconfirmedRequest: return "Unconfirmed Request";
                case APDU.SimpleAck: return "Simple ACK";
                case APDU.ComplexAck: return "Complex ACK";
                case APDU.SegmentAck: return "Segment ACK";
                case APDU.Error: return "Error";
                case APDU.Reject: return "Reject";
                case APDU.Abort: return "Abort";
                default: return $"Unknown (0x{pduType:X2})";
            }
        }
        
        private static string GetServiceName(byte serviceChoice)
        {
            switch (serviceChoice)
            {
                case APDU.WhoIs: return "Who-Is";
                case APDU.IAm: return "I-Am";
                case APDU.ReadProperty: return "ReadProperty";
                case APDU.WriteProperty: return "WriteProperty";
                default: return $"Unknown (0x{serviceChoice:X2})";
            }
        }
        
        private static void ProcessIAmResponse(APDU apdu)
        {
            try
            {
                if (apdu.Parameters == null || apdu.Parameters.Length < 7)
                {
                    Console.WriteLine("  Invalid I-Am response: missing parameters");
                    return;
                }

                using (var stream = new MemoryStream(apdu.Parameters))
                using (var reader = new BinaryReader(stream))
                {
                    // Read object identifier (Device ID)
                    byte tag = reader.ReadByte();
                    if ((tag >> 4) != 12) // Application tag 12 (Object Identifier)
                    {
                        Console.WriteLine("  Invalid I-Am response: missing object identifier");
                        return;
                    }
                    
                    uint objectId = ReadUInt32(reader);
                    ushort objectType = (ushort)(objectId >> 22);
                    uint instanceNumber = objectId & 0x3FFFFF;
                    
                    // Read max APDU length
                    byte maxAPDUTag = reader.ReadByte();
                    byte maxAPDULen = reader.ReadByte();
                    int maxAPDU = GetMaxAPDU(maxAPDULen);
                    
                    // Read segmentation support
                    byte segmentationTag = reader.ReadByte();
                    byte segmentation = reader.ReadByte();
                    
                    // Read vendor ID
                    byte vendorIdTag = reader.ReadByte();
                    byte vendorId = reader.ReadByte();
                    
                    Console.WriteLine($"  Device ID: {instanceNumber}");
                    Console.WriteLine($"  Max APDU Length: {maxAPDU} bytes");
                    Console.WriteLine($"  Segmentation: {GetSegmentationName(segmentation)}");
                    Console.WriteLine($"  Vendor ID: {vendorId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing I-Am: {ex.Message}");
            }
        }
        
        private static void ProcessReadPropertyResponse(APDU apdu)
        {
            try
            {
                if (apdu.Parameters == null)
                {
                    Console.WriteLine("  Empty ReadProperty response");
                    return;
                }
                
                using (var stream = new MemoryStream(apdu.Parameters))
                using (var reader = new BinaryReader(stream))
                {
                    // Read object identifier
                    byte tag = reader.ReadByte();
                    if ((tag & 0x08) == 0 || (tag & 0x07) != 0) // Not context tag 0
                    {
                        Console.WriteLine("  Invalid ReadProperty response: missing object identifier");
                        return;
                    }
                    
                    reader.ReadByte(); // Length, should be 4
                    uint objectId = ReadUInt32(reader);
                    ushort objectType = (ushort)(objectId >> 22);
                    uint instanceNumber = objectId & 0x3FFFFF;
                    
                    // Read property identifier
                    tag = reader.ReadByte();
                    if ((tag & 0x08) == 0 || (tag & 0x07) != 1) // Not context tag 1
                    {
                        Console.WriteLine("  Invalid ReadProperty response: missing property identifier");
                        return;
                    }
                    
                    byte len = reader.ReadByte();
                    uint propertyId = ReadUInt(reader, len);
                    
                    // Skip optional array index if present
                    tag = reader.ReadByte();
                    if ((tag & 0x08) != 0 && (tag & 0x07) == 2) // Context tag 2
                    {
                        len = reader.ReadByte();
                        reader.ReadBytes(len); // Skip array index
                        tag = reader.ReadByte(); // Read next tag
                    }
                    
                    // Read property value
                    if ((tag & 0x08) == 0 || (tag & 0x07) != 3) // Not context tag 3
                    {
                        Console.WriteLine("  Invalid ReadProperty response: missing property value");
                        return;
                    }
                    
                    if ((tag & 0x07) == 3 && (tag & 0xF0) == 0x30) // Opening tag 3
                    {
                        // Read the first value tag
                        tag = reader.ReadByte();
                        
                        // Decode application-tagged value
                        object value = DecodeApplicationTaggedValue(tag, reader);
                        
                        Console.WriteLine($"  Object: {GetObjectTypeName(objectType)} {instanceNumber}");
                        Console.WriteLine($"  Property: {GetPropertyName(propertyId)}");
                        Console.WriteLine($"  Value: {value}");
                    }
                    else
                    {
                        Console.WriteLine("  Invalid property value encoding");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing ReadProperty response: {ex.Message}");
            }
        }
        
        private static void ProcessErrorResponse(APDU apdu)
        {
            try
            {
                if (apdu.Parameters == null || apdu.Parameters.Length < 3)
                {
                    Console.WriteLine("  Invalid Error response");
                    return;
                }
                
                using (var stream = new MemoryStream(apdu.Parameters))
                using (var reader = new BinaryReader(stream))
                {
                    // Read original service
                    byte tag = reader.ReadByte();
                    if ((tag & 0x08) == 0 || (tag & 0x07) != 0) // Not context tag 0
                    {
                        Console.WriteLine("  Invalid Error response format");
                        return;
                    }
                    
                    byte len = reader.ReadByte();
                    byte originalService = reader.ReadByte();
                    
                    // Read error class
                    tag = reader.ReadByte();
                    if ((tag >> 4) != 9) // Not enumerated (9)
                    {
                        Console.WriteLine("  Invalid Error response: missing error class");
                        return;
                    }
                    
                    byte errorClass = reader.ReadByte();
                    
                    // Read error code
                    tag = reader.ReadByte();
                    if ((tag >> 4) != 9) // Not enumerated (9)
                    {
                        Console.WriteLine("  Invalid Error response: missing error code");
                        return;
                    }
                    
                    byte errorCode = reader.ReadByte();
                    
                    Console.WriteLine($"  Service: {GetServiceName(originalService)}");
                    Console.WriteLine($"  Error Class: {GetErrorClassName(errorClass)}");
                    Console.WriteLine($"  Error Code: {GetErrorCodeName(errorCode)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing Error response: {ex.Message}");
            }
        }
        
        private static string GetObjectTypeName(ushort objectType)
        {
            switch (objectType)
            {
                case 0: return "Analog Input";
                case 1: return "Analog Output";
                case 2: return "Analog Value";
                case 3: return "Binary Input";
                case 4: return "Binary Output";
                case 5: return "Binary Value";
                case 8: return "Device";
                case 13: return "Multi-state Input";
                case 14: return "Multi-state Output";
                case 19: return "Multi-state Value";
                default: return $"Unknown ({objectType})";
            }
        }
        
        private static string GetPropertyName(uint propertyId)
        {
            switch (propertyId)
            {
                case 76: return "Present Value";
                case 77: return "Object Name";
                case 78: return "Priority Array";
                case 79: return "Description";
                case 85: return "Reliability";
                case 103: return "Units";
                case 104: return "Update Interval";
                case 117: return "Object Identifier";
                case 118: return "Object Name";
                case 119: return "Object Type";
                default: return $"Unknown ({propertyId})";
            }
        }
        
        private static string GetSegmentationName(byte segmentation)
        {
            switch (segmentation)
            {
                case 0: return "Segmented Both";
                case 1: return "Segmented Transmit";
                case 2: return "Segmented Receive";
                case 3: return "No Segmentation";
                default: return $"Unknown ({segmentation})";
            }
        }
        
        private static int GetMaxAPDU(byte maxAPDULenCode)
        {
            switch (maxAPDULenCode)
            {
                case 0: return 50;
                case 1: return 128;
                case 2: return 206;
                case 3: return 480;
                case 4: return 1024;
                case 5: return 1476;
                default: return 480; // Default to a reasonable value
            }
        }
        
        private static string GetErrorClassName(byte errorClass)
        {
            switch (errorClass)
            {
                case 0: return "Device";
                case 1: return "Object";
                case 2: return "Property";
                case 3: return "Resources";
                case 4: return "Security";
                case 5: return "Services";
                case 6: return "VT";
                case 7: return "Communication";
                default: return $"Unknown ({errorClass})";
            }
        }
        
        private static string GetErrorCodeName(byte errorCode)
        {
            switch (errorCode)
            {
                case 0: return "Other";
                case 32: return "Device Not Found";
                case 31: return "Object Unknown";
                case 25: return "Property Unknown";
                case 33: return "Value Out of Range";
                case 42: return "Optional Functionality Not Supported";
                case 104: return "Timeout";
                default: return $"Unknown ({errorCode})";
            }
        }
        
        private static object DecodeApplicationTaggedValue(byte tag, BinaryReader reader)
        {
            byte tagNumber = (byte)(tag >> 4);
            byte length = (byte)(tag & 0x07);
            bool isExtendedLength = (length == 5);
            
            if (isExtendedLength)
            {
                // Extended length encoding
                byte lenInfo = reader.ReadByte();
                if (lenInfo == 1)
                {
                    length = reader.ReadByte();
                }
                else if (lenInfo == 2)
                {
                    byte b1 = reader.ReadByte();
                    byte b2 = reader.ReadByte();
                    length = (byte)((b1 << 8) | b2);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported extended length: {lenInfo}");
                }
            }
            
            switch (tagNumber)
            {
                case 0: // Null
                    return "Null";
                    
                case 1: // Boolean
                    return (length == 1) ? true : false;
                    
                case 2: // Unsigned Int
                    return ReadUInt(reader, length);
                    
                case 3: // Signed Int
                    return ReadInt(reader, length);
                    
                case 4: // Real (Float)
                    if (length == 4)
                    {
                        byte[] bytes = reader.ReadBytes(4);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }
                        return BitConverter.ToSingle(bytes, 0);
                    }
                    else
                    {
                        throw new NotSupportedException($"Invalid length for Real: {length}");
                    }
                    
                case 5: // Double
                    if (length == 8)
                    {
                        byte[] bytes = reader.ReadBytes(8);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }
                        return BitConverter.ToDouble(bytes, 0);
                    }
                    else
                    {
                        throw new NotSupportedException($"Invalid length for Double: {length}");
                    }
                    
                case 6: // Octet String
                    return $"[{length} octets]";
                    
                case 7: // Character String
                    byte encoding = reader.ReadByte();
                    if (encoding != 0) // Only ANSI X3.4 is supported now
                    {
                        return $"[String with unsupported encoding: {encoding}]";
                    }
                    
                    byte[] stringBytes = reader.ReadBytes(length - 1);
                    return System.Text.Encoding.ASCII.GetString(stringBytes);
                    
                default:
                    reader.ReadBytes(length);
                    return $"[{GetApplicationTagName(tagNumber)}: {length} bytes]";
            }
        }
        
        private static string GetApplicationTagName(byte tagNumber)
        {
            switch (tagNumber)
            {
                case 0: return "Null";
                case 1: return "Boolean";
                case 2: return "Unsigned Int";
                case 3: return "Signed Int";
                case 4: return "Real";
                case 5: return "Double";
                case 6: return "Octet String";
                case 7: return "Character String";
                case 8: return "Bit String";
                case 9: return "Enumerated";
                case 10: return "Date";
                case 11: return "Time";
                case 12: return "Object Identifier";
                default: return $"Unknown ({tagNumber})";
            }
        }
        
        private static uint ReadUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }
        
        private static uint ReadUInt(BinaryReader reader, int length)
        {
            uint value = 0;
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | reader.ReadByte();
            }
            return value;
        }
        
        private static int ReadInt(BinaryReader reader, int length)
        {
            // Handle sign bit
            byte firstByte = reader.ReadByte();
            bool isNegative = (firstByte & 0x80) != 0;
            
            int value = firstByte;
            for (int i = 1; i < length; i++)
            {
                value = (value << 8) | reader.ReadByte();
            }
            
            return value;
        }
    }
}