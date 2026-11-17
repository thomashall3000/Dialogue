using System;
using System.Collections.Generic;

public class DialogueVariableRegistry
{

    public event Action<string> VariableChanged;

    private readonly Dictionary<string, DialogueVariable> variables = new Dictionary<string, DialogueVariable>();

    public (bool Success, string Error) Register<T>(string id, T initialValue)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return (false, $"Variable ID : {id} is invalid.");
        }

        if (variables.ContainsKey(id))
        {
            return (false, $"Variable ID: {id} is already in the registry");
        }

        try
        {
            DialogueVariable variable = new DialogueVariable(id, typeof(T), initialValue);

            variables.Add(id, variable);
        }
        catch (ArgumentException exception)
        {
            return (false, exception.Message);
        }

        return (true, null);
    }

    public (bool Success, string Error) SetValue<T>(string id, T newValue)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (false, "Variable ID is invalid.");

        if (!variables.TryGetValue(id, out DialogueVariable variable))
        {
            return (false, $"Variable '{id}' is not registered.");
        }

        var result = variable.ChangeValue(newValue);

        if (result.Success)
        {
            VariableChanged?.Invoke(id);
        }

        return result;
    }

    public bool TryGetValue<T>(string id, out T value)
    {
        value = default(T);

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!variables.TryGetValue(id, out DialogueVariable variable))
        {
            return false;
        }

        if (!(variable.ValueType == typeof(T)))
        {
            return false;
        }

        value = (T)variable.Value;

        return true;
    }

    public (bool Success, string Error) ModifyValue<T>(string id, Func<T, T> modification)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return (false, $"Variable ID: {id} is invalid");
        }

        if (!variables.ContainsKey(id))
        {
            return (false, $"Variable ID: {id} is not registered");
        }

        if (!TryGetValue<T>(id, out T currentValue))
        {
            return (false, $"Variable ID: {id} was not found or has an incompatible type.");
        }

        if (modification == null)
        {
            return (false, "The modification function cannot be null.");
        }

        T newValue = modification(currentValue);

        return SetValue(id, newValue);
    }

    public bool TryGetRawValue(string id, out object value, out Type valueType)
    {
        value = null;
        valueType = null;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        if (!variables.TryGetValue(id, out DialogueVariable variable))
        {
            return false;
        }

        value = variable.Value;
        valueType = variable.ValueType;

        return true;
    }
}
