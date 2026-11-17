using Newtonsoft.Json;

public class DialogueReferenceNodeData : DialogueNodeData
{
    [JsonProperty("dialogueGuid")]
    private string dialogueGuid;

    public string DialogueGuid => dialogueGuid;

    public override DialogueNodeType NodeType => DialogueNodeType.DialogueReference;
}