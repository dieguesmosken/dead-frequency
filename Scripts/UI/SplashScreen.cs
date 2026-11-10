using Godot;

namespace DeadFrequency;

public partial class SplashScreen : Control
{
    private TextureRect _image;

    public override void _Ready()
    {
        _image = GetNode<TextureRect>("TextureRect");
        _image.StretchMode = TextureRect.StretchModeEnum.Scale;
        _image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _image.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        _image.PivotOffset = GetViewportRect().Size / 2f;
        _image.Scale = Vector2.One;

        Tween tween = CreateTween();
        tween.TweenProperty(_image, "scale", new Vector2(1.12f, 1.12f), 4.0)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(OpenMainMenu));
    }

    private void OpenMainMenu()
    {
        Error error = GetTree().ChangeSceneToFile("res://Scenes/UI/main_menu.tscn");
        if (error != Error.Ok)
            GD.PushError($"Falha ao abrir o menu principal: {error}");
    }
}
