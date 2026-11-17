using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class ChoiceNodeData : DialogueNodeData
{

    [SerializeField]
    [JsonProperty("responses")]
    private List<ChoiceResponseData> responses = new List<ChoiceResponseData>();

    [SerializeField]
    [JsonProperty("characterId")]
    private string characterId;

    public string CharacterId => characterId;
    public override DialogueNodeType NodeType => DialogueNodeType.Choice;

    [SerializeField]
    [JsonProperty("responsePresentationOverride")]
    private ResponsePresentationOverride settingsOverride = ResponsePresentationOverride.UseDialogueDefault;

    public IReadOnlyList<ChoiceResponseData> Responses => responses;
    public ResponsePresentationOverride SettingsOverride => settingsOverride;
}

public enum ResponsePresentationOverride
{
    UseDialogueDefault,
    Immediate,
    DisplayAsLine
}


