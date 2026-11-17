using System;
using System.Collections.Generic;

public sealed class DialogueLoopFrame
{
    public LoopNodeData Node { get; }
    public DialogueLoopExecutor Executor { get; }

    public DialogueLoopFrame(LoopNodeData node, DialogueLoopExecutor executor)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }
}

public class DialogueExecutionContext
{
    public DialogueData Dialogue { get; }
    public string DialogueGuid { get; }
    public string ReturnNodeId { get; }

    public string CurrentNodeId { get; set; }
    public string PendingResponseId { get; set; }

    public DialogueActionExecutor ActiveActionExecutor { get; set; }
    public AmbientDialogueSelector AmbientSelector { get; set; }

    public Stack<DialogueLoopFrame> ActiveLoops { get; } = new Stack<DialogueLoopFrame>();

    public DialogueExecutionContext(DialogueData dialogue, string dialogueGuid = null, string returnNodeId = null)
    {
        Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
        DialogueGuid = dialogueGuid;
        ReturnNodeId = returnNodeId;
    }

    public void Cancel()
    {
        ActiveActionExecutor?.Cancel();
        ActiveActionExecutor = null;

        while (ActiveLoops.Count > 0)
        {
            DialogueLoopFrame frame = ActiveLoops.Pop();
            frame.Executor.Cancel();
        }

        AmbientSelector = null;
        PendingResponseId = null;
        CurrentNodeId = null;
    }
}
