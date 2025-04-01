using System;

namespace BACnet.Core.Protocol.ASN1
{
    /// <summary>
    /// Helper class with utility methods for ASN.1 encoding and decoding in BACnet
    /// </summary>
    public static class ASN1Helper
    {
        /// <summary>
        /// Encodes a value to its ASN.1 representation
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <param name="contextSpecific">Whether this is a context-specific tag</param>
        /// <param name="contextTag">The context tag number (if context-specific)</param>
        /// <returns>Byte array with the encoded value</returns>
        public static byte[] Encode(object value, bool contextSpecific = false, byte contextTag = 0)
        {
            var encoder = new ASN1Encoder();
            
            switch (value)
            {
                case null:
                    encoder.EncodeNull(contextSpecific, contextTag);
                    break;
                case bool boolValue:
                    encoder.EncodeBoolean(boolValue, contextSpecific, contextTag);
                    break;
                case byte byteValue:
                    encoder.EncodeUnsigned(byteValue, contextSpecific, contextTag);
                    break;
                case ushort ushortValue:
                    encoder.EncodeUnsigned(ushortValue, contextSpecific, contextTag);
                    break;
                case uint uintValue:
                    encoder.EncodeUnsigned(uintValue, contextSpecific, contextTag);
                    break;
                case sbyte sbyteValue:
                    encoder.EncodeSigned(sbyteValue, contextSpecific, contextTag);
                    break;
                case short shortValue:
                    encoder.EncodeSigned(shortValue, contextSpecific, contextTag);
                    break;
                case int intValue:
                    encoder.EncodeSigned(intValue, contextSpecific, contextTag);
                    break;
                case float floatValue:
                    encoder.EncodeReal(floatValue, contextSpecific, contextTag);
                    break;
                case string stringValue:
                    encoder.EncodeString(stringValue, contextSpecific, contextTag);
                    break;
                case byte[] byteArrayValue:
                    encoder.EncodeOctetString(byteArrayValue, contextSpecific, contextTag);
                    break;
                case DateTime dateTimeValue:
                    // Encode as BACnet Date or Time based on context tag
                    if (contextTag == 0)
                    {
                        // Default to Date
                        encoder.EncodeDate(dateTimeValue, contextSpecific, contextTag);
                    }
                    else if (contextTag == 1)
                    {
                        // Time
                        encoder.EncodeTime(dateTimeValue, contextSpecific, contextTag);
                    }
                    else
                    {
                        // Default to Date
                        encoder.EncodeDate(dateTimeValue, contextSpecific, contextTag);
                    }
                    break;
                case Enum enumValue:
                    encoder.EncodeEnumerated(Convert.ToUInt32(enumValue), contextSpecific, contextTag);
                    break;
                default:
                    throw new ArgumentException($"Unsupported value type: {value.GetType().Name}");
            }
            
            return encoder.GetBytes();
        }
        
        /// <summary>
        /// Extracts the tag type from the first byte of an ASN.1 encoded value
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>The ASN.1 tag type</returns>
        public static ASN1Type GetTagType(byte firstByte)
        {
            // The tag number is in the lower 4 bits
            return (ASN1Type)(firstByte & 0x0F);
        }
        
        /// <summary>
        /// Checks if the tag in the first byte is context-specific
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>True if the tag is context-specific, false otherwise</returns>
        public static bool IsContextSpecific(byte firstByte)
        {
            // Context-specific flag is bit 3 (0x08)
            return (firstByte & 0x08) != 0;
        }
        
        /// <summary>
        /// Gets the encoded length from the first byte
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>The length encoded in the byte, or 5 if extended</returns>
        public static byte GetLength(byte firstByte)
        {
            // Length is in bits 4-6
            return (byte)((firstByte & 0xF0) >> 4);
        }
        
        /// <summary>
        /// Checks if this is an opening tag
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>True if this is an opening tag</returns>
        public static bool IsOpeningTag(byte firstByte)
        {
            // Opening tag has bits 4-7 as 1110 (0xE0)
            return (firstByte & 0xF0) == 0xE0;
        }
        
        /// <summary>
        /// Checks if this is a closing tag
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>True if this is a closing tag</returns>
        public static bool IsClosingTag(byte firstByte)
        {
            // Closing tag has bits 4-7 as 1111 (0xF0)
            return (firstByte & 0xF0) == 0xF0;
        }
        
        /// <summary>
        /// Gets the tag number from an opening or closing tag
        /// </summary>
        /// <param name="firstByte">The first byte of the encoded data</param>
        /// <returns>The tag number (bits 0-3)</returns>
        public static byte GetTagNumber(byte firstByte)
        {
            return (byte)(firstByte & 0x0F);
        }
    }
}