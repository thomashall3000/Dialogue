using Newtonsoft.Json;
using UnityEngine;

public class ConditionNodeData : DialogueNodeData
{
    [SerializeField]
    [JsonProperty("rootExpression")]
    private ConditionExpression rootExpression;

    public ConditionExpression RootExpression => rootExpression;

    public override DialogueNodeType NodeType => DialogueNodeType.Condition;
}