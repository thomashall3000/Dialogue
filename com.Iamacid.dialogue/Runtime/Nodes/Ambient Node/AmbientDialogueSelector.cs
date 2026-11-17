using System;
using System.Collections.Generic;

public class AmbientDialogueSelector
{
    private readonly List<AmbientDialogueLineData> lines;
    private readonly AmbientSelectionMode selectionMode;
    private readonly AmbientExitMode exitMode;
    private readonly Random random = new Random();

    private readonly HashSet<int> completedIndices = new HashSet<int>();
    private readonly List<int> shuffledIndices = new List<int>();

    private int sequentialIndex = 0;
    private int shuffleIndex = 0;
    private int currentIndex = -1;
    private int completedLineCount = 0;

    private bool stopRequested = false;

    public bool IsWaitingForCompletion => currentIndex >= 0;

    public AmbientDialogueSelector(AmbientDialogueNodeData node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (node.Lines == null || node.Lines.Count == 0)
            throw new ArgumentException("Ambient dialogue must contain at least one line.");

        if (!Enum.IsDefined(typeof(AmbientSelectionMode), node.SelectionMode))
            throw new ArgumentException("Invalid ambient selection mode.");

        if (!Enum.IsDefined(typeof(AmbientExitMode), node.ExitMode))
            throw new ArgumentException("Invalid ambient exit mode.");

        lines = new List<AmbientDialogueLineData>(node.Lines);
        selectionMode = node.SelectionMode;
        exitMode = node.ExitMode;

        foreach (AmbientDialogueLineData line in lines)
        {
            if (line == null)
                throw new ArgumentException("Ambient dialogue contains a null line.");
        }
    }

    public bool TrySelectNextLine(out AmbientDialogueLineData line, out string error)
    {
        line = null;
        error = null;

        if (IsWaitingForCompletion)
        {
            error = "The current ambient line has not been completed.";
            return false;
        }

        if (ShouldExit())
        {
            error = "Ambient dialogue has finished.";
            return false;
        }

        switch (selectionMode)
        {
            case AmbientSelectionMode.Sequential:
                currentIndex = SelectSequential();
                break;

            case AmbientSelectionMode.Random:
                currentIndex = SelectRandom();
                break;

            case AmbientSelectionMode.Shuffle:
                currentIndex = SelectShuffled();
                break;

            default:
                error = "Unsupported ambient selection mode.";
                return false;
        }

        line = lines[currentIndex];
        return true;
    }

    private int SelectSequential()
    {
        int selectedIndex = sequentialIndex;
        sequentialIndex = (sequentialIndex + 1) % lines.Count;
        return selectedIndex;
    }

    private int SelectRandom()
    {
        if (exitMode != AmbientExitMode.AfterAll)
            return random.Next(lines.Count);

        List<int> availableIndices = new List<int>();

        for (int i = 0; i < lines.Count; i++)
        {
            if (!completedIndices.Contains(i))
                availableIndices.Add(i);
        }

        return availableIndices[random.Next(availableIndices.Count)];
    }

    private int SelectShuffled()
    {
        if (shuffleIndex >= shuffledIndices.Count)
            GenerateShuffle();

        int selectedIndex = shuffledIndices[shuffleIndex];
        shuffleIndex++;

        return selectedIndex;
    }

    private void GenerateShuffle()
    {
        shuffledIndices.Clear();

        for (int i = 0; i < lines.Count; i++)
            shuffledIndices.Add(i);

        for (int i = shuffledIndices.Count - 1; i > 0; i--)
        {
            int randomIndex = random.Next(i + 1);
            int temporary = shuffledIndices[i];
            shuffledIndices[i] = shuffledIndices[randomIndex];
            shuffledIndices[randomIndex] = temporary;
        }

        shuffleIndex = 0;
    }

    public (bool Success, string Error) CompleteCurrentLine()
    {
        if (!IsWaitingForCompletion)
            return (false, "There is no ambient line awaiting completion.");

        completedIndices.Add(currentIndex);
        completedLineCount++;
        currentIndex = -1;

        return (true, null);
    }

    public bool ShouldExit()
    {
        switch (exitMode)
        {
            case AmbientExitMode.AfterOne:
                return ShouldExitAfterOne();

            case AmbientExitMode.AfterAll:
                return ShouldExitAfterAll();

            case AmbientExitMode.UntilStopped:
                return ShouldExitWhenStopped();

            default:
                throw new InvalidOperationException("Unsupported ambient exit mode.");
        }
    }

    private bool ShouldExitAfterOne()
    {
        return completedLineCount >= 1;
    }

    private bool ShouldExitAfterAll()
    {
        return completedIndices.Count >= lines.Count;
    }

    private bool ShouldExitWhenStopped()
    {
        return stopRequested && !IsWaitingForCompletion;
    }

    public void RequestStop()
    {
        stopRequested = true;
    }
}
