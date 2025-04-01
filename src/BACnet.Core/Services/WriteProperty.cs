using System;
using BACnet.Core.Objects;

namespace BACnet.Core.Services
{
    public class WriteProperty
    {
        private readonly BACnetObject _targetObject;
        private readonly string _propertyIdentifier;
        private readonly object _value;
        private bool _writeConfirmed = false;
        
        public WriteProperty(BACnetObject targetObject, string propertyIdentifier, object value)
        {
            _targetObject = targetObject ?? throw new ArgumentNullException(nameof(targetObject));
            _propertyIdentifier = propertyIdentifier ?? throw new ArgumentNullException(nameof(propertyIdentifier));
            _value = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public void Execute()
        {
            try
            {
                // Write the property to the object
                _targetObject.WriteProperty(_propertyIdentifier, _value);
                _writeConfirmed = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing WriteProperty: {ex.Message}");
                _writeConfirmed = false;
                throw;
            }
        }

        public bool IsWriteConfirmed()
        {
            return _writeConfirmed;
        }

        public override string ToString()
        {
            return $"WriteProperty Request: Object={_targetObject.ObjectType}:{_targetObject.ObjectIdentifier}, Property={_propertyIdentifier}, Value={_value}";
        }
    }
}