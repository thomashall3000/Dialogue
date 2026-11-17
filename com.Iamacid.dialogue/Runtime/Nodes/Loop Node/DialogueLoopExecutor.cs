
using System;

public class DialogueLoopExecutor
{
    private readonly LoopNodeData node;
    private readonly DialogueActionRegistry actionRegistry;
    private readonly Func<ConditionExpression, bool> conditionEvaluator;

    private Action<string> onActionFailure;

    private bool started;
    private bool active;
    private bool insideBody;
    private bool startingAction;
    private bool actionResultReceived;
    private bool actionSucceeded;
    private string actionError;

    public int IterationsStarted { get; private set; }
    public bool IsActive => active;
    public bool IsInsideBody => insideBody;
    public bool ActionSucceeded => actionSucceeded;

    public DialogueLoopExecutor(LoopNodeData node, DialogueActionRegistry actionRegistry, Func<ConditionExpression, bool> conditionEvaluator)
    {
        this.node = node ?? throw new ArgumentNullException(nameof(node));
        this.actionRegistry = actionRegistry;
        this.conditionEvaluator = conditionEvaluator;
    }

    public (bool Success, string Error) Start(Action<string> onActionFailure = null)
    {
        if (started)
            return (false, "This loop executor has already started.");

        if (!Enum.IsDefined(typeof(LoopMode), node.Mode))
            return (false, "Unsupported loop mode.");

        if (node.Mode == LoopMode.FixedCount && node.IterationCount < 0)
            return (false, "Loop iteration count cannot be negative.");

        if ((node.Mode == LoopMode.WhileCondition || node.Mode == LoopMode.UntilCondition) && (node.Condition == null || conditionEvaluator == null))
            return (false, "The loop requires a condition and a condition evaluator.");

        started = true;
        active = true;
        this.onActionFailure = onActionFailure;

        if (node.Mode == LoopMode.UntilAction)
            return StartCompletionAction();

        return (true, null);
    }

    private (bool Success, string Error) StartCompletionAction()
    {
        if (node.CompletionAction == null || actionRegistry == null)
        {
            Cancel();
            return (false, "The loop requires a registered completion action.");
        }

        if (node.CompletionAction.CompletionMode != ActionCompletionMode.Explicit)
        {
            Cancel();
            return (false, "Loop completion actions must use explicit completion.");
        }

        if (!actionRegistry.TryGetHandler(node.CompletionAction.ActionId, out DialogueActionHandler handler))
        {
            Cancel();
            return (false, $"No action handler registered for '{node.CompletionAction.ActionId}'.");
        }

        try
        {
            startingAction = true;
            handler(node.CompletionAction.Parameters, HandleActionResult);
        }
        catch (Exception exception)
        {
            Cancel();
            return (false, $"Loop completion action threw an exception: {exception.Message}");
        }
        finally
        {
            startingAction = false;
        }

        if (actionError != null)
        {
            string error = actionError;
            Cancel();
            return (false, error);
        }

        return (true, null);
    }

    private void HandleActionResult(DialogueActionResult result)
    {
        if (!active || actionResultReceived)
            return;

        actionResultReceived = true;

        if (result != null && result.Status == DialogueActionStatus.Succeeded)
        {
            actionSucceeded = true;
            return;
        }

        actionError = result?.Error;

        if (string.IsNullOrWhiteSpace(actionError))
            actionError = result?.Status == DialogueActionStatus.Cancelled ? "Loop completion action was cancelled." : "Loop completion action failed.";

        if (!startingAction)
            onActionFailure?.Invoke(actionError);
    }

    public (bool Success, bool EnterBody, string Error) BeginNextIteration()
    {
        if (!active)
            return (false, false, "The loop is not active.");

        if (insideBody)
            return (false, false, "The current iteration has not finished.");

        if (actionError != null)
            return (false, false, actionError);

        bool shouldContinue;

        try
        {
            shouldContinue = ShouldContinue();
        }
        catch (Exception exception)
        {
            return (false, false, $"Could not evaluate loop condition: {exception.Message}");
        }

        if (!shouldContinue)
        {
            Cancel();
            return (true, false, null);
        }

        IterationsStarted++;
        insideBody = true;

        return (true, true, null);
    }

    private bool ShouldContinue()
    {
        switch (node.Mode)
        {
            case LoopMode.FixedCount:
                return ShouldContinueFixedCount();

            case LoopMode.WhileCondition:
                return ShouldContinueWhileCondition();

            case LoopMode.UntilCondition:
                return ShouldContinueUntilCondition();

            case LoopMode.UntilAction:
                return ShouldContinueUntilAction();

            default:
                throw new InvalidOperationException("Unsupported loop mode.");
        }
    }

    private bool ShouldContinueFixedCount()
    {
        return IterationsStarted < node.IterationCount;
    }

    private bool ShouldContinueWhileCondition()
    {
        return conditionEvaluator(node.Condition);
    }

    private bool ShouldContinueUntilCondition()
    {
        return !conditionEvaluator(node.Condition);
    }

    private bool ShouldContinueUntilAction()
    {
        return !actionSucceeded;
    }

    public (bool Success, string Error) CompleteIteration()
    {
        if (!active || !insideBody)
            return (false, "There is no active loop iteration to complete.");

        insideBody = false;

        return (true, null);
    }

    public void Cancel()
    {
        active = false;
        insideBody = false;
        onActionFailure = null;
    }
}
