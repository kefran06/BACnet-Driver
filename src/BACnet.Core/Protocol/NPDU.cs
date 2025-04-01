using System;
using System.Collections.Generic;

namespace BACnet.Core.Protocol
{
    /// <summary>
    /// Represents the BACnet Network Protocol Data Unit (NPDU)
    /// Handles network layer addressing and routing in BACnet communications
    /// </summary>
    public class NPDU
    {
        // NPDU Constants
        public const byte BACNET_PROTOCOL_VERSION = 0x01;
        
        // NPDU Control Bits
        public const byte NPDU_NETWORK_LAYER_MESSAGE = 0x80;
        public const byte NPDU_DESTINATION_SPECIFIED = 0x20;
        public const byte NPDU_SOURCE_SPECIFIED = 0x08;
        public const byte NPDU_EXPECTING_REPLY = 0x04;
        public const byte NPDU_PRIORITY_NORMAL = 0x00;
        public const byte NPDU_PRIORITY_URGENT = 0x01;
        public const byte NPDU_PRIORITY_CRITICAL = 0x02;
        public const byte NPDU_PRIORITY_LIFE_SAFETY = 0x03;
        
        // Network Layer Message Types
        public const byte NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK = 0x00;
        public const byte NETWORK_MESSAGE_I_AM_ROUTER_TO_NETWORK = 0x01;
        public const byte NETWORK_MESSAGE_I_COULD_BE_ROUTER_TO_NETWORK = 0x02;
        public const byte NETWORK_MESSAGE_REJECT_MESSAGE_TO_NETWORK = 0x03;
        public const byte NETWORK_MESSAGE_ROUTER_BUSY_TO_NETWORK = 0x04;
        public const byte NETWORK_MESSAGE_ROUTER_AVAILABLE_TO_NETWORK = 0x05;
        public const byte NETWORK_MESSAGE_INIT_RT_TABLE = 0x06;
        public const byte NETWORK_MESSAGE_INIT_RT_TABLE_ACK = 0x07;
        public const byte NETWORK_MESSAGE_ESTABLISH_CONNECTION_TO_NETWORK = 0x08;
        public const byte NETWORK_MESSAGE_DISCONNECT_CONNECTION_TO_NETWORK = 0x09;
        
        /// <summary>
        /// Gets or sets the protocol version (should be 1 for BACnet)
        /// </summary>
        public byte Version { get; set; }
        
        /// <summary>
        /// Gets or sets the control byte that determines NPDU structure
        /// </summary>
        public byte Control { get; set; }
        
        /// <summary>
        /// Gets or sets the network number of the destination device
        /// </summary>
        public ushort? DestinationNetworkNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the MAC address of the destination device
        /// </summary>
        public byte[] DestinationMacAddress { get; set; }
        
        /// <summary>
        /// Gets or sets the network number of the source device
        /// </summary>
        public ushort? SourceNetworkNumber { get; set; }
        
        /// <summary>
        /// Gets or sets the MAC address of the source device
        /// </summary>
        public byte[] SourceMacAddress { get; set; }
        
        /// <summary>
        /// Gets or sets the hop count for routing messages
        /// </summary>
        public byte HopCount { get; set; }
        
        /// <summary>
        /// Gets or sets the network layer message type (if this is a network layer message)
        /// </summary>
        public byte? NetworkLayerMessageType { get; set; }
        
        /// <summary>
        /// Gets or sets the vendor ID for proprietary messages (if applicable)
        /// </summary>
        public ushort? VendorId { get; set; }
        
        /// <summary>
        /// Gets or sets the application layer data
        /// </summary>
        public byte[] ApplicationData { get; set; }

        /// <summary>
        /// Initializes a new instance of the NPDU class with default values
        /// </summary>
        public NPDU()
        {
            Version = BACNET_PROTOCOL_VERSION;
            Control = 0;
            HopCount = 255; // Max hop count
            DestinationMacAddress = new byte[0];
            SourceMacAddress = new byte[0];
            ApplicationData = new byte[0];
        }

