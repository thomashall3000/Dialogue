
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class ConditionExpressionValidator
{
    public static (bool Success, string Error) Validate(
        ConditionExpression expression)
    {
        HashSet<ConditionExpression> activeExpressions = new HashSet<ConditionExpression>();

        return ValidateExpression(expression, activeExpressions);
    }

    private static (bool Success, string Error) ValidateExpression(ConditionExpression expression, HashSet<ConditionExpression> activeExpressions)
    {
        if (expression == null)
        {
            return (false, "Condition expression is null.");
        }
        if (!activeExpressions.Add(expression))
        {
            return (false, "Condition expression contains a circular reference.");
        }

        try
        {
            if (expression is DialogueVariableComparison comparison)
            {
                if (string.IsNullOrWhiteSpace(comparison.VariableId))
                {
                    return (false, "A variable comparison has a missing variable ID.");
                }

                if (!Enum.IsDefined(typeof(ComparisonOperator), comparison.ComparisonOperator))
                {
                    return (false, $"Unsupported comparison operator " + $"'{comparison.ComparisonOperator}'.");
                }

                if (comparison.ComparisonValue == null ||comparison.ComparisonValue.Type == JTokenType.Null || comparison.ComparisonValue.Type == JTokenType.Undefined)
                {
                    return (false, $"Comparison for variable '{comparison.VariableId}' " + "has no comparison value.");
                }

                return (true, null);
            }

            if (expression is ConditionGroup group)
            {
                if (!Enum.IsDefined(typeof(LogicalOperator), group.LogicalOperator))
                {
                    return (false, $"Unsupported logical operator " + $"'{group.LogicalOperator}'.");
                }

                if (group.Children == null || group.Children.Count == 0)
                {
                    return (false, "Condition group contains no child expressions.");
                }

                if (group.LogicalOperator == LogicalOperator.Xor && group.Children.Count != 2)
                {
                    return (false, "XOR condition groups must contain exactly two children.");
                }

                for (int i = 0; i < group.Children.Count; i++)
                {
                    var childResult = ValidateExpression(group.Children[i], activeExpressions);

                    if (!childResult.Success)
                    {
                        return (false, $"Condition group child {i}: {childResult.Error}");
                    }
                }

                return (true, null);
            }

            return (false, $"Unsupported condition expression type " + $"'{expression.GetType().Name}'.");
        }
        finally
        {
            activeExpressions.Remove(expression);
        }
    }
}
