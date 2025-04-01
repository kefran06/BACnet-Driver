using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using BACnet.Core.Objects;
using BACnet.Core.Protocol;
using BACnet.Device;
using BACnet.Transport.IP;

namespace SimpleServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("BACnet Simple Server");
            Console.WriteLine("====================");
            
            // Create a CancellationTokenSource for graceful shutdown
            var cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                // Create BACnet virtual device
                var device = CreateVirtualDevice();
                
                // Display device information
                device.DisplayDeviceInfo();
                
                // Start BACnet IP server
                var server = new BACnetIPServer(47808);
                
                // Handle incoming messages
                server.MessageReceived += (sender, e) => 
                {
                    // Cast the message to byte array
                    byte[] messageBytes = e.Message as byte[];
                    
                    if (messageBytes == null)
                    {
                        Console.WriteLine($"Error: Received message is not a byte array");
                        return;
                    }
                    
                    Console.WriteLine($"Received message from {e.RemoteEndPoint}, {messageBytes.Length} bytes");
                    
                    try
                    {
                        // Parse the BVLC message
                        var bvlc = new BVLC();
                        bvlc.Decode(messageBytes);
                        
                        // Extract NPDU from BVLC
                        var npdu = new NPDU();
                        int consumed = npdu.Decode(bvlc.Data, 0, bvlc.Data.Length);
                        
                        // Extract APDU from NPDU
                        var apdu = new APDU();
                        
                        // Only process if there's application data
                        if (npdu.ApplicationData != null && npdu.ApplicationData.Length > 0)
                        {
                            apdu.Decode(npdu.ApplicationData);
                            
                            // Process the message based on type
                            ProcessMessage(apdu, device, e.RemoteEndPoint, server);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing message: {ex.Message}");
                    }
                };
                
                // Start the server
                Console.WriteLine("Starting BACnet server on port 47808...");
                server.Start();
                
                Console.WriteLine("Server is running. Press Ctrl+C to stop.");
                Console.WriteLine($"Virtual device {device.DeviceId} ({device.DeviceName}) ready to respond to BACnet requests");
                
                // Use TaskCompletionSource to keep the application running
                var tcs = new TaskCompletionSource<bool>();
                
                // Handle Ctrl+C to initiate graceful shutdown
                Console.CancelKeyPress += (sender, e) => 
                {
                    e.Cancel = true;  // Prevent the process from terminating immediately
                    cancellationTokenSource.Cancel();
                    tcs.TrySetResult(true);
                };
                
                // Simulate device behavior (e.g., updating sensor values)
                _ = SimulateDeviceAsync(device, cancellationTokenSource.Token);
                
                // Wait until canceled
                await tcs.Task;
                
                // Clean up
                Console.WriteLine("Stopping server...");
                server.Stop();
                Console.WriteLine("Server stopped.");
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
        
        /// <summary>
        /// Creates a virtual BACnet device with various object types
        /// </summary>
        private static BACnetDevice CreateVirtualDevice()
        {
            // Create the BACnet device
            var device = new BACnetDevice(
                389001,                 // Device ID
                "Virtual BACnet Device", // Name
                "Server Room",          // Location
                "ACME BACnet",          // Vendor name
                42,                     // Vendor ID
                101,                    // Model number
                1                       // Firmware revision
            );
            
            // Set IP information
            device.IPAddress = IPAddress.Parse("127.0.0.1");
            device.Port = 47808;
            
            // Create some analog inputs (e.g., temperature sensors)
            var tempSensor1 = new AnalogInput(1)
            {
                ObjectName = "Zone Temp 1",
                PresentValue = 72.5f,
                Units = "degF"
            };
            
            var tempSensor2 = new AnalogInput(2)
            {
                ObjectName = "Zone Temp 2",
                PresentValue = 73.8f,
                Units = "degF"
            };
            
            var humiditySensor = new AnalogInput(3)
            {
                ObjectName = "Zone Humidity",
                PresentValue = 45.0f,
                Units = "%RH"
            };
            
            // Create some analog outputs (e.g., setpoints)
            var coolSetpoint = new AnalogOutput(1)
            {
                ObjectName = "Cool Setpoint",
                PresentValue = 74.0f,
                Units = "degF"
            };
            
            var heatSetpoint = new AnalogOutput(2)
            {
                ObjectName = "Heat Setpoint",
                PresentValue = 68.0f,
                Units = "degF"
            };
            
            // Add all objects to the device
            device.AddObject(tempSensor1);
            device.AddObject(tempSensor2);
            device.AddObject(humiditySensor);
            device.AddObject(coolSetpoint);
            device.AddObject(heatSetpoint);
            
            return device;
        }
        
        /// <summary>
        /// Processes a BACnet message and generates appropriate responses
        /// </summary>
        private static void ProcessMessage(APDU apdu, BACnetDevice device, IPEndPoint remoteEndPoint, BACnetIPServer server)
        {
            Console.WriteLine($"Processing APDU: PDU Type={apdu.PDUType:X2}, Service Choice={apdu.ServiceChoice:X2}");
            
            // Handle WhoIs request (real implementation)
            if (apdu.PDUType == APDU.UnconfirmedRequest && apdu.ServiceChoice == APDU.WhoIs)
            {
                Console.WriteLine("Received WhoIs request, sending I-Am response");
                
                // Parse WhoIs request to check instance range if specified
                uint? lowRange = null;
                uint? highRange = null;
                
                if (apdu.Parameters != null && apdu.Parameters.Length > 0)
                {
                    try
                    {
                        using (var stream = new MemoryStream(apdu.Parameters))
                        using (var reader = new BinaryReader(stream))
                        {
                            // Try to read context tags for low and high range
                            byte tag = reader.ReadByte();
                            if ((tag & 0x08) != 0 && (tag & 0x07) == 0) // Context tag 0
                            {
                                byte len = reader.ReadByte();
                                lowRange = ReadUInt(reader, len);
                                
                                tag = reader.ReadByte();
                                if ((tag & 0x08) != 0 && (tag & 0x07) == 1) // Context tag 1
                                {
                                    len = reader.ReadByte();
                                    highRange = ReadUInt(reader, len);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing WhoIs range: {ex.Message}");
                    }
                }
                
                // Check if our device is in the specified range
                if ((lowRange.HasValue && highRange.HasValue) &&
                    (device.DeviceId < lowRange.Value || device.DeviceId > highRange.Value))
                {
                    Console.WriteLine($"Device ID {device.DeviceId} not in requested range {lowRange}-{highRange}");
                    return;
                }
                
                // Create a real I-Am response according to the BACnet standard
                SendIAmResponse(device, remoteEndPoint, server);
            }
            else if (apdu.PDUType == APDU.ConfirmedRequest && apdu.ServiceChoice == APDU.ReadProperty)
            {
                Console.WriteLine("Received ReadProperty request");
                
                try
                {
                    using (var stream = new MemoryStream(apdu.Parameters))
                    using (var reader = new BinaryReader(stream))
                    {
                        // Parse the ReadProperty request according to BACnet standard
                        // First, get object identifier (context tag 0)
                        byte tag = reader.ReadByte();
                        if ((tag & 0x08) != 0 && (tag & 0x07) == 0) // Context tag 0
                        {
                            reader.ReadByte(); // Length, should be 4
                            uint objectId = ReadUInt32(reader);
                            ushort objectType = (ushort)(objectId >> 22);
                            uint instanceNumber = objectId & 0x3FFFFF;
                            
                            // Next, get property identifier (context tag 1)
                            tag = reader.ReadByte();
                            if ((tag & 0x08) != 0 && (tag & 0x07) == 1) // Context tag 1
                            {
                                byte len = reader.ReadByte();
                                uint propertyId = ReadUInt(reader, len);
                                
                                Console.WriteLine($"Read request for object type={objectType}, instance={instanceNumber}, property={propertyId}");
                                
                                // Find the requested object
                                var bacnetObject = device.GetObject(GetObjectTypeName(objectType), instanceNumber);
                                if (bacnetObject != null)
                                {
                                    // Get the property value (simplified - in real code would map propertyId to actual property)
                                    object? propertyValue = default;
                                    string propertyName = GetPropertyName(propertyId);
                                    
                                    if (bacnetObject is AnalogInput analogInput && propertyName == "PresentValue")
                                    {
                                        propertyValue = analogInput.PresentValue;
                                        Console.WriteLine($"Found property value: {propertyValue}");
                                        
                                        // Create response data
                                        using (var responseStream = new MemoryStream())
                                        using (var writer = new BinaryWriter(responseStream))
                                        {
                                            // Context tag 0 - ObjectIdentifier
                                            writer.Write((byte)0x0C); // Context tag 0, length 4
                                            writer.Write((byte)0x04); // Length 4
                                            writer.Write((byte)((objectId >> 24) & 0xFF));
                                            writer.Write((byte)((objectId >> 16) & 0xFF));
                                            writer.Write((byte)((objectId >> 8) & 0xFF));
                                            writer.Write((byte)(objectId & 0xFF));
                                            
                                            // Context tag 1 - Property ID
                                            writer.Write((byte)0x19); // Context tag 1, length 1
                                            writer.Write((byte)propertyId);
                                            
                                            // Context tag 3 - Property Value
                                            writer.Write((byte)0x3E); // Context tag 3, opening
                                            
                                            // Application tag 4 - Real (float)
                                            writer.Write((byte)0x44); // App tag 4, length 4
                                            float floatValue = (float)propertyValue;
                                            byte[] floatBytes = BitConverter.GetBytes(floatValue);
                                            if (BitConverter.IsLittleEndian)
                                            {
                                                Array.Reverse(floatBytes);
                                            }
                                            writer.Write(floatBytes);
                                            
                                            // Close context tag 3
                                            writer.Write((byte)0x3F); // Context tag 3, closing
                                            
                                            // Create response APDU
                                            var responseApdu = new APDU
                                            {
                                                PDUType = APDU.ComplexAck,
                                                ServiceChoice = APDU.ReadProperty,
                                                InvokeID = apdu.InvokeID,
                                                Parameters = responseStream.ToArray()
                                            };
                                            
                                            // Send response
                                            SendResponse(responseApdu, remoteEndPoint, server);
                                            Console.WriteLine($"Sent ReadProperty response for {bacnetObject.ObjectName}, property={propertyName}, value={propertyValue}");
                                            return;
                                        }
                                    }
                                    else if (bacnetObject is AnalogOutput analogOutput && propertyName == "PresentValue")
                                    {
                                        propertyValue = analogOutput.PresentValue;
                                        Console.WriteLine($"Found property value: {propertyValue}");
                                        
                                        // Create response data - same code as above, but for an AnalogOutput
                                        using (var responseStream = new MemoryStream())
                                        using (var writer = new BinaryWriter(responseStream))
                                        {
                                            // Context tag 0 - ObjectIdentifier
                                            writer.Write((byte)0x0C); // Context tag 0, length 4
                                            writer.Write((byte)0x04); // Length 4
                                            writer.Write((byte)((objectId >> 24) & 0xFF));
                                            writer.Write((byte)((objectId >> 16) & 0xFF));
                                            writer.Write((byte)((objectId >> 8) & 0xFF));
                                            writer.Write((byte)(objectId & 0xFF));
                                            
                                            // Context tag 1 - Property ID
                                            writer.Write((byte)0x19); // Context tag 1, length 1
                                            writer.Write((byte)propertyId);
                                            
                                            // Context tag 3 - Property Value
                                            writer.Write((byte)0x3E); // Context tag 3, opening
                                            
                                            // Application tag 4 - Real (float)
                                            writer.Write((byte)0x44); // App tag 4, length 4
                                            float floatValue = (float)propertyValue;
                                            byte[] floatBytes = BitConverter.GetBytes(floatValue);
                                            if (BitConverter.IsLittleEndian)
                                            {
                                                Array.Reverse(floatBytes);
                                            }
                                            writer.Write(floatBytes);
                                            
                                            // Close context tag 3
                                            writer.Write((byte)0x3F); // Context tag 3, closing
                                            
                                            // Create response APDU
                                            var responseApdu = new APDU
                                            {
                                                PDUType = APDU.ComplexAck,
                                                ServiceChoice = APDU.ReadProperty,
                                                InvokeID = apdu.InvokeID,
                                                Parameters = responseStream.ToArray()
                                            };
                                            
                                            // Send response
                                            SendResponse(responseApdu, remoteEndPoint, server);
                                            Console.WriteLine($"Sent ReadProperty response for {bacnetObject.ObjectName}, property={propertyName}, value={propertyValue}");
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Object not found: type={objectType}, instance={instanceNumber}");
                                    
                                    // Create error response - Object Unknown
                                    var responseApdu = CreateErrorResponse(apdu.InvokeID, APDU.ReadProperty, 0x1F, 0x31);
                                    SendResponse(responseApdu, remoteEndPoint, server);
                                    Console.WriteLine("Sent Error response: Object Unknown");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing ReadProperty: {ex.Message}");
                    
                    // Send Error response - Other Error
                    var responseApdu = CreateErrorResponse(apdu.InvokeID, APDU.ReadProperty, 0x1F, 0x00);
                    SendResponse(responseApdu, remoteEndPoint, server);
                    Console.WriteLine("Sent Error response: General Error");
                }
            }
            else if (apdu.PDUType == APDU.ConfirmedRequest && apdu.ServiceChoice == APDU.WriteProperty)
            {
                Console.WriteLine("Received WriteProperty request");
                
                try
                {
                    using (var stream = new MemoryStream(apdu.Parameters))
                    using (var reader = new BinaryReader(stream))
                    {
                        // Parse the WriteProperty request according to BACnet standard
                        // First, get object identifier (context tag 0)
                        byte tag = reader.ReadByte();
                        if ((tag & 0x08) != 0 && (tag & 0x07) == 0) // Context tag 0
                        {
                            reader.ReadByte(); // Length, should be 4
                            uint objectId = ReadUInt32(reader);
                            ushort objectType = (ushort)(objectId >> 22);
                            uint instanceNumber = objectId & 0x3FFFFF;
                            
                            // Next, get property identifier (context tag 1)
                            tag = reader.ReadByte();
                            if ((tag & 0x08) != 0 && (tag & 0x07) == 1) // Context tag 1
                            {
                                byte len = reader.ReadByte();
                                uint propertyId = ReadUInt(reader, len);
                                
                                // Check for optional array index (context tag 2)
                                uint arrayIndex = uint.MaxValue; // No array index
                                tag = reader.ReadByte();
                                
                                if ((tag & 0x08) != 0 && (tag & 0x07) == 2) // Context tag 2 (array index)
                                {
                                    len = reader.ReadByte();
                                    arrayIndex = ReadUInt(reader, len);
                                    tag = reader.ReadByte(); // Read next tag
                                }
                                
                                // Get property value (context tag 3)
                                if ((tag & 0x08) != 0 && (tag & 0x07) == 3) // Context tag 3 (opening)
                                {
                                    // Read application-tagged value
                                    tag = reader.ReadByte();
                                    object? value = default;
                                    
                                    // Get the tag number and length
                                    byte tagNumber = (byte)(tag >> 4);
                                    
                                    // Parse the value based on tag number
                                    switch (tagNumber)
                                    {
                                        case 1: // Boolean
                                            value = (tag & 0x01) == 1; // True if length = 1
                                            break;
                                            
                                        case 2: // Unsigned Integer
                                            byte length = (byte)(tag & 0x07);
                                            value = ReadUInt(reader, length);
                                            break;
                                            
                                        case 4: // Real (Float)
                                            byte[] floatBytes = reader.ReadBytes(4);
                                            if (BitConverter.IsLittleEndian)
                                            {
                                                Array.Reverse(floatBytes);
                                            }
                                            value = BitConverter.ToSingle(floatBytes, 0);
                                            break;
                                            
                                        case 7: // Character String
                                            int stringLen = tag & 0x07;
                                            if (stringLen == 5) // Extended length
                                            {
                                                byte lenInfo = reader.ReadByte();
                                                if (lenInfo == 1)
                                                {
                                                    stringLen = reader.ReadByte();
                                                }
                                                else
                                                {
                                                    throw new NotSupportedException("Extended string length > 255 not supported");
                                                }
                                            }
                                            
                                            byte encoding = reader.ReadByte(); // Should be 0 for ANSI X3.4
                                            byte[] stringBytes = reader.ReadBytes(stringLen - 1);
                                            value = System.Text.Encoding.ASCII.GetString(stringBytes);
                                            break;
                                            
                                        default:
                                            Console.WriteLine($"Unsupported tag type: {tagNumber}");
                                            break;
                                    }
                                    
                                    // Skip to closing tag
                                    while (stream.Position < stream.Length)
                                    {
                                        byte nextByte = reader.ReadByte();
                                        if (nextByte == 0x3F) // Context tag 3, closing
                                        {
                                            break;
                                        }
                                    }
                                    
                                    // Check for priority (context tag 4)
                                    byte priority = 16; // Default priority (lowest)
                                    if (stream.Position < stream.Length)
                                    {
                                        tag = reader.ReadByte();
                                        if ((tag & 0x08) != 0 && (tag & 0x07) == 4) // Context tag 4
                                        {
                                            len = reader.ReadByte();
                                            priority = (byte)ReadUInt(reader, len);
                                        }
                                    }
                                    
                                    Console.WriteLine($"WriteProperty request: Object={GetObjectTypeName(objectType)},{instanceNumber} " +
                                                    $"Property={GetPropertyName(propertyId)} Value={value} Priority={priority}");
                                    
                                    // Find the object and apply the write
                                    var bacnetObject = device.GetObject(GetObjectTypeName(objectType), instanceNumber);
                                    if (bacnetObject != null)
                                    {
                                        if (bacnetObject is AnalogOutput analogOutput && propertyId == 85) // Present Value
                                        {
                                            // Convert value to float if needed
                                            float floatValue;
                                            if (value is float f)
                                            {
                                                floatValue = f;
                                            }
                                            else if (value is uint u)
                                            {
                                                floatValue = u;
                                            }
                                            else if (value is int i)
                                            {
                                                floatValue = i;
                                            }
                                            else
                                            {
                                                Console.WriteLine($"Cannot convert {value} to float for analog output");
                                                var errorResponseApdu = CreateErrorResponse(apdu.InvokeID, APDU.WriteProperty, 0x2C, 0x10); // Parameter out of range
                                                SendResponse(errorResponseApdu, remoteEndPoint, server);
                                                return;
                                            }
                                            
                                            try
                                            {
                                                analogOutput.PresentValue = floatValue;
                                                Console.WriteLine($"Successfully wrote value {floatValue} to {bacnetObject.ObjectType} {bacnetObject.ObjectIdentifier}");
                                                
                                                // Send a simple ACK
                                                var responseApdu = new APDU
                                                {
                                                    PDUType = APDU.SimpleAck,
                                                    ServiceChoice = APDU.WriteProperty,
                                                    InvokeID = apdu.InvokeID
                                                };
                                                
                                                SendResponse(responseApdu, remoteEndPoint, server);
                                                return;
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error writing property: {ex.Message}");
                                                var errorResponseApdu = CreateErrorResponse(apdu.InvokeID, APDU.WriteProperty, 0x1F, 0x00); // General error
                                                SendResponse(errorResponseApdu, remoteEndPoint, server);
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Write to {GetPropertyName(propertyId)} not supported for {bacnetObject.ObjectType}");
                                            var errorResponseApdu = CreateErrorResponse(apdu.InvokeID, APDU.WriteProperty, 0x2F, 0x2D); // Property is not writable
                                            SendResponse(errorResponseApdu, remoteEndPoint, server);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Object not found: type={objectType}, instance={instanceNumber}");
                                        var errorResponseApdu = CreateErrorResponse(apdu.InvokeID, APDU.WriteProperty, 0x1F, 0x31); // Object unknown
                                        SendResponse(errorResponseApdu, remoteEndPoint, server);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    
                    // If we get here, something went wrong with the request format
                    var rejectApdu = new APDU
                    {
                        PDUType = APDU.Reject,
                        ServiceChoice = 0, // Unused for Reject
                        InvokeID = apdu.InvokeID,
                        Parameters = new byte[] { 1 } // Invalid parameter data type
                    };
                    
                    SendResponse(rejectApdu, remoteEndPoint, server);
                    Console.WriteLine("Sent Reject response: Invalid parameter data");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing WriteProperty: {ex.Message}");
                    
                    // Send Error response - Other Error
                    var responseApdu = CreateErrorResponse(apdu.InvokeID, APDU.WriteProperty, 0x1F, 0x00);
                    SendResponse(responseApdu, remoteEndPoint, server);
                    Console.WriteLine("Sent Error response: General Error");
                }
            }
        }
        
        /// <summary>
        /// Sends a proper I-Am response according to BACnet standard
        /// </summary>
        private static void SendIAmResponse(BACnetDevice device, IPEndPoint remoteEndPoint, BACnetIPServer server)
        {
            try
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    // Object ID (Device, instance) - BACnet application tag 12 (object identifier)
                    // Encoding format: first 10 bits = object type, remaining 22 bits = instance
                    // Object type 8 = Device
                    uint encodedObjectId = (8u << 22) | (device.DeviceId & 0x3FFFFF);
                    
                    // Application tag 12 (object identifier)
                    writer.Write((byte)0xC4); // Tag 12, length 4 bytes
                    writer.Write((byte)((encodedObjectId >> 24) & 0xFF)); // Higher bytes first (big-endian)
                    writer.Write((byte)((encodedObjectId >> 16) & 0xFF));
                    writer.Write((byte)((encodedObjectId >> 8) & 0xFF));
                    writer.Write((byte)(encodedObjectId & 0xFF));
                    
                    // Max APDU Length - BACnet application tag 2 (unsigned)
                    writer.Write((byte)0x21); // Tag 2, length 1
                    writer.Write((byte)0x04); // 1024 octets (encoded as 4)
                    
                    // Segmentation support - BACnet application tag 9 (enumerated)
                    writer.Write((byte)0x91); // Tag 9, length 1
                    writer.Write((byte)0x00); // No segmentation
                    
                    // Vendor ID - BACnet application tag 2 (unsigned)
                    writer.Write((byte)0x21); // Tag 2, length 1
                    writer.Write((byte)device.VendorId); // Vendor ID
                    
                    // Create the APDU for I-Am
                    var responseApdu = new APDU
                    {
                        PDUType = APDU.UnconfirmedRequest,
                        ServiceChoice = APDU.IAm,
                        Parameters = stream.ToArray()
                    };
                    
                    // Send the response
                    SendResponse(responseApdu, remoteEndPoint, server);
                    Console.WriteLine($"I-Am response sent to {remoteEndPoint}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating I-Am response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sends a BACnet response to the client
        /// </summary>
        private static void SendResponse(APDU apdu, IPEndPoint remoteEndPoint, BACnetIPServer server)
        {
            try
            {
                // Build the response from APDU -> NPDU -> BVLC
                byte[] apduData = apdu.Encode();
                
                var npdu = new NPDU
                {
                    Version = NPDU.BACNET_PROTOCOL_VERSION,
                    Control = 0,
                    ApplicationData = apduData
                };
                
                byte[] npduData = npdu.Encode();
                
                var bvlc = new BVLC(BVLC.BVLC_ORIGINAL_UNICAST_NPDU, npduData);
                byte[] bvlcData = bvlc.Encode();
                
                // Send the response
                server.SendResponse(bvlcData, remoteEndPoint);
                Console.WriteLine("Response sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reads an unsigned integer of specific length from a BinaryReader
        /// </summary>
        private static uint ReadUInt(BinaryReader reader, int length)
        {
            uint value = 0;
            for (int i = 0; i < length; i++)
            {
                value = (value << 8) | reader.ReadByte();
            }
            return value;
        }
        
        /// <summary>
        /// Reads an unsigned 32-bit integer from a BinaryReader
        /// </summary>
        private static uint ReadUInt32(BinaryReader reader)
        {
            uint value = 0;
            for (int i = 0; i < 4; i++)
            {
                value = (value << 8) | reader.ReadByte();
            }
            return value;
        }
        
        /// <summary>
        /// Simulates device behavior by updating sensor values periodically
        /// </summary>
        private static async Task SimulateDeviceAsync(BACnetDevice device, CancellationToken cancellationToken)
        {
            try
            {
                Random random = new Random();
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Simulate temperature changes
                    var tempSensor1 = device.GetObject("AnalogInput", 1);
                    var tempSensor2 = device.GetObject("AnalogInput", 2);
                    var humiditySensor = device.GetObject("AnalogInput", 3);
                    
                    if (tempSensor1 != null)
                    {
                        float currentTemp = ((AnalogInput)tempSensor1).PresentValue;
                        float newTemp = currentTemp + (float)((random.NextDouble() - 0.5) * 0.2); // Small random change
                        ((AnalogInput)tempSensor1).PresentValue = newTemp;
                        Console.WriteLine($"Updated Zone Temp 1: {newTemp:F1} °F");
                    }
                    
                    if (tempSensor2 != null)
                    {
                        float currentTemp = ((AnalogInput)tempSensor2).PresentValue;
                        float newTemp = currentTemp + (float)((random.NextDouble() - 0.5) * 0.2); // Small random change
                        ((AnalogInput)tempSensor2).PresentValue = newTemp;
                        Console.WriteLine($"Updated Zone Temp 2: {newTemp:F1} °F");
                    }
                    
                    if (humiditySensor != null)
                    {
                        float currentHumidity = ((AnalogInput)humiditySensor).PresentValue;
                        float newHumidity = Math.Min(100, Math.Max(0, currentHumidity + (float)((random.NextDouble() - 0.5) * 0.5)));
                        ((AnalogInput)humiditySensor).PresentValue = newHumidity;
                        Console.WriteLine($"Updated Zone Humidity: {newHumidity:F1} %RH");
                    }
                    
                    // Wait before the next update
                    await Task.Delay(5000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when canceled
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in device simulation: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates a BACnet error response APDU
        /// </summary>
        private static APDU CreateErrorResponse(byte invokeID, byte serviceChoice, byte errorClass, byte errorCode)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Original invoking service (context tag 0)
                writer.Write((byte)0x09); // Context-specific tag 0, length 1
                writer.Write(serviceChoice);
                
                // Error class (application tag 9 - enumerated)
                writer.Write((byte)0x91); // Tag 9, length 1
                writer.Write(errorClass);
                
                // Error code (application tag 9 - enumerated)
                writer.Write((byte)0x91); // Tag 9, length 1
                writer.Write(errorCode);
                
                return new APDU
                {
                    PDUType = APDU.Error,
                    ServiceChoice = serviceChoice,
                    InvokeID = invokeID,
                    Parameters = stream.ToArray()
                };
            }
        }
        
        /// <summary>
        /// Gets the BACnet object type name from a numeric object type
        /// </summary>
        private static string GetObjectTypeName(ushort objectType)
        {
            switch (objectType)
            {
                case 0: return "AnalogInput";
                case 1: return "AnalogOutput";
                case 2: return "AnalogValue";
                case 3: return "BinaryInput";
                case 4: return "BinaryOutput";
                case 5: return "BinaryValue";
                case 8: return "Device";
                case 13: return "MultiStateInput";
                case 14: return "MultiStateOutput";
                case 19: return "MultiStateValue";
                default: return $"UnknownType_{objectType}";
            }
        }
        
        /// <summary>
        /// Gets the BACnet property name from a numeric property ID
        /// </summary>
        private static string GetPropertyName(uint propertyId)
        {
            switch (propertyId)
            {
                case 0: return "ACK_Required";
                case 1: return "ACK_Timer";
                case 2: return "Action";
                case 3: return "Action_Text";
                case 4: return "Active_Text";
                case 5: return "Active_VT_Sessions";
                case 6: return "Alarm_Value";
                case 7: return "Alarm_Values";
                case 8: return "All";
                case 9: return "All_Writes_Successful";
                case 10: return "APDU_Segment_Timeout";
                case 11: return "APDU_Timeout";
                case 12: return "Application_Software_Version";
                case 13: return "Archive";
                case 14: return "Bias";
                case 15: return "Change_Of_State_Count";
                case 16: return "Change_Of_State_Time";
                case 17: return "Notification_Class";
                case 76: return "PresentValue";
                case 77: return "Priority";
                case 78: return "Priority_Array";
                case 85: return "Reliability";
                case 87: return "Required";
                case 96: return "Status_Flags";
                case 98: return "Time_Delay";
                case 103: return "Units";
                case 117: return "ObjectIdentifier";
                case 118: return "ObjectName";
                case 119: return "ObjectType";
                default: return $"UnknownProperty_{propertyId}";
            }
        }
    }
}