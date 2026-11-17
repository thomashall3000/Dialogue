using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DialogueVariableRegistryInitializer
{
    public static bool TryCreate(DialogueVariableDatabase database, out DialogueVariableRegistry registry, out string error)
    {
        registry = null;
        error = null;

        if (database == null)
        {
            error = "Dialogue variable database is null.";
            return false;
        }

        DialogueVariableRegistry newRegistry = new DialogueVariableRegistry();

        foreach (DialogueVariableDefinition definition in database.Variables)
        {
            if (!DialogueVariableDefaultValueConverter.TryConvert(definition, out object value, out string conversionError))
            {
                error = conversionError;
                return false;
            }

            (bool Success, string Error) registrationResult;

            switch (definition.TypeId)
            {
                case "int":
                    registrationResult = newRegistry.Register( definition.Id, (int)value);
                    break;

                case "bool":
                    registrationResult = newRegistry.Register( definition.Id, (bool)value);
                    break;

                case "float":
                    registrationResult = newRegistry.Register( definition.Id, (float)value);
                    break;

                case "string":
                    registrationResult = newRegistry.Register( definition.Id, (string)value);
                    break;

                default:
                    error = $"Unsupported variable type '{definition.TypeId}'.";
                    return false;
            }

            if (!registrationResult.Success)
            {
                error = registrationResult.Error;
                return false;
            }
        }

        registry = newRegistry;
        return true;
    }
}
