using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class ChoiceResponseData
{

    [SerializeField]
    [JsonProperty("responseId")]
    private string responseId;

    [SerializeField]
    [JsonProperty("text")]
    private string text;

    public string ResponseId => responseId;
    public string Text => text;

    public ChoiceResponseData(string responseId, string text)
    {
        this.responseId = responseId;
        this.text = text;
    }
}
