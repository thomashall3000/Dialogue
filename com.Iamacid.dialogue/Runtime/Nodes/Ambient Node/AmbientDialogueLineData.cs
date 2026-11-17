using Newtonsoft.Json;

[JsonObject(MemberSerialization.OptIn)]
public class AmbientDialogueLineData
{
    [JsonProperty("lineId")]
    private string lineId;

    [JsonProperty("characterId")]
    private string characterId;

    [JsonProperty("text")]
    private string text;

    public string LineId => lineId;
    public string CharacterId => characterId;
    public string Text => text;
}