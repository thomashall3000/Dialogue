using System;
using System.Collections.Generic;

public sealed class DialogueActionExecutor
{
    private readonly DialogueActionRegistry actionRegistry;
    private readonly DialogueVariableRegistry variableRegistry;

    private IReadOnlyList<DialogueActionReference> actions;
    private DialogueActionHandler[] handlers;
    private ActionExecutionMode executionMode;

    private Action<DialogueActionResult> onFinished;

    private bool[] started;
    private bool[] completed;

    private int completedCount;
    private int nextIndex;

    private bool hasStarted;
    private bool isRunning;
    private bool processingSequential;
    private bool subscribedToVariables;

    public bool IsRunning => isRunning;

    public DialogueActionExecutor(DialogueActionRegistry actionRegistry, DialogueVariableRegistry variableRegistry = null)
    {
        this.actionRegistry = actionRegistry ??
            throw new ArgumentNullException(nameof(actionRegistry));

        this.variableRegistry = variableRegistry;
    }

    public (bool Success, string Error) Start(ActionNodeData node, Action<DialogueActionResult> onFinished)
    {
        if (hasStarted)
            return (false, "This executor has already been used.");

        if (node == null)
            return (false, "Action node is null.");

        if (onFinished == null)
            return (false, "Completion callback is null.");

        if (node.Actions == null || node.Actions.Count == 0)
            return (false, "Action node contains no actions.");

        if (!Enum.IsDefined(typeof(ActionExecutionMode), node.ExecutionMode))
        {
            return (false, "Unsupported action execution mode.");
        }

        actions = new List<DialogueActionReference>(node.Actions);

        handlers = new DialogueActionHandler[actions.Count];

        bool requiresVariables = false;

        for (int i = 0; i < actions.Count; i++)
        {
            DialogueActionReference action = actions[i];

            if (action == null)
                return (false, $"Action at index {i} is null.");

            if (!actionRegistry.TryGetHandler(action.ActionId, out handlers[i]))
            {
                return (false, $"Action '{action.ActionId}' is not registered.");
            }

            if (!Enum.IsDefined(typeof(ActionCompletionMode), action.CompletionMode))
            {
                return (false, $"Action '{action.ActionId}' has an invalid completion mode.");
            }

            if (action.CompletionMode == ActionCompletionMode.Condition)
            {
                requiresVariables = true;

                var validation = ConditionExpressionValidator.Validate(action.CompletionCondition);

                if (!validation.Success)
                {
                    return (false, $"Action '{action.ActionId}': {validation.Error}");
                }
            }
        }

        if (requiresVariables && variableRegistry == null)
        {
            return (false, "Condition-based actions require a variable registry.");
        }

        started = new bool[actions.Count];
        completed = new bool[actions.Count];

        executionMode = node.ExecutionMode;
        this.onFinished = onFinished;

        hasStarted = true;
        isRunning = true;

        if (requiresVariables)
        {
            variableRegistry.VariableChanged += OnVariableChanged;
            subscribedToVariables = true;
        }

        if (executionMode == ActionExecutionMode.Sequential)
        {
            ProcessSequential();
        }
        else
        {
            for (int i = 0; i < actions.Count && isRunning; i++)
            {
                StartAction(i);
            }
        }

        return (true, null);
    }

    private void ProcessSequential()
    {
        if (!isRunning || processingSequential)
            return;

        processingSequential = true;

        try
        {
            while (isRunning && nextIndex < actions.Count)
            {
                int index = nextIndex++;

                StartAction(index);

                if (!completed[index])
                    break;
            }
        }
        finally
        {
            processingSequential = false;
        }
    }

    private void StartAction(int index)
    {
        if (!isRunning)
            return;

        started[index] = true;

        DialogueActionReference action = actions[index];

        try
        {
            handlers[index](action.Parameters, result => HandleActionResult(index, result));
        }
        catch (Exception exception)
        {
            Finish(DialogueActionResult.Failed($"Action '{action.ActionId}' threw an exception: " + exception.Message));

            return;
        }

        if (isRunning && !completed[index] && action.CompletionMode == ActionCompletionMode.Condition)
        {
            CheckCondition(index);
        }
    }

    private void HandleActionResult(int index, DialogueActionResult result)
    {
        if (!isRunning || completed[index])
            return;

        if (result == null)
        {
            Finish(DialogueActionResult.Failed( $"Action '{actions[index].ActionId}' returned a null result."));

            return;
        }

        switch (result.Status)
        {
            case DialogueActionStatus.Succeeded:
                if (actions[index].CompletionMode == ActionCompletionMode.Explicit)
                {
                    MarkCompleted(index);
                }
                else
                {
                    CheckCondition(index);
                }
                break;

            case DialogueActionStatus.Failed:

            case DialogueActionStatus.Cancelled:
                Finish(result);
                break;

            default:
                Finish(DialogueActionResult.Failed("Action returned an unsupported result status."));
                break;
        }
    }

    private void OnVariableChanged(string variableId)
    {
        if (!isRunning)
            return;

        for (int i = 0; i < actions.Count && isRunning; i++)
        {
            if (started[i] && !completed[i] && actions[i].CompletionMode == ActionCompletionMode.Condition)
            {
                CheckCondition(i);
            }
        }
    }

    private void CheckCondition(int index)
    {
        if (!isRunning || completed[index])
            return;

        DialogueActionReference action = actions[index];

        bool success = ConditionExpressionEvaluator.TryEvaluate(action.CompletionCondition, variableRegistry, out bool conditionMet, out string error);

        if (!success)
        {
            Finish(DialogueActionResult.Failed($"Could not evaluate completion condition " + $"for action '{action.ActionId}': {error}"));

            return;
        }

        if (conditionMet)
            MarkCompleted(index);
    }

    private void MarkCompleted(int index)
    {
        if (!isRunning || completed[index])
            return;

        completed[index] = true;
        completedCount++;

        if (completedCount == actions.Count)
        {
            Finish(DialogueActionResult.Succeeded());
            return;
        }

        if (executionMode == ActionExecutionMode.Sequential)
            ProcessSequential();
    }

    private void Finish(DialogueActionResult result)
    {
        if (!isRunning)
            return;

        isRunning = false;

        Cleanup();

        Action<DialogueActionResult> callback = onFinished;
        onFinished = null;

        callback?.Invoke(result);
    }

    public void Cancel()
    {
        if (!isRunning)
            return;

        isRunning = false;
        Cleanup();
        onFinished = null;
    }

    private void Cleanup()
    {
        if (subscribedToVariables)
        {
            variableRegistry.VariableChanged -= OnVariableChanged;
            subscribedToVariables = false;
        }
    }
}