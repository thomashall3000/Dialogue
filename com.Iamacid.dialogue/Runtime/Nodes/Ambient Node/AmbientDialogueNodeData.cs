using Newtonsoft.Json;
using System.Collections.Generic;

public class AmbientDialogueNodeData : DialogueNodeData
{
    [JsonProperty("lines")]
    private List<AmbientDialogueLineData> lines = new List<AmbientDialogueLineData>();

    [JsonProperty("selectionMode")]
    private AmbientSelectionMode selectionMode;

    [JsonProperty("exitMode")]
    private AmbientExitMode exitMode;

    public IReadOnlyList<AmbientDialogueLineData> Lines => lines;
    public AmbientSelectionMode SelectionMode => selectionMode;
    public AmbientExitMode ExitMode => exitMode;

    public override DialogueNodeType NodeType => DialogueNodeType.AmbientDialogue;
}