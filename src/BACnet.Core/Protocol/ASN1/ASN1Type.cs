using System;

namespace BACnet.Core.Protocol.ASN1
{
    /// <summary>
    /// BACnet Application Tags as defined in the BACnet standard
    /// These are used to identify the type of data in ASN.1 encoded messages
    /// </summary>
    public enum ASN1Type : byte
    {
        // BACnet Application Tags (0-14)
        Null = 0,
        Boolean = 1,
        UnsignedInteger = 2,
        SignedInteger = 3,
        Real = 4,
        Double = 5,
        OctetString = 6,
        CharacterString = 7,
        BitString = 8,
        Enumerated = 9,
        Date = 10,
        Time = 11,
        ObjectIdentifier = 12,
        Reserved1 = 13,
        Reserved2 = 14,
        
        // BACnet Context Tags (0-14 with context specific bit)
        ContextTag0 = 0x08,
        ContextTag1 = 0x09,
        ContextTag2 = 0x0A,
        ContextTag3 = 0x0B,
        ContextTag4 = 0x0C,
        ContextTag5 = 0x0D,
        ContextTag6 = 0x0E,
        ContextTag7 = 0x0F,
        ContextTag8 = 0x10,
        ContextTag9 = 0x11,
        ContextTag10 = 0x12,
        ContextTag11 = 0x13,
        ContextTag12 = 0x14,
        ContextTag13 = 0x15,
        ContextTag14 = 0x16,
        
        // Special BACnet Tags
        Opening = 0x0E, // Context specific, and opening tag
        Closing = 0x0F, // Context specific, and closing tag
        
        // Length/Value/Type encodings
        ExtendedTag = 0x0F,  // Value 15 indicates extended tag
        ExtendedValue = 0x05 // Value 5 indicates extended value
    }
    
    /// <summary>
    /// BACnet Tag class flags for ASN.1 encoding
    /// </summary>
    [Flags]
    public enum ASN1TagClass
    {
        ApplicationTag = 0x00,
        ContextSpecificTag = 0x08,
        OpeningTag = 0x0E,
        ClosingTag = 0x0F
    }
}