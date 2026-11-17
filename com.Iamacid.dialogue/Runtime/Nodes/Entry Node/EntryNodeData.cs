using System;
using Newtonsoft.Json;

[Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class EntryNodeData : DialogueNodeData
{
    public override DialogueNodeType NodeType => DialogueNodeType.Entry;
}