using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ConditionExpressionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(ConditionExpression);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        JObject json = JObject.Load(reader);

        string expressionType = json.Value<string>("expressionType");

        json.Remove("expressionType");

        switch (expressionType)
        {
            case "comparison":
                return json.ToObject<DialogueVariableComparison>(serializer);

            case "group":
                return json.ToObject<ConditionGroup>(serializer);

            default:
                throw new JsonSerializationException($"Unsupported expression type '{expressionType}'.");
        }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        JObject json = JObject.FromObject(value, serializer);

        if (value is DialogueVariableComparison)
        {
            json["expressionType"] = "comparison";
        }
        else if (value is ConditionGroup)
        {
            json["expressionType"] = "group";
        }
        else
        {
            throw new JsonSerializationException($"Unsupported condition expression type " + $"'{value.GetType().Name}'.");
        }

        json.WriteTo(writer);
    }
}