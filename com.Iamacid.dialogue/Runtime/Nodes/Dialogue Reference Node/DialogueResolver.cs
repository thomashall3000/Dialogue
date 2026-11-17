using System;
using System.Collections.Generic;

public class DialogueResolver
{
    private readonly Dictionary<string, DialogueData> dialogues = new Dictionary<string, DialogueData>(StringComparer.OrdinalIgnoreCase);

    public (bool Success, string Error) Register(string dialogueGuid, DialogueData dialogue)
    {
        if (string.IsNullOrWhiteSpace(dialogueGuid))
            return (false, "Dialogue GUID cannot be empty.");

        if (dialogue == null)
            return (false, "Cannot register a null dialogue.");

        if (dialogues.ContainsKey(dialogueGuid))
            return (false, $"Dialogue GUID '{dialogueGuid}' is already registered.");

        dialogues.Add(dialogueGuid, dialogue);

        return (true, null);
    }

    public (bool Success, DialogueData Dialogue, string Error) TryResolve(string dialogueGuid)
    {
        if (string.IsNullOrWhiteSpace(dialogueGuid))
            return (false, null, "Dialogue reference has no GUID.");

        if (!dialogues.TryGetValue(dialogueGuid, out DialogueData dialogue))
            return (false, null, $"Could not resolve dialogue GUID '{dialogueGuid}'.");

        return (true, dialogue, null);
    }

    public bool Unregister(string dialogueGuid)
    {
        if (string.IsNullOrWhiteSpace(dialogueGuid))
            return false;

        return dialogues.Remove(dialogueGuid);
    }
}
