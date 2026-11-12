// Scripts/UI/MenuStyles.cs
// Menus iniciais novos (v0.9.9), liberados por código secreto em Opções > Interface > Menu inicial:
//   ORBITA966   → menu "Órbita" (cidade à noite vista do alto, menu na lateral)
//   CINEMA966   → menu "Cinema" (faixas pretas de filme, menu numa linha embaixo)
//   TERMINAL966 → menu "Terminal" (tela de despacho do depósito, em fósforo verde)
//   VISUAL966   → libera os três de uma vez e mostra a escolha do menu nas Opções
//   CLASSICO966 → volta ao menu antigo (e esconde a escolha de novo)
// Fica salvo em user://settings.cfg (GameSettings.MenuStyle e MenuPicker).
namespace DeadFrequency;

public enum MenuStyle
{
    Classico = 0,
    Orbita = 1,
    Cinema = 2,
    Terminal = 3,
}

public static class MenuStyles
{
    public static readonly string[] Names = { "Clássico", "Órbita", "Cinema", "Terminal" };

    /// <summary>Fundo de cada menu novo (capturas da própria cidade). O clássico não tem.</summary>
    public static string Background(MenuStyle style) => style switch
    {
        MenuStyle.Orbita => "res://Assets/Textures/Menu/menu_orbita.jpg",
        MenuStyle.Cinema => "res://Assets/Textures/Menu/menu_cinema.jpg",
        MenuStyle.Terminal => "res://Assets/Textures/Menu/menu_terminal.jpg",
        _ => null,
    };

    /// <summary>Resultado de digitar um código: se valeu e a mensagem para mostrar.</summary>
    public readonly record struct CodeResult(bool Ok, string Message);

    /// <summary>Aplica o código (sem diferenciar maiúsculas) e salva. O menu muda ao voltar para a tela inicial.</summary>
    public static CodeResult Apply(string code)
    {
        GameSettings s = GameSettings.Instance;
        if (s == null)
            return new CodeResult(false, "Configurações não carregadas.");
        string c = (code ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        CodeResult result;
        switch (c)
        {
            case "ORBITA966":
            case "ÓRBITA966":
                s.MenuStyle = (int)MenuStyle.Orbita;
                result = new CodeResult(true, "Menu Órbita ativado.");
                break;
            case "CINEMA966":
                s.MenuStyle = (int)MenuStyle.Cinema;
                result = new CodeResult(true, "Menu Cinema ativado.");
                break;
            case "TERMINAL966":
                s.MenuStyle = (int)MenuStyle.Terminal;
                result = new CodeResult(true, "Menu Terminal ativado.");
                break;
            case "VISUAL966":
                s.MenuPicker = true;
                result = new CodeResult(true, "Os três menus foram liberados: escolha logo abaixo.");
                break;
            case "CLASSICO966":
            case "CLÁSSICO966":
                s.MenuStyle = (int)MenuStyle.Classico;
                s.MenuPicker = false;
                result = new CodeResult(true, "De volta ao menu clássico.");
                break;
            default:
                return new CodeResult(false, c.Length == 0 ? "Digite um código." : "Código inválido. Sintonize melhor...");
        }
        s.Save();
        return result;
    }
}
