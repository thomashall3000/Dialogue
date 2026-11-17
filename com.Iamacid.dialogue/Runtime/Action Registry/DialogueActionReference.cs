using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[JsonObject(MemberSerialization.OptIn)]
public class DialogueActionReference
{
    [JsonProperty("actionId")]
    private string actionId;

    [JsonProperty("parameters")]
    private JToken parameters;

    [JsonProperty("completionMode")]
    private ActionCompletionMode completionMode = ActionCompletionMode.Explicit;

    [JsonProperty("completionCondition")]
    [JsonConverter(typeof(ConditionExpressionConverter))]
    private ConditionExpression completionCondition;

    public string ActionId => actionId;
    public JToken Parameters => parameters;
    public ActionCompletionMode CompletionMode => completionMode;
    public ConditionExpression CompletionCondition => completionCondition;

    public DialogueActionReference()
    {
    }

    public DialogueActionReference(string actionId, JToken parameters = null, ActionCompletionMode completionMode = ActionCompletionMode.Explicit, ConditionExpression completionCondition = null)
    {
        this.actionId = actionId;
        this.parameters = parameters;
        this.completionMode = completionMode;
        this.completionCondition = completionCondition;
    }
}