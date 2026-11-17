using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class ConditionExpressionEvaluator
{

    public static bool TryEvaluate(ConditionExpression expression, DialogueVariableRegistry registry, out bool result, out string error)
    {

        result = false;
        error = null;

        if (expression == null)
        {
            error = "Condition expression cannot be null.";
            return false;
        }

        if (registry == null)
        {
            error = "Dialogue variable registry cannot be null.";
            return false;
        }

        if (expression is DialogueVariableComparison comparison)
        {
            return DialogueVariableComparisonEvaluator.TryEvaluate(comparison, registry, out result, out error);
        }

        if (expression is ConditionGroup group)
        {
            if (group.Children == null || group.Children.Count == 0)
            {
                error = "Condition group contains no child expressions.";
                return false;
            }

            switch (group.LogicalOperator)
            {
                case LogicalOperator.And:
                    {
                        foreach (ConditionExpression child in group.Children)
                        {
                            if (!TryEvaluate(child, registry, out bool childResult, out error))
                            {
                                return false;
                            }

                            if (!childResult)
                            {
                                result = false;
                                return true;
                            }
                        }

                        result = true;
                        return true;
                    }

                case LogicalOperator.Or:
                    {
                        foreach (ConditionExpression child in group.Children)
                        {
                            if (!TryEvaluate(child, registry, out bool childResult, out error))
                            {
                                return false;
                            }

                            if (childResult)
                            {
                                result = true;
                                return true;
                            }
                        }

                        result = false;
                        return true;
                    }

                case LogicalOperator.Xor:
                    {
                        if (group.Children.Count != 2)
                        {
                            error = "XOR condition groups must contain exactly two children.";
                            return false;
                        }

                        if (!TryEvaluate(group.Children[0], registry, out bool firstResult, out error))
                        {
                            return false;
                        }

                        if (!TryEvaluate(group.Children[1], registry, out bool secondResult, out error))
                        {
                            return false;
                        }

                        result = firstResult != secondResult;
                        return true;
                    }

                default:
                    error = $"Unsupported logical operator '{group.LogicalOperator}'.";
                    return false;
            }
        }

        error = $"Unsupported condition expression type '{expression.GetType().Name}'.";
        return false;

    }

}
