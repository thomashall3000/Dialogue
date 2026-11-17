# Dialogue examples

I've included two example JSON files to show how you can use the Dialogue runtime without needing the visual editor. They're deliberately quite simple so you can open them in a text editor and see how the nodes and connections work.

## What's included

**`01_Lighthouse_Choices.json`** is a short conversation with a lighthouse keeper. You get a choice to help or decline, and the dialogue follows a different path depending on what you pick. This is probably the best one to start with.

**`02_Beacon_FixedCount_Loop.json`** is a small example of a Loop. It runs the same calibration line three times before moving on to the final line. I included this one because the way Loop bodies connect back to their parent node isn't immediately obvious from JSON alone.

Neither example needs variables, custom Actions or references to other dialogue assets. The character IDs are just identifiers; your UI decides what names to show for them.

## Trying them in Unity

1. Copy the JSON files into your Unity project's `Assets` folder, or import them through Package Manager if you've added the sample entry to `package.json`.
2. Create a script that loads a JSON `TextAsset` and starts a `DialogueRunner`. There's a basic example below.
3. Attach the script to a GameObject and assign one of the JSON files in the Inspector. Make sure you're assigning the **JSON**, not a C# script.
4. Enter Play Mode. Click **Continue** to go through lines, or choose one of the response buttons when a Choice appears.

### Basic example player

This uses Unity's built-in `OnGUI` just to make it easy to try the examples. It's not meant to be the UI you'd use in a finished game.

Create `Assets/SampleDialoguePlayer.cs` and add:

