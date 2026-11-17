using Newtonsoft.Json.Linq;

public static class DialogueVariableDefaultValueConverter
{
    public static bool TryConvert(DialogueVariableDefinition definition, out object value, out string error)
    {
        value = null;
        error = null;

        if (definition == null)
        {
            error = "Variable definition is null.";
            return false;
        }

        if (definition.DefaultValue == null || definition.DefaultValue.Type == JTokenType.Null)
        {
            error = "Variable does not have a valid default value.";
            return false;
        }

        switch (definition.TypeId)
        {
            case "int":

                if (definition.DefaultValue.Type != JTokenType.Integer)
                {
                    error = "Default value is not an integer.";
                    return false;
                }

                value = definition.DefaultValue.Value<int>();
                return true;


            case "bool":

                if (definition.DefaultValue.Type != JTokenType.Boolean)
                {
                    error = "Default value is not a boolean.";
                    return false;
                }

                value = definition.DefaultValue.Value<bool>();
                return true;


            case "float":

                if (definition.DefaultValue.Type != JTokenType.Float && definition.DefaultValue.Type != JTokenType.Integer)
                {
                    error = "Default value is not a number.";
                    return false;
                }

                value = definition.DefaultValue.Value<float>();
                return true;


            case "string":

                if (definition.DefaultValue.Type != JTokenType.String)
                {
                    error = "Default value is not a string.";
                    return false;
                }

                value = definition.DefaultValue.Value<string>();
                return true;


            default:
                error = $"Unsupported variable type: {definition.TypeId}";
                return false;
        }
    }
}