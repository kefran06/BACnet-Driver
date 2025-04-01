using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace BACnet.Core.Protocol.ASN1
{
    /// <summary>
    /// Provides functionality for encoding BACnet data in ASN.1 format
    /// </summary>
    public class ASN1Encoder
    {
        private readonly MemoryStream _stream;

        public ASN1Encoder()
        {
            _stream = new MemoryStream();
        }
        
        public ASN1Encoder(int capacity)
        {
            _stream = new MemoryStream(capacity);
        }
        
        /// <summary>
        /// Gets the encoded bytes
        /// </summary>
        public byte[] GetBytes()
        {
            return _stream.ToArray();
        }
        
        /// <summary>
        /// Encodes a tag and length for BACnet ASN.1 format
        /// </summary>
        public void EncodeTag(ASN1Type tagNumber, bool contextSpecific, uint length)
        {
            // First octet format:
            // Bit 7: Context-specific flag
            // Bits 4-6: Tag length
            // Bits 0-3: Tag number if 0-14, 0x0F if extended
            
            byte firstOctet = 0;
            
            if (contextSpecific)
            {
                // Set bit 7
                firstOctet |= 0x08;
            }
            
            // Set tag number or extended flag
            if ((byte)tagNumber <= 14)
            {
                firstOctet |= (byte)tagNumber;
            }
            else
            {
                firstOctet |= 0x0F; // Extended tag
            }
            
            // Encode length/value/type
            if (length <= 4)
            {
                // Length fits in first octet
                firstOctet |= (byte)(length << 4);
                _stream.WriteByte(firstOctet);
            }
            else
            {
                // Extended length
                firstOctet |= 0x05 << 4; // Set length = 5 to indicate extended length
                _stream.WriteByte(firstOctet);
                
                // Write the extended length
                EncodeUnsigned(length);
            }
            
            // If tag number was extended, write the extended tag number
            if ((byte)tagNumber > 14)
            {
                EncodeUnsigned((uint)tagNumber);
            }
        }

        /// <summary>
        /// Encodes a null value
        /// </summary>
        public void EncodeNull(bool contextSpecific = false, byte contextTag = 0)
        {
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.Null;
                
            EncodeTag(tagType, contextSpecific, 0);
        }

        /// <summary>
        /// Encodes a boolean value
        /// </summary>
        public void EncodeBoolean(bool value, bool contextSpecific = false, byte contextTag = 0)
        {
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.Boolean;
                
            EncodeTag(tagType, contextSpecific, 1);
            _stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Encodes an unsigned integer value
        /// </summary>
        public void EncodeUnsigned(uint value, bool contextSpecific = false, byte contextTag = 0)
        {
            // Determine the number of bytes required
            int length;
            
            if (value <= 0xFF)
                length = 1;
            else if (value <= 0xFFFF)
                length = 2;
            else if (value <= 0xFFFFFF)
                length = 3;
            else
                length = 4;
                
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.UnsignedInteger;
                
            EncodeTag(tagType, contextSpecific, (uint)length);
            
            // Write the bytes in big endian order
            for (int i = length - 1; i >= 0; i--)
            {
                byte b = (byte)((value >> (8 * i)) & 0xFF);
                _stream.WriteByte(b);
            }
        }

        /// <summary>
        /// Encodes a signed integer value
        /// </summary>
        public void EncodeSigned(int value, bool contextSpecific = false, byte contextTag = 0)
        {
            // Determine the minimum number of bytes required
            List<byte> bytes = new List<byte>();
            
            // Handle zero
            if (value == 0)
            {
                bytes.Add(0);
            }
            else
            {
                // Convert to bytes preserving sign bit
                bool negative = value < 0;
                uint absValue = (uint)(negative ? -value : value);
                
                while (absValue != 0)
                {
                    bytes.Add((byte)(absValue & 0xFF));
                    absValue >>= 8;
                }
                
                // If the highest bit of most significant byte is set 
                // and we're encoding a positive number, we need an extra byte
                if ((bytes[bytes.Count - 1] & 0x80) != 0 && !negative)
                {
                    bytes.Add(0);
                }
                
                // If the highest bit of most significant byte is not set
                // and we're encoding a negative number, we need an extra byte
                if ((bytes[bytes.Count - 1] & 0x80) == 0 && negative)
                {
                    bytes.Add(0xFF);
                }
                
                // If negative, convert to two's complement
                if (negative)
                {
                    // Invert all bits
                    for (int i = 0; i < bytes.Count; i++)
                    {
                        bytes[i] = (byte)(~bytes[i] & 0xFF);
                    }
                    
                    // Add one
                    int carry = 1;
                    for (int i = 0; i < bytes.Count; i++)
                    {
                        int sum = bytes[i] + carry;
                        bytes[i] = (byte)(sum & 0xFF);
                        carry = sum >> 8;
                    }
                }
            }
            
            // Reverse the bytes for big-endian order
            bytes.Reverse();
            
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.SignedInteger;
                
            EncodeTag(tagType, contextSpecific, (uint)bytes.Count);
            
            // Write the bytes
            foreach (byte b in bytes)
            {
                _stream.WriteByte(b);
            }
        }

        /// <summary>
        /// Encodes a real (float) value
        /// </summary>
        public void EncodeReal(float value, bool contextSpecific = false, byte contextTag = 0)
        {
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.Real;
                
            EncodeTag(tagType, contextSpecific, 4);
            
            // Convert to 4 bytes IEEE-754 representation
            byte[] bytes = BitConverter.GetBytes(value);
            
            // Adjust for endianness if needed
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            
            // Write the bytes
            _stream.Write(bytes, 0, 4);
        }

        /// <summary>
        /// Encodes a string value
        /// </summary>
        public void EncodeString(string value, bool contextSpecific = false, byte contextTag = 0)
        {
            if (value == null)
            {
                EncodeNull(contextSpecific, contextTag);
                return;
            }
            
            // Convert string to UTF-8 bytes
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.CharacterString;
                
            // String encoding:
            // Byte 0: String encoding (0 for ASCII/UTF-8)
            // Bytes 1..n: The string bytes
            
            EncodeTag(tagType, contextSpecific, (uint)stringBytes.Length + 1);
            
            // Write string encoding
            _stream.WriteByte(0); // UTF-8/ASCII
            
            // Write string bytes
            _stream.Write(stringBytes, 0, stringBytes.Length);
        }

        /// <summary>
        /// Encodes an octet string value
        /// </summary>
        public void EncodeOctetString(byte[] value, bool contextSpecific = false, byte contextTag = 0)
        {
            if (value == null)
            {
                EncodeNull(contextSpecific, contextTag);
                return;
            }
            
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.OctetString;
                
            EncodeTag(tagType, contextSpecific, (uint)value.Length);
            
            // Write the bytes
            _stream.Write(value, 0, value.Length);
        }

        /// <summary>
        /// Encodes a BACnet Object Identifier
        /// </summary>
        public void EncodeObjectIdentifier(uint objectType, uint instanceNumber, 
            bool contextSpecific = false, byte contextTag = 0)
        {
            // BACnet Object ID = (object type << 22) | instance number
            uint objectId = (objectType << 22) | (instanceNumber & 0x3FFFFF);
            
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.ObjectIdentifier;
                
            EncodeTag(tagType, contextSpecific, 4);
            
            // Write the 4 bytes in big endian order
            _stream.WriteByte((byte)((objectId >> 24) & 0xFF));
            _stream.WriteByte((byte)((objectId >> 16) & 0xFF));
            _stream.WriteByte((byte)((objectId >> 8) & 0xFF));
            _stream.WriteByte((byte)(objectId & 0xFF));
        }
        
        /// <summary>
        /// Encodes an opening tag with the specified tag number
        /// </summary>
        public void EncodeOpeningTag(byte tagNumber)
        {
            byte tag = (byte)((tagNumber & 0x0F) | 0x0E << 4);
            _stream.WriteByte(tag);
        }
        
        /// <summary>
        /// Encodes a closing tag with the specified tag number
        /// </summary>
        public void EncodeClosingTag(byte tagNumber)
        {
            byte tag = (byte)((tagNumber & 0x0F) | 0x0F << 4);
            _stream.WriteByte(tag);
        }
        
        /// <summary>
        /// Encodes a BACnet date value
        /// </summary>
        public void EncodeDate(DateTime date, bool contextSpecific = false, byte contextTag = 0)
        {
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.Date;
                
            EncodeTag(tagType, contextSpecific, 4);
            
            // BACnet date encoding:
            // Byte 1: Year - 1900
            // Byte 2: Month (1-12)
            // Byte 3: Day (1-31)
            // Byte 4: Day of week (1-7) where 1 is Monday
            
            _stream.WriteByte((byte)(date.Year - 1900));
            _stream.WriteByte((byte)date.Month);
            _stream.WriteByte((byte)date.Day);
            
            // Convert .NET DayOfWeek (where Sunday = 0) to BACnet DayOfWeek (where Monday = 1)
            int bacnetDayOfWeek = ((int)date.DayOfWeek + 6) % 7 + 1;
            _stream.WriteByte((byte)bacnetDayOfWeek);
        }
        
        /// <summary>
        /// Encodes a BACnet time value
        /// </summary>
        public void EncodeTime(DateTime time, bool contextSpecific = false, byte contextTag = 0)
        {
            ASN1Type tagType = contextSpecific ? 
                (ASN1Type)(contextTag | (byte)ASN1TagClass.ContextSpecificTag) : 
                ASN1Type.Time;
                
            EncodeTag(tagType, contextSpecific, 4);
            
            // BACnet time encoding:
            // Byte 1: Hour (0-23)
            // Byte 2: Minute (0-59)
            // Byte 3: Second (0-59)
            // Byte 4: Hundredths (0-99)
            
            _stream.WriteByte((byte)time.Hour);
            _stream.WriteByte((byte)time.Minute);
            _stream.WriteByte((byte)time.Second);
            _stream.WriteByte((byte)(time.Millisecond / 10)); // Convert ms to hundredths
        }
        
        /// <summary>
        /// Encodes a BACnet enumerated value
        /// </summary>
        public void EncodeEnumerated(uint value, bool contextSpecific = false, byte contextTag = 0)
        {
            // Enumerated is encoded the same as Unsigned
            EncodeUnsigned(value, contextSpecific, contextTag);
        }
    }
}