// Scripts/Story/StoryDirector.cs
// Montado pelo StoryManager quando uma fase abre com o Modo História ligado (offline).
// Mostra o capítulo e a meta no alto da tela, avisa quando a estrada libera e, na cidade, cria a
// ChapterTransitionZone na saída leste (avenida que leva ao rancho). Não mexe em profissões nem no DeliveryManager.
using Godot;

namespace DeadFrequency;

public partial class StoryDirector : Node
{
    private CityGenerator _city;
    private Label _title;
    private Label _goal;
    private CanvasLayer _hud;
    private Node3D _exitMarker;

    public override void _Ready()
    {
        _city = GetParent().FindChild("City", true, false) as CityGenerator;
        StoryManager sm = StoryManager.Instance;
        if (sm == null)
            return;
        sm.ProgressChanged += Refresh;
        sm.ChapterReady += OnChapterReady;
        sm.GoalReset += OnGoalReset;
        BuildHud();
        Refresh();
        StoryChapters.Chapter c = sm.Chapter;
        ChapterBanner.Show(this, c.Roman, c.Title, 3.0f);
        // Falas de abertura, uma vez por capítulo (depois do banner).
        if (!sm.IntroSeen(c.Number) && Professions.CurrentLevel == c.Level)
        {
            sm.MarkIntroSeen(c.Number);
            GetTree().CreateTimer(3.4).Timeout += () => { if (IsInstanceValid(this)) StoryDialog.Play(this, c.Intro); };
        }
        if (Professions.CurrentLevel == "city" && _city != null)
            Callable.From(BuildCityExit).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (StoryManager.Instance is StoryManager sm)
        {
            sm.ProgressChanged -= Refresh;
            sm.ChapterReady -= OnChapterReady;
            sm.GoalReset -= OnGoalReset;
        }
    }

    private void BuildHud()
    {
        _hud = new CanvasLayer { Name = "StoryHud", Layer = 5 };
        AddChild(_hud);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        // Canto de cima à esquerda (o meio e a direita já têm o relógio, o turno e a lista de entregas).
        box.Position = new Vector2(16f, 12f);
        box.Size = new Vector2(300f, 0f);
        _hud.AddChild(box);
        _title = UiStyle.MakeLabel("", 16, HorizontalAlignment.Left, UiStyle.Phosphor);
        _goal = UiStyle.MakeLabel("", 14, HorizontalAlignment.Left, UiStyle.PhosphorDim);
        _goal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _title.CustomMinimumSize = new Vector2(300f, 0f);
        _goal.CustomMinimumSize = new Vector2(300f, 0f);
        foreach (Label l in new[] { _title, _goal })
        {
            l.AddThemeColorOverride("font_outline_color", Colors.Black);
            l.AddThemeConstantOverride("outline_size", 6);
            box.AddChild(l);
        }
    }

    private void Refresh()
    {
        StoryManager sm = StoryManager.Instance;
        if (sm == null || _title == null)
            return;
        StoryChapters.Chapter c = sm.Chapter;
        _title.Text = c.Roman + (string.IsNullOrEmpty(c.Title) ? "" : " — " + c.Title);
        string count = c.Goal > 0 ? $"  [{Mathf.Min(sm.GoalProgress, c.Goal)}/{c.Goal}]" : "";
        string goal = c.Hint + count;
        if (sm.CanAdvance)
        {
            StoryChapters.Chapter next = StoryChapters.Get(c.Number + 1);
            goal = !sm.NextChapterPlayable
                ? $"Capítulo concluído! {next.Roman} — {next.Title} está em construção. Siga jogando livre."
                : c.Level == "city" ? "Meta cumprida! Pegue a estrada do rancho: saída leste da cidade, de carro."
                : $"Meta cumprida! Siga para {next.Roman} — {next.Title}.";
        }
        if (c.MaxBroken >= 0 && !sm.CanAdvance && sm.BrokenThisChapter > 0)
            goal += $"\nCaixas quebradas: {sm.BrokenThisChapter}/{c.MaxBroken}";
        if (Professions.CurrentLevel != c.Level)
            goal = $"Este capítulo acontece em: {GameLevels.Get(c.Level).Name}.";
        _goal.Text = goal;
        if (_exitMarker != null)
            _exitMarker.Visible = sm.CanAdvance && sm.NextChapterPlayable;
    }

    private void OnChapterReady(int chapter)
    {
        Refresh();
        StoryDialog.Play(this, StoryChapters.Get(chapter).Outro);
        GameHud.Toast(this, StoryManager.Instance?.NextChapterPlayable == true
            ? "Meta do capítulo cumprida! A estrada do rancho está liberada (saída leste)."
            : "Capítulo concluído! O próximo ainda está em construção.", UiStyle.Phosphor, 6.0);
    }

    private void OnGoalReset(int chapter)
    {
        StoryDialog.Play(this, StoryChapters.Get(chapter).TooManyBroken);
        Refresh();
    }

    /// <summary>Saída leste: avenida em z = Period/2 até o muro do limite. A zona fica 70 m antes do muro.</summary>
    private void BuildCityExit()
    {
        float limit = (_city.Radius + 1) * _city.Period;
        float roadZ = _city.Period * 0.5f;
        Vector3 start = _city.GlobalTransform * new Vector3(limit - 75f, 0f, roadZ);
        var zone = new ChapterTransitionZone
        {
            Name = "ExitToRanch",
            RequiredChapter = 1,
            NextChapter = 2,
            ChapterNumberText = StoryChapters.Get(2).Roman,
            ChapterSubtitle = StoryChapters.Get(2).Title,
            TargetScenePath = GameLevels.Get("farm").Scene,
            TargetLevel = "farm",
            Position = start,
        };
        zone.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(6f, 4f, 16f) }, Position = new Vector3(0f, 2f, 0f) });
        GetParent().AddChild(zone);

        // Marcador da saída (só aparece com a meta cumprida): feixe verde e placa.
        _exitMarker = new Node3D { Name = "ExitMarker", Position = start };
        GetParent().AddChild(_exitMarker);
        var beamMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.3f, 1f, 0.45f, 0.35f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        _exitMarker.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.2f, BottomRadius = 1.2f, Height = 40f, RadialSegments = 12 }, Position = new Vector3(0f, 20f, 0f), MaterialOverride = beamMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _exitMarker.AddChild(new Label3D
        {
            Text = "ESTRADA DO RANCHO →",
            FontSize = 96,
            PixelSize = 0.02f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = new Color(0.55f, 1f, 0.62f),
            OutlineSize = 12,
            Position = new Vector3(0f, 6f, 0f),
        });
        _exitMarker.AddToGroup(MinimapView.MarkerGroup);
        _exitMarker.SetMeta("map_kind", "poi");
        _exitMarker.SetMeta("map_label", "→");
        _exitMarker.SetMeta("map_color", new Color(0.3f, 1f, 0.45f));
        Refresh();
    }
}
