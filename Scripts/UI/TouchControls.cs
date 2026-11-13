// Scripts/UI/TouchControls.cs
// Controles de toque para o celular (v0.10). Autoload: aparece só em tela de toque (ou se forçado em
// Opções > Controles > Toque) e só durante o jogo, nunca no menu.
//   Esquerda : joystick que nasce onde o dedo encosta (andar / acelerar, ré e volante na van).
//   Direita  : arrastar mexe a câmera; botões grandes de USAR (E), PULAR/FREIO, JOGAR, SOLTAR e CORRER.
//   Topo esq.: PAUSA, MOCHILA, LUZ, VISÃO e RÁDIO (na van, + ESTAÇÃO).
//   Com mochila/loja aberta só fica o VOLTAR (as telas fecham com a ação "pause").
// Tudo vira ações do Input Map (move_*, jump, interact...), então PlayerController e VanController não
// precisam saber que é toque. Desenhado em código (verde fósforo), sem textura.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class TouchControls : CanvasLayer
{
    public static TouchControls Instance { get; private set; }

    /// <summary>Verdadeiro quando os controles de jogo estão na tela (o mouse emulado pelo toque é ignorado).</summary>
    public static bool Active => Instance != null && Instance._mode == Mode.Game;

    private enum Mode { Hidden, Game, Modal }

    private enum Kind { Hold, Toggle }

    private sealed class TouchButton
    {
        public string Action;
        public string Label;
        public Vector2 Center;
        public float Radius;
        public Kind Kind;
        public bool Down;
        public bool Visible = true;
    }

    private const float LookSensitivity = 1.25f;

    private Mode _mode = Mode.Hidden;
    private Control _canvas;
    private readonly List<TouchButton> _buttons = new();
    private readonly Dictionary<int, TouchButton> _buttonFingers = new();

    private int _stickFinger = -1;
    private Vector2 _stickBase;
    private Vector2 _stickKnob;
    private Vector2 _stickRest;
    private float _stickRadius;
    private int _lookFinger = -1;

    private TouchButton _interact, _jump, _throw, _drop, _sprint;
    private TouchButton _pause, _bag, _light, _vision, _radio, _station, _back, _cameraView;

    /// <summary>Botão sem ação no Input Map: alterna 1ª/3ª pessoa direto no PlayerController.</summary>
    private const string CameraAction = "_camera_view";

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Layer = 90;
        ProcessMode = ProcessModeEnum.Always;
        _canvas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.Draw += DrawControls;
        AddChild(_canvas);

        _interact = Add("interact", "USAR", Kind.Hold);
        _jump = Add("jump", "PULAR", Kind.Hold);
        _throw = Add("throw", "JOGAR", Kind.Hold);
        _drop = Add("drop_item", "SOLTAR", Kind.Hold);
        _sprint = Add("sprint", "CORRER", Kind.Toggle);
        _pause = Add("pause", "PAUSA", Kind.Hold);
        _bag = Add("inventory", "MOCHILA", Kind.Hold);
        _light = Add("flashlight", "LUZ", Kind.Hold);
        _vision = Add("night_vision", "VISÃO", Kind.Hold);
        _radio = Add("radio", "RÁDIO", Kind.Hold);
        _station = Add("radio_next", "ESTAÇÃO", Kind.Hold);
        _back = Add("pause", "VOLTAR", Kind.Hold);
        _cameraView = Add(CameraAction, "CÂMERA", Kind.Hold);
        Visible = false;
    }

    private TouchButton Add(string action, string label, Kind kind)
    {
        var b = new TouchButton { Action = action, Label = label, Kind = kind };
        _buttons.Add(b);
        return b;
    }

    // ---------------------------------------------------------------- estado

    private static bool Enabled
    {
        get
        {
            GameSettings s = GameSettings.Instance;
            int mode = s?.TouchMode ?? 0;
            // Automático = só no celular. Alguns PCs (e o X11 do Linux) dizem ter tela de toque sem ter:
            // notebook com toque escolhe "Sempre" nas Opções.
            return mode == 1 || (mode == 0 && GameSettings.IsMobile);
        }
    }

    private Mode WantedMode()
    {
        if (!Enabled)
            return Mode.Hidden;
        if (ModalScreen.AnyOpen)
            return Mode.Modal;
        PlayerController p = PlayerController.Local;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree() || !p.InputEnabled || GetTree().Paused
            || PauseMenu.Instance?.IsOpen == true)
            return Mode.Hidden;
        return Mode.Game;
    }

    public override void _Process(double delta)
    {
        Mode wanted = WantedMode();
        if (wanted != _mode)
        {
            ReleaseAll();
            _mode = wanted;
            Visible = _mode != Mode.Hidden;
        }
        if (_mode == Mode.Hidden)
            return;
        Layout();
        _canvas.QueueRedraw();
    }

    /// <summary>Posições em coordenadas do viewport, respeitando o entalhe da câmera (safe area).</summary>
    private void Layout()
    {
        Rect2 view = _canvas.GetViewportRect();
        Vector2 size = view.Size;
        float u = Mathf.Clamp(size.Y / 720f, 0.6f, 2f) * 64f * (GameSettings.Instance?.TouchScale ?? 1f);

        // Margem do entalhe: a safe area vem em pixels da tela, o viewport pode estar esticado.
        Vector2 margin = new(u * 0.35f, u * 0.35f);
        Vector2I win = DisplayServer.WindowGetSize();
        Rect2I safe = DisplayServer.GetDisplaySafeArea();
        if (win.X > 0 && win.Y > 0 && safe.Size.X > 0)
        {
            Vector2 k = size / new Vector2(win.X, win.Y);
            margin.X = Mathf.Max(margin.X, Mathf.Max(safe.Position.X, win.X - safe.End.X) * k.X);
            margin.Y = Mathf.Max(margin.Y, Mathf.Max(safe.Position.Y, win.Y - safe.End.Y) * k.Y);
        }
        float left = margin.X, right = size.X - margin.X, top = margin.Y, bottom = size.Y - margin.Y;

        PlayerController p = PlayerController.Local;
        bool seated = p != null && IsInstanceValid(p) && p.CurrentVehicle != null;
        bool holding = p != null && IsInstanceValid(p) && p.IsHolding;

        // Quem aparece: no jogo, os botões de andar ou de dirigir; com tela aberta, só o VOLTAR.
        bool game = _mode == Mode.Game;
        foreach (TouchButton b in _buttons)
            b.Visible = game;
        _throw.Visible = _drop.Visible = _sprint.Visible = game && !seated;
        _radio.Visible = _station.Visible = game && seated;
        _back.Visible = _mode == Mode.Modal;
        _jump.Label = seated ? "FREIO" : "PULAR";
        _throw.Label = holding ? "JOGAR" : "ITEM";

        _stickRadius = u * 1.5f;
        _stickRest = new Vector2(left + u * 2.1f, bottom - u * 2.1f);

        Place(_interact, right - u * 1.2f, bottom - u * 2.95f, u * 0.95f);
        Place(_jump, right - u * 2.95f, bottom - u * 1.05f, u * 0.82f);
        Place(_throw, right - u * 1.1f, bottom - u * 1.0f, u * 0.72f);
        Place(_drop, right - u * 4.55f, bottom - u * 0.8f, u * 0.55f);
        Place(_sprint, right - u * 3.05f, bottom - u * 2.85f, u * 0.6f);

        // Fileira de cima no canto esquerdo: o direito é da lista de entregas do HUD.
        float x = left + u * 0.62f;
        foreach (TouchButton b in new[] { _pause, _bag, _cameraView, _light, _vision, _radio, _station })
        {
            if (!b.Visible)
                continue;
            Place(b, x, top + u * 0.62f, u * 0.55f);
            x += u * 1.3f;
        }
        Place(_back, right - u * 0.9f, top + u * 0.75f, u * 0.65f);
        if (_stickFinger < 0)
            _stickBase = _stickKnob = _stickRest;
    }

    private static void Place(TouchButton b, float x, float y, float r)
    {
        b.Center = new Vector2(x, y);
        b.Radius = r;
    }

    // ---------------------------------------------------------------- toque

    public override void _Input(InputEvent @event)
    {
        if (_mode == Mode.Hidden)
            return;
        if (@event is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
                TouchDown(touch.Index, touch.Position);
            else
                TouchUp(touch.Index);
        }
        else if (@event is InputEventScreenDrag drag)
        {
            TouchMove(drag.Index, drag.Position, drag.Relative);
        }
    }

    private void TouchDown(int finger, Vector2 pos)
    {
        foreach (TouchButton b in _buttons)
        {
            if (!b.Visible || pos.DistanceTo(b.Center) > b.Radius * 1.15f)
                continue;
            _buttonFingers[finger] = b;
            if (b.Kind == Kind.Toggle)
                SetButton(b, !b.Down);
            else
                SetButton(b, true);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_mode != Mode.Game)
            return;

        Vector2 size = _canvas.GetViewportRect().Size;
        if (_stickFinger < 0 && pos.X < size.X * 0.42f && pos.Y > size.Y * 0.3f)
        {
            _stickFinger = finger;
            _stickBase = _stickKnob = pos;
            GetViewport().SetInputAsHandled();
        }
        else if (_lookFinger < 0 && pos.X >= size.X * 0.42f)
        {
            _lookFinger = finger;
            GetViewport().SetInputAsHandled();
        }
    }

    private void TouchMove(int finger, Vector2 pos, Vector2 relative)
    {
        if (finger == _stickFinger)
        {
            Vector2 offset = (pos - _stickBase).LimitLength(_stickRadius);
            _stickKnob = _stickBase + offset;
            ApplyStick(offset / _stickRadius);
            GetViewport().SetInputAsHandled();
        }
        else if (finger == _lookFinger)
        {
            PlayerController p = PlayerController.Local;
            if (p != null && IsInstanceValid(p))
                p.TouchLook(relative * LookSensitivity);
            GetViewport().SetInputAsHandled();
        }
    }

    private void TouchUp(int finger)
    {
        if (_buttonFingers.Remove(finger, out TouchButton b))
        {
            if (b.Kind == Kind.Hold)
                SetButton(b, false);
            GetViewport().SetInputAsHandled();
        }
        if (finger == _stickFinger)
        {
            _stickFinger = -1;
            ApplyStick(Vector2.Zero);
            if (_sprint.Down)
                SetButton(_sprint, false); // soltou o joystick: para de correr
        }
        if (finger == _lookFinger)
            _lookFinger = -1;
    }

    /// <summary>Joystick analógico: cada direção vira a força da ação (GetVector/GetAxis leem a força).</summary>
    private static void ApplyStick(Vector2 v)
    {
        const float dead = 0.15f;
        if (v.Length() < dead)
            v = Vector2.Zero;
        SetAxis("move_right", v.X);
        SetAxis("move_left", -v.X);
        SetAxis("move_back", v.Y);
        SetAxis("move_forward", -v.Y);
    }

    private static void SetAxis(string action, float strength)
    {
        if (strength > 0.01f)
            Input.ActionPress(action, Mathf.Min(strength * 1.15f, 1f));
        else
            Input.ActionRelease(action);
    }

    /// <summary>Manda a ação como evento de verdade: funciona com IsActionJustPressed e com _UnhandledInput.</summary>
    private static void SetButton(TouchButton b, bool down)
    {
        if (b.Down == down)
            return;
        b.Down = down;
        if (b.Action == CameraAction)
        {
            if (down && PlayerController.Local is { } p && IsInstanceValid(p))
                p.ToggleCameraView();
            return;
        }
        Input.ParseInputEvent(new InputEventAction { Action = b.Action, Pressed = down, Strength = down ? 1f : 0f });
    }

    private void ReleaseAll()
    {
        foreach (TouchButton b in _buttons)
            SetButton(b, false);
        _buttonFingers.Clear();
        if (_stickFinger >= 0)
            ApplyStick(Vector2.Zero);
        _stickFinger = -1;
        _lookFinger = -1;
    }

    /// <summary>Centro do botão com esse texto (testes automáticos).</summary>
    public Vector2 DebugButtonPos(string label)
    {
        foreach (TouchButton b in _buttons)
            if (b.Visible && b.Label == label)
                return b.Center;
        return new Vector2(-1000, -1000);
    }

    // ---------------------------------------------------------------- desenho

    private void DrawControls()
    {
        Font font = ThemeDB.FallbackFont;
        Color line = UiStyle.Phosphor;
        if (_mode == Mode.Game)
        {
            bool active = _stickFinger >= 0;
            _canvas.DrawCircle(_stickBase, _stickRadius, new Color(0f, 0.05f, 0.02f, active ? 0.35f : 0.18f));
            _canvas.DrawArc(_stickBase, _stickRadius, 0f, Mathf.Tau, 48, line with { A = active ? 0.7f : 0.35f }, 3f, true);
            _canvas.DrawCircle(_stickKnob, _stickRadius * 0.42f, line with { A = active ? 0.55f : 0.3f });
        }
        foreach (TouchButton b in _buttons)
        {
            if (!b.Visible)
                continue;
            Color fill = b.Down ? line with { A = 0.55f } : new Color(0f, 0.05f, 0.02f, 0.45f);
            _canvas.DrawCircle(b.Center, b.Radius, fill);
            _canvas.DrawArc(b.Center, b.Radius, 0f, Mathf.Tau, 40, line with { A = 0.8f }, 2.5f, true);
            int fs = Mathf.Max(10, (int)(b.Radius * (b.Label.Length > 6 || b.Label.Contains('\n') ? 0.36f : 0.46f)));
            string[] rows = b.Label.Split('\n');
            float h = font.GetHeight(fs);
            float y0 = b.Center.Y - h * rows.Length * 0.5f + font.GetAscent(fs);
            for (int i = 0; i < rows.Length; i++)
            {
                float w = font.GetStringSize(rows[i], HorizontalAlignment.Left, -1, fs).X;
                _canvas.DrawString(font, new Vector2(b.Center.X - w * 0.5f, y0 + i * h), rows[i],
                    HorizontalAlignment.Left, -1, fs, b.Down ? Colors.Black : line);
            }
        }
    }
}