        /// <summary>
        /// Creates an NPDU for sending a message to a device
        /// </summary>
        /// <param name="destinationNetwork">Network number of destination, or null for local network</param>
        /// <param name="destinationAddress">MAC address of destination</param>
        /// <param name="sourceNetwork">Network number of source, or null for local network</param>
        /// <param name="sourceAddress">MAC address of source</param>
        /// <param name="expectingReply">Whether a reply is expected</param>
        /// <param name="hopCount">Maximum number of hops</param>
        /// <returns>A new NPDU instance</returns>
        public static NPDU CreateForDestination(
            ushort? destinationNetwork,
            byte[] destinationAddress,
            ushort? sourceNetwork,
            byte[] sourceAddress,
            bool expectingReply,
            byte hopCount = 255)
        {
            var npdu = new NPDU
            {
                Version = BACNET_PROTOCOL_VERSION,
                Control = 0,
                HopCount = hopCount,
                DestinationNetworkNumber = destinationNetwork,
                DestinationMacAddress = destinationAddress ?? new byte[0],
                SourceNetworkNumber = sourceNetwork,
                SourceMacAddress = sourceAddress ?? new byte[0]
            };
            
            // Set control bits based on parameters
            if (destinationNetwork.HasValue)
            {
                npdu.Control |= NPDU_DESTINATION_SPECIFIED;
            }
            
            if (sourceNetwork.HasValue)
            {
                npdu.Control |= NPDU_SOURCE_SPECIFIED;
            }
            
            if (expectingReply)
            {
                npdu.Control |= NPDU_EXPECTING_REPLY;
            }
            
            return npdu;
        }

        /// <summary>
        /// Creates an NPDU for a network layer message
        /// </summary>
        /// <param name="messageType">The network layer message type</param>
        /// <param name="destinationNetwork">Network number of destination, or null for local network</param>
        /// <param name="destinationAddress">MAC address of destination</param>
        /// <param name="hopCount">Maximum number of hops</param>
        /// <returns>A new NPDU instance</returns>
        public static NPDU CreateNetworkLayerMessage(
            byte messageType,
            ushort? destinationNetwork,
            byte[] destinationAddress,
            byte hopCount = 255)
        {
            var npdu = new NPDU
            {
                Version = BACNET_PROTOCOL_VERSION,
                Control = NPDU_NETWORK_LAYER_MESSAGE,
                NetworkLayerMessageType = messageType,
                HopCount = hopCount
            };
            
            if (destinationNetwork.HasValue)
            {
                npdu.Control |= NPDU_DESTINATION_SPECIFIED;
                npdu.DestinationNetworkNumber = destinationNetwork;
                npdu.DestinationMacAddress = destinationAddress ?? new byte[0];
            }
            
            return npdu;
        }

