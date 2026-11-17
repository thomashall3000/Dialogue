using Newtonsoft.Json;
using System;
using UnityEngine;

[Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class DialogueConnectionData
{

    [SerializeField]
    [JsonProperty("sourceNodeId")]
    private string sourceNodeId;

    [SerializeField]
    [JsonProperty("sourcePortId")]
    private string sourcePortId;

    [SerializeField]
    [JsonProperty("targetNodeId")]
    private string targetNodeId;

    public string SourceNodeId => sourceNodeId;
    public string SourcePortId => sourcePortId;
    public string TargetNodeId => targetNodeId;

    public DialogueConnectionData(string sourceNodeId, string sourcePortId, string targetNodeId)
    {
        this.sourceNodeId = sourceNodeId;
        this.sourcePortId = sourcePortId;
        this.targetNodeId = targetNodeId;
    }

}
