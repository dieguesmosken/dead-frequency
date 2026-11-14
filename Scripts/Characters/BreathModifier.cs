// Scripts/Characters/BreathModifier.cs
// Idle respirando (v0.10.1). A animação "Idle" dos modelos vira a cabeça de um lado para o outro o tempo todo;
// parado, este SkeletonModifier3D segura pescoço e cabeça olhando para a frente (pose de descanso) e faz o peito
// subir e descer devagar (coluna e ombros), cada personagem num ritmo um pouco diferente.
// Roda antes do IK (o LookAtModifier3D da cabeça continua podendo virar a cabeça para o jogador).
using Godot;

namespace DeadFrequency;

public partial class BreathModifier : SkeletonModifier3D
{
    /// <summary>Parado (idle ou sentado): liga a respiração e segura a cabeça.</summary>
    public bool Resting { get; set; }

    private int _neck = -1, _head = -1, _spineA = -1, _spineB = -1, _shoulderL = -1, _shoulderR = -1;
    private float _weight, _time, _rate;

    public override void _Ready()
    {
        Skeleton3D sk = GetSkeleton();
        if (sk == null)
            return;
        _neck = Find(sk, "neck", "Neck");
        _head = sk.FindBone("Head");
        _spineA = Find(sk, "Spine01", "Spine1");
        _spineB = Find(sk, "Spine02", "Spine2");
        _shoulderL = sk.FindBone("LeftShoulder");
        _shoulderR = sk.FindBone("RightShoulder");
        // Ritmo entre ~13 e ~17 respirações por minuto, diferente para cada um.
        _rate = 0.22f + (GetInstanceId() % 7) * 0.012f;
        _time = (GetInstanceId() % 100) * 0.37f;
    }

    private static int Find(Skeleton3D sk, string a, string b)
    {
        int i = sk.FindBone(a);
        return i >= 0 ? i : sk.FindBone(b);
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        Skeleton3D sk = GetSkeleton();
        if (sk == null)
            return;
        float dt = (float)delta;
        _weight = Mathf.MoveToward(_weight, Resting ? 1f : 0f, dt * 2.5f);
        if (_weight <= 0.001f)
            return;
        _time += dt;

        // Cabeça e pescoço voltam para a pose de descanso (olhando para a frente).
        foreach (int b in new[] { _neck, _head })
            if (b >= 0)
                sk.SetBonePoseRotation(b, sk.GetBonePoseRotation(b).Slerp(sk.GetBoneRest(b).Basis.GetRotationQuaternion(), _weight));

        // Peito: inclina a coluna para trás e sobe os ombros ao puxar o ar.
        float breath = Mathf.Sin(_time * Mathf.Tau * _rate);
        Vector3 right = SkeletonAxis(sk, Vector3.Right);
        Vector3 forward = SkeletonAxis(sk, Vector3.Back);
        Tilt(sk, _spineA, right, -0.02f * breath * _weight);
        Tilt(sk, _spineB, right, -0.03f * breath * _weight);
        Tilt(sk, _shoulderL, forward, 0.03f * breath * _weight);
        Tilt(sk, _shoulderR, forward, -0.03f * breath * _weight);
    }

    /// <summary>Eixo do personagem (direita, frente) no espaço do esqueleto (o Meshy vem com escala e giro no Skeleton3D).</summary>
    private static Vector3 SkeletonAxis(Skeleton3D sk, Vector3 modelAxis)
    {
        Node3D model = sk.GetParent() as Node3D;
        while (model != null && model is not CharacterModel)
            model = model.GetParent() as Node3D;
        Vector3 world = (model ?? sk).GlobalBasis * modelAxis;
        return (sk.GlobalBasis.Inverse() * world).Normalized();
    }

    /// <summary>Gira o osso em volta de um eixo dado no espaço do esqueleto.</summary>
    private static void Tilt(Skeleton3D sk, int bone, Vector3 axisSkeleton, float angle)
    {
        if (bone < 0 || Mathf.Abs(angle) < 0.00001f)
            return;
        int parent = sk.GetBoneParent(bone);
        Basis parentBasis = parent >= 0 ? sk.GetBoneGlobalPose(parent).Basis : Basis.Identity;
        Vector3 axis = (parentBasis.Inverse() * axisSkeleton).Normalized();
        if (axis.LengthSquared() < 0.5f)
            return;
        sk.SetBonePoseRotation(bone, new Quaternion(axis, angle) * sk.GetBonePoseRotation(bone));
    }
}
