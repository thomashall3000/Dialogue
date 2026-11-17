using System;
using System.Collections.Generic;

public class DialogueGraphValidator
{
    public (bool Success, string Error) Validate(DialogueData dialogue)
    {
        if (dialogue == null)
            return (false, "Dialogue cannot be null.");

        if (dialogue.Nodes == null || dialogue.Connections == null)
            return (false, "Dialogue nodes or connections are null.");

        string entryNodeId = null;
        HashSet<(string NodeId, string PortId)> usedOutputs = new HashSet<(string NodeId, string PortId)>();

        var connectionsResult = ValidateConnections(dialogue, usedOutputs);

        if (!connectionsResult.Success)
            return connectionsResult;

        var nodesResult = ValidateNodes(dialogue, usedOutputs, out entryNodeId);

        if (!nodesResult.Success)
            return nodesResult;

        var loopStructureResult = ValidateLoopStructure(dialogue, out Dictionary<string, string> ownerByNode);

        if (!loopStructureResult.Success)
            return loopStructureResult;

        var loopConnectionResult = ValidateLoopConnectionScopes(dialogue, ownerByNode);

        if (!loopConnectionResult.Success)
            return loopConnectionResult;

        var loopReturnResult = ValidateLoopReturnPaths(dialogue, ownerByNode);

        if (!loopReturnResult.Success)
            return loopReturnResult;

        var reachabilityResult = ValidateReachability(dialogue, entryNodeId);

        if (!reachabilityResult.Success)
            return reachabilityResult;

        return (true, null);
    }

