
using System;
using System.Collections.Generic;

public class DialogueRunner
{
    private const int MaxAutomaticTransitions = 128;
    private int automaticTransitions = 0;

    private const int MaxReferenceDepth = 16;
    private readonly DialogueActionRegistry actionRegistry;
    private readonly DialogueResolver resolver;
    private readonly Stack<DialogueExecutionContext> executionContexts = new Stack<DialogueExecutionContext>();

    private DialogueExecutionContext CurrentContext => executionContexts.Count > 0 ? executionContexts.Peek() : null;
    private DialogueData currentDialogue => CurrentContext?.Dialogue;
    private Stack<DialogueLoopFrame> activeLoops => CurrentContext?.ActiveLoops;

    private string currentNodeId
    {
        get => CurrentContext?.CurrentNodeId;
        set { if (CurrentContext != null) CurrentContext.CurrentNodeId = value; }
    }

    private string pendingResponseId
    {
        get => CurrentContext?.PendingResponseId;
        set { if (CurrentContext != null) CurrentContext.PendingResponseId = value; }
    }

    private DialogueActionExecutor activeExecutor
    {
        get => CurrentContext?.ActiveActionExecutor;
        set { if (CurrentContext != null) CurrentContext.ActiveActionExecutor = value; }
    }

    private AmbientDialogueSelector ambientSelector
    {
        get => CurrentContext?.AmbientSelector;
        set { if (CurrentContext != null) CurrentContext.AmbientSelector = value; }
    }

    public event Action DialogueStateChanged;
    public event Action<string> DialogueExecutionFailed;

    public DialogueState CurrentState { get; private set; } = DialogueState.Inactive;
    public string CurrentText { get; private set; }
    public string CurrentSpeakerId { get; private set; }
    public IReadOnlyList<ChoiceResponseData> CurrentChoices { get; private set; }
    public string LastError { get; private set; }

    public DialogueActionRegistry ActionRegistry => actionRegistry;
    public DialogueResolver Resolver => resolver;
    public int ReferenceDepth => executionContexts.Count;

    public DialogueRunner(DialogueActionRegistry actionRegistry = null, DialogueResolver resolver = null)
    {
        this.actionRegistry = actionRegistry ?? new DialogueActionRegistry();
        this.resolver = resolver ?? new DialogueResolver();
    }

    public (bool Success, string Error) StartDialogue(DialogueData dialogue, string rootDialogueGuid = null)
    {
        if (dialogue == null)
            return (false, "Cannot start dialogue: the dialogue is null.");

        if (CurrentContext != null)
            return (false, "A dialogue is already running.");

        LastError = null;
        var preparation = PrepareDialogue(dialogue);

        if (!preparation.Success)
            return (false, preparation.Error);

        var entryResult = FindEntryNodeId(dialogue);

        if (!entryResult.Success)
            return (false, "Cannot start dialogue: no Entry node was found.");

        executionContexts.Push(new DialogueExecutionContext(dialogue, rootDialogueGuid));
        automaticTransitions = 0;

        var startResult = EnterNode(entryResult.EntryNodeId);

        if (!startResult.Success)
        {
            LastError = startResult.Error;

            if (CurrentContext != null)
                StopDialogue();
        }

        return startResult;
    }

    private (bool Success, string Error) PrepareDialogue(DialogueData dialogue)
    {
        var preparationResult = dialogue.Prepare();

        if (!preparationResult.Success)
        {
            return (false, $"Cannot start dialogue: {preparationResult.Error}");
        }

        DialogueGraphValidator validator = new DialogueGraphValidator();
        var validationResult = validator.Validate(dialogue);

        if (!validationResult.Success)
        {
            return (false, $"Cannot start dialogue: {validationResult.Error}");
        }

        bool hasConditions = false;
        bool needsVariables = false;

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node is ConditionNodeData)
            {
                hasConditions = true;
                needsVariables = true;
            }

            if (node is ActionNodeData actionNode && actionNode.Actions != null)
            {
                foreach (DialogueActionReference action in actionNode.Actions)
                {
                    if (action != null && action.CompletionMode == ActionCompletionMode.Condition)
                    {
                        needsVariables = true;
                    }
                }
            }

