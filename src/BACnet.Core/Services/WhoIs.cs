using System;
using System.Collections.Generic;
using System.IO;
using BACnet.Core.Objects;
using BACnet.Core.Protocol;

namespace BACnet.Core.Services
{
    /// <summary>
    /// Implements the BACnet Who-Is service for device discovery
    /// </summary>
    public class WhoIs
    {
        // Range limits for the WhoIs request
        private readonly uint? _lowInstanceId;
        private readonly uint? _highInstanceId;
        
        // Track discovered devices
        private readonly List<Device> _discoveredDevices = new List<Device>();

        /// <summary>
        /// Creates a new WhoIs service (global broadcast)
        /// </summary>
        public WhoIs()
        {
            _lowInstanceId = null;
            _highInstanceId = null;
        }

        /// <summary>
        /// Creates a new WhoIs service with instance range limits
        /// </summary>
        /// <param name="lowInstanceId">The lowest device instance to discover</param>
        /// <param name="highInstanceId">The highest device instance to discover</param>
        /// <exception cref="ArgumentException">Thrown when low instance is greater than high instance</exception>
        public WhoIs(uint lowInstanceId, uint highInstanceId)
        {
            if (lowInstanceId > highInstanceId)
            {
                throw new ArgumentException("Low instance ID must be less than or equal to high instance ID");
            }

            _lowInstanceId = lowInstanceId;
            _highInstanceId = highInstanceId;
        }

        /// <summary>
        /// Encodes the WhoIs request as an APDU according to the BACnet protocol specification
        /// </summary>
        /// <returns>The encoded APDU</returns>
        public APDU EncodeAPDU()
        {
            var apdu = new APDU
            {
                PDUType = APDU.UnconfirmedRequest,
                ServiceChoice = APDU.WhoIs
            };
            
            // If range limits are specified, encode them as per BACnet standard
            if (_lowInstanceId.HasValue && _highInstanceId.HasValue)
            {
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    // Add context-specific tag 0 (low limit) with unsigned integer value
                    EncodeContextTag(writer, 0, _lowInstanceId.Value);
                    
                    // Add context-specific tag 1 (high limit) with unsigned integer value
                    EncodeContextTag(writer, 1, _highInstanceId.Value);
                    
                    apdu.Parameters = stream.ToArray();
                }
            }
            else
            {
                // Global WhoIs has no parameters
                apdu.Parameters = new byte[0];
            }
            
            return apdu;
        }
        
