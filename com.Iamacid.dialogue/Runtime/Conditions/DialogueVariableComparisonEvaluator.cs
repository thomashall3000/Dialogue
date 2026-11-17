using Newtonsoft.Json.Linq;
using System;


public class DialogueVariableComparisonEvaluator
{

    public static bool TryEvaluate(DialogueVariableComparison comparison, DialogueVariableRegistry registry, out bool result, out string error)
    {
        result = false;
        error = null;

        if (comparison == null)
        {
            error = "Comparison cannot be null.";
            return false;
        }

        if (registry == null)
        {
            error = "Dialogue variable registry cannot be null.";
            return false;
        }

        if (!registry.TryGetRawValue(comparison.VariableId, out object currentValue, out Type valueType))
        {
            error = $"Variable '{comparison.VariableId}' is not registered.";

            return false;
        }

        if (!TryConvertComparisonValue(comparison.ComparisonValue, valueType, out object targetValue, out error))
        {
            return false;
        }

        switch (comparison.ComparisonOperator)
        {
            case ComparisonOperator.Equal:
                result = Equals(currentValue, targetValue);
                return true;

            case ComparisonOperator.NotEqual:
                result = !Equals(currentValue, targetValue);
                return true;
        }

        if (valueType == typeof(bool) || valueType == typeof(string))
        {
            error = $"Operator '{comparison.ComparisonOperator}' " + $"is not valid for type '{valueType.Name}'.";

            return false;
        }

        if (!(currentValue is IComparable comparable))
        {
            error = $"Variable type '{valueType.Name}' cannot be ordered.";

            return false;
        }

        int comparisonResult = comparable.CompareTo(targetValue);

        switch (comparison.ComparisonOperator)
        {
            case ComparisonOperator.GreaterThan:
                result = comparisonResult > 0;
                return true;

            case ComparisonOperator.GreaterThanOrEqual:
                result = comparisonResult >= 0;
                return true;

            case ComparisonOperator.LessThan:
                result = comparisonResult < 0;
                return true;

            case ComparisonOperator.LessThanOrEqual:
                result = comparisonResult <= 0;
                return true;

            default:
                error = $"Unsupported comparison operator " + $"'{comparison.ComparisonOperator}'.";

                return false;
        }
    }

    private static bool TryConvertComparisonValue(JToken token, Type targetType, out object value, out string error)
    {
        value = null;
        error = null;

        if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
        {
            error = "Comparison value is null.";
            return false;
        }

        if (targetType == typeof(int))
        {
            if (token.Type != JTokenType.Integer)
            {
                error = "Comparison value must be an integer.";
                return false;
            }

            value = token.Value<int>();
            return true;
        }

        if (targetType == typeof(float))
        {
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
            {
                error = "Comparison value must be numeric.";
                return false;
            }

            value = token.Value<float>();
            return true;
        }

        if (targetType == typeof(bool))
        {
            if (token.Type != JTokenType.Boolean)
            {
                error = "Comparison value must be a boolean.";
                return false;
            }

            value = token.Value<bool>();
            return true;
        }

        if (targetType == typeof(string))
        {
            if (token.Type != JTokenType.String)
            {
                error = "Comparison value must be a string.";
                return false;
            }

            value = token.Value<string>();
            return true;
        }

        error = $"Unsupported dialogue variable type '{targetType.Name}'.";
        return false;
    }

}
