using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[JsonObject(MemberSerialization.OptIn)]
public class DialogueVariableComparison : ConditionExpression
{
    [JsonProperty("variableId")]
    private string variableId;

    [JsonProperty("comparisonOperator")]
    private ComparisonOperator comparisonOperator;

    [JsonProperty("comparisonValue")]
    private JToken comparisonValue;

    public string VariableId => variableId;

    public ComparisonOperator ComparisonOperator => comparisonOperator;

    public JToken ComparisonValue => comparisonValue;
}