using System;

namespace BACnet.Core.Objects
{
    public class AnalogInput : BACnetObject
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

        public AnalogInput(uint instanceNumber) 
            : base(instanceNumber, "AnalogInput")
        {
            ObjectName = $"AI_{instanceNumber}";
            SetProperty("PresentValue", 0.0f);
            SetProperty("MinPresentValue", float.MinValue);
            SetProperty("MaxPresentValue", float.MaxValue);
            SetProperty("Units", "units"); // Default unit, can be changed
        }

        public void UpdateValue(float newValue)
        {
            if (newValue < MinPresentValue || newValue > MaxPresentValue)
            {
                Console.WriteLine($"Attempted to set value {newValue} outside of range [{MinPresentValue}, {MaxPresentValue}] for {ObjectName}");
                throw new ArgumentOutOfRangeException(nameof(newValue), "Value is out of range.");
            }
            
            Console.WriteLine($"Updating {ObjectName} value from {PresentValue} to {newValue}");
            SetProperty("PresentValue", newValue);
        }

        public override void ReadProperty(string propertyName)
        {
            try
            {
                // Check if the property exists
                if (!HasProperty(propertyName))
                {
                    Console.WriteLine($"Property '{propertyName}' not found in {ObjectType} {ObjectIdentifier}");
                    throw new KeyNotFoundException($"Property '{propertyName}' not found");
                }

                Console.WriteLine($"Reading property {propertyName} from {ObjectType} {ObjectIdentifier}");
                
                // For properties that might require special handling or hardware access
                switch (propertyName)
                {
                    case "PresentValue":
                        // In a real system, this would read from hardware/sensor
                        // For this implementation, we'll simulate reading from an external source
                        TryReadFromHardware();
                        break;
                        
                    case "Units":
                    case "MinPresentValue":
                    case "MaxPresentValue":
                        // These properties are stored locally, no external read needed
                        Console.WriteLine($"Retrieved local property {propertyName}: {GetProperty<object>(propertyName)}");
                        break;
                        
                    default:
                        // Standard property retrieval
                        object value = GetProperty<object>(propertyName);
                        Console.WriteLine($"Retrieved property {propertyName}: {value}");
                        break;
                }
            }
            catch (Exception ex) when (!(ex is KeyNotFoundException))
            {
                Console.WriteLine($"Error reading property {propertyName}: {ex.Message}");
                throw new InvalidOperationException($"Failed to read property {propertyName}", ex);
            }
        }
        
        private void TryReadFromHardware()
        {
            try
            {
                // Simulate communication with hardware (e.g., polling a sensor)
                Console.WriteLine($"Polling hardware for current value of {ObjectName}...");
                
                // In a real implementation, this would be code to communicate with
                // actual hardware, like:
                // - Reading from a serial port
                // - Making a network request to an IoT device
                // - Reading from a controller via Modbus, etc.
                
                // For simulation, we'll slightly adjust the current value
                // to simulate a real sensor with minor fluctuations
                Random random = new Random();
                float currentValue = PresentValue;
                float noise = (float)((random.NextDouble() - 0.5) * 0.1); // Small random fluctuation
                float newValue = currentValue + noise;
                
                // Ensure value stays within defined limits
                newValue = Math.Max(MinPresentValue, Math.Min(MaxPresentValue, newValue));
                
                // Update the property with the "read" value
                SetProperty("PresentValue", newValue);
                Console.WriteLine($"Successfully read hardware value: {newValue} {Units}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hardware communication error for {ObjectName}: {ex.Message}");
                // In a production system, you might set fault flags here
            }
        }

        public override void WriteProperty(string propertyName, object value)
        {
            // In a real implementation, this would write to a physical device or data source
            if (propertyName == "PresentValue")
            {
                if (value is float floatValue)
                {
                    UpdateValue(floatValue);
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
            }
            
            Console.WriteLine($"Writing {value} to property {propertyName} of {ObjectType} {ObjectIdentifier}");
        }
    }
}