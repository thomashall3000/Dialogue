using System.Collections.Generic;
using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
public class ConditionGroup : ConditionExpression
{
    [JsonProperty("logicalOperator")]
    private LogicalOperator logicalOperator;

    [JsonProperty("children", ItemConverterType = typeof(ConditionExpressionConverter))]
    private List<ConditionExpression> children = new List<ConditionExpression>();

    public LogicalOperator LogicalOperator => logicalOperator;

    public List<ConditionExpression> Children => children;
}