using Newtonsoft.Json;
using System;

[Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class EndNodeData : DialogueNodeData
{
    public override DialogueNodeType NodeType => DialogueNodeType.End;
}
