using System;
using System.Collections.Generic;
using BACnet.Core.Objects;

namespace BACnet.Core.Services
{
    public class ReadProperty
    {
        private readonly BACnetObject _targetObject;
        private readonly string _propertyIdentifier;
        
        public ReadProperty(BACnetObject targetObject, string propertyIdentifier)
        {
            _targetObject = targetObject ?? throw new ArgumentNullException(nameof(targetObject));
            _propertyIdentifier = propertyIdentifier ?? throw new ArgumentNullException(nameof(propertyIdentifier));
        }
        
        public object Execute()
        {
            try
            {
                _targetObject.ReadProperty(_propertyIdentifier);
                
                // The ReadProperty method is abstract and implemented by derived classes
                // It should trigger any necessary hardware reads or state updates 
                
                // For properties that directly map to BACnetObject properties, we can retrieve them:
                var propertyValue = GetPropertyValue();
                return propertyValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing ReadProperty: {ex.Message}");
                throw;
            }
        }

        public object GetPropertyValue()
        {
            try
            {
                // Use reflection to get the property value based on the property identifier
                var propertyInfo = _targetObject.GetType().GetProperty(_propertyIdentifier);
                if (propertyInfo != null)
                {
                    return propertyInfo.GetValue(_targetObject);
                }
                
                // If it's not a direct property, try to get it from the BACnetObject's dictionary
                if (_targetObject.HasProperty(_propertyIdentifier))
                {
                    // Use dynamic to avoid knowing the exact type
                    return _targetObject.GetType()
                        .GetMethod("GetProperty")
                        .MakeGenericMethod(typeof(object))
                        .Invoke(_targetObject, new object[] { _propertyIdentifier });
                }
                
                throw new KeyNotFoundException($"Property {_propertyIdentifier} not found on {_targetObject.ObjectType} object {_targetObject.ObjectIdentifier}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving property value: {ex.Message}");
                throw;
            }
        }

        public override string ToString()
        {
            return $"ReadProperty Request: Object={_targetObject.ObjectType}:{_targetObject.ObjectIdentifier}, Property={_propertyIdentifier}";
        }
    }
}