    private (bool Success, string Error) ValidateConnections(DialogueData dialogue, HashSet<(string NodeId, string PortId)> usedOutputs)
    {
        foreach (DialogueConnectionData connection in dialogue.Connections)
        {
            if (connection == null)
                return (false, "The dialogue contains a null connection.");

            if (!dialogue.TryGetNode(connection.SourceNodeId, out DialogueNodeData sourceNode))
                return (false, $"Connection source node not found: {connection.SourceNodeId}");

            if (!dialogue.TryGetNode(connection.TargetNodeId, out DialogueNodeData targetNode))
                return (false, $"Connection target node not found: {connection.TargetNodeId}");

            if (sourceNode.NodeType == DialogueNodeType.End)
                return (false, "An End node cannot have an outgoing connection.");

            if (sourceNode.NodeType == DialogueNodeType.Entry || sourceNode.NodeType == DialogueNodeType.CharacterLine || sourceNode.NodeType == DialogueNodeType.Action || sourceNode.NodeType == DialogueNodeType.AmbientDialogue || sourceNode.NodeType == DialogueNodeType.Loop || sourceNode.NodeType == DialogueNodeType.DialogueReference)
            {
                if (connection.SourcePortId != "next")
                    return (false, $"Invalid output port '{connection.SourcePortId}' on node {connection.SourceNodeId}. Expected 'next'.");
            }

            if (sourceNode.NodeType == DialogueNodeType.Choice)
            {
                ChoiceNodeData choiceNode = (ChoiceNodeData)sourceNode;

                if (choiceNode.Responses == null)
                    return (false, $"Choice node {choiceNode.NodeId} has a null responses list.");

                bool matchingResponseFound = false;

                foreach (ChoiceResponseData response in choiceNode.Responses)
                {
                    if (response == null)
                        return (false, $"Choice node {choiceNode.NodeId} contains a null response.");

                    if (response.ResponseId == connection.SourcePortId)
                    {
                        matchingResponseFound = true;
                        break;
                    }
                }

                if (!matchingResponseFound)
                    return (false, $"Choice node {choiceNode.NodeId} has no response with ID '{connection.SourcePortId}'.");
            }

            if (sourceNode.NodeType == DialogueNodeType.Condition)
            {
                if (connection.SourcePortId != "true" && connection.SourcePortId != "false")
                    return (false, $"Condition node {sourceNode.NodeId} has an invalid output port '{connection.SourcePortId}'. Expected 'true' or 'false'.");
            }

            if (!usedOutputs.Add((connection.SourceNodeId, connection.SourcePortId)))
                return (false, $"Node {connection.SourceNodeId} has more than one connection from output '{connection.SourcePortId}'.");
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateNodes(DialogueData dialogue, HashSet<(string NodeId, string PortId)> usedOutputs, out string entryNodeId)
    {
        entryNodeId = null;
        int entryCount = 0;

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node == null)
                return (false, "The dialogue contains a null node.");

            if (node.NodeType == DialogueNodeType.Entry || node.NodeType == DialogueNodeType.CharacterLine)
            {
                if (node.NodeType == DialogueNodeType.Entry)
                {
                    entryCount++;
                    entryNodeId = node.NodeId;
                }

                if (!usedOutputs.Contains((node.NodeId, "next")))
                    return (false, $"Node {node.NodeId} is missing its required 'next' connection.");
            }

            if (node.NodeType == DialogueNodeType.Choice)
            {
                ChoiceNodeData choiceNode = (ChoiceNodeData)node;

                if (choiceNode.Responses == null || choiceNode.Responses.Count == 0)
                    return (false, $"Choice node {node.NodeId} has no responses.");

                var result = ValidateResponseIds(choiceNode);

                if (!result.Success)
                    return result;

                foreach (ChoiceResponseData response in choiceNode.Responses)
                {
                    if (!usedOutputs.Contains((node.NodeId, response.ResponseId)))
                        return (false, $"Choice node {node.NodeId} has no connection for response '{response.ResponseId}'.");
                }
            }

            if (node.NodeType == DialogueNodeType.Condition)
            {
                if (!(node is ConditionNodeData condition))
                    return (false, $"Condition node {node.NodeId} has an invalid data type.");

                var expressionResult = ConditionExpressionValidator.Validate(condition.RootExpression);

                if (!expressionResult.Success)
                    return (false, $"Condition node {node.NodeId}: {expressionResult.Error}");

                if (!usedOutputs.Contains((node.NodeId, "true")))
                    return (false, $"Condition node {node.NodeId} is missing its required 'true' connection.");

                if (!usedOutputs.Contains((node.NodeId, "false")))
                    return (false, $"Condition node {node.NodeId} is missing its required 'false' connection.");
            }

            if (node.NodeType == DialogueNodeType.Action)
            {
                if (!(node is ActionNodeData actionNode))
                    return (false, $"Action node {node.NodeId} has an invalid data type.");

                var actionResult = ValidateActionNode(actionNode);

                if (!actionResult.Success)
                    return actionResult;

                if (!usedOutputs.Contains((node.NodeId, "next")))
                    return (false, $"Action node {node.NodeId} is missing its required 'next' connection.");
            }

            if (node.NodeType == DialogueNodeType.Loop)
            {
                if (node is not LoopNodeData loopNode)
                    return (false, $"Loop node {node.NodeId} has an invalid data type.");

                var loopResult = ValidateLoopNode(loopNode);

                if (!loopResult.Success)
                    return loopResult;

                if (!usedOutputs.Contains((node.NodeId, "next")))
                    return (false, $"Loop node {node.NodeId} is missing its required 'next' connection.");
            }

            if (node.NodeType == DialogueNodeType.DialogueReference)
            {
                if (node is not DialogueReferenceNodeData reference || string.IsNullOrWhiteSpace(reference.DialogueGuid))
                    return (false, $"Dialogue reference node {node.NodeId} requires a dialogue GUID.");

                if (!usedOutputs.Contains((node.NodeId, "next")))
                    return (false, $"Dialogue reference node {node.NodeId} is missing its required 'next' connection.");
            }

            if (node.NodeType == DialogueNodeType.AmbientDialogue)
            {
                if (node is not AmbientDialogueNodeData ambientNode)
                {
                    return (false, $"Ambient dialogue node {node.NodeId} has an invalid data type.");
                }

                if (ambientNode.Lines == null || ambientNode.Lines.Count == 0)
                {
                    return (false, $"Ambient dialogue node {node.NodeId} contains no lines.");
                }

                if (!Enum.IsDefined(typeof(AmbientSelectionMode), ambientNode.SelectionMode))
                {
                    return (false, $"Ambient dialogue node {node.NodeId} has an invalid selection mode.");
                }

                if (!Enum.IsDefined(typeof(AmbientExitMode), ambientNode.ExitMode))
                {
                    return (false, $"Ambient dialogue node {node.NodeId} has an invalid exit mode.");
                }

                HashSet<string> lineIds = new HashSet<string>();

                foreach (AmbientDialogueLineData line in ambientNode.Lines)
                {
                    if (line == null)
                    {
                        return (false, $"Ambient dialogue node {node.NodeId} contains a null line.");
                    }

                    if (string.IsNullOrWhiteSpace(line.LineId))
                    {
                        return (false, $"Ambient dialogue node {node.NodeId} contains a line without an ID.");
                    }

                    if (!lineIds.Add(line.LineId))
                    {
                        return (false, $"Ambient dialogue node {node.NodeId} contains duplicate line ID '{line.LineId}'.");
                    }

                    if (string.IsNullOrWhiteSpace(line.Text))
                    {
                        return (false, $"Ambient dialogue node {node.NodeId} contains an empty dialogue line.");
                    }
                }

                if (!usedOutputs.Contains((node.NodeId, "next")))
                {
                    return (false, $"Ambient dialogue node {node.NodeId} is missing its required 'next' connection.");
                }
            }
        }

        if (entryCount != 1)
            return (false, $"A dialogue must contain exactly one Entry node. Found: {entryCount}.");

        return (true, null);
    }

