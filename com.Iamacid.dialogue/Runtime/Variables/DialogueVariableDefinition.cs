using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[System.Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class DialogueVariableDefinition
{

    [SerializeField]
    [JsonProperty("id")]
    private string id;

    [SerializeField]
    [JsonProperty("name")]
    private string name;

    [SerializeField]
    [JsonProperty("typeId")]
    private string typeId;

    [JsonProperty("defaultValue")]
    private JToken defaultValue;

    public string Id => id;
    public string Name => name;
    public string TypeId => typeId;
    public JToken DefaultValue => defaultValue;

    public DialogueVariableDefinition (string id, string name, string typeId, JToken defaultValue)
    {
        this.id = id;
        this.name = name;
        this.typeId = typeId;
        this.defaultValue = defaultValue;
    }
}
