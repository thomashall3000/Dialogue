using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

public class DialogueSerializer
{
    public string SerializeNode(DialogueNodeData node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        JObject jsonNode = JObject.FromObject(node);

        jsonNode["nodeType"] = node.NodeType.ToString();

        if (node is ConditionNodeData condition)
        {
            ReplaceJsonProperty(jsonNode, "rootExpression", SerializeConditionExpression(condition.RootExpression));
        }

        return jsonNode.ToString(Formatting.Indented);
    }

    public DialogueNodeData DeserializeNode(string json)
    {
        JObject jsonNode = JObject.Parse(json);

        string nodeType = jsonNode.Value<string>("nodeType");

        switch (nodeType)
        {
            case "CharacterLine":
                return jsonNode.ToObject<CharacterLineNodeData>();

            case "Entry":
                return jsonNode.ToObject<EntryNodeData>();

            case "End":
                return jsonNode.ToObject<EndNodeData>();

            case "Choice":
                return jsonNode.ToObject<ChoiceNodeData>();

            case "Condition":
                JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings{Converters ={new ConditionExpressionConverter()}});
                return jsonNode.ToObject<ConditionNodeData>(serializer);

            case "Action":
                return jsonNode.ToObject<ActionNodeData>();

            case "AmbientDialogue":
                return jsonNode.ToObject<AmbientDialogueNodeData>();

            case "Loop":
                return jsonNode.ToObject<LoopNodeData>();

            case "DialogueReference":
                return jsonNode.ToObject<DialogueReferenceNodeData>();

            default:
                throw new JsonSerializationException($"Unsupported dialogue node type: {nodeType}");
        }
    }

    public string Serialize(DialogueData dialogue)
    {
        if (dialogue == null)
        {
            throw new ArgumentNullException(nameof(dialogue));
        }

        JObject jsonDialogue = new JObject();
        JArray jsonNodes = new JArray();
        JArray jsonConnections = new JArray();

        jsonDialogue["schemaVersion"] = dialogue.SchemaVersion;
        jsonDialogue["dialogueId"] = dialogue.DialogueId;

        if (dialogue.Settings == null)
        {
            throw new JsonSerializationException("The dialogue settings are null.");
        }

        jsonDialogue["dialogueSettings"] = JObject.FromObject(dialogue.Settings);

        if (dialogue.Nodes == null)
        {
            throw new JsonSerializationException("The dialogue node collection is null.");
        }

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node == null)
            {
                throw new JsonSerializationException("The dialogue contains a null node.");
            }

            string jsonString = SerializeNode(node);

            JObject jsonObject = JObject.Parse(jsonString);

            jsonNodes.Add(jsonObject);
        }


        if (dialogue.Connections == null)
        {
            throw new JsonSerializationException("The dialogue connection collection is null.");
        }

        foreach (DialogueConnectionData connection in dialogue.Connections)
        {
            if (connection == null)
            {
                throw new JsonSerializationException("The dialogue contains a null connection.");
            }

            JObject jsonConnection = JObject.FromObject(connection);

            jsonConnections.Add(jsonConnection);
        }

        jsonDialogue["nodes"] = jsonNodes;
        jsonDialogue["connections"] = jsonConnections;

        return jsonDialogue.ToString(Formatting.Indented);
    }

    public DialogueData Deserialize(string json)
    {
        JObject jsonDialogue = JObject.Parse(json);

        JToken versionToken = jsonDialogue["schemaVersion"];

        if (versionToken == null || versionToken.Type != JTokenType.Integer)
        {
            throw new JsonSerializationException("The dialogue JSON does not contain a valid schema version.");
        }

        int schemaVersion = versionToken.Value<int>();

        if (schemaVersion != DialogueData.CurrentSchemaVersion)
        {
            throw new JsonSerializationException($"Unsupported dialogue schema version: {schemaVersion}");
        }

        string dialogueId = jsonDialogue.Value<string>("dialogueId");

        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            throw new JsonSerializationException("The dialogue JSON does not contain a valid dialogue ID.");
        }

        JToken settingsToken = jsonDialogue["dialogueSettings"];

        DialogueSettings settings;

        if (settingsToken == null)
        {
            settings = new DialogueSettings();
        }
        else if (settingsToken.Type == JTokenType.Object)
        {
            settings = settingsToken.ToObject<DialogueSettings>();

            if (settings == null)
            {
                throw new JsonSerializationException("The dialogue settings could not be deserialized.");
            }
        }
        else
        {
            throw new JsonSerializationException("The dialogue JSON contains an invalid dialogueSettings value.");
        }

        JArray jsonNodes = jsonDialogue["nodes"] as JArray;

        if (jsonNodes == null)
        {
            throw new JsonSerializationException("The dialogue JSON does not contain a valid nodes array.");
        }

        JArray jsonConnections = jsonDialogue["connections"] as JArray;

        if (jsonConnections == null)
        {
            throw new JsonSerializationException("The dialogue JSON does not contain a valid connections array.");
        }

        List<DialogueNodeData> nodes = new List<DialogueNodeData>();

        foreach (JToken entry in jsonNodes)
        {
            string jsonString = entry.ToString(Formatting.None);

            DialogueNodeData node = DeserializeNode(jsonString);

            nodes.Add(node);
        }

        List<DialogueConnectionData> connections = new List<DialogueConnectionData>();

        foreach (JToken entry in jsonConnections)
        {
            DialogueConnectionData connection = entry.ToObject<DialogueConnectionData>();

            connections.Add(connection);
        }

        DialogueData dialogue = new DialogueData(dialogueId, schemaVersion, nodes, connections, settings);

        var result = dialogue.Prepare();

        if (!result.Success)
        {
            throw new JsonSerializationException(result.Error);
        }

        return dialogue;
    }

    private static JToken SerializeConditionExpression(ConditionExpression expression)
    {
        if (expression == null)
        {
            return JValue.CreateNull();
        }

        if (expression is DialogueVariableComparison comparison)
        {
            JObject json = JObject.FromObject(comparison);

            json["expressionType"] = "comparison";

            return json;
        }

        if (expression is ConditionGroup group)
        {
            JObject json = JObject.FromObject(group);

            json["expressionType"] = "group";

            if (group.Children != null)
            {
                JArray children = new JArray();

                foreach (ConditionExpression child in group.Children)
                {
                    children.Add(SerializeConditionExpression(child));
                }

                ReplaceJsonProperty(json, "children", children);
            }

            return json;
        }

        throw new JsonSerializationException($"Unsupported condition expression type '{expression.GetType().Name}'.");
    }

    private static void ReplaceJsonProperty(JObject json, string propertyName, JToken value)
    {
        JProperty existingProperty = json.Property(propertyName, StringComparison.OrdinalIgnoreCase
        );

        if (existingProperty != null)
        {
            existingProperty.Remove();
        }

        json[propertyName] = value;
    }
}