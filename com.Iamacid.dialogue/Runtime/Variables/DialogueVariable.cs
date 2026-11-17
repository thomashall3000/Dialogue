using System;
using System.Collections.Generic;

public class DialogueVariable
{
    public string Id { get;}
    public Type ValueType { get;}
    public object Value { get; private set; }

    public DialogueVariable(string id, Type valueType, object value)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("The variable ID cannot be null, empty or whitespace.", nameof(id));
        }

        if (valueType == null)
        {
            throw new ArgumentNullException(nameof(valueType));
        }

        if (value == null)
        {
            if (valueType.IsValueType && Nullable.GetUnderlyingType(valueType) == null)
            {
                throw new ArgumentException($"Variable '{id}' cannot have a null value because " + $"{valueType.Name} is a non-nullable value type.", nameof(value));
            }
        }
        else if (!valueType.IsInstanceOfType(value))
        {
            throw new ArgumentException($"Variable '{id}' expects a value of type {valueType.Name}, " + $"but received {value.GetType().Name}.", nameof(value));
        }

        Id = id;
        ValueType = valueType;
        Value = value;
    }

    public (bool Success, string Error) ChangeValue(object newValue)
    {
        if (newValue == null)
        {
            if (ValueType.IsValueType && Nullable.GetUnderlyingType(ValueType) == null)
            {
                return (false, $"Variable '{Id}' cannot be null because " + $"{ValueType.Name} is a non-nullable value type.");
            }

            Value = null;
            return (true, null);
        }

        if (!ValueType.IsInstanceOfType(newValue))
        {
            return (false, $"Variable '{Id}' expects a value of type {ValueType.Name}.");
        }

        Value = newValue;

        return (true, null);
    }
}