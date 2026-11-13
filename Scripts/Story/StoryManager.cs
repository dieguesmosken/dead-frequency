// Scripts/Story/StoryManager.cs
// Autoload do Modo História (offline). Conta o progresso pelos JobEvents e libera o avanço de capítulo.
// Não troca de cena por conta própria: quem faz a viagem é a ChapterTransitionZone.
// Ver docs/tech/01_ARQUITETURA_MODO_HISTORIA.md. A cidade livre e o online continuam iguais: nada aqui
// roda se Active for false (o jogador entra pelo botão "Modo História" do menu).
using System;
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class StoryManager : Node
{
    public static StoryManager Instance { get; private set; }

    /// <summary>Modo História ligado nesta partida (menu "Modo História"). Turno livre e online: false.</summary>
    public bool Active { get; private set; }
    public int CurrentChapter { get; private set; } = 1;
    public bool CanAdvance { get; private set; }
    public int BrokenThisChapter { get; private set; }
    public IReadOnlyCollection<int> CompletedChapters => _completed;
    public StoryChapters.Chapter Chapter => StoryChapters.Get(CurrentChapter);

    /// <summary>O capítulo atual cumpriu a meta (a UI avisa: "siga pela estrada").</summary>
    public event Action<int> ChapterReady;
    /// <summary>Entrou num capítulo novo.</summary>
    public event Action<int> ChapterChanged;
    /// <summary>O contador da meta mudou (HUD).</summary>
    public event Action ProgressChanged;

    private readonly HashSet<int> _completed = new();
    private readonly Dictionary<string, int> _progress = new();
    private Node _lastScene;

    public override void _Ready()
    {
        Instance = this;
        JobEvents.TaskDone += OnTaskDone;
        JobEvents.CargoBroken += OnCargoBroken;
        FromSave(SaveManager.Load()?.Story);
    }

    public override void _ExitTree()
    {
        JobEvents.TaskDone -= OnTaskDone;
        JobEvents.CargoBroken -= OnCargoBroken;
        if (Instance == this)
            Instance = null;
    }

    private static bool Offline => NetClient.Instance == null || !NetClient.Instance.IsOnline;

    /// <summary>Liga o Modo História para a próxima partida e devolve a cena da fase do capítulo salvo.</summary>
    public string Begin()
    {
        Active = true;
        return GameLevels.Get(Chapter.Level).Scene;
    }

    /// <summary>Volta ao jogo livre (menu principal, online).</summary>
    public void End() => Active = false;

    /// <summary>Começa a campanha do zero (o save do resto do jogo continua).</summary>
    public void Restart()
    {
        CurrentChapter = 1;
        CanAdvance = false;
        BrokenThisChapter = 0;
        _completed.Clear();
        _progress.Clear();
        SaveNow();
    }

    /// <summary>Tarefas que já contam para a meta do capítulo atual.</summary>
    public int GoalProgress
    {
        get
        {
            StoryChapters.Chapter c = Chapter;
            if (c.Job != null)
                return _progress.GetValueOrDefault(c.Job);
            int sum = 0;
            foreach (int v in _progress.Values)
                sum += v;
            return sum;
        }
    }

    public override void _Process(double delta)
    {
        // Ao entrar numa fase com o Modo História ligado, monta o que a história precisa nela (zona da estrada, aviso).
        Node scene = GetTree().CurrentScene;
        if (scene == _lastScene)
            return;
        _lastScene = scene;
        if (!Active || !Offline || scene == null || scene.GetNodeOrNull("StoryDirector") != null)
            return;
        if (scene.FindChild("City", true, false) is CityGenerator)
            scene.AddChild(new StoryDirector { Name = "StoryDirector" });
    }

    private void OnTaskDone(string job, string what, int payment)
    {
        if (!Active || !Offline || CanAdvance)
            return;
        // Só conta se a tarefa foi feita na fase do capítulo.
        if (Professions.CurrentLevel != Chapter.Level)
            return;
        _progress[job] = _progress.GetValueOrDefault(job) + 1;
        ProgressChanged?.Invoke();
        CheckGoal();
    }

    private void OnCargoBroken(string cargoName)
    {
        if (!Active || !Offline)
            return;
        BrokenThisChapter++;
        ProgressChanged?.Invoke();
    }

    private void CheckGoal()
    {
        StoryChapters.Chapter c = Chapter;
        if (c.Goal <= 0 || GoalProgress < c.Goal)
            return;
        if (c.MaxBroken >= 0 && BrokenThisChapter > c.MaxBroken)
            return;
        CanAdvance = true;
        SaveNow();
        ChapterReady?.Invoke(CurrentChapter);
    }

    /// <summary>A ChapterTransitionZone pergunta antes de começar a viagem.</summary>
    public bool CanTriggerTransition(int requiredChapter) =>
        Active && Offline && CurrentChapter == requiredChapter && CanAdvance && !_completed.Contains(requiredChapter);

    /// <summary>Chamado pela ChapterTransitionZone logo antes de trocar de cena.</summary>
    public void AdvanceToChapter(int nextChapter)
    {
        _completed.Add(CurrentChapter);
        CurrentChapter = StoryChapters.Exists(nextChapter) ? nextChapter : CurrentChapter;
        CanAdvance = false;
        BrokenThisChapter = 0;
        _progress.Clear();
        GD.Print($"[StoryManager] Capítulo {CurrentChapter}");
        SaveNow();
        ChapterChanged?.Invoke(CurrentChapter);
    }

    /// <summary>Teste/depuração: simula uma tarefa paga (passa pelo mesmo caminho dos JobEvents).</summary>
    public void DebugTask(string job, string what, int payment) => JobEvents.RaiseTaskDone(job, what, payment);

    /// <summary>Grava só o progresso da história no save (o resto é gravado pela cama, como sempre).</summary>
    private void SaveNow() => SaveManager.SaveStory(ToSave());

    // ── Save: vai em SaveData.Story (user://save.tzn, System.Text.Json) ──
    public StorySave ToSave() => new()
    {
        Chapter = CurrentChapter,
        CanAdvance = CanAdvance,
        Broken = BrokenThisChapter,
        Completed = new List<int>(_completed),
        Progress = new Dictionary<string, int>(_progress),
    };

    public void FromSave(StorySave s)
    {
        if (s == null)
            return;
        CurrentChapter = StoryChapters.Exists(s.Chapter) ? s.Chapter : 1;
        CanAdvance = s.CanAdvance;
        BrokenThisChapter = s.Broken;
        _completed.Clear();
        _completed.UnionWith(s.Completed ?? new List<int>());
        _progress.Clear();
        foreach (var kv in s.Progress ?? new Dictionary<string, int>())
            _progress[kv.Key] = kv.Value;
    }
}

/// <summary>Progresso do Modo História dentro do SaveData (user://save.tzn).</summary>
public class StorySave
{
    public int Chapter { get; set; } = 1;
    public bool CanAdvance { get; set; }
    public int Broken { get; set; }
    public List<int> Completed { get; set; } = new();
    public Dictionary<string, int> Progress { get; set; } = new();
}
