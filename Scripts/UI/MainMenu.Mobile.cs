// Scripts/UI/MainMenu.Mobile.cs
// Menu inicial no celular (v0.10): a interface fica 1,6x maior (GameSettings.UiScale), então a tela
// "encolhe" para ~1000x450 e o menu do PC não cabe. Aqui: título menor, uma linha de subtítulo,
// botões grandes em duas colunas e tudo dentro de uma rolagem (profissões, personagem e multiplayer
// passam da altura e rolam com o dedo). Os visuais por código (Órbita, Cinema, Terminal) são só do PC.
using Godot;

namespace DeadFrequency;

public partial class MainMenu
{
    private const int MobileTitleSize = 46;

    private void ApplyMobileLayout()
    {
        // Rolagem no lugar da centralização: o conteúdo pode ser mais alto que a tela.
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = MouseFilterEnum.Pass,
        };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        scroll.OffsetTop = 10;
        scroll.OffsetBottom = -34; // espaço do rodapé (versão e save)
        AddChild(scroll);
        MoveChild(scroll, _center.GetIndex());
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(column);
        _center.RemoveChild(_stack);
        _stack.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(_stack);
        _center.Visible = false;

        _title.AddThemeFontSizeOverride("font_size", MobileTitleSize);
        _stack.AddThemeConstantOverride("separation", 8);
        _header.AddThemeConstantOverride("separation", 4);
        if (_header.GetChildCount() > 2)
        {
            if (_header.GetChild(0) is Label sub)
                sub.AddThemeFontSizeOverride("font_size", 16);
            if (_header.GetChild(1) is Label sub2)
                sub2.Visible = false;
            if (_header.GetChild(2) is Control spacer)
                spacer.CustomMinimumSize = new Vector2(0, 6);
        }

        // Botões em duas colunas, altos o bastante para o dedo.
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 10);
        foreach (Node n in _menu.GetChildren())
        {
            if (n is not Button b)
                continue;
            _menu.RemoveChild(b);
            b.CustomMinimumSize = new Vector2(300, 56);
            b.SizeFlagsHorizontal = SizeFlags.Fill;
            b.AddThemeFontSizeOverride("font_size", 20);
            grid.AddChild(b);
        }
        _menu.AddChild(grid);
    }
}
