
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class ConditionVariableValidator
{
    public static (bool Success, string Error) Validate(ConditionExpression expression, DialogueVariableDatabase database)
    {
        if (database == null)
            return (false, "Variable database is null.");

        if (database.Variables == null)
            return (false, "Variable database has no variable collection.");

        var structureResult = ConditionExpressionValidator.Validate(expression);

        if (!structureResult.Success)
            return structureResult;

        Dictionary<string, DialogueVariableDefinition> definitions = new Dictionary<string, DialogueVariableDefinition>( StringComparer.Ordinal);

        foreach (DialogueVariableDefinition definition in database.Variables)
        {
            if (definition == null)
                return (false, "Variable database contains a null definition.");

            if (string.IsNullOrWhiteSpace(definition.Id))
                return (false, "Variable database contains a blank variable ID.");

            if (definitions.ContainsKey(definition.Id))
            {
                return (false, $"Duplicate variable ID '{definition.Id}'.");
            }

            definitions.Add(definition.Id, definition);
        }

        return ValidateExpression(expression, definitions);
    }

    private static (bool Success, string Error) ValidateExpression(ConditionExpression expression, Dictionary<string, DialogueVariableDefinition> definitions)
    {
        if (expression is ConditionGroup group)
        {
            foreach (ConditionExpression child in group.Children)
            {
                var result = ValidateExpression(child, definitions);

                if (!result.Success)
                    return result;
            }

            return (true, null);
        }

        if (expression is DialogueVariableComparison comparison)
        {
            if (!definitions.TryGetValue(comparison.VariableId, out DialogueVariableDefinition definition))
            {
                return (false, $"Variable '{comparison.VariableId}' does not exist.");
            }

            return ValidateComparison(comparison, definition);
        }

        return (false, "Unsupported condition expression.");
    }

    private static (bool Success, string Error) ValidateComparison(DialogueVariableComparison comparison, DialogueVariableDefinition definition)
    {
        JToken token = comparison.ComparisonValue;
        string typeId = definition.TypeId;

        bool equalityOperator = comparison.ComparisonOperator == ComparisonOperator.Equal || comparison.ComparisonOperator == ComparisonOperator.NotEqual;

        if ((typeId == "bool" || typeId == "string") && !equalityOperator)
        {
            return (false, $"Operator '{comparison.ComparisonOperator}' " + $"is not supported for variable '{definition.Id}' " + $"of type '{typeId}'.");
        }

        if (token == null)
        {
            return (false, $"Comparison value for '{definition.Id}' is null.");
        }

        try
        {
            switch (typeId)
            {
                case "int":
                    if (token.Type != JTokenType.Integer)
                        return InvalidType(definition, "integer");

                    token.Value<int>();
                    break;

                case "float":
                    if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                    {
                        return InvalidType(definition, "numeric");
                    }

                    float floatValue = token.Value<float>();

                    if (float.IsNaN(floatValue) || float.IsInfinity(floatValue))
                    {
                        return (false, $"Variable '{definition.Id}' has a " + "non-finite comparison value.");
                    }

                    break;

                case "bool":
                    if (token.Type != JTokenType.Boolean)
                        return InvalidType(definition, "boolean");

                    break;

                case "string":
                    if (token.Type != JTokenType.String)
                        return InvalidType(definition, "string");

                    break;

                default:
                    return (false, $"Variable '{definition.Id}' uses unsupported " + $"type '{typeId}'.");
            }
        }
        catch (Exception exception)
        {
            return (false, $"Invalid comparison value for variable " + $"'{definition.Id}': {exception.Message}");
        }

        return (true, null);
    }

    private static (bool Success, string Error) InvalidType(DialogueVariableDefinition definition, string expectedType)
    {
        return (false, $"Variable '{definition.Id}' requires a " + $"{expectedType} comparison value.");
    }
}
