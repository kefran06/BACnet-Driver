using System;
using System.Collections.Generic;

namespace BACnet.Core.Objects
{
    public abstract class BACnetObject
    {
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

        public uint ObjectIdentifier { get; set; }
        public string ObjectName 
        { 
            get => GetProperty<string>("ObjectName");
            set => SetProperty("ObjectName", value); 
        }
        public string ObjectType 
        { 
            get => GetProperty<string>("ObjectType");
            set => SetProperty("ObjectType", value);
        }

        protected BACnetObject(uint objectIdentifier, string objectType)
        {
            ObjectIdentifier = objectIdentifier;
            SetProperty("ObjectType", objectType);
        }

        public virtual T GetProperty<T>(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            if (_properties.TryGetValue(propertyName, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                
                try
                {
                    // Attempt to convert the value to the requested type
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (InvalidCastException)
                {
                    throw new InvalidCastException($"Property '{propertyName}' cannot be converted to type {typeof(T).Name}");
                }
            }
            
            throw new KeyNotFoundException($"Property '{propertyName}' does not exist");
        }

        public virtual void SetProperty(string propertyName, object value)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));

            _properties[propertyName] = value;
            Console.WriteLine($"Property '{propertyName}' set to '{value}' for {ObjectType}:{ObjectIdentifier}");
        }

        public virtual bool HasProperty(string propertyName)
        {
            return !string.IsNullOrEmpty(propertyName) && _properties.ContainsKey(propertyName);
        }

        public abstract void ReadProperty(string propertyName);
        public abstract void WriteProperty(string propertyName, object value);

        public override string ToString()
        {
            return $"{ObjectType}:{ObjectIdentifier} '{ObjectName}'";
        }
    }
}