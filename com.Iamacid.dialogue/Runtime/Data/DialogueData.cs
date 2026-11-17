using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueData
{

    [SerializeField]
    private string dialogueId;

    [SerializeField]
    private List<DialogueConnectionData> connections = new List<DialogueConnectionData>();

    [SerializeField]
    private List<DialogueNodeData> nodes;

    public const int CurrentSchemaVersion = 1;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    private Dictionary<string, DialogueNodeData> nodesById;

    [SerializeField]
    [JsonProperty("dialogueSettings")]
    private DialogueSettings settings = new DialogueSettings();

    public DialogueSettings Settings => settings;

    public int SchemaVersion => schemaVersion;
    public string DialogueId => dialogueId;
    public IReadOnlyList<DialogueNodeData> Nodes => nodes;
    public IReadOnlyList<DialogueConnectionData> Connections => connections;


    public DialogueData(string dialogueId, int schemaVersion, List<DialogueNodeData> nodes, List<DialogueConnectionData> connections = null, DialogueSettings settings = null)
    {
        this.dialogueId = dialogueId;
        this.schemaVersion = schemaVersion;
        this.nodes = nodes;
        this.connections = connections ?? new List<DialogueConnectionData>();
        this.settings = settings ?? new DialogueSettings();
    }

    private (bool Success, string Error) BuildNodeLookup()
    {
        nodesById = null;

        if (nodes == null)
        {
            return (false, "List of nodes is invalid");
        }

        var lookup = new Dictionary<string, DialogueNodeData>();

        foreach (DialogueNodeData node in nodes)
        {
            if (node == null)
            {
                return (false, $"Node: {node} is not valid");
            }

            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                return (false, $"Node ID: {node.NodeId} is not valid");
            }

            if (lookup.ContainsKey(node.NodeId))
            {
                return (false, $"Node ID: {node.NodeId} appears twice");
            }

            lookup.Add(node.NodeId, node);

        }

        nodesById = lookup;

        return (true, "");
    }

    public bool TryGetNode(string nodeId, out DialogueNodeData node)
    {
        if (nodesById == null || string.IsNullOrWhiteSpace(nodeId))
        {
            node = null;
            return false;
        }

        return nodesById.TryGetValue(nodeId, out node);
    }

    public (bool Success, string Error) AddNode(DialogueNodeData node)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.NodeId))
            return (false, "Cannot add a node with a missing or invalid ID.");

        if (nodes == null)
            nodes = new List<DialogueNodeData>();

        if (nodesById == null)
        {
            var preparation = Prepare();

            if (!preparation.Success)
                return preparation;
        }

        if (nodesById.ContainsKey(node.NodeId))
            return (false, $"Node '{node.NodeId}' already exists.");

        nodes.Add(node);
        nodesById.Add(node.NodeId, node);

        return (true, null);
    }

    public (bool Success, string Error) AddConnection(DialogueConnectionData connection)
    {
        if (connection == null)
            return (false, "Cannot add a null connection.");

        if (nodesById == null)
        {
            var preparation = Prepare();

            if (!preparation.Success)
                return preparation;
        }

        if (!nodesById.ContainsKey(connection.SourceNodeId) || !nodesById.ContainsKey(connection.TargetNodeId))
            return (false, "Connection source or target node does not exist.");

        if (connections == null)
            connections = new List<DialogueConnectionData>();

        foreach (DialogueConnectionData existing in connections)
        {
            if (existing.SourceNodeId == connection.SourceNodeId && existing.SourcePortId == connection.SourcePortId)
                return (false, "This output already has a connection.");
        }

        connections.Add(connection);

        return (true, null);
    }

    public (bool Success, string Error) Prepare()
    {
        return BuildNodeLookup();
    }
}

public enum SelectedResponsePresentation
{
    Immediate,
    DisplayAsLine
}

[Serializable]
[JsonObject(MemberSerialization.OptIn)]
public class DialogueSettings
{
    [SerializeField]
    [JsonProperty("defaultResponsePresentation")]
    private SelectedResponsePresentation defaultResponsePresentation = SelectedResponsePresentation.Immediate;

    public SelectedResponsePresentation DefaultResponsePresentation => defaultResponsePresentation;
}
