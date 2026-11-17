using System.Collections.Generic;
using Newtonsoft.Json;

public class ActionNodeData : DialogueNodeData
{
    [JsonProperty("actions")]
    private List<DialogueActionReference> actions = new List<DialogueActionReference>();

    [JsonProperty("executionMode")]
    private ActionExecutionMode executionMode = ActionExecutionMode.Sequential;

    public IReadOnlyList<DialogueActionReference> Actions => actions;

    public ActionExecutionMode ExecutionMode => executionMode;

    public override DialogueNodeType NodeType => DialogueNodeType.Action;
}