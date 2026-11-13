// Scripts/Characters/CharacterLook.cs
// Aparência escolhida pelo jogador (v0.9.6): homem/mulher, cor da pele, do cabelo, da camisa e da calça.
// Salva em user://settings.cfg (GameSettings) e mandada no online como texto curto ("w,2,1,5,0").
using Godot;

namespace DeadFrequency;

public readonly record struct CharacterLook(bool Woman, int Skin, int Hair, int Shirt, int Pants)
{
    public static readonly Color[] Skins =
    {
        new(0.95f, 0.8f, 0.68f), new(0.88f, 0.7f, 0.55f), new(0.76f, 0.58f, 0.44f),
        new(0.62f, 0.44f, 0.31f), new(0.47f, 0.32f, 0.22f), new(0.33f, 0.22f, 0.15f),
    };
    /// <summary>Mesma ordem das cores de cabelo do CharacterModel.</summary>
    public static readonly string[] HairNames = { "Preto", "Castanho-escuro", "Castanho", "Castanho-claro", "Loiro", "Grisalho", "Ruivo" };
    public static readonly Color[] HairColors =
    {
        new(0.08f, 0.06f, 0.05f), new(0.2f, 0.12f, 0.07f), new(0.36f, 0.22f, 0.1f),
        new(0.55f, 0.38f, 0.18f), new(0.75f, 0.6f, 0.32f), new(0.45f, 0.45f, 0.45f), new(0.5f, 0.16f, 0.08f),
    };
    public static readonly Color[] Shirts =
    {
        new(0.12f, 0.45f, 0.22f), new(0.9f, 0.9f, 0.88f), new(0.12f, 0.12f, 0.14f), new(0.7f, 0.15f, 0.12f),
        new(0.15f, 0.3f, 0.6f), new(0.9f, 0.75f, 0.2f), new(0.5f, 0.25f, 0.55f), new(0.95f, 0.5f, 0.15f),
    };
    public static readonly Color[] PantsColors =
    {
        new(0.12f, 0.13f, 0.18f), new(0.2f, 0.28f, 0.5f), new(0.08f, 0.08f, 0.08f),
        new(0.4f, 0.32f, 0.22f), new(0.3f, 0.32f, 0.3f), new(0.55f, 0.5f, 0.42f),
    };

    public static CharacterLook Default => new(false, 2, 1, 0, 0);

    /// <summary>A aparência salva nas Opções do jogador local.</summary>
    public static CharacterLook Local
    {
        get
        {
            GameSettings s = GameSettings.Instance;
            return s == null ? Default : new CharacterLook(s.CharWoman, s.CharSkin, s.CharHair, s.CharShirt, s.CharPants).Clamped();
        }
    }

    public CharacterLook Clamped() => new(Woman,
        Mathf.PosMod(Skin, Skins.Length), Mathf.PosMod(Hair, HairColors.Length),
        Mathf.PosMod(Shirt, Shirts.Length), Mathf.PosMod(Pants, PantsColors.Length));

    public string Encode() => $"{(Woman ? "w" : "m")},{Skin},{Hair},{Shirt},{Pants}";

    public static bool TryDecode(string text, out CharacterLook look)
    {
        look = Default;
        if (string.IsNullOrEmpty(text))
            return false;
        string[] p = text.Split(',');
        if (p.Length != 5 || (p[0] != "m" && p[0] != "w"))
            return false;
        if (!int.TryParse(p[1], out int skin) || !int.TryParse(p[2], out int hair) || !int.TryParse(p[3], out int shirt) || !int.TryParse(p[4], out int pants))
            return false;
        look = new CharacterLook(p[0] == "w", skin, hair, shirt, pants).Clamped();
        return true;
    }

    /// <summary>Monta o boneco com esta aparência (vest = colete refletivo opcional por cima da camisa).</summary>
    public CharacterModel Build(Color? vest = null)
    {
        CharacterLook l = Clamped();
        return CharacterModel.Create(Shirts[l.Shirt], PantsColors[l.Pants], Skins[l.Skin], vest, l.Hair * 2 + (l.Woman ? 1 : 0), player: true);
    }
}
