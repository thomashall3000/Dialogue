using System;
using System.Collections.Generic;

public class DialogueActionRegistry
{
    private readonly Dictionary<string, DialogueActionHandler> handlers = new Dictionary<string, DialogueActionHandler>(StringComparer.Ordinal);

    public (bool Success, string Error) Register(string actionId, DialogueActionHandler handler)
    {
        if (string.IsNullOrWhiteSpace(actionId))
            return (false, "Action ID cannot be empty.");

        if (handler == null)
            return (false, "Action handler cannot be null.");

        if (handlers.ContainsKey(actionId))
        {
            return (false, $"Action '{actionId}' is already registered.");
        }

        handlers.Add(actionId, handler);

        return (true, null);
    }

    public bool TryGetHandler(string actionId, out DialogueActionHandler handler)
    {
        handler = null;

        if (string.IsNullOrWhiteSpace(actionId))
            return false;

        return handlers.TryGetValue(actionId, out handler);
    }
}