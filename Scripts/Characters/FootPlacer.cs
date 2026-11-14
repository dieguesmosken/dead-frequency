// Scripts/Characters/FootPlacer.cs
// Pés no chão (v0.10.1): SkeletonModifier3D que roda ANTES do TwoBoneIK3D das pernas (CharacterModel.IK).
// Lê onde a animação pôs cada pé, mede a altura do chão embaixo dele (raio no _PhysicsProcess, camada World)
// e põe o alvo do pé nessa altura. Se um pé fica mais baixo (descendo degrau, beira da calçada), abaixa o
// quadril para a perna alcançar. Em chão plano não muda nada.
using Godot;

namespace DeadFrequency;

public partial class FootPlacer : SkeletonModifier3D
{
    /// <summary>Liga/desliga o ajuste (jogadores perto da câmera).</summary>
    public bool Enabled { get; set; }
    /// <summary>Outro dono dos alvos dos pés (pedais, escada): aqui não mexe.</summary>
    public bool Manual { get; set; }

    private const float MaxUp = 0.45f, MaxDown = 0.35f, RayUp = 0.6f;

    private CharacterModel _model;
    private Node3D _targetL, _targetR;
    private int _footL = -1, _footR = -1, _hips = -1;
    private Vector3 _footWorldL, _footWorldR;
    private float _groundL, _groundR, _smoothL, _smoothR, _hipDrop;
    private bool _haveFeet;

    public void Setup(CharacterModel model, Node3D left, Node3D right)
    {
        _model = model;
        _targetL = left;
        _targetR = right;
    }

    public override void _Ready()
    {
        Skeleton3D sk = GetSkeleton();
        if (sk == null)
            return;
        _footL = sk.FindBone("LeftFoot");
        _footR = sk.FindBone("RightFoot");
        _hips = sk.FindBone("Hips");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Enabled || Manual || !_haveFeet || _model == null || !_model.IsVisibleInTree())
            return;
        float baseY = _model.GlobalPosition.Y;
        PhysicsDirectSpaceState3D space = _model.GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return;
        _groundL = Ground(space, _footWorldL, baseY);
        _groundR = Ground(space, _footWorldR, baseY);
    }

    /// <summary>Altura do chão embaixo do pé, relativa aos pés do personagem (0 = mesmo nível).</summary>
    private static float Ground(PhysicsDirectSpaceState3D space, Vector3 foot, float baseY)
    {
        var from = new Vector3(foot.X, baseY + RayUp, foot.Z);
        var q = PhysicsRayQueryParameters3D.Create(from, from + Vector3.Down * (RayUp + MaxDown + 0.1f), 1u);
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return 0f;
        return Mathf.Clamp(((Vector3)hit["position"]).Y - baseY, -MaxDown, MaxUp);
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        Skeleton3D sk = GetSkeleton();
        if (sk == null || _footL < 0 || _footR < 0 || _model == null)
            return;
        Transform3D g = sk.GlobalTransform;
        _footWorldL = g * sk.GetBoneGlobalPose(_footL).Origin;
        _footWorldR = g * sk.GetBoneGlobalPose(_footR).Origin;
        _haveFeet = true;
        if (!Enabled || Manual)
        {
            _smoothL = _smoothR = _hipDrop = 0f;
            return;
        }
        float k = 1f - Mathf.Exp(-14f * (float)delta);
        _smoothL = Mathf.Lerp(_smoothL, _groundL, k);
        _smoothR = Mathf.Lerp(_smoothR, _groundR, k);
        _hipDrop = Mathf.Min(0f, Mathf.Min(_smoothL, _smoothR));

        // Abaixa o quadril (no espaço do esqueleto, que pode ter escala).
        if (_hips >= 0 && _hipDrop < -0.005f)
        {
            Vector3 local = g.Basis.Inverse() * (Vector3.Up * _hipDrop);
            sk.SetBonePosePosition(_hips, sk.GetBonePosePosition(_hips) + local);
        }
        _targetL.GlobalPosition = _footWorldL + Vector3.Up * _smoothL;
        _targetR.GlobalPosition = _footWorldR + Vector3.Up * _smoothR;
    }
}
