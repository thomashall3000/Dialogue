using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class DialogueVariableDatabaseSerializer
{

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class SerializedDatabase
    {
        [JsonProperty("schemaVersion", Required = Required.Always)]
        public int SchemaVersion { get; set; }

        [JsonProperty("variables", Required = Required.Always)]
        public List<DialogueVariableDefinition> Variables { get; set; } = new List<DialogueVariableDefinition>();
    }

    public static string Serialize(DialogueVariableDatabase database)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        SerializedDatabase serializedDatabase = new SerializedDatabase
        {
            SchemaVersion = database.SchemaVersion,
            Variables = new List<DialogueVariableDefinition>(database.Variables)
        };

        return JsonConvert.SerializeObject(
            serializedDatabase,
            Formatting.Indented);
    }

    public static bool TryDeserialize(string json, out DialogueVariableDatabase database, out string error)
    {
        database = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Variable database JSON is empty.";
            return false;
        }

        JObject root;

        try
        {
            root = JObject.Parse(json);
        }
        catch (JsonReaderException exception)
        {
            error = $"Variable database JSON is malformed: {exception.Message}";
            return false;
        }

        JToken schemaVersionToken = root["schemaVersion"];

        if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer)
        {
            error = "Variable database does not contain a valid schemaVersion.";
            return false;
        }

        int schemaVersion = schemaVersionToken.Value<int>();

        if (schemaVersion > DialogueVariableDatabase.CurrentSchemaVersion)
        {
            error = $"Variable database uses schema version {schemaVersion}, " + $"but this package only supports up to version " + $"{DialogueVariableDatabase.CurrentSchemaVersion}.";

            return false;
        }

        if (schemaVersion < DialogueVariableDatabase.CurrentSchemaVersion)
        {
            error =  $"Variable database uses schema version {schemaVersion} " + "and requires migration before it can be loaded.";

            return false;
        }

        SerializedDatabase serializedDatabase;

        try
        {
            serializedDatabase = root.ToObject<SerializedDatabase>();
        }
        catch (JsonException exception)
        {
            error =
                $"Variable database could not be deserialized: " +
                exception.Message;

            return false;
        }

        if (serializedDatabase == null)
        {
            error = "Variable database could not be deserialized.";
            return false;
        }

        database = new DialogueVariableDatabase(serializedDatabase.SchemaVersion, serializedDatabase.Variables);

        return true;
    }
}