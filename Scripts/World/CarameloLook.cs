// Scripts/World/CarameloLook.cs
// O companheiro do sítio é um vira-lata CARAMELO (v0.10), não um Shiba: usa o esqueleto e as animações do
// ShibaInu da Quaternius (CC0), mas pinta de caramelo com focinho, peito e patas creme e tomba um pouco as
// orelhas (as do Shiba são bem pontudas). O rabo enrolado fica: é modelado na malha, e muito caramelo tem.
// O ajuste das orelhas roda como SkeletonModifier3D, por cima da animação, a cada quadro.
using Godot;

namespace DeadFrequency;

public partial class CarameloLook : SkeletonModifier3D
{
    public static readonly Color Caramel = new(0.80f, 0.53f, 0.26f);
    public static readonly Color Cream = new(0.93f, 0.84f, 0.66f);

    /// <summary>Orelhas: quanto as pontas tombam para a frente e para fora (0 = em pé como o Shiba).</summary>
    [Export] public float EarDroop { get; set; } = 0.55f;

    private int[] _tail = System.Array.Empty<int>();
    private int[] _ears = System.Array.Empty<int>();
    private int _head = -1;

    /// <summary>Aplica cor e postura num ShibaInu instanciado.</summary>
    public static void Apply(Node3D body)
    {
        foreach (Node n in body.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)n;
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is not BaseMaterial3D m)
                    continue;
                string name = (mi.Mesh is ArrayMesh am ? am.SurfaceGetName(s) : "") + "|" + m.ResourceName;
                Color? c = name.Contains("Main_Light") ? Cream : name.Contains("Main") ? Caramel : null;
                if (c == null)
                    continue;
                var copy = (BaseMaterial3D)m.Duplicate();
                copy.AlbedoColor = c.Value;
                mi.SetSurfaceOverrideMaterial(s, copy);
            }
        }
        if (body.FindChild("Skeleton3D", true, false) is Skeleton3D skel)
            skel.AddChild(new CarameloLook { Name = "CarameloLook" });
    }

    public override void _Ready()
    {
        Skeleton3D skel = GetSkeleton();
        if (skel == null)
            return;
        var tail = new System.Collections.Generic.List<int>();
        var ears = new System.Collections.Generic.List<int>();
        for (int i = 0; i < skel.GetBoneCount(); i++)
        {
            string b = skel.GetBoneName(i);
            if (b.StartsWith("Tail"))
                tail.Add(i);
            else if (b.StartsWith("Ear2") || b.StartsWith("Ear3") || b.StartsWith("Ear4"))
                ears.Add(i);
            else if (b == "Head")
                _head = i;
        }
        _tail = tail.ToArray();
        _ears = ears.ToArray();
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        Skeleton3D skel = GetSkeleton();
        if (skel == null || _tail.Length == 0 || _head < 0 || EarDroop <= 0f)
            return;
        // Direções no espaço do esqueleto: "trás" = da cabeça para a base do rabo.
        Vector3 headPos = skel.GetBoneGlobalPose(_head).Origin;
        Vector3 back = skel.GetBoneGlobalPose(_tail[0]).Origin - headPos;
        back.Y = 0f;
        back = back.Normalized();
        foreach (int b in _ears)
        {
            Transform3D g = skel.GetBoneGlobalPose(b);
            Vector3 side = new Vector3(g.Origin.X - headPos.X, 0f, g.Origin.Z - headPos.Z);
            side -= back * side.Dot(back);
            Vector3 droop = (Vector3.Up * 0.35f - back * 0.9f + side.Normalized() * 0.6f).Normalized();
            AimBone(skel, b, droop, EarDroop);
        }
    }

    /// <summary>Gira o osso (em espaço do esqueleto) para o eixo Y dele apontar para dir; amount 0..1.</summary>
    private static void AimBone(Skeleton3D skel, int bone, Vector3 dir, float amount)
    {
        Transform3D g = skel.GetBoneGlobalPose(bone);
        Vector3 y = g.Basis.Y.Normalized();
        Vector3 target = y.Slerp(dir, Mathf.Clamp(amount, 0f, 1f));
        if (y.Dot(target) > 0.9999f)
            return;
        var q = new Quaternion(y, target);
        g.Basis = new Basis(q) * g.Basis;
        skel.SetBoneGlobalPose(bone, g);
    }
}
