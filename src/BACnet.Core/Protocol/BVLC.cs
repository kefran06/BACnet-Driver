using System;

namespace BACnet.Core.Protocol
{
    /// <summary>
    /// Represents the BACnet Virtual Link Control (BVLC) layer used in BACnet/IP communications
    /// </summary>
    public class BVLC
    {
        // BVLC Constants
        public const byte BVLL_TYPE_BACNET_IP = 0x81;
        
        // BVLC Function Codes
        public const byte BVLC_RESULT = 0x00;
        public const byte BVLC_WRITE_BROADCAST_DISTRIBUTION_TABLE = 0x01;
        public const byte BVLC_READ_BROADCAST_DISTRIBUTION_TABLE = 0x02;
        public const byte BVLC_READ_BROADCAST_DISTRIBUTION_TABLE_ACK = 0x03;
        public const byte BVLC_FORWARDED_NPDU = 0x04;
        public const byte BVLC_REGISTER_FOREIGN_DEVICE = 0x05;
        public const byte BVLC_READ_FOREIGN_DEVICE_TABLE = 0x06;
        public const byte BVLC_READ_FOREIGN_DEVICE_TABLE_ACK = 0x07;
        public const byte BVLC_DELETE_FOREIGN_DEVICE_TABLE_ENTRY = 0x08;
        public const byte BVLC_DISTRIBUTE_BROADCAST_TO_NETWORK = 0x09;
        public const byte BVLC_ORIGINAL_UNICAST_NPDU = 0x0A;
        public const byte BVLC_ORIGINAL_BROADCAST_NPDU = 0x0B;
        
        // BVLC Results
        public const byte BVLC_RESULT_SUCCESSFUL_COMPLETION = 0x0000;
        public const byte BVLC_RESULT_WRITE_BROADCAST_DISTRIBUTION_TABLE_NAK = 0x0010;
        public const byte BVLC_RESULT_READ_BROADCAST_DISTRIBUTION_TABLE_NAK = 0x0020;
        public const byte BVLC_RESULT_REGISTER_FOREIGN_DEVICE_NAK = 0x0030;
        public const byte BVLC_RESULT_READ_FOREIGN_DEVICE_TABLE_NAK = 0x0040;
        public const byte BVLC_RESULT_DELETE_FOREIGN_DEVICE_TABLE_ENTRY_NAK = 0x0050;
        public const byte BVLC_RESULT_DISTRIBUTE_BROADCAST_TO_NETWORK_NAK = 0x0060;

        /// <summary>
        /// Gets or sets the BVLL Type (typically 0x81 for BACnet/IP)
        /// </summary>
        public byte Type { get; set; }
        
        /// <summary>
        /// Gets or sets the Function Code for the BVLC message
        /// </summary>
        public byte Function { get; set; }
        
        /// <summary>
        /// Gets or sets the length of the BVLC message (including Type, Function, and Length fields)
        /// </summary>
        public ushort Length { get; set; }
        
        /// <summary>
        /// Gets or sets the payload data for the BVLC message
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Initializes a new instance of the BVLC class with default values
        /// </summary>
        public BVLC()
        {
            Type = BVLL_TYPE_BACNET_IP;
            Function = BVLC_ORIGINAL_UNICAST_NPDU;
            Length = 4; // Minimum BVLC header size
            Data = new byte[0];
        }

        /// <summary>
        /// Initializes a new instance of the BVLC class with the specified function and payload
        /// </summary>
        /// <param name="function">The BVLC function code</param>
        /// <param name="data">The payload data</param>
        public BVLC(byte function, byte[] data)
        {
            Type = BVLL_TYPE_BACNET_IP;
            Function = function;
            Data = data ?? new byte[0];
            Length = (ushort)(4 + Data.Length); // 4 bytes for header + data length
        }

