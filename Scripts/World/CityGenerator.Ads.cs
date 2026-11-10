// Scripts/World/CityGenerator.Ads.cs
// Anúncios do autor (v0.9): cartazes discretos do recadin.com.br espalhados pela cidade (ponto de ônibus,
// escritório, saguão, mercado, posto, cerca da rádio) e o outdoor "siga twitch.tv/teuzin0dev ·
// github.com/dieguesmosken" (um fixo na rádio + o primeiro outdoor da beira da cidade).
using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    public const string RecadinPoster = "res://Assets/Textures/Ads/ad_recadin.png";
    public const string TeuzinAd = "teuzin";

    private static StandardMaterial3D _posterMat;

    /// <summary>Cartaz pequeno (0,45 x 0,6 m) com moldura fina. Estático para o BusLine também usar.</summary>
    public static void Poster(Node3D parent, Vector3 pos, float rotY, float scale = 1f)
    {
        if (_posterMat == null)
        {
            if (!ResourceLoader.Exists(RecadinPoster))
                return;
            _posterMat = new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>(RecadinPoster), Roughness = 0.5f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
        }
        var size = new Vector2(0.45f, 0.6f) * scale;
        var frame = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(size.X + 0.04f, size.Y + 0.04f, 0.01f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) },
            Position = pos - new Vector3(0f, 0f, 0.012f).Rotated(Vector3.Up, rotY),
            Rotation = new Vector3(0f, rotY, 0f),
        };
        parent.AddChild(frame);
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            MaterialOverride = _posterMat,
            Position = pos,
            Rotation = new Vector3(0f, rotY, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Outdoor do teuzin0dev dentro do terreno da rádio, virado para a avenida.</summary>
    private void BuildTeuzinBillboard(Node3D st)
    {
        var bb = new Node3D { Name = "Outdoor_teuzin", Position = new Vector3(6.5f, 0f, 12.2f) };
        st.AddChild(bb);
        const float w = 7.5f, h = 2.5f, baseY = 4.2f;
        foreach (float x in new[] { -2.4f, 2.4f })
            AddBox(bb, new Vector3(x, (baseY + h) * 0.5f, -0.3f), new Vector3(0.3f, baseY + h, 0.3f), _darkMetal);
        AddBox(bb, new Vector3(0f, baseY + h * 0.5f, -0.12f), new Vector3(w + 0.3f, h + 0.3f, 0.2f), _darkMetal, false);
        TexQuad(bb, AdTexturePath(TeuzinAd), new Vector3(0f, baseY + h * 0.5f, 0f), new Vector2(w, h), 0f, 0.5f);
        bb.AddChild(new SpotLight3D
        {
            Position = new Vector3(0f, baseY - 0.1f, 1.2f),
            RotationDegrees = new Vector3(70f, 0f, 0f),
            LightColor = new Color(0.85f, 0.75f, 1f),
            LightEnergy = 5f,
            SpotRange = 6f,
            SpotAngle = 60f,
            ShadowEnabled = false,
        });
    }
}