        /// <summary>
        /// Processes an I-Am response from a device
        /// </summary>
        /// <param name="apdu">The received APDU containing I-Am data</param>
        /// <param name="sourceAddress">The address of the responding device</param>
        /// <param name="sourcePort">The port of the responding device</param>
        /// <returns>The discovered device, or null if the APDU is not a valid I-Am</returns>
        public Device ProcessIAmResponse(APDU apdu, string sourceAddress, int sourcePort)
        {
            if (apdu == null || apdu.PDUType != APDU.UnconfirmedRequest || apdu.ServiceChoice != APDU.IAm)
            {
                return null; // Not an I-Am response
            }
            
            try
            {
                // Parse I-Am response according to BACnet standard
                if (apdu.Parameters != null && apdu.Parameters.Length >= 7)
                {
                    using (MemoryStream stream = new MemoryStream(apdu.Parameters))
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        // Make sure we have enough data to read the device object ID
                        if (stream.Length - stream.Position < 5) // 1 byte for tag + 4 bytes for ID
                        {
                            Console.WriteLine("I-Am response too short: not enough data for device ID");
                            return null;
                        }
                        
                        // Extract object-identifier for device
                        uint deviceId = 0;
                        if (TryReadObjectId(reader, out _, out deviceId))
                        {
                            // Check if we have enough data for the rest of the fields
                            if (stream.Length - stream.Position < 3) // Minimum for remaining fields
                            {
                                Console.WriteLine("I-Am response too short: not enough data for device properties");
                                return null;
                            }
                            
                            try
                            {
                                // Read other properties from I-Am response
                                uint maxApduLength = ReadUnsigned(reader);
                                
                                // Check if we have enough data for segmentation
                                if (stream.Length - stream.Position < 1)
                                {
                                    Console.WriteLine("I-Am response too short: not enough data for segmentation");
                                    return null;
                                }
                                byte segmentation = reader.ReadByte();
                                
                                // Check if we have enough data for vendor ID
                                if (stream.Length - stream.Position < 2) // Minimum for vendor ID
                                {
                                    Console.WriteLine("I-Am response too short: not enough data for vendor ID");
                                    return null;
                                }
                                uint vendorId = ReadUnsigned(reader);
                                
                                // Create device with parsed information
                                var device = new Device(
                                    deviceId,          // Device ID from response
                                    $"Device_{deviceId}", // Default name (can be read later via ReadProperty)
                                    "Unknown",         // Default location
                                    $"Vendor_{vendorId}", // Default vendor name
                                    vendorId,          // Vendor ID from response
                                    0,                 // Default model number
                                    0                  // Default firmware revision
                                );
                                
                                // Add to discovered devices if not already present
                                if (!_discoveredDevices.Exists(d => d.ObjectIdentifier == deviceId))
                                {
                                    _discoveredDevices.Add(device);
                                    Console.WriteLine($"Discovered BACnet device: ID={deviceId}, Vendor={vendorId}");
                                }
                                
                                return device;
                            }
                            catch (EndOfStreamException)
                            {
                                Console.WriteLine("I-Am response parsing failed: unexpected end of data");
                                return null;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"I-Am response too short: expected 7+ bytes, got {apdu.Parameters?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing I-Am response: {ex.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Gets a list of all discovered devices
        /// </summary>
        /// <returns>A read-only list of discovered devices</returns>
        public IReadOnlyList<Device> GetDiscoveredDevices()
        {
            return _discoveredDevices.AsReadOnly();
        }
        
        /// <summary>
        /// Clears the list of discovered devices
        /// </summary>
        public void ClearDiscoveredDevices()
        {
            _discoveredDevices.Clear();
        }

        /// <summary>
        /// Encodes a BACnet context tag with unsigned integer value
        /// </summary>
        /// <param name="writer">Binary writer to write to</param>
        /// <param name="tagNumber">The context tag number</param>
        /// <param name="value">The unsigned integer value</param>
        private void EncodeContextTag(BinaryWriter writer, byte tagNumber, uint value)
        {
            // Determine the appropriate tag format based on value size
            if (value <= 0xFF)
            {
                // 8-bit value
                writer.Write((byte)(0x08 | tagNumber));
                writer.Write((byte)1); // Length = 1 byte
                writer.Write((byte)value);
            }
            else if (value <= 0xFFFF)
            {
                // 16-bit value
                writer.Write((byte)(0x08 | tagNumber));
                writer.Write((byte)2); // Length = 2 bytes
                writer.Write((byte)(value >> 8));
                writer.Write((byte)(value & 0xFF));
            }
            else
            {
                // 32-bit value
                writer.Write((byte)(0x08 | tagNumber));
                writer.Write((byte)4); // Length = 4 bytes
                writer.Write((byte)((value >> 24) & 0xFF));
                writer.Write((byte)((value >> 16) & 0xFF));
                writer.Write((byte)((value >> 8) & 0xFF));
                writer.Write((byte)(value & 0xFF));
            }
        }
        
        /// <summary>
        /// Attempts to read a BACnet object identifier
        /// </summary>
        /// <param name="reader">The binary reader</param>
        /// <param name="objectType">The object type value</param>
        /// <param name="instanceNumber">The instance number</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool TryReadObjectId(BinaryReader reader, out ushort objectType, out uint instanceNumber)
        {
            objectType = 0;
            instanceNumber = 0;
            
            try
            {
                // Application tag 12 (object-identifier) is 4 bytes
                byte tag = reader.ReadByte();
                if ((tag >> 4) == 12) // Application tag 12 (object identifier)
                {
                    uint value = ReadUint32(reader);
                    objectType = (ushort)(value >> 22);
                    instanceNumber = value & 0x3FFFFF; // 22 bits for instance number
                    return true;
                }
            }
            catch
            {
                // Error reading object ID
            }
            
            return false;
        }
        
        /// <summary>
        /// Reads an unsigned value based on tag size
        /// </summary>
        private uint ReadUnsigned(BinaryReader reader)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 1)
            {
                throw new EndOfStreamException("Not enough data to read tag byte");
            }
            
            byte tag = reader.ReadByte();
            byte tagNumber = (byte)(tag & 0x0F);
            bool contextSpecific = (tag & 0x08) != 0;
            
            if (contextSpecific)
            {
                // Context-specific tag
                if (reader.BaseStream.Length - reader.BaseStream.Position < 1)
                {
                    throw new EndOfStreamException("Not enough data to read length byte");
                }
                
                byte len = reader.ReadByte();
                
                if (reader.BaseStream.Length - reader.BaseStream.Position < len)
                {
                    throw new EndOfStreamException($"Not enough data to read {len} bytes");
                }
                
                return ReadUintOfLength(reader, len);
            }
            else
            {
                // Application tag
                uint value = 0;
                switch (tagNumber)
                {
                    case 2: // Unsigned Int
                        if (reader.BaseStream.Length - reader.BaseStream.Position < 1)
                        {
                            throw new EndOfStreamException("Not enough data to read length byte");
                        }
                        
                        byte len = reader.ReadByte();
                        
                        if (reader.BaseStream.Length - reader.BaseStream.Position < len)
                        {
                            throw new EndOfStreamException($"Not enough data to read {len} bytes");
                        }
                        
                        return ReadUintOfLength(reader, len);
                    default:
                        throw new InvalidOperationException($"Unexpected application tag: {tagNumber}");
                }
            }
        }
        
        /// <summary>
        /// Reads an unsigned 32-bit integer in BACnet byte order (big-endian)
        /// </summary>
        private uint ReadUint32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }
        
        /// <summary>
        /// Reads an unsigned integer of specific length in BACnet byte order (big-endian)
        /// </summary>
        private uint ReadUintOfLength(BinaryReader reader, byte length)
        {
            uint value = 0;
            for (int i = 0; i < length; i++)
            {
                value <<= 8;
                value |= reader.ReadByte();
            }
            return value;
        }
        
        /// <summary>
        /// Returns a string representation of this WhoIs request
        /// </summary>
        public override string ToString()
        {
            if (_lowInstanceId.HasValue && _highInstanceId.HasValue)
            {
                return $"WhoIs Request (Range: {_lowInstanceId} to {_highInstanceId})";
            }
            
            return "WhoIs Request (Global)";
        }
    }
}