```csharp
using System;
using UnityEngine;

public sealed class SampleDialoguePlayer : MonoBehaviour
{
    [SerializeField] private TextAsset dialogueJson;
    [SerializeField, Range(0.75f, 1.5f)] private float uiScale = 1f;

    private DialogueRunner runner;
    private string error;
    private Vector2 choiceScroll;

    private GUIStyle panelStyle;
    private GUIStyle headingStyle;
    private GUIStyle speakerStyle;
    private GUIStyle textStyle;
    private GUIStyle buttonStyle;
    private GUIStyle errorStyle;

    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;

    private void Start()
    {
        StartDialogue();
    }

    private void StartDialogue()
    {
        error = null;
        runner = null;
        choiceScroll = Vector2.zero;

        if (dialogueJson == null)
        {
            error = "Assign a dialogue JSON TextAsset in the Inspector.";
            return;
        }

        try
        {
            DialogueData data = new DialogueSerializer().Deserialize(dialogueJson.text);
            runner = new DialogueRunner();

            var result = runner.StartDialogue(data);
            Check(result);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError("Dialogue sample error: " + exception);
        }
    }

    private void OnGUI()
    {
        CreateStylesIfNeeded();

        float scale = Mathf.Clamp(Screen.height / 720f, 0.85f, 1.6f) * uiScale;
        float viewWidth = Screen.width / scale;
        float viewHeight = Screen.height / scale;

        const float margin = 18f;
        float panelWidth = Mathf.Min(1060f, viewWidth - margin * 2f);
        float panelHeight = Mathf.Min(360f, viewHeight - margin * 2f);

        if (panelWidth <= 0f || panelHeight <= 0f)
            return;

        Rect panel = new Rect(
            (viewWidth - panelWidth) * 0.5f,
            viewHeight - panelHeight - margin,
            panelWidth,
            panelHeight);

        Matrix4x4 previousMatrix = GUI.matrix;

        try
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.Box(panel, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 16f,
                panel.width - 48f, panel.height - 32f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("DIALOGUE SAMPLE", headingStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Restart", buttonStyle,
                GUILayout.Width(140f), GUILayout.Height(44f)))
            {
                StartDialogue();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            if (!string.IsNullOrEmpty(error))
            {
                GUILayout.Label("Dialogue error: " + error, errorStyle);
            }
            else if (runner != null)
            {
                DrawDialogueState();
            }

            GUILayout.EndArea();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawDialogueState()
    {
        switch (runner.CurrentState)
        {
            case DialogueState.CharacterLine:
                string speaker = runner.CurrentSpeakerId;
                if (string.IsNullOrWhiteSpace(speaker))
                    speaker = "Speaker";

                GUILayout.Label(speaker.Replace('-', ' ').Replace('_', ' '), speakerStyle);
                GUILayout.Space(6f);
                GUILayout.Label(runner.CurrentText ?? string.Empty, textStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Continue", buttonStyle, GUILayout.Height(58f)))
                    Check(runner.Advance());
                break;

            case DialogueState.Choice:
                GUILayout.Label("Choose a response", speakerStyle);
                GUILayout.Space(8f);

                choiceScroll = GUILayout.BeginScrollView(choiceScroll,
                    GUILayout.ExpandHeight(true));

                if (runner.CurrentChoices != null)
                {
                    foreach (ChoiceResponseData response in runner.CurrentChoices)
                    {
                        if (GUILayout.Button(response.Text ?? string.Empty,
                            buttonStyle, GUILayout.MinHeight(58f)))
                        {
                            Check(runner.SelectResponse(response.ResponseId));
                            break;
                        }
                        GUILayout.Space(8f);
                    }
                }
                GUILayout.EndScrollView();
                break;

            case DialogueState.SelectedResponse:
                GUILayout.Label(runner.CurrentText ?? string.Empty, textStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Continue", buttonStyle, GUILayout.Height(58f)))
                    Check(runner.Advance());
                break;

            case DialogueState.Inactive:
                GUILayout.Label("Dialogue finished.", textStyle);
                GUILayout.Space(8f);
                GUILayout.Label("Press Restart to play it again.", speakerStyle);
                break;

            default:
                GUILayout.Label("Current state: " + runner.CurrentState, textStyle);
                GUILayout.Label("This sample player only handles lines and choices.", speakerStyle);
                break;
        }
    }

    private void Check((bool Success, string Error) result)
    {
        if (!result.Success)
        {
            error = result.Error;
            Debug.LogError("Dialogue sample error: " + error);
        }
    }

    private void CreateStylesIfNeeded()
    {
        if (panelStyle != null)
            return;

        panelTexture = MakeTexture(new Color(0.09f, 0.11f, 0.16f, 0.97f));
        buttonTexture = MakeTexture(new Color(0.19f, 0.29f, 0.43f, 1f));
        buttonHoverTexture = MakeTexture(new Color(0.27f, 0.40f, 0.58f, 1f));

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;

        headingStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headingStyle.normal.textColor = new Color(0.68f, 0.76f, 0.88f);

        speakerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        speakerStyle.normal.textColor = new Color(0.80f, 0.87f, 1f);

        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        textStyle.normal.textColor = Color.white;

        errorStyle = new GUIStyle(textStyle);
        errorStyle.normal.textColor = new Color(1f, 0.56f, 0.56f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fontStyle = FontStyle.Normal,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(14, 14, 10, 10),
            margin = new RectOffset(0, 0, 2, 2)
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = buttonHoverTexture;
        buttonStyle.active.background = buttonHoverTexture;
        buttonStyle.focused.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        buttonStyle.focused.textColor = Color.white;
    }

    private static Texture2D MakeTexture(Color colour)
    {
        var texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, colour);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (panelTexture != null) Destroy(panelTexture);
        if (buttonTexture != null) Destroy(buttonTexture);
        if (buttonHoverTexture != null) Destroy(buttonHoverTexture);
    }
}
```

Attach that component to a GameObject and assign `01_Lighthouse_Choices.json` to its **Dialogue Json** field. You can swap it for the Loop example afterwards.

## How the JSON is laid out

The main things to look for are `nodes` and `connections`. Every node has a `nodeId` and a `nodeType`, and each connection points from a node's output port to another node.

- **Entry** and **CharacterLine** normally use an output called `next`.
- **Choice** uses each response's `responseId` as its output, so the connection stays attached to the same response even if you change its text.
- **End** doesn't connect to anything else.
- **FixedCount Loops** store an iteration count, a body entry node and a list of nodes they contain. The last node in the body connects back to the Loop's input, and the Loop's `next` output is used when it finishes.

If you change the IDs in a file, make sure you update any connections pointing to them as well.

## If something doesn't work

If you get a JSON parsing error straight away, check that you've assigned a `.json` file rather than a `.cs` script. Unity can treat both as TextAssets, which makes that mistake fairly easy to make.

If the dialogue fails validation, check the node IDs and the output port names in `connections`. For the Loop example, also make sure the body node is listed in `childNodeIds` and that `entryNodeId` points to it.

If you see an ID instead of a character's name, that's expected. The runtime keeps track of character IDs, but it doesn't decide how your game displays them.

I haven't run these particular sample files in every Unity version, so it's worth checking that both work in your project before building anything larger around them.
