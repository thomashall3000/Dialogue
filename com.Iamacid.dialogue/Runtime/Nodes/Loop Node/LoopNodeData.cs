using System.Collections.Generic;
using Newtonsoft.Json;

public class LoopNodeData : DialogueNodeData
{
    [JsonProperty("mode")]
    private LoopMode mode;

    [JsonProperty("iterationCount")]
    private int iterationCount;

    [JsonProperty("condition")]
    [JsonConverter(typeof(ConditionExpressionConverter))]
    private ConditionExpression condition;

    [JsonProperty("completionAction")]
    private DialogueActionReference completionAction;

    [JsonProperty("childNodeIds")]
    private List<string> childNodeIds = new List<string>();

    [JsonProperty("entryNodeId")]
    private string entryNodeId;

    public LoopMode Mode => mode;
    public int IterationCount => iterationCount;
    public ConditionExpression Condition => condition;
    public DialogueActionReference CompletionAction => completionAction;
    public IReadOnlyList<string> ChildNodeIds => childNodeIds;
    public string EntryNodeId => entryNodeId;

    public override DialogueNodeType NodeType => DialogueNodeType.Loop;
}