using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BACnet.Core.Protocol.ASN1
{
    /// <summary>
    /// Class for decoding BACnet ASN.1 encoded data
    /// </summary>
    public class ASN1Decoder : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly BinaryReader _reader;
        private bool _disposed;

        /// <summary>
        /// Current position in the stream
        /// </summary>
        public long Position => _stream.Position;
        
        /// <summary>
        /// Length of the stream
        /// </summary>
        public long Length => _stream.Length;
        
        /// <summary>
        /// Remaining bytes in the stream
        /// </summary>
        public long RemainingBytes => _stream.Length - _stream.Position;
        
        /// <summary>
        /// Whether there are more bytes to read
        /// </summary>
        public bool HasMoreBytes => RemainingBytes > 0;

        /// <summary>
        /// Creates a new ASN1Decoder with the provided encoded bytes
        /// </summary>
        public ASN1Decoder(byte[] encodedBytes)
        {
            if (encodedBytes == null)
                throw new ArgumentNullException(nameof(encodedBytes));
                
            _stream = new MemoryStream(encodedBytes);
            _reader = new BinaryReader(_stream);
        }

        /// <summary>
        /// Decode a tag and return its information
        /// </summary>
        public (ASN1Type TagNumber, bool IsContextSpecific, uint Length) DecodeTag()
        {
            if (!HasMoreBytes)
                throw new InvalidOperationException("No more bytes to read");
            
            // Read the first octet
            byte firstOctet = _reader.ReadByte();
            
            // Bit 7 (0x80) is the Context-specific flag
            bool isContextSpecific = (firstOctet & 0x08) != 0;
            
            // Bits 4-6 (0x70) are for length encoding
            uint length = (uint)((firstOctet & 0xF0) >> 4);
            
            // Bits 0-3 (0x0F) are the tag number (or extended tag indicator)
            byte tagValue = (byte)(firstOctet & 0x0F);
            
            // Check if length is extended (value 5)
            if (length == 5)
            {
                length = DecodeUnsigned();
            }
            
            // Check if tag is extended (value 15)
            ASN1Type tagNumber;
            if (tagValue == 0x0F) // Extended tag
            {
                uint extendedTag = DecodeUnsigned();
                tagNumber = (ASN1Type)extendedTag;
            }
            else
            {
                tagNumber = (ASN1Type)tagValue;
            }
            
            return (tagNumber, isContextSpecific, length);
        }

        /// <summary>
        /// Decode a null value
        /// </summary>
        public void DecodeNull()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Null)
                throw new InvalidOperationException($"Expected Null tag, got {tagNumber}");
                
            if (length != 0)
                throw new InvalidOperationException($"Invalid length for Null tag: {length}");
        }

        /// <summary>
        /// Decode a boolean value
        /// </summary>
        public bool DecodeBoolean()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Boolean)
                throw new InvalidOperationException($"Expected Boolean tag, got {tagNumber}");
                
            if (length != 1)
                throw new InvalidOperationException($"Invalid length for Boolean tag: {length}");
                
            byte value = _reader.ReadByte();
            return value != 0;
        }

        /// <summary>
        /// Decode an unsigned integer value
        /// </summary>
        public uint DecodeUnsigned()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.UnsignedInteger)
                throw new InvalidOperationException($"Expected Unsigned tag, got {tagNumber}");
                
            if (length == 0)
                return 0;
                
            if (length > 4)
                throw new InvalidOperationException($"Invalid length for Unsigned tag: {length}");
                
            uint result = 0;
            
            // Read bytes in big-endian order
            for (int i = 0; i < length; i++)
            {
                byte b = _reader.ReadByte();
                result = (result << 8) | b;
            }
            
            return result;
        }

        /// <summary>
        /// Decode a signed integer value
        /// </summary>
        public int DecodeSigned()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.SignedInteger)
                throw new InvalidOperationException($"Expected Signed tag, got {tagNumber}");
                
            if (length == 0)
                return 0;
                
            if (length > 4)
                throw new InvalidOperationException($"Invalid length for Signed tag: {length}");
                
            // Read all bytes
            byte[] bytes = _reader.ReadBytes((int)length);
            
            // Check if negative (high bit of first byte is set)
            bool negative = (bytes[0] & 0x80) != 0;
            
            int result = 0;
            
            // Process bytes in big-endian order
            for (int i = 0; i < length; i++)
            {
                result = (result << 8) | bytes[i];
            }
            
            // Handle negative values (two's complement)
            if (negative)
            {
                // Create a mask for the bits we've read
                int mask = (1 << ((int)length * 8)) - 1;
                // Invert the bits for the ones' complement
                result = ~result & mask;
                // Add 1 for two's complement and negate
                result = -(result + 1);
            }
            
            return result;
        }

        /// <summary>
        /// Decode a real (float) value
        /// </summary>
        public float DecodeReal()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Real)
                throw new InvalidOperationException($"Expected Real tag, got {tagNumber}");
                
            if (length != 4)
                throw new InvalidOperationException($"Invalid length for Real tag: {length}");
                
            byte[] bytes = _reader.ReadBytes(4);
            
            // Convert from big-endian if needed
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            
            return BitConverter.ToSingle(bytes, 0);
        }

        /// <summary>
        /// Decode a string value
        /// </summary>
        public string DecodeString()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.CharacterString)
                throw new InvalidOperationException($"Expected CharacterString tag, got {tagNumber}");
                
            if (length == 0)
                return string.Empty;
                
            // First byte is encoding type
            byte encodingType = _reader.ReadByte();
            
            // Read the string bytes
            byte[] stringBytes = _reader.ReadBytes((int)length - 1);
            
            // Decode based on encoding type
            switch (encodingType)
            {
                case 0: // ASCII/UTF-8
                    return Encoding.UTF8.GetString(stringBytes);
                case 1: // DBCS
                    throw new NotSupportedException("DBCS encoding not supported");
                case 2: // JIS
                    throw new NotSupportedException("JIS encoding not supported");
                case 3: // UCS-2
                    return Encoding.Unicode.GetString(stringBytes);
                case 4: // UCS-4
                    return Encoding.UTF32.GetString(stringBytes);
                case 5: // ISO 8859-1
                    return Encoding.GetEncoding("ISO-8859-1").GetString(stringBytes);
                default:
                    throw new NotSupportedException($"Unsupported string encoding: {encodingType}");
            }
        }

        /// <summary>
        /// Decode an octet string value
        /// </summary>
        public byte[] DecodeOctetString()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.OctetString)
                throw new InvalidOperationException($"Expected OctetString tag, got {tagNumber}");
                
            if (length == 0)
                return Array.Empty<byte>();
                
            return _reader.ReadBytes((int)length);
        }

        /// <summary>
        /// Decode a BACnet Object Identifier
        /// </summary>
        public (uint ObjectType, uint InstanceNumber) DecodeObjectIdentifier()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.ObjectIdentifier)
                throw new InvalidOperationException($"Expected ObjectIdentifier tag, got {tagNumber}");
                
            if (length != 4)
                throw new InvalidOperationException($"Invalid length for ObjectIdentifier tag: {length}");
                
            // Read 4 bytes in big-endian order
            uint objectId = 0;
            for (int i = 0; i < 4; i++)
            {
                objectId = (objectId << 8) | _reader.ReadByte();
            }
            
            // Extract object type (first 10 bits) and instance number (last 22 bits)
            uint objectType = (objectId >> 22);
            uint instanceNumber = (objectId & 0x3FFFFF);
            
            return (objectType, instanceNumber);
        }
        
        /// <summary>
        /// Decode an opening tag
        /// </summary>
        public byte DecodeOpeningTag()
        {
            byte tag = _reader.ReadByte();
            
            // Should have bits 4-7 set to 0xE (1110)
            if ((tag & 0xF0) != 0xE0)
                throw new InvalidOperationException("Expected opening tag");
                
            return (byte)(tag & 0x0F); // Return tag number (bits 0-3)
        }
        
        /// <summary>
        /// Decode a closing tag
        /// </summary>
        public byte DecodeClosingTag()
        {
            byte tag = _reader.ReadByte();
            
            // Should have bits 4-7 set to 0xF (1111)
            if ((tag & 0xF0) != 0xF0)
                throw new InvalidOperationException("Expected closing tag");
                
            return (byte)(tag & 0x0F); // Return tag number (bits 0-3)
        }
        
        /// <summary>
        /// Decode a BACnet date value
        /// </summary>
        public DateTime DecodeDate()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Date)
                throw new InvalidOperationException($"Expected Date tag, got {tagNumber}");
                
            if (length != 4)
                throw new InvalidOperationException($"Invalid length for Date tag: {length}");
                
            // BACnet date encoding:
            // Byte 1: Year - 1900
            // Byte 2: Month (1-12)
            // Byte 3: Day (1-31)
            // Byte 4: Day of week (1-7) where 1 is Monday (ignored in conversion)
            
            int year = _reader.ReadByte() + 1900;
            int month = _reader.ReadByte();
            int day = _reader.ReadByte();
            byte dayOfWeek = _reader.ReadByte(); // Not used in DateTime construction
            
            return new DateTime(year, month, day);
        }
        
        /// <summary>
        /// Decode a BACnet time value
        /// </summary>
        public DateTime DecodeTime()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Time)
                throw new InvalidOperationException($"Expected Time tag, got {tagNumber}");
                
            if (length != 4)
                throw new InvalidOperationException($"Invalid length for Time tag: {length}");
                
            // BACnet time encoding:
            // Byte 1: Hour (0-23)
            // Byte 2: Minute (0-59)
            // Byte 3: Second (0-59)
            // Byte 4: Hundredths (0-99)
            
            int hour = _reader.ReadByte();
            int minute = _reader.ReadByte();
            int second = _reader.ReadByte();
            int hundredths = _reader.ReadByte();
            int milliseconds = hundredths * 10; // Convert hundredths to milliseconds
            
            // Create a DateTime with just the time components (date will be today)
            DateTime today = DateTime.Today;
            return new DateTime(today.Year, today.Month, today.Day, hour, minute, second, milliseconds);
        }
        
        /// <summary>
        /// Decode a BACnet enumerated value
        /// </summary>
        public uint DecodeEnumerated()
        {
            var (tagNumber, isContextSpecific, length) = DecodeTag();
            
            if (!isContextSpecific && tagNumber != ASN1Type.Enumerated)
                throw new InvalidOperationException($"Expected Enumerated tag, got {tagNumber}");
                
            if (length == 0)
                return 0;
                
            if (length > 4)
                throw new InvalidOperationException($"Invalid length for Enumerated tag: {length}");
                
            uint result = 0;
            
            // Read bytes in big-endian order
            for (int i = 0; i < length; i++)
            {
                byte b = _reader.ReadByte();
                result = (result << 8) | b;
            }
            
            return result;
        }
        
        /// <summary>
        /// Skip a specified number of bytes in the stream
        /// </summary>
        public void Skip(int count)
        {
            _stream.Seek(count, SeekOrigin.Current);
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _reader.Dispose();
                _stream.Dispose();
                _disposed = true;
            }
        }
    }
}