    private (bool Success, string Error) ValidateActionNode(ActionNodeData node)
    {
        if (node.Actions == null || node.Actions.Count == 0)
            return (false, $"Action node {node.NodeId} contains no actions.");

        if (!Enum.IsDefined(typeof(ActionExecutionMode), node.ExecutionMode))
            return (false, $"Action node {node.NodeId} has an unsupported execution mode.");

        for (int i = 0; i < node.Actions.Count; i++)
        {
            DialogueActionReference action = node.Actions[i];

            if (action == null)
                return (false, $"Action node {node.NodeId} contains a null action at index {i}.");

            if (string.IsNullOrWhiteSpace(action.ActionId))
                return (false, $"Action node {node.NodeId} contains an action with an invalid ActionId.");

            if (!Enum.IsDefined(typeof(ActionCompletionMode), action.CompletionMode))
                return (false, $"Action '{action.ActionId}' has an unsupported completion mode.");

            if (action.CompletionMode == ActionCompletionMode.Condition)
            {
                var result = ConditionExpressionValidator.Validate(action.CompletionCondition);

                if (!result.Success)
                    return (false, $"Action '{action.ActionId}' has an invalid completion condition: {result.Error}");
            }
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateLoopNode(LoopNodeData loop)
    {
        if (!Enum.IsDefined(typeof(LoopMode), loop.Mode))
            return (false, $"Loop '{loop.NodeId}' has an unsupported mode.");

        if (loop.ChildNodeIds == null || loop.ChildNodeIds.Count == 0)
            return (false, $"Loop '{loop.NodeId}' must contain at least one node.");

        if (string.IsNullOrWhiteSpace(loop.EntryNodeId))
            return (false, $"Loop '{loop.NodeId}' has no entry node ID.");

        switch (loop.Mode)
        {
            case LoopMode.FixedCount:
                if (loop.IterationCount < 0)
                    return (false, $"Loop '{loop.NodeId}' cannot have a negative iteration count.");
                break;

            case LoopMode.WhileCondition:
            case LoopMode.UntilCondition:
                var conditionResult = ConditionExpressionValidator.Validate(loop.Condition);

                if (!conditionResult.Success)
                    return (false, $"Loop '{loop.NodeId}' has an invalid condition: {conditionResult.Error}");
                break;

            case LoopMode.UntilAction:
                if (loop.CompletionAction == null || string.IsNullOrWhiteSpace(loop.CompletionAction.ActionId))
                    return (false, $"Loop '{loop.NodeId}' requires a completion action with an ActionId.");

                if (loop.CompletionAction.CompletionMode != ActionCompletionMode.Explicit)
                    return (false, $"Loop '{loop.NodeId}' requires an explicit-completion action.");
                break;
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateLoopStructure(DialogueData dialogue, out Dictionary<string, string> ownerByNode)
    {
        ownerByNode = new Dictionary<string, string>();
        HashSet<string> nodeIds = new HashSet<string>();

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
                return (false, "A dialogue node has a missing or blank ID.");

            if (!nodeIds.Add(node.NodeId))
                return (false, $"The dialogue contains duplicate node ID '{node.NodeId}'.");
        }

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node is not LoopNodeData loop)
                continue;

            HashSet<string> localChildren = new HashSet<string>();

            foreach (string childId in loop.ChildNodeIds)
            {
                if (string.IsNullOrWhiteSpace(childId))
                    return (false, $"Loop '{loop.NodeId}' contains a blank child ID.");

                if (!localChildren.Add(childId))
                    return (false, $"Loop '{loop.NodeId}' contains duplicate child ID '{childId}'.");

                if (childId == loop.NodeId)
                    return (false, $"Loop '{loop.NodeId}' cannot contain itself.");

                if (!dialogue.TryGetNode(childId, out DialogueNodeData child))
                    return (false, $"Loop '{loop.NodeId}' references missing child node '{childId}'.");

                if (child.NodeType == DialogueNodeType.Entry)
                    return (false, $"Loop '{loop.NodeId}' cannot contain the dialogue Entry node.");

                if (child.NodeType == DialogueNodeType.End)
                    return (false, $"Loop '{loop.NodeId}' cannot contain End node '{childId}'. Move End outside the container.");

                if (ownerByNode.TryGetValue(childId, out string previousOwner))
                    return (false, $"Node '{childId}' belongs to multiple loop containers: '{previousOwner}' and '{loop.NodeId}'.");

                ownerByNode.Add(childId, loop.NodeId);
            }

            if (!localChildren.Contains(loop.EntryNodeId))
                return (false, $"Loop '{loop.NodeId}' entry node '{loop.EntryNodeId}' must be a direct child.");
        }

        foreach (string childId in ownerByNode.Keys)
        {
            HashSet<string> visited = new HashSet<string>();
            string cursor = childId;

            while (ownerByNode.TryGetValue(cursor, out string parentId))
            {
                if (!visited.Add(cursor))
                    return (false, $"Cyclic loop containment detected around node '{cursor}'.");

                cursor = parentId;
            }
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateLoopConnectionScopes(DialogueData dialogue, Dictionary<string, string> ownerByNode)
    {
        foreach (DialogueConnectionData connection in dialogue.Connections)
        {
            ownerByNode.TryGetValue(connection.SourceNodeId, out string sourceOwner);
            ownerByNode.TryGetValue(connection.TargetNodeId, out string targetOwner);

            if (sourceOwner == targetOwner)
                continue;

            if (connection.TargetNodeId == sourceOwner && dialogue.TryGetNode(connection.TargetNodeId, out DialogueNodeData targetNode) && targetNode is LoopNodeData)
                continue;

            return (false, $"Invalid loop boundary crossing from '{connection.SourceNodeId}' to '{connection.TargetNodeId}'. Connections must stay in their scope or return directly to the owning loop.");
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateLoopReturnPaths(DialogueData dialogue, Dictionary<string, string> ownerByNode)
    {
        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node is not LoopNodeData loop)
                continue;

            HashSet<string> canFinish = new HashSet<string>();
            bool hasReturn = false;

            foreach (string childId in loop.ChildNodeIds)
            {
                if (dialogue.TryGetNode(childId, out DialogueNodeData child) && child.NodeType == DialogueNodeType.End)
                    canFinish.Add(childId);
            }

            foreach (DialogueConnectionData connection in dialogue.Connections)
            {
                if (connection.TargetNodeId == loop.NodeId && ownerByNode.TryGetValue(connection.SourceNodeId, out string owner) && owner == loop.NodeId)
                {
                    hasReturn = true;
                    canFinish.Add(connection.SourceNodeId);
                }
            }

            if (!hasReturn)
                return (false, $"Loop '{loop.NodeId}' has no connection from its body back to the loop container.");

            bool changed;

            do
            {
                changed = false;

                foreach (DialogueConnectionData connection in dialogue.Connections)
                {
                    if (!ownerByNode.TryGetValue(connection.SourceNodeId, out string owner) || owner != loop.NodeId)
                        continue;

                    if (canFinish.Contains(connection.TargetNodeId) && canFinish.Add(connection.SourceNodeId))
                        changed = true;
                }
            }
            while (changed);

            foreach (string childId in loop.ChildNodeIds)
            {
                if (!canFinish.Contains(childId))
                    return (false, $"Loop '{loop.NodeId}' child '{childId}' has no path back to its loop or to an End node.");
            }
        }

        return (true, null);
    }

    private (bool Success, string Error) ValidateReachability(DialogueData dialogue, string entryNodeId)
    {
        HashSet<string> visitedNodeIds = new HashSet<string>();
        Explore(dialogue, entryNodeId, visitedNodeIds);

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (!visitedNodeIds.Contains(node.NodeId))
                return (false, $"Node {node.NodeId} cannot be reached from the Entry node.");
        }

        return (true, null);
    }

    private void Explore(DialogueData dialogue, string currentNodeId, HashSet<string> visitedNodeIds)
    {
        if (visitedNodeIds.Contains(currentNodeId))
            return;

        visitedNodeIds.Add(currentNodeId);

        if (dialogue.TryGetNode(currentNodeId, out DialogueNodeData node) && node is LoopNodeData loop)
            Explore(dialogue, loop.EntryNodeId, visitedNodeIds);

        foreach (DialogueConnectionData connection in dialogue.Connections)
        {
            if (connection.SourceNodeId == currentNodeId)
                Explore(dialogue, connection.TargetNodeId, visitedNodeIds);
        }
    }

    private (bool Success, string Error) ValidateResponseIds(ChoiceNodeData choiceNode)
    {
        HashSet<string> checkedResponseIds = new HashSet<string>();

        foreach (ChoiceResponseData response in choiceNode.Responses)
        {
            if (response == null)
                return (false, $"Choice node {choiceNode.NodeId} contains a null response.");

            if (string.IsNullOrWhiteSpace(response.ResponseId))
                return (false, $"Choice node {choiceNode.NodeId} contains a response with a missing or blank ID.");

            if (!checkedResponseIds.Add(response.ResponseId))
                return (false, $"Choice node {choiceNode.NodeId} contains duplicate response ID '{response.ResponseId}'.");
        }

        return (true, null);
    }

    public (bool Success, string Error) Validate(DialogueData dialogue, DialogueVariableDatabase variableDatabase)
    {
        var graphResult = Validate(dialogue);

        if (!graphResult.Success)
            return graphResult;

        if (variableDatabase == null)
            return (false, "Variable database is required.");

        foreach (DialogueNodeData node in dialogue.Nodes)
        {
            if (node is ConditionNodeData condition)
            {
                var result = ConditionVariableValidator.Validate(condition.RootExpression, variableDatabase);

                if (!result.Success)
                    return (false, $"Condition node '{condition.NodeId}': {result.Error}");
            }

            if (node is ActionNodeData actionNode)
            {
                foreach (DialogueActionReference action in actionNode.Actions)
                {
                    if (action.CompletionMode != ActionCompletionMode.Condition)
                        continue;

                    var result = ConditionVariableValidator.Validate(action.CompletionCondition, variableDatabase);

                    if (!result.Success)
                        return (false, $"Action '{action.ActionId}' in node '{actionNode.NodeId}': {result.Error}");
                }
            }

            if (node is LoopNodeData loop && (loop.Mode == LoopMode.WhileCondition || loop.Mode == LoopMode.UntilCondition))
            {
                var result = ConditionVariableValidator.Validate(loop.Condition, variableDatabase);

                if (!result.Success)
                    return (false, $"Loop '{loop.NodeId}': {result.Error}");
            }
        }

        return (true, null);
    }
}
