# Arquitetura do Modo História

using Godot;
using System.Collections.Generic;

public partial class StoryManager : Node
{
    public static StoryManager Instance { get; private set; }

    public int CurrentChapter { get; private set; } = 1;
    public bool CanAdvanceChapter { get; set; } = false;
    public HashSet<int> CompletedChapters { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public bool CanTriggerTransition(int requiredChapter)
    {
        return CurrentChapter == requiredChapter
               && CanAdvanceChapter
               && !CompletedChapters.Contains(requiredChapter);
    }

    public void AdvanceToChapter(int nextChapter)
    {
        CompletedChapters.Add(CurrentChapter);
        CurrentChapter = nextChapter;
        CanAdvanceChapter = false;
        GD.Print($"[StoryManager] Avançou para o Capítulo {CurrentChapter}");
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var completedArray = new Godot.Collections.Array<int>();
        foreach (var ch in CompletedChapters)
            completedArray.Add(ch);

        return new Godot.Collections.Dictionary
        {
            { "current_chapter", CurrentChapter },
            { "can_advance", CanAdvanceChapter },
            { "completed_chapters", completedArray }
        };
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        if (data.ContainsKey("current_chapter"))
            CurrentChapter = (int)data["current_chapter"];
        if (data.ContainsKey("can_advance"))
            CanAdvanceChapter = (bool)data["can_advance"];

        CompletedChapters.Clear();
        if (data.ContainsKey("completed_chapters"))
        {
            foreach (int ch in (Godot.Collections.Array)data["completed_chapters"])
                CompletedChapters.Add(ch);
        }
    }
}
