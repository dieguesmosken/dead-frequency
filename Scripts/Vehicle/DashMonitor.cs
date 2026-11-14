// Scripts/Vehicle/DashMonitor.cs
// Conteúdo do monitor CRT verde do painel: um SubViewport pequeno (256x192) com texto, redesenhado só
// algumas vezes por segundo e só enquanto alguém está sentado na caminhonete. Três telas, trocadas pelo
// botão TELA: entregas do turno, sistema (bateria, gasolina, motor, fusíveis) e rádio.
using System.Text;
using Godot;

namespace DeadFrequency;

public partial class DashMonitor : Node
{
    public const int Pages = 3;
    public static readonly string[] PageNames = { "ENTREGAS", "SISTEMA", "RÁDIO" };

    public SubViewport Viewport { get; private set; }
    public int Page { get; private set; }

    private Label _title, _body, _footer;
    private DeliveryManager _delivery;

    /// <summary>v0.10.1: o dobro da resolução (era 256x192) com letras maiores e mais grossas, para ler dirigindo.</summary>
    private static readonly Vector2I Res = new(512, 384);
    private const float K = 2f;

    public override void _Ready()
    {
        Viewport = new SubViewport
        {
            Size = new Vector2I(Res.X, Res.Y),
            TransparentBg = false,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        AddChild(Viewport);
        var root = new Control { Theme = UiStyle.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Viewport.AddChild(root);
        var bg = new ColorRect { Color = new Color(0.01f, 0.035f, 0.015f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(bg);
        _title = Line(root, 3 * K, (int)(17 * K), Colors.White);
        var rule = new ColorRect { Color = new Color(0.7f, 0.7f, 0.7f), Position = new Vector2(6 * K, 25 * K), Size = new Vector2(244 * K, 2) };
        root.AddChild(rule);
        _body = Line(root, 28 * K, (int)(16 * K), Colors.White);
        _footer = Line(root, 172 * K, (int)(13 * K), new Color(0.75f, 0.75f, 0.75f));
    }

    private static Label Line(Control root, float y, int size, Color color)
    {
        var label = new Label { Position = new Vector2(8 * K, y), Size = new Vector2(240 * K, 20 * K) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        // Contorno da mesma cor engrossa o traço: o texto não some nas linhas de varredura do CRT.
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", color);
        label.AddThemeConstantOverride("line_spacing", -2);
        root.AddChild(label);
        return label;
    }

    public void NextPage() => Page = (Page + 1) % Pages;

    private static string Bar(float value, int cells = 10)
    {
        int full = Mathf.Clamp(Mathf.RoundToInt(value * cells), 0, cells);
        return "[" + new string('#', full) + new string('-', cells - full) + "]";
    }

    private static string Clip(string text, int max) => text.Length <= max ? text.PadRight(max) : text[..(max - 1)] + ".";

    /// <summary>Monta o texto da tela atual e pede um quadro novo ao SubViewport. Cabem 24 colunas e 7 linhas.</summary>
    public void Refresh(VanController van)
    {
        if (Viewport == null || van == null)
            return;
        float hour = DayNightController.Hour;
        string clock = $"{(int)hour:00}:{(int)(hour % 1f * 60f):00}";
        _title.Text = $"{PageNames[Page],-17}{clock}";
        var sb = new StringBuilder();
        switch (Page)
        {
            case 0: Deliveries(sb, van); break;
            case 1: SystemPage(sb, van); break;
            default: RadioPage(sb, van); break;
        }
        _body.Text = sb.ToString();
        _footer.Text = $"TELA > {PageNames[(Page + 1) % Pages]}";
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private void Deliveries(StringBuilder sb, VanController van)
    {
        if (_delivery == null || !IsInstanceValid(_delivery))
            _delivery = GetTree().GetFirstNodeInGroup(DeliveryManager.GroupName) as DeliveryManager;
        if (_delivery == null || !_delivery.EntregadorMode)
        {
            sb.AppendLine("SEM ROTA DE ENTREGA").AppendLine().AppendLine("CENTRAL: 96.6 FM").Append("BOM TRABALHO!");
            return;
        }
        string left = $"TURNO {_delivery.Shift} {_delivery.ShiftDelivered}/{_delivery.ShiftTotal}";
        sb.AppendLine($"{left,-14}{"$" + _delivery.Money,10}");
        int shown = 0;
        foreach (DeliveryJob job in _delivery.Jobs)
        {
            if (shown >= 3)
                break;
            float d = van.GlobalPosition.DistanceTo(job.Point.Position);
            float integrity = job.Cargo != null && IsInstanceValid(job.Cargo) ? job.Cargo.Integrity : 0f;
            sb.AppendLine($">{Clip(job.CargoName.ToUpperInvariant(), 23)}");
            sb.AppendLine($" {Bar(integrity, 8)}{integrity * 100f,4:0}%{d,6:0}m");
            shown++;
        }
        if (shown == 0)
            sb.AppendLine().AppendLine("TUDO ENTREGUE.").Append("VOLTE AO DEPOSITO.");
    }

    private static void SystemPage(StringBuilder sb, VanController van)
    {
        ElectricalSystem el = van.Electrical;
        EngineSystem en = van.Engine;
        if (el == null || en == null)
            return;
        sb.AppendLine($"BATERIA {Bar(el.Charge, 8)}{el.Voltage,5:0.0}V");
        sb.AppendLine($"TANQUE  {Bar(en.Fuel, 8)}{en.FuelLiters,5:0}L");
        sb.AppendLine($"MOTOR   {en.Temperature,3:0}C " + (en.Overheated ? "FERVENDO!" : en.Running ? "OK" : "DESLIGADO"));
        sb.AppendLine($"GIRO    {en.Rpm,4:0} RPM");
        sb.Append("FUSIVEL ");
        string[] shortNames = { "FAR", "RAD", "PNL", "LUZ" };
        bool allOk = true;
        for (int i = 0; i < 4; i++)
        {
            var c = (Circuit)i;
            if (!el.FuseOk(c)) { sb.Append(shortNames[i]).Append("! "); allOk = false; }
            else if (el.IsJumper(c)) { sb.Append(shortNames[i]).Append("~ "); allOk = false; }
        }
        sb.AppendLine(allOk ? "TODOS OK" : "");
        sb.AppendLine($"RESERVA {el.Spares} FUSIVEIS");
        sb.Append($"INTERF. {Bar(el.Interference, 8)}");
    }

    private static void RadioPage(StringBuilder sb, VanController van)
    {
        bool on = van.Radio != null && van.Radio.On;
        if (!on)
        {
            sb.AppendLine().AppendLine("RADIO DESLIGADO").AppendLine().AppendLine("APERTE RADIO NO PAINEL").Append("(OU A TECLA R)");
            return;
        }
        sb.AppendLine("96.6 FM - A VOZ DO VALE").AppendLine();
        string playing = van.Radio.NowPlaying.ToUpperInvariant();
        for (int i = 0; i < playing.Length && i < 24 * 4; i += 24)
            sb.AppendLine(playing.Substring(i, Mathf.Min(24, playing.Length - i)));
        float interference = van.Electrical?.Interference ?? 0f;
        sb.Append(interference > 0.25f ? "SINAL: CHIADO (CARGA)" : "SINAL: LIMPO");
    }
}