        /// <summary>
        /// Encodes the NPDU into a byte array for transmission
        /// </summary>
        /// <returns>A byte array containing the encoded NPDU</returns>
        public byte[] Encode()
        {
            try
            {
                List<byte> buffer = new List<byte>();
                
                // Version
                buffer.Add(Version);
                
                // Control
                buffer.Add(Control);
                
                // Destination network and address, if specified
                if ((Control & NPDU_DESTINATION_SPECIFIED) != 0)
                {
                    // Network number (2 bytes)
                    if (DestinationNetworkNumber.HasValue)
                    {
                        buffer.Add((byte)(DestinationNetworkNumber.Value >> 8));
                        buffer.Add((byte)(DestinationNetworkNumber.Value & 0xFF));
                    }
                    else
                    {
                        buffer.Add(0);
                        buffer.Add(0);
                    }
                    
                    // MAC address length
                    buffer.Add((byte)(DestinationMacAddress?.Length ?? 0));
                    
                    // MAC address
                    if (DestinationMacAddress != null && DestinationMacAddress.Length > 0)
                    {
                        buffer.AddRange(DestinationMacAddress);
                    }
                }
                
                // Source network and address, if specified
                if ((Control & NPDU_SOURCE_SPECIFIED) != 0)
                {
                    // Network number (2 bytes)
                    if (SourceNetworkNumber.HasValue)
                    {
                        buffer.Add((byte)(SourceNetworkNumber.Value >> 8));
                        buffer.Add((byte)(SourceNetworkNumber.Value & 0xFF));
                    }
                    else
                    {
                        buffer.Add(0);
                        buffer.Add(0);
                    }
                    
                    // MAC address length
                    buffer.Add((byte)(SourceMacAddress?.Length ?? 0));
                    
                    // MAC address
                    if (SourceMacAddress != null && SourceMacAddress.Length > 0)
                    {
                        buffer.AddRange(SourceMacAddress);
                    }
                }
                
                // Hop count, if message is being sent to a remote network
                if ((Control & NPDU_DESTINATION_SPECIFIED) != 0)
                {
                    buffer.Add(HopCount);
                }
                
                // Network layer message type, if this is a network layer message
                if ((Control & NPDU_NETWORK_LAYER_MESSAGE) != 0 && NetworkLayerMessageType.HasValue)
                {
                    buffer.Add(NetworkLayerMessageType.Value);
                    
                    // Add vendor ID for proprietary messages (type > 0x80)
                    if (NetworkLayerMessageType.Value >= 0x80 && VendorId.HasValue)
                    {
                        buffer.Add((byte)(VendorId.Value >> 8));
                        buffer.Add((byte)(VendorId.Value & 0xFF));
                    }
                }
                
                // Add application data if any
                if (ApplicationData != null && ApplicationData.Length > 0)
                {
                    buffer.AddRange(ApplicationData);
                }
                
                return buffer.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to encode NPDU: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decodes a byte array into this NPDU
        /// </summary>
        /// <param name="data">The byte array to decode</param>
        /// <param name="offset">The offset to start decoding from</param>
        /// <param name="length">The number of bytes to decode</param>
        /// <returns>The number of bytes consumed from the buffer</returns>
        public int Decode(byte[] data, int offset, int length)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            
            if (offset < 0 || offset >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            
            if (length < 2) // Minimum NPDU size is 2 bytes (version + control)
            {
                throw new ArgumentException("NPDU data must be at least 2 bytes long", nameof(length));
            }
            
            try
            {
                int originalOffset = offset;
                
                // Version
                Version = data[offset++];
                
                // Control
                Control = data[offset++];
                
                // Destination network and address, if specified
                if ((Control & NPDU_DESTINATION_SPECIFIED) != 0)
                {
                    if (offset + 3 > data.Length)
                    {
                        throw new ArgumentException("NPDU data is too short for destination specification");
                    }
                    
                    // Network number (2 bytes)
                    DestinationNetworkNumber = (ushort)((data[offset] << 8) | data[offset + 1]);
                    offset += 2;
                    
                    // MAC address length and address
                    byte macLen = data[offset++];
                    
                    if (macLen > 0)
                    {
                        if (offset + macLen > data.Length)
                        {
                            throw new ArgumentException("NPDU data is too short for destination MAC address");
                        }
                        
                        DestinationMacAddress = new byte[macLen];
                        Array.Copy(data, offset, DestinationMacAddress, 0, macLen);
                        offset += macLen;
                    }
                    else
                    {
                        DestinationMacAddress = new byte[0];
                    }
                }
                else
                {
                    DestinationNetworkNumber = null;
                    DestinationMacAddress = new byte[0];
                }
                
                // Source network and address, if specified
                if ((Control & NPDU_SOURCE_SPECIFIED) != 0)
                {
                    if (offset + 3 > data.Length)
                    {
                        throw new ArgumentException("NPDU data is too short for source specification");
                    }
                    
                    // Network number (2 bytes)
                    SourceNetworkNumber = (ushort)((data[offset] << 8) | data[offset + 1]);
                    offset += 2;
                    
                    // MAC address length and address
                    byte macLen = data[offset++];
                    
                    if (macLen > 0)
                    {
                        if (offset + macLen > data.Length)
                        {
                            throw new ArgumentException("NPDU data is too short for source MAC address");
                        }
                        
                        SourceMacAddress = new byte[macLen];
                        Array.Copy(data, offset, SourceMacAddress, 0, macLen);
                        offset += macLen;
                    }
                    else
                    {
                        SourceMacAddress = new byte[0];
                    }
                }
                else
                {
                    SourceNetworkNumber = null;
                    SourceMacAddress = new byte[0];
                }
                
                // Hop count, if message is being sent to a remote network
                if ((Control & NPDU_DESTINATION_SPECIFIED) != 0)
                {
                    if (offset >= data.Length)
                    {
                        throw new ArgumentException("NPDU data is too short for hop count");
                    }
                    
                    HopCount = data[offset++];
                }
                else
                {
                    HopCount = 0;
                }
                
                // Network layer message type, if this is a network layer message
                if ((Control & NPDU_NETWORK_LAYER_MESSAGE) != 0)
                {
                    if (offset >= data.Length)
                    {
                        throw new ArgumentException("NPDU data is too short for network layer message type");
                    }
                    
                    NetworkLayerMessageType = data[offset++];
                    
                    // Handle vendor ID for proprietary messages
                    if (NetworkLayerMessageType >= 0x80)
                    {
                        if (offset + 2 > data.Length)
                        {
                            throw new ArgumentException("NPDU data is too short for vendor ID");
                        }
                        
                        VendorId = (ushort)((data[offset] << 8) | data[offset + 1]);
                        offset += 2;
                    }
                }
                else
                {
                    NetworkLayerMessageType = null;
                    VendorId = null;
                    
                    // Any remaining data is application layer data
                    int appDataLen = length - (offset - originalOffset);
                    if (appDataLen > 0)
                    {
                        ApplicationData = new byte[appDataLen];
                        Array.Copy(data, offset, ApplicationData, 0, appDataLen);
                        offset += appDataLen;
                    }
                    else
                    {
                        ApplicationData = new byte[0];
                    }
                }
                
                // Return the number of bytes consumed
                return offset - originalOffset;
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new ArgumentException("Invalid NPDU data format", nameof(data), ex);
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException))
            {
                throw new InvalidOperationException($"Failed to decode NPDU: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Returns a string representation of this NPDU
        /// </summary>
        /// <returns>A string describing this NPDU</returns>
        public override string ToString()
        {
            List<string> parts = new List<string>();
            
            parts.Add($"Version={Version}");
            
            List<string> controlBits = new List<string>();
            if ((Control & NPDU_NETWORK_LAYER_MESSAGE) != 0) controlBits.Add("Network-Layer-Message");
            if ((Control & NPDU_DESTINATION_SPECIFIED) != 0) controlBits.Add("Destination-Specified");
            if ((Control & NPDU_SOURCE_SPECIFIED) != 0) controlBits.Add("Source-Specified");
            if ((Control & NPDU_EXPECTING_REPLY) != 0) controlBits.Add("Expecting-Reply");
            
            string priority = (Control & 0x03) switch
            {
                NPDU_PRIORITY_NORMAL => "Normal",
                NPDU_PRIORITY_URGENT => "Urgent",
                NPDU_PRIORITY_CRITICAL => "Critical",
                NPDU_PRIORITY_LIFE_SAFETY => "Life-Safety",
                _ => "Unknown"
            };
            
            controlBits.Add($"Priority-{priority}");
            
            parts.Add($"Control=[{string.Join(", ", controlBits)}]");
            
            if (DestinationNetworkNumber.HasValue)
            {
                parts.Add($"DestNet={DestinationNetworkNumber}");
                
                if (DestinationMacAddress != null && DestinationMacAddress.Length > 0)
                {
                    parts.Add($"DestAddr={BitConverter.ToString(DestinationMacAddress)}");
                }
            }
            
            if (SourceNetworkNumber.HasValue)
            {
                parts.Add($"SrcNet={SourceNetworkNumber}");
                
                if (SourceMacAddress != null && SourceMacAddress.Length > 0)
                {
                    parts.Add($"SrcAddr={BitConverter.ToString(SourceMacAddress)}");
                }
            }
            
            if ((Control & NPDU_DESTINATION_SPECIFIED) != 0)
            {
                parts.Add($"HopCount={HopCount}");
            }
            
            if (NetworkLayerMessageType.HasValue)
            {
                string networkMessageName = NetworkLayerMessageType.Value switch
                {
                    NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK => "Who-Is-Router-To-Network",
                    NETWORK_MESSAGE_I_AM_ROUTER_TO_NETWORK => "I-Am-Router-To-Network",
                    NETWORK_MESSAGE_I_COULD_BE_ROUTER_TO_NETWORK => "I-Could-Be-Router-To-Network",
                    NETWORK_MESSAGE_REJECT_MESSAGE_TO_NETWORK => "Reject-Message-To-Network",
                    NETWORK_MESSAGE_ROUTER_BUSY_TO_NETWORK => "Router-Busy-To-Network",
                    NETWORK_MESSAGE_ROUTER_AVAILABLE_TO_NETWORK => "Router-Available-To-Network",
                    NETWORK_MESSAGE_INIT_RT_TABLE => "Initialize-Routing-Table",
                    NETWORK_MESSAGE_INIT_RT_TABLE_ACK => "Initialize-Routing-Table-Ack",
                    NETWORK_MESSAGE_ESTABLISH_CONNECTION_TO_NETWORK => "Establish-Connection-To-Network",
                    NETWORK_MESSAGE_DISCONNECT_CONNECTION_TO_NETWORK => "Disconnect-Connection-To-Network",
                    _ => $"Network-Message-Type-{NetworkLayerMessageType:X2}"
                };
                
                parts.Add(networkMessageName);
                
                if (NetworkLayerMessageType >= 0x80 && VendorId.HasValue)
                {
                    parts.Add($"VendorID={VendorId}");
                }
            }
            
            return $"NPDU: {string.Join(", ", parts)}, DataLength={ApplicationData?.Length ?? 0}";
        }
    }
}