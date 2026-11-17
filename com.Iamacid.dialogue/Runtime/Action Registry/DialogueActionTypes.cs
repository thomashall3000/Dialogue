using System;
using Newtonsoft.Json.Linq;

public enum ActionExecutionMode
{
    Sequential,
    Parallel
}

public enum ActionCompletionMode
{
    Explicit,
    Condition
}

public enum DialogueActionStatus
{
    Succeeded,
    Failed,
    Cancelled
}

public sealed class DialogueActionResult
{
    public DialogueActionStatus Status { get; }
    public string Error { get; }

    private DialogueActionResult(DialogueActionStatus status, string error = null)
    {
        Status = status;
        Error = error;
    }

    public static DialogueActionResult Succeeded()
    {
        return new DialogueActionResult(DialogueActionStatus.Succeeded);
    }

    public static DialogueActionResult Failed(string error)
    {
        return new DialogueActionResult(DialogueActionStatus.Failed, error);
    }

    public static DialogueActionResult Cancelled(string reason = null)
    {
        return new DialogueActionResult(DialogueActionStatus.Cancelled, reason);
    }
}

public delegate void DialogueActionHandler(JToken parameters, Action<DialogueActionResult> onComplete);
