// Scripts/Story/ChapterBanner.cs
// Banner retrô de capítulo ("CAPÍTULO II — Asfalto e Barro") no tema CRT, com faixa escura e scanlines.
using Godot;

namespace DeadFrequency;

public partial class ChapterBanner : CanvasLayer
{
    /// <summary>Mostra o banner. seconds &lt;= 0 = fica até alguém fechar (QueueFree).</summary>
    public static ChapterBanner Show(Node parent, string number, string subtitle, float seconds)
    {
        var banner = new ChapterBanner { Name = "ChapterBanner", Layer = 60 };
        var band = new ColorRect { Color = new Color(0f, 0f, 0f, 0.78f), MouseFilter = Control.MouseFilterEnum.Ignore };
        band.SetAnchorsPreset(Control.LayoutPreset.HcenterWide);
        band.OffsetTop = -90f;
        band.OffsetBottom = 90f;
        banner.AddChild(band);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        band.AddChild(box);
        box.AddChild(UiStyle.MakeLabel(number, 44, HorizontalAlignment.Center, UiStyle.Phosphor));
        if (!string.IsNullOrEmpty(subtitle))
            box.AddChild(UiStyle.MakeLabel(subtitle, 24, HorizontalAlignment.Center, UiStyle.PhosphorDim));
        banner.AddChild(UiStyle.MakeScanlines(0.18f));
        parent.AddChild(banner);
        banner.FadeIn();
        if (seconds > 0f)
            banner.GetTree().CreateTimer(seconds).Timeout += banner.FadeOutAndFree;
        return banner;
    }

    private void FadeIn()
    {
        foreach (Node n in GetChildren())
            if (n is CanvasItem c)
            {
                c.Modulate = new Color(1f, 1f, 1f, 0f);
                CreateTween().TweenProperty(c, "modulate:a", 1f, 0.5);
            }
    }

    public void FadeOutAndFree()
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
            return;
        Tween t = CreateTween().SetParallel();
        foreach (Node n in GetChildren())
            if (n is CanvasItem c)
                t.TweenProperty(c, "modulate:a", 0f, 0.6);
        t.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
