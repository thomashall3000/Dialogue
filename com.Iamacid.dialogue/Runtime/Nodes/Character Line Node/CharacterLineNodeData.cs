using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class CharacterLineNodeData : DialogueNodeData
{
    [SerializeField]
    [JsonProperty("characterId")]
    private string characterId;

    [SerializeField]
    [JsonProperty("text")]
    private string text;

    public string CharacterId => characterId;
    public string Text => text;

    public override DialogueNodeType NodeType => DialogueNodeType.CharacterLine;
}