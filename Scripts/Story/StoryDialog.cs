// Scripts/Story/StoryDialog.cs
// Falas do Modo História, como transmissão de rádio: caixa no pé da tela com o nome de quem fala e o texto
// aparecendo letra por letra. Passa sozinho (tempo pelo tamanho do texto) ou com E / clique / toque.
// Não pausa o jogo e não trava o jogador.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class StoryDialog : CanvasLayer
{
    private static StoryDialog _current;
    private readonly Queue<StoryChapters.Line> _queue = new();
    private PanelContainer _panel;
    private Label _speaker;
    private Label _text;
    private string _full = "";
    private float _shown;
    private float _hold;
    private bool _waiting;

    /// <summary>Enfileira falas. Se já houver uma caixa aberta, entram depois das que estão nela.</summary>
    public static void Play(Node from, IEnumerable<StoryChapters.Line> lines)
    {
        if (from?.GetTree() == null || lines == null)
            return;
        if (_current == null || !IsInstanceValid(_current))
        {
            _current = new StoryDialog { Name = "StoryDialog", Layer = 55 };
            from.GetTree().Root.AddChild(_current);
        }
        foreach (StoryChapters.Line l in lines)
            _current._queue.Enqueue(l);
        if (!_current._waiting && _current._full.Length == 0)
            _current.Next();
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", UiStyle.Box(new Color(0.01f, 0.03f, 0.02f, 0.88f), UiStyle.PhosphorDim, 2, 12));
        _panel.AnchorLeft = 0.5f; _panel.AnchorRight = 0.5f; _panel.AnchorTop = 1f; _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = -360f; _panel.OffsetRight = 360f; _panel.OffsetTop = -190f; _panel.OffsetBottom = -96f;
        AddChild(_panel);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(box);
        _speaker = UiStyle.MakeLabel("", 15, HorizontalAlignment.Left, new Color(1f, 0.85f, 0.4f));
        _text = UiStyle.MakeLabel("", 19, HorizontalAlignment.Left, UiStyle.Phosphor);
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(690f, 0f);
        box.AddChild(_speaker);
        box.AddChild(_text);
        _panel.Visible = false;
    }

    public override void _ExitTree()
    {
        if (_current == this)
            _current = null;
    }

    private void Next()
    {
        if (_queue.Count == 0)
        {
            QueueFree();
            return;
        }
        StoryChapters.Line l = _queue.Dequeue();
        _speaker.Text = "📻 " + l.Speaker;
        _full = l.Text;
        _shown = 0f;
        _text.Text = "";
        _waiting = false;
        _hold = 2.2f + l.Text.Length * 0.045f;
        _panel.Visible = true;
    }

    public override void _Process(double delta)
    {
        if (_full.Length == 0)
            return;
        float dt = (float)delta;
        if (!_waiting)
        {
            _shown += dt * 45f; // letras por segundo
            int n = Mathf.Min(_full.Length, (int)_shown);
            _text.Text = _full.Substring(0, n);
            if (n >= _full.Length)
                _waiting = true;
            return;
        }
        _hold -= dt;
        if (_hold <= 0f)
            Next();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_full.Length == 0)
            return;
        bool press = (e.IsActionPressed("interact") && !e.IsEcho()) ||
                     (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Input.MouseMode != Input.MouseModeEnum.Captured) ||
                     e is InputEventScreenTouch { Pressed: true, Index: > 0 };
        if (!press)
            return;
        // Primeiro toque completa o texto; o segundo passa para a próxima fala.
        if (!_waiting)
        {
            _shown = _full.Length;
            return;
        }
        _hold = 0f;
    }
}
