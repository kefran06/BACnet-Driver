using System;

namespace BACnet.Core.Objects
{
    public class AnalogOutput : BACnetObject
    {
        public float PresentValue 
        { 
            get => GetProperty<float>("PresentValue");
            set => SetProperty("PresentValue", value); 
        }
        
        public float MinPresentValue 
        { 
            get => GetProperty<float>("MinPresentValue");
            set => SetProperty("MinPresentValue", value); 
        }
        
        public float MaxPresentValue 
        { 
            get => GetProperty<float>("MaxPresentValue");
            set => SetProperty("MaxPresentValue", value); 
        }
        
        public string Units 
        { 
            get => GetProperty<string>("Units");
            set => SetProperty("Units", value); 
        }
        
        public string OutOfService 
        { 
            get => GetProperty<string>("OutOfService");
            set => SetProperty("OutOfService", value); 
        }
        
        public string StatusFlags 
        { 
            get => GetProperty<string>("StatusFlags");
            set => SetProperty("StatusFlags", value); 
        }

        public AnalogOutput(uint instanceNumber) : base(instanceNumber, "AnalogOutput")
        {
            ObjectName = $"AO_{instanceNumber}";
            SetProperty("PresentValue", 0.0f);
            SetProperty("MinPresentValue", 0.0f);
            SetProperty("MaxPresentValue", 100.0f);
            SetProperty("Units", "noUnits");
            SetProperty("OutOfService", "false");
            SetProperty("StatusFlags", "0000"); // No flags set
        }

        public void SetOutputValue(float newValue)
        {
            if (newValue < MinPresentValue || newValue > MaxPresentValue)
            {
                Console.WriteLine($"Attempted to set value {newValue} outside of range [{MinPresentValue}, {MaxPresentValue}] for {ObjectName}");
                throw new ArgumentOutOfRangeException(nameof(newValue), "Value is out of range.");
            }
            
            float oldValue = PresentValue;
            PresentValue = newValue;
            
            Console.WriteLine($"Set output value for {ObjectName} from {oldValue} to {newValue}");
        }
        
        public override void ReadProperty(string propertyName)
        {
            // In a real implementation, this would read from a physical device or control system
            Console.WriteLine($"Reading property {propertyName} from {ObjectType} {ObjectIdentifier}");
        }

        public override void WriteProperty(string propertyName, object value)
        {
            // In a real implementation, this would write to a physical device or control system
            if (propertyName == "PresentValue")
            {
                if (value is float floatValue)
                {
                    SetOutputValue(floatValue);
                }
                else
                {
                    Console.WriteLine($"Cannot write non-float value {value} to PresentValue of {ObjectName}");
                    throw new ArgumentException($"Cannot write non-float value to PresentValue");
                }
            }
            else
            {
                SetProperty(propertyName, value);
                Console.WriteLine($"Writing {value} to property {propertyName} of {ObjectType} {ObjectIdentifier}");
            }
        }
    }
}