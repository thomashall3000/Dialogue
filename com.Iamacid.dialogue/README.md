# Iamacid Dialogue

This is a dialogue system I've been working on for Unity. The basic idea is that you can write a dialogue in JSON and have the runtime handle moving between lines, choices, conditions and other nodes. How you actually display the dialogue in-game is left up to you.

I'm also working on a visual editor for this as part of another package called **Iamacid Workspace**, but that isn't finished yet, so this version only includes the runtime. You can still use the whole dialogue system by writing the JSON files yourself.

## What it can do

- Dialogue between different characters, with player choices and branching paths
- Conditions that check values stored in a variable registry
- Actions that can call methods you've registered in your game, including sequential and parallel actions
- Loops, including conditional loops and loops inside other loops
- Ambient dialogue and references to other dialogue files
- Validation to catch some problems with the dialogue structure before it runs

It doesn't come with a dialogue UI. That's intentional, since I wanted the runtime to be usable with different types of games rather than making everyone use a particular UI.

Some of the features above need a bit of setup. For example, Conditions need a variable database, Actions need callbacks registered in code, and referenced dialogues need to be registered with the resolver. The two examples I've included avoid those extra requirements.

## Getting started

1. Add the package to your Unity project, making sure its dependencies are installed (including Newtonsoft JSON).
2. Import the examples from `Samples~/Dialogue Examples` into your project's `Assets` folder. You can also just create your own `.json` file.
3. Assign the JSON file to a `TextAsset` field in a MonoBehaviour.
4. Deserialize the file, create a `DialogueRunner`, and start the dialogue.

For example:

```csharp
DialogueData dialogue = new DialogueSerializer().Deserialize(dialogueJson.text);
DialogueRunner runner = new DialogueRunner();

var result = runner.StartDialogue(dialogue);
if (!result.Success)
    Debug.LogError(result.Error);

// Your UI can use these to display and progress the dialogue:
// runner.CurrentText
// runner.CurrentChoices
// runner.Advance()
// runner.SelectResponse(responseId)
```

You can find a slightly more complete example with a basic Unity GUI in the [samples README](Samples~/Dialogue%20Examples/README.md). It's nothing fancy, but it should make it easier to try the system out.

## Writing a dialogue

A dialogue has a list of nodes and connections between them. Each node has an ID, and connections use those IDs to tell the runner where to go next.

Here's a very small example:

```json
{
  "schemaVersion": 1,
  "dialogueId": "my-dialogue",
  "dialogueSettings": {},
  "nodes": [
    { "nodeType": "Entry", "nodeId": "entry" },
    { "nodeType": "CharacterLine", "nodeId": "line", "characterId": "npc", "text": "Hello!" },
    { "nodeType": "End", "nodeId": "end" }
  ],
  "connections": [
    { "sourceNodeId": "entry", "sourcePortId": "next", "targetNodeId": "line" },
    { "sourceNodeId": "line", "sourcePortId": "next", "targetNodeId": "end" }
  ]
}
```

I'd recommend looking at the included JSON examples first, especially if you want to see how choices and loops are connected.

## A couple of things to know

- **The visual editor isn't included yet.** I'm still developing that separately through Iamacid Workspace.
- **Variables need a runtime database.** `DialogueVariableRuntime` looks for a `VariableRuntimeDatabaseAsset` under `Resources/Dialogue/Generated/VariableRuntimeDatabase`. That asset needs to reference your variable definitions JSON.
- **Actions don't automatically do anything in your game.** You need to register the code that should run when an Action ID is reached.
- **Dialogue references need registering.** The runtime can't use Unity's editor-only `AssetDatabase` to look up GUIDs in a built game.

This is still an early version, so there are things I want to improve and expand. In particular, the visual editing tools are a separate work in progress. I've checked the example JSON files against the current format, but I'd still recommend testing them in your Unity version before relying on them in a project.