            if (node is LoopNodeData loop && (loop.Mode == LoopMode.WhileCondition || loop.Mode == LoopMode.UntilCondition))
            {
                needsVariables = true;
            }
        }

        if (needsVariables)
        {
            if (!DialogueVariableRuntime.TryInitialize(out string initializationError))
            {
                return (false, $"Cannot initialize dialogue variables: {initializationError}");
            }
        }

        if (hasConditions)
        {
            var variableValidation = validator.Validate(dialogue, DialogueVariableRuntime.Database);

            if (!variableValidation.Success)
            {
                return (false, $"Cannot start dialogue: {variableValidation.Error}");
            }
        }

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node is ActionNodeData actionNode && actionNode.Actions != null)
            {
                foreach (DialogueActionReference action in actionNode.Actions)
                {
                    if (action == null || action.CompletionMode != ActionCompletionMode.Condition)
                    {
                        continue;
                    }

                    var result = ConditionVariableValidator.Validate(action.CompletionCondition, DialogueVariableRuntime.Database);

                    if (!result.Success)
                    {
                        return (false, $"Action '{action.ActionId}': {result.Error}");
                    }
                }
            }

            if (node is LoopNodeData loop && (loop.Mode == LoopMode.WhileCondition || loop.Mode == LoopMode.UntilCondition))
            {
                var result = ConditionVariableValidator.Validate(loop.Condition, DialogueVariableRuntime.Database);

                if (!result.Success)
                {
                    return (false, $"Loop '{loop.NodeId}': {result.Error}");
                }
            }
        }

        return (true, null);
    }

    private (bool Success, string EntryNodeId) FindEntryNodeId(DialogueData dialogue)
    {
        if (dialogue == null || dialogue.Nodes == null)
        {
            return (false, null);
        }

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node != null && node.NodeType == DialogueNodeType.Entry)
            {
                return (true, node.NodeId);
            }
        }

        return (false, null);
    }

    private (bool Success, string TargetNodeId) FindNextNodeId(DialogueData dialogue, string sourceNodeId, string sourcePortId)
    {
        if (dialogue == null || dialogue.Connections == null)
        {
            return (false, null);
        }

        foreach (DialogueConnectionData connection in dialogue.Connections)
        {
            if (connection != null && connection.SourceNodeId == sourceNodeId && connection.SourcePortId == sourcePortId)
            {
                return (true, connection.TargetNodeId);
            }
        }

        return (false, null);
    }

    private (bool Success, string Error) EnterNode(string nodeId)
    {
        if (!currentDialogue.TryGetNode(nodeId, out DialogueNodeData node))
        {
            return (false, $"Could not find dialogue node '{nodeId}'.");
        }

        var scopeResult = ValidateLoopScope(nodeId);

        if (!scopeResult.Success)
        {
            return scopeResult;
        }

        automaticTransitions++;

        if (automaticTransitions > MaxAutomaticTransitions)
        {
            return (false, $"Dialogue exceeded {MaxAutomaticTransitions} consecutive node transitions without pausing. Possible infinite automatic loop near node '{nodeId}'.");
        }

        currentNodeId = nodeId;

        switch (node.NodeType)
        {
            case DialogueNodeType.Entry:
                return MoveThroughPort("next");

            case DialogueNodeType.CharacterLine:
                return HandleCharacterLineNode((CharacterLineNodeData)node);

            case DialogueNodeType.Choice:
                return HandleChoiceNode((ChoiceNodeData)node);

            case DialogueNodeType.Condition:
                return HandleConditionNode((ConditionNodeData)node);

            case DialogueNodeType.Action:
                return HandleActionNode((ActionNodeData)node);

            case DialogueNodeType.AmbientDialogue:
                return HandleAmbientDialogueNode((AmbientDialogueNodeData)node);

            case DialogueNodeType.Loop:
                return HandleLoopNode((LoopNodeData)node);

            case DialogueNodeType.DialogueReference:
                return HandleDialogueReferenceNode((DialogueReferenceNodeData)node);

            case DialogueNodeType.End:
                return HandleEndNode();

            default:
                return (false, $"Unsupported node type: {node.NodeType}.");
        }
    }

    private (bool Success, string Error) MoveThroughPort(string portId)
    {
        var result = FindNextNodeId(currentDialogue, currentNodeId, portId);

        if (!result.Success)
        {
            return (false, $"No connection found from node '{currentNodeId}' through port '{portId}'.");
        }

        return EnterNode(result.TargetNodeId);
    }

    private (bool Success, string Error) HandleDialogueReferenceNode(DialogueReferenceNodeData node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.DialogueGuid))
            return (false, "DialogueReference node requires a dialogue GUID.");

        if (executionContexts.Count >= MaxReferenceDepth)
            return (false, $"Dialogue reference depth exceeded {MaxReferenceDepth}.");

        var resolution = resolver.TryResolve(node.DialogueGuid);

        if (!resolution.Success)
            return (false, resolution.Error);

        foreach (DialogueExecutionContext context in executionContexts)
        {
            if (string.Equals(context.DialogueGuid, node.DialogueGuid, StringComparison.OrdinalIgnoreCase) || ReferenceEquals(context.Dialogue, resolution.Dialogue))
                return (false, $"Circular dialogue reference detected at '{node.DialogueGuid}'.");
        }

        var preparation = PrepareDialogue(resolution.Dialogue);

        if (!preparation.Success)
            return (false, $"Referenced dialogue '{node.DialogueGuid}': {preparation.Error}");

        var entry = FindEntryNodeId(resolution.Dialogue);

        if (!entry.Success)
            return (false, $"Referenced dialogue '{node.DialogueGuid}' has no Entry node.");

        DialogueExecutionContext child = new DialogueExecutionContext(resolution.Dialogue, node.DialogueGuid, node.NodeId);
        executionContexts.Push(child);
        var result = EnterNode(entry.EntryNodeId);

        if (!result.Success && executionContexts.Contains(child))
        {
            while (executionContexts.Count > 0 && CurrentContext != child)
                executionContexts.Pop().Cancel();

            executionContexts.Pop().Cancel();
        }

        return result;
    }

    private (bool Success, string Error) HandleEndNode()
    {
        if (executionContexts.Count <= 1)
            return StopDialogue();

        DialogueExecutionContext child = executionContexts.Pop();
        string returnNodeId = child.ReturnNodeId;
        child.Cancel();

        if (CurrentContext == null || currentNodeId != returnNodeId)
            return (false, $"Cannot return from referenced dialogue: reference node '{returnNodeId}' is not active.");

        return MoveThroughPort("next");
    }

    public (bool Success, string Error) Advance()
    {
        if (currentDialogue == null)
        {
            return (false, "No dialogue is currently running.");
        }

        if (CurrentState == DialogueState.AmbientDialogue)
        {
            return (false, "Ambient dialogue must be advanced through CompleteAmbientLine().");
        }

        if (CurrentState == DialogueState.ExecutingActions)
        {
            return (false, "Cannot advance while actions are executing.");
        }

        if (CurrentState == DialogueState.SelectedResponse)
        {
            if (string.IsNullOrWhiteSpace(pendingResponseId))
            {
                return (false, "There is no selected response to advance from.");
            }

            string responseId = pendingResponseId;
            pendingResponseId = null;

            return MoveThroughPort(responseId);
        }

        if (!currentDialogue.TryGetNode(currentNodeId, out DialogueNodeData node))
        {
            return (false, "The current dialogue node could not be found.");
        }

        if (node.NodeType != DialogueNodeType.CharacterLine)
        {
            return (false, "The current node cannot be advanced this way.");
        }

        return MoveThroughPort("next");
    }

    public (bool Success, string Error) SelectResponse(string responseId)
    {
        if (currentDialogue == null || CurrentState != DialogueState.Choice)
        {
            return (false, "The dialogue is not currently waiting for a response.");
        }

        if (CurrentChoices == null)
        {
            return (false, "The current Choice node has no available responses.");
        }

        if (!currentDialogue.TryGetNode(currentNodeId, out DialogueNodeData node) || node is not ChoiceNodeData choice)
        {
            return (false, "The current Choice node could not be found.");
        }

        foreach (ChoiceResponseData response in CurrentChoices)
        {
            if (response == null || response.ResponseId != responseId)
            {
                continue;
            }

            SelectedResponsePresentation presentation;

            switch (choice.SettingsOverride)
            {
                case ResponsePresentationOverride.UseDialogueDefault:
                    presentation = currentDialogue.Settings.DefaultResponsePresentation;
                    break;

                case ResponsePresentationOverride.Immediate:
                    presentation = SelectedResponsePresentation.Immediate;
                    break;

                case ResponsePresentationOverride.DisplayAsLine:
                    presentation = SelectedResponsePresentation.DisplayAsLine;
                    break;

                default:
                    return (false, "The Choice node has an unsupported presentation setting.");
            }

            switch (presentation)
            {
                case SelectedResponsePresentation.Immediate:
                    return MoveThroughPort(responseId);

                case SelectedResponsePresentation.DisplayAsLine:
                    pendingResponseId = responseId;
                    CurrentState = DialogueState.SelectedResponse;
                    CurrentSpeakerId = choice.CharacterId;
                    CurrentText = response.Text;
                    CurrentChoices = null;
                    DialogueStateChanged?.Invoke();
                    return (true, null);

                default:
                    return (false, "The dialogue has an unsupported response presentation setting.");
            }
        }

        return (false, $"Response '{responseId}' is not available for the current Choice node.");
    }

    private (bool Success, string Error) HandleCharacterLineNode(CharacterLineNodeData line)
    {
        automaticTransitions = 0;
        CurrentState = DialogueState.CharacterLine;
        CurrentSpeakerId = line.CharacterId;
        CurrentText = line.Text;
        CurrentChoices = null;
        pendingResponseId = null;

        DialogueStateChanged?.Invoke();

        return (true, null);
    }

    private (bool Success, string Error) HandleChoiceNode(ChoiceNodeData choice)
    {
        automaticTransitions = 0;
        CurrentState = DialogueState.Choice;
        CurrentChoices = choice.Responses;
        pendingResponseId = null;

        DialogueStateChanged?.Invoke();

        return (true, null);
    }

    private (bool Success, string Error) HandleConditionNode(ConditionNodeData condition)
    {
        if (condition == null)
        {
            return (false, "Condition node is null.");
        }

        if (!DialogueVariableRuntime.TryInitialize(out string initializationError))
        {
            return (false, $"Could not initialize dialogue variables: {initializationError}");
        }

        if (!ConditionExpressionEvaluator.TryEvaluate(condition.RootExpression, DialogueVariableRuntime.Registry, out bool result, out string evaluationError))
        {
            return (false, $"Could not evaluate condition: {evaluationError}");
        }

        return MoveThroughPort(result ? "true" : "false");
    }

    private (bool Success, string Error) HandleActionNode(ActionNodeData node)
    {
        if (node == null)
        {
            return (false, "Action node is null.");
        }

        DialogueVariableRegistry variables = DialogueVariableRuntime.IsInitialized ? DialogueVariableRuntime.Registry : null;
        DialogueActionExecutor executor = new DialogueActionExecutor(actionRegistry, variables);
        DialogueExecutionContext owner = CurrentContext;

        activeExecutor = executor;
        CurrentState = DialogueState.ExecutingActions;
        CurrentText = null;
        CurrentSpeakerId = null;
        CurrentChoices = null;
        pendingResponseId = null;

        DialogueStateChanged?.Invoke();

        bool isStarting = true;
        DialogueActionResult immediateResult = null;

        var startResult = executor.Start(node, result =>
        {
            if (!executionContexts.Contains(owner) || owner.ActiveActionExecutor != executor)
            {
                return;
            }

            if (isStarting)
            {
                immediateResult = result;
            }
            else
            {
                OnActionExecutorFinished(owner, executor, result);
            }
        });

        isStarting = false;

        if (!startResult.Success)
        {
            activeExecutor = null;
            StopDialogue();
            return (false, $"Could not start ActionNode: {startResult.Error}");
        }

        if (immediateResult != null)
        {
            var completion = CompleteActionNode(executor, immediateResult);

            if (!completion.Success && currentDialogue != null)
            {
                StopDialogue();
            }

            return completion;
        }

        automaticTransitions = 0;

        return (true, null);
    }

    private (bool Success, string Error) ValidateLoopScope(string nodeId)
    {
        if (activeLoops.Count > 0 && activeLoops.Peek().Node.NodeId == nodeId)
        {
            return (true, null);
        }

        string owningLoopId = null;

        foreach (DialogueNodeData dialogueNode in currentDialogue.Nodes)
        {
            if (dialogueNode is not LoopNodeData loop || loop.ChildNodeIds == null)
            {
                continue;
            }

            foreach (string childId in loop.ChildNodeIds)
            {
                if (childId != nodeId)
                {
                    continue;
                }

                if (owningLoopId != null)
                {
                    return (false, $"Node '{nodeId}' belongs to multiple loop containers.");
                }

                owningLoopId = loop.NodeId;
            }
        }

        string expectedOwnerId = activeLoops.Count > 0 ? activeLoops.Peek().Node.NodeId : null;

        if (owningLoopId != expectedOwnerId)
        {
            return (false, $"Node '{nodeId}' cannot be entered from the current loop scope.");
        }

        return (true, null);
    }

    private (bool Success, string Error) HandleLoopNode(LoopNodeData node)
    {
        if (node == null)
        {
            return (false, "Loop node is null.");
        }

        if (activeLoops.Count > 0 && activeLoops.Peek().Node.NodeId == node.NodeId)
        {
            DialogueLoopFrame existingLoop = activeLoops.Peek();
            var completion = existingLoop.Executor.CompleteIteration();

            if (!completion.Success)
            {
                return (false, $"Could not complete loop '{node.NodeId}': {completion.Error}");
            }

            return ContinueLoop(existingLoop);
        }

        if (node.ChildNodeIds == null || string.IsNullOrWhiteSpace(node.EntryNodeId) || node.EntryNodeId == node.NodeId || !ContainsLoopChild(node, node.EntryNodeId))
        {
            return (false, $"Loop '{node.NodeId}' requires an entry node among its direct children.");
        }

        DialogueExecutionContext owner = CurrentContext;
        DialogueLoopExecutor executor = new DialogueLoopExecutor(node, actionRegistry, EvaluateLoopCondition);
        DialogueLoopFrame context = new DialogueLoopFrame(node, executor);
        activeLoops.Push(context);

        var start = executor.Start(error => OnLoopActionFailed(owner, context, error));

        if (!start.Success)
        {
            activeLoops.Pop();
            executor.Cancel();
            return (false, $"Could not start loop '{node.NodeId}': {start.Error}");
        }

        return ContinueLoop(context);
    }

    private bool ContainsLoopChild(LoopNodeData loop, string childId)
    {
        foreach (string candidate in loop.ChildNodeIds)
        {
            if (candidate == childId)
            {
                return true;
            }
        }

        return false;
    }

    private bool EvaluateLoopCondition(ConditionExpression expression)
    {
        if (!DialogueVariableRuntime.IsInitialized)
        {
            throw new InvalidOperationException("Dialogue variables have not been initialized.");
        }

        if (!ConditionExpressionEvaluator.TryEvaluate(expression, DialogueVariableRuntime.Registry, out bool result, out string error))
        {
            throw new InvalidOperationException(error);
        }

        return result;
    }

    private (bool Success, string Error) ContinueLoop(DialogueLoopFrame context)
    {
        if (activeLoops.Count == 0 || activeLoops.Peek() != context)
        {
            return (false, "The active loop stack is inconsistent.");
        }

        var iteration = context.Executor.BeginNextIteration();

        if (!iteration.Success)
        {
            return (false, $"Could not evaluate loop '{context.Node.NodeId}': {iteration.Error}");
        }

        if (iteration.EnterBody)
        {
            return EnterNode(context.Node.EntryNodeId);
        }

        activeLoops.Pop();
        return MoveThroughPort("next");
    }

    private void OnLoopActionFailed(DialogueExecutionContext owner, DialogueLoopFrame frame, string error)
    {
        if (!executionContexts.Contains(owner) || !owner.ActiveLoops.Contains(frame))
            return;

        FailExecution($"Loop '{frame.Node.NodeId}' completion action failed: {error}");
    }

    private void FailExecution(string error)
    {
        LastError = error;
        StopDialogue();
        DialogueExecutionFailed?.Invoke(error);
    }

    private (bool Success, string Error) HandleAmbientDialogueNode(AmbientDialogueNodeData node)
    {
        if (node == null)
        {
            return (false, "Ambient dialogue node is null.");
        }

        try
        {
            ambientSelector = new AmbientDialogueSelector(node);
        }
        catch (ArgumentException exception)
        {
            return (false, $"Could not initialise ambient dialogue: {exception.Message}");
        }

        return PresentNextAmbientLine();
    }

    private (bool Success, string Error) PresentNextAmbientLine()
    {
        if (ambientSelector == null)
        {
            return (false, "No ambient dialogue selector is active.");
        }

        if (!ambientSelector.TrySelectNextLine(out AmbientDialogueLineData line, out string error))
        {
            return (false, $"Could not select ambient line: {error}");
        }

        automaticTransitions = 0;
        CurrentState = DialogueState.AmbientDialogue;
        CurrentText = line.Text;
        CurrentSpeakerId = line.CharacterId;
        CurrentChoices = null;
        pendingResponseId = null;

        DialogueStateChanged?.Invoke();

        return (true, null);

    }

    public (bool Success, string Error) CompleteAmbientLine()
    {
        if (CurrentState != DialogueState.AmbientDialogue || ambientSelector == null)
        {
            return (false, "No ambient dialogue line is currently active.");
        }

        var completionResult = ambientSelector.CompleteCurrentLine();

        if (!completionResult.Success)
        {
            return completionResult;
        }

        if (ambientSelector.ShouldExit())
        {
            ambientSelector = null;
            return MoveThroughPort("next");
        }

        return PresentNextAmbientLine();
    }

    public (bool Success, string Error) RequestStopAmbientDialogue()
    {
        if (CurrentState != DialogueState.AmbientDialogue || ambientSelector == null)
        {
            return (false, "No ambient dialogue is currently active.");
        }

        if (!currentDialogue.TryGetNode(currentNodeId, out DialogueNodeData node) || node is not AmbientDialogueNodeData ambientNode)
        {
            return (false, "Could not find the current ambient dialogue node.");
        }

        if (ambientNode.ExitMode != AmbientExitMode.UntilStopped)
        {
            return (false, "The current ambient dialogue is not configured for manual stopping.");
        }

        ambientSelector.RequestStop();

        return (true, null);
    }

    private void OnActionExecutorFinished(DialogueExecutionContext owner, DialogueActionExecutor executor, DialogueActionResult result)
    {
        if (!executionContexts.Contains(owner) || owner.ActiveActionExecutor != executor)
            return;

        if (CurrentContext != owner)
        {
            FailExecution("An action completed in a suspended dialogue context.");
            return;
        }

        var completion = CompleteActionNode(executor, result);

        if (!completion.Success)
            FailExecution(completion.Error);
    }

    private (bool Success, string Error) CompleteActionNode(DialogueActionExecutor executor, DialogueActionResult result)
    {
        if (activeExecutor != executor || currentDialogue == null)
        {
            return (false, "Action execution is no longer active.");
        }

        activeExecutor = null;

        if (result == null)
        {
            return (false, "Action execution returned a null result.");
        }

        if (result.Status != DialogueActionStatus.Succeeded)
        {
            return (false, $"Action execution {result.Status}: {result.Error ?? "No details provided."}");
        }

        return MoveThroughPort("next");
    }

    public (bool Success, string Error) StopDialogue()
    {
        while (executionContexts.Count > 0)
            executionContexts.Pop().Cancel();

        automaticTransitions = 0;
        CurrentState = DialogueState.Inactive;
        CurrentSpeakerId = null;
        CurrentText = null;
        CurrentChoices = null;
        DialogueStateChanged?.Invoke();

        return (true, null);
    }
}
