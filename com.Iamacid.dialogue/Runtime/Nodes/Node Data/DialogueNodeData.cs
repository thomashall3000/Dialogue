using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
[JsonObject(MemberSerialization.OptIn)]
public abstract class DialogueNodeData
{
    [SerializeField]
    [JsonProperty("nodeId")]
    private string nodeId;

    public string NodeId => nodeId;

    public abstract DialogueNodeType NodeType { get; }
}