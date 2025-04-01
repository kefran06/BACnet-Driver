using System;
using System.Collections.Generic;

namespace BACnet.Core.Protocol
{
    /// <summary>
    /// Represents the Application Protocol Data Unit (APDU) in BACnet protocol
    /// Handles encoding and decoding of BACnet APDU messages
    /// </summary>
    public class APDU
    {
        // BACnet APDU Types
        public const byte ConfirmedRequest = 0x00;
        public const byte UnconfirmedRequest = 0x10;
        public const byte SimpleAck = 0x20;
        public const byte ComplexAck = 0x30;
        public const byte SegmentAck = 0x40;
        public const byte Error = 0x50;
        public const byte Reject = 0x60;
        public const byte Abort = 0x70;
        
        // BACnet Confirmed Service Choice
        public const byte ReadProperty = 0x0C;
        public const byte WriteProperty = 0x0F;
        public const byte WhoIs = 0x08;
        public const byte IAm = 0x00;

        /// <summary>
        /// Gets or sets the PDU type (first 4 bits of the first octet)
        /// </summary>
        public byte PDUType { get; set; }
        
        /// <summary>
        /// Gets or sets the invoke ID (used to match requests with responses)
        /// </summary>
        public byte InvokeID { get; set; }
        
        /// <summary>
        /// Gets or sets the service choice (which BACnet service is being used)
        /// </summary>
        public byte ServiceChoice { get; set; }
        
        /// <summary>
        /// Gets or sets the parameters/data for the APDU
        /// </summary>
        public byte[] Parameters { get; set; }
        
        /// <summary>
        /// Gets or sets any flags in the APDU (e.g., segmentation)
        /// </summary>
        public byte Flags { get; set; }

        /// <summary>
        /// Initializes a new instance of the APDU class with default values
        /// </summary>
        public APDU()
        {
            // Initialize properties with default values
            PDUType = 0;
            InvokeID = 0;
            ServiceChoice = 0;
            Flags = 0;
            Parameters = new byte[0];
        }
        
        /// <summary>
        /// Initializes a new instance of the APDU class with specified values
        /// </summary>
        /// <param name="pduType">The PDU type</param>
        /// <param name="invokeID">The invoke ID</param>
        /// <param name="serviceChoice">The service choice</param>
        public APDU(byte pduType, byte invokeID, byte serviceChoice)
        {
            PDUType = pduType;
            InvokeID = invokeID;
            ServiceChoice = serviceChoice;
            Flags = 0;
            Parameters = new byte[0];
        }

        /// <summary>
        /// Encodes the APDU into a byte array for transmission
        /// </summary>
        /// <returns>A byte array containing the encoded APDU</returns>
        public byte[] Encode()
        {
            try
            {
                // Calculate the size of the encoded APDU
                int size = 0;
                
                // First byte is always the PDU type and flags
                size += 1;
                
                // For confirmed and complex ack, add invoke ID
                if (PDUType == ConfirmedRequest || PDUType == ComplexAck)
                {
                    size += 1; // Invoke ID
                    size += 1; // Service choice
                }
                // For unconfirmed requests, just add service choice
                else if (PDUType == UnconfirmedRequest)
                {
                    size += 1; // Service choice
                }
                // For simple ack, add invoke ID and service ack choice
                else if (PDUType == SimpleAck)
                {
                    size += 1; // Invoke ID
                    size += 1; // Service ACK choice
                }
                
                // Add the parameters
                size += Parameters.Length;
                
                // Create the buffer and start encoding
                byte[] buffer = new byte[size];
                int offset = 0;
                
                // First byte: PDU type and flags
                buffer[offset++] = (byte)(PDUType | Flags);
                
                // Add invoke ID for relevant PDU types
                if (PDUType == ConfirmedRequest || PDUType == ComplexAck || PDUType == SimpleAck)
                {
                    buffer[offset++] = InvokeID;
                }
                
                // Add service choice for relevant PDU types
                if (PDUType == ConfirmedRequest || PDUType == UnconfirmedRequest || PDUType == ComplexAck || PDUType == SimpleAck)
                {
                    buffer[offset++] = ServiceChoice;
                }
                
                // Add parameters if any
                if (Parameters.Length > 0)
                {
                    Array.Copy(Parameters, 0, buffer, offset, Parameters.Length);
                }
                
                return buffer;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to encode APDU: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decodes a byte array into this APDU
        /// </summary>
        /// <param name="data">The byte array to decode</param>
        /// <exception cref="ArgumentNullException">Thrown when data is null</exception>
        /// <exception cref="ArgumentException">Thrown when data is invalid or too short</exception>
        public void Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            
            if (data.Length < 1)
            {
                throw new ArgumentException("APDU data must be at least 1 byte long", nameof(data));
            }
            
            try
            {
                int offset = 0;
                
                // First byte: PDU type and flags
                PDUType = (byte)(data[offset] & 0xF0); // Top 4 bits
                Flags = (byte)(data[offset] & 0x0F);   // Bottom 4 bits
                offset++;
                
                // Handle different PDU types
                if (PDUType == ConfirmedRequest)
                {
                    if (data.Length < offset + 2)
                    {
                        throw new ArgumentException("Confirmed Request APDU must be at least 3 bytes long", nameof(data));
                    }
                    
                    InvokeID = data[offset++];
                    ServiceChoice = data[offset++];
                }
                else if (PDUType == UnconfirmedRequest)
                {
                    if (data.Length < offset + 1)
                    {
                        throw new ArgumentException("Unconfirmed Request APDU must be at least 2 bytes long", nameof(data));
                    }
                    
                    ServiceChoice = data[offset++];
                }
                else if (PDUType == SimpleAck || PDUType == ComplexAck)
                {
                    if (data.Length < offset + 2)
                    {
                        throw new ArgumentException("Simple/Complex Ack APDU must be at least 3 bytes long", nameof(data));
                    }
                    
                    InvokeID = data[offset++];
                    ServiceChoice = data[offset++];
                }
                
                // Extract parameters if any
                if (offset < data.Length)
                {
                    Parameters = new byte[data.Length - offset];
                    Array.Copy(data, offset, Parameters, 0, Parameters.Length);
                }
                else
                {
                    Parameters = new byte[0];
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new ArgumentException("Invalid APDU data format", nameof(data), ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decode APDU: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Returns a string representation of this APDU
        /// </summary>
        /// <returns>A string describing this APDU</returns>
        public override string ToString()
        {
            string pduTypeString = PDUType switch
            {
                ConfirmedRequest => "Confirmed-Request",
                UnconfirmedRequest => "Unconfirmed-Request",
                SimpleAck => "Simple-ACK",
                ComplexAck => "Complex-ACK",
                SegmentAck => "Segment-ACK",
                Error => "Error",
                Reject => "Reject",
                Abort => "Abort",
                _ => $"Unknown-PDU-Type({PDUType:X2})"
            };
            
            return $"{pduTypeString}, ServiceChoice={ServiceChoice:X2}, InvokeID={InvokeID}, DataLength={Parameters.Length}";
        }
    }
}