        /// <summary>
        /// Encodes the BVLC message into a byte array for transmission
        /// </summary>
        /// <returns>A byte array containing the encoded BVLC message</returns>
        public byte[] Encode()
        {
            try
            {
                // Ensure length is correct
                Length = (ushort)(4 + (Data?.Length ?? 0)); // 4 bytes for header + data length
                
                // Create buffer for the BVLC message
                byte[] buffer = new byte[Length];
                
                // Add the BVLL Type
                buffer[0] = Type;
                
                // Add the Function
                buffer[1] = Function;
                
                // Add the Length (big-endian)
                buffer[2] = (byte)(Length >> 8);   // High byte
                buffer[3] = (byte)(Length & 0xFF); // Low byte
                
                // Add the payload if any
                if (Data != null && Data.Length > 0)
                {
                    Array.Copy(Data, 0, buffer, 4, Data.Length);
                }
                
                return buffer;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to encode BVLC message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decodes a byte array into this BVLC message
        /// </summary>
        /// <param name="buffer">The byte array to decode</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null</exception>
        /// <exception cref="ArgumentException">Thrown when buffer is too short or invalid</exception>
        public void Decode(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            
            if (buffer.Length < 4)
            {
                throw new ArgumentException("BVLC message must be at least 4 bytes long", nameof(buffer));
            }
            
            try
            {
                // Read the BVLL Type
                Type = buffer[0];
                
                // Verify it's a BACnet/IP message
                if (Type != BVLL_TYPE_BACNET_IP)
                {
                    throw new ArgumentException($"Unexpected BVLL Type: {Type}, expected {BVLL_TYPE_BACNET_IP}", nameof(buffer));
                }
                
                // Read the Function
                Function = buffer[1];
                
                // Read the Length (big-endian)
                Length = (ushort)((buffer[2] << 8) | buffer[3]);
                
                // Verify the length matches the buffer
                if (Length > buffer.Length)
                {
                    throw new ArgumentException($"BVLC message length ({Length}) exceeds buffer length ({buffer.Length})", nameof(buffer));
                }
                
                // Extract the payload
                int dataLength = Length - 4;
                if (dataLength > 0)
                {
                    Data = new byte[dataLength];
                    Array.Copy(buffer, 4, Data, 0, dataLength);
                }
                else
                {
                    Data = new byte[0];
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new ArgumentException("Invalid BVLC message format", nameof(buffer), ex);
            }
            catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException))
            {
                throw new InvalidOperationException($"Failed to decode BVLC message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new BVLC message for forwarding an NPDU
        /// </summary>
        /// <param name="data">The NPDU data to forward</param>
        /// <param name="address">The original source address</param>
        /// <returns>A new BVLC instance configured for forwarding</returns>
        public static BVLC CreateForwardedNPDU(byte[] data, string address)
        {
            // This would format the data with the originating address in a real implementation
            // For now, we'll just create a simple forwarding message
            return new BVLC(BVLC_FORWARDED_NPDU, data);
        }

        /// <summary>
        /// Creates a new BVLC message for an original unicast NPDU
        /// </summary>
        /// <param name="data">The NPDU data to send</param>
        /// <returns>A new BVLC instance configured for unicast</returns>
        public static BVLC CreateOriginalUnicastNPDU(byte[] data)
        {
            return new BVLC(BVLC_ORIGINAL_UNICAST_NPDU, data);
        }

        /// <summary>
        /// Creates a new BVLC message for an original broadcast NPDU
        /// </summary>
        /// <param name="data">The NPDU data to broadcast</param>
        /// <returns>A new BVLC instance configured for broadcast</returns>
        public static BVLC CreateOriginalBroadcastNPDU(byte[] data)
        {
            return new BVLC(BVLC_ORIGINAL_BROADCAST_NPDU, data);
        }

        /// <summary>
        /// Returns a string representation of this BVLC message
        /// </summary>
        /// <returns>A string describing this BVLC message</returns>
        public override string ToString()
        {
            string functionName = Function switch
            {
                BVLC_RESULT => "Result",
                BVLC_WRITE_BROADCAST_DISTRIBUTION_TABLE => "Write-Broadcast-Distribution-Table",
                BVLC_READ_BROADCAST_DISTRIBUTION_TABLE => "Read-Broadcast-Distribution-Table",
                BVLC_READ_BROADCAST_DISTRIBUTION_TABLE_ACK => "Read-Broadcast-Distribution-Table-Ack",
                BVLC_FORWARDED_NPDU => "Forwarded-NPDU",
                BVLC_REGISTER_FOREIGN_DEVICE => "Register-Foreign-Device",
                BVLC_READ_FOREIGN_DEVICE_TABLE => "Read-Foreign-Device-Table",
                BVLC_READ_FOREIGN_DEVICE_TABLE_ACK => "Read-Foreign-Device-Table-Ack",
                BVLC_DELETE_FOREIGN_DEVICE_TABLE_ENTRY => "Delete-Foreign-Device-Table-Entry",
                BVLC_DISTRIBUTE_BROADCAST_TO_NETWORK => "Distribute-Broadcast-To-Network",
                BVLC_ORIGINAL_UNICAST_NPDU => "Original-Unicast-NPDU",
                BVLC_ORIGINAL_BROADCAST_NPDU => "Original-Broadcast-NPDU",
                _ => $"Unknown-Function-{Function:X2}"
            };
            
            return $"BVLC: Type={Type:X2}, Function={functionName}, Length={Length}, DataLength={Data?.Length ?? 0}";
        }
    }
}