// Scripts/Characters/CharacterModel.IK.cs
// Inverse Kinematics dos personagens (v0.10.1), só com os nós novos da Godot 4 (SkeletonModifier3D):
//  - TwoBoneIK3D nos braços (ombro → cotovelo → mão) e nas pernas (coxa → joelho → pé);
//  - LookAtModifier3D na cabeça (NPC olhando o jogador que passa perto);
//  - FootPlacer (pés no chão em degrau, meio-fio e escada), que move os alvos das pernas antes do TwoBoneIK3D.
// Usos: ciclista (mãos no guidão, pés nos pedais), subir a escada de marinheiro (mãos e pés nos degraus),
// jogadores andando em degrau e NPCs virando a cabeça. Nomes dos ossos conferidos nos modelos do Meshy e
// da Quaternius (LeftArm/LeftForeArm/LeftHand, LeftUpLeg/LeftLeg/LeftFoot, Head). Nunca SkeletonIK3D.
// É só visual e local: nada disso vai pela rede (o online manda só o estado, ex.: st=3 subindo escada).
using Godot;

namespace DeadFrequency;

public partial class CharacterModel
{
    private TwoBoneIK3D _armIK, _legIK;
    private LookAtModifier3D _lookIK;
    private FootPlacer _footPlacer;
    private Node3D _ikTargetsRoot;
    private Node3D _handTargetL, _handTargetR, _footTargetL, _footTargetR;
    private float _armGoal, _legGoal, _lookGoal;
    private bool _climbing;
    private float _climbPhase;

    /// <summary>Tem esqueleto (os modelos de caixa de reserva não têm IK).</summary>
    public bool HasIK => _skeleton != null && _boneHandL >= 0;

    /// <summary>Alvo da cabeça (LookAtModifier3D). null = olha para a frente de novo.</summary>
    public Node3D LookTarget
    {
        get => _lookIK?.TargetNode is { IsEmpty: false } p ? _lookIK.GetNodeOrNull<Node3D>(p) : null;
        set
        {
            if (!HasIK)
                return;
            EnsureLook();
            if (_lookIK == null)
                return;
            // null: só sai devagar (mantém o alvo antigo enquanto a influência cai, sem estalo).
            if (value != null && value.IsInsideTree())
                _lookIK.TargetNode = _lookIK.GetPathTo(value);
            _lookGoal = value != null ? 1f : 0f;
        }
    }

    /// <summary>Pés acompanham o chão (degrau, meio-fio, escada). Ligado nos jogadores.</summary>
    public bool FootGrounding
    {
        get => _footPlacer != null && _footPlacer.Enabled;
        set
        {
            if (!HasIK)
                return;
            if (value)
            {
                EnsureLimbs();
                _footPlacer.Enabled = true;
                if (!_footPlacer.Manual)
                    _legGoal = 1f;
            }
            else if (_footPlacer != null)
            {
                _footPlacer.Enabled = false;
                if (!_footPlacer.Manual)
                    _legGoal = 0f;
            }
        }
    }

    /// <summary>Mãos seguindo dois nós do mundo (ex.: guidão da bicicleta). null, null = mãos da animação.</summary>
    public void SetHandTargets(Node3D left, Node3D right)
    {
        if (!HasIK)
            return;
        EnsureLimbs();
        if (left == null || right == null)
        {
            _armGoal = 0f;
            return;
        }
        PointSetting(_armIK, 0, left);
        PointSetting(_armIK, 1, right);
        _armGoal = 1f;
    }

    /// <summary>Pés seguindo dois nós do mundo (ex.: pedais). null, null = pés da animação (ou no chão, se FootGrounding).</summary>
    public void SetFootTargets(Node3D left, Node3D right)
    {
        if (!HasIK)
            return;
        EnsureLimbs();
        if (left == null || right == null)
        {
            PointSetting(_legIK, 0, _footTargetL);
            PointSetting(_legIK, 1, _footTargetR);
            _footPlacer.Manual = false;
            _legGoal = _footPlacer.Enabled ? 1f : 0f;
            return;
        }
        _footPlacer.Manual = true;
        PointSetting(_legIK, 0, left);
        PointSetting(_legIK, 1, right);
        _legGoal = 1f;
    }

    /// <summary>
    /// Subindo/descendo a escada de marinheiro: mãos e pés vão de degrau em degrau (alternando lados).
    /// phase = metros já subidos (a altura do personagem serve). A escada fica a ~0,35 m na frente.
    /// </summary>
    public void SetClimbing(bool climbing, float phase = 0f)
    {
        if (!HasIK)
            return;
        EnsureLimbs();
        _climbPhase = phase;
        if (climbing == _climbing)
            return;
        _climbing = climbing;
        if (climbing)
        {
            Play(_idle, 0.2f);
            PointSetting(_armIK, 0, _handTargetL);
            PointSetting(_armIK, 1, _handTargetR);
            _footPlacer.Manual = true;
            PointSetting(_legIK, 0, _footTargetL);
            PointSetting(_legIK, 1, _footTargetR);
            _armGoal = _legGoal = 1f;
        }
        else
        {
            _footPlacer.Manual = false;
            _armGoal = 0f;
            _legGoal = _footPlacer.Enabled ? 1f : 0f;
        }
    }

    public bool Climbing => _climbing;

    private float _lookCheck;

    /// <summary>NPC que conversa (Chatty): vira a cabeça para o jogador que chega perto (até ~5 m).</summary>
    private void UpdateLookAtPlayer(float dt)
    {
        _lookCheck -= dt;
        if (_lookCheck > 0f)
            return;
        _lookCheck = 0.3f;
        PlayerController player = ProfessionManager.Instance?.Player;
        Camera3D cam = GetViewport()?.GetCamera3D();
        bool near = player != null && IsInstanceValid(player) && cam != null && Visible
            && player.State == PlayerState.Walking
            && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 25f;
        if (near && (_lookGoal < 1f || LookTarget != cam))
            LookTarget = cam;
        else if (!near && _lookGoal > 0f)
            LookTarget = null;
    }

    private const float RungStep = 0.3f;

    /// <summary>Degraus de 30 cm: cada mão/pé fica num degrau e troca de degrau na sua vez.</summary>
    private void UpdateClimbTargets()
    {
        // Cada lado sobe dois degraus por ciclo; os lados estão defasados meio ciclo (mão esquerda com pé direito).
        float cycle = _climbPhase / (RungStep * 2f);
        float a = SteppedRise(cycle), b = SteppedRise(cycle + 0.5f);
        float baseY = -Mathf.PosMod(_climbPhase, RungStep * 2f);
        const float z = -0.3f;
        _handTargetL.Position = new Vector3(-0.2f, 1.62f + baseY + a, z);
        _handTargetR.Position = new Vector3(0.2f, 1.62f + baseY + b, z);
        _footTargetL.Position = new Vector3(-0.12f, 0.12f + baseY + b, z + 0.08f);
        _footTargetR.Position = new Vector3(0.12f, 0.12f + baseY + a, z + 0.08f);
    }

    /// <summary>0 até 2 degraus por ciclo: parado na maior parte do tempo, sobe rápido para o próximo degrau.</summary>
    private static float SteppedRise(float cycle)
    {
        float f = Mathf.PosMod(cycle, 1f);
        float t = Mathf.Clamp((f - 0.6f) / 0.4f, 0f, 1f);
        return RungStep * 2f * Mathf.SmoothStep(0f, 1f, t);
    }

    /// <summary>Chamado no _Process: entra/sai o IK suave (sem estalo) e atualiza a escada.</summary>
    private void UpdateIK(float dt)
    {
        if (Chatty && HasIK)
            UpdateLookAtPlayer(dt);
        if (_armIK == null && _lookIK == null)
            return;
        if (_climbing)
            UpdateClimbTargets();
        float k = 1f - Mathf.Exp(-10f * dt);
        if (_armIK != null)
        {
            _armIK.Influence = Mathf.Lerp(_armIK.Influence, _armGoal, k);
            _armIK.Active = _armIK.Influence > 0.01f;
        }
        if (_legIK != null)
        {
            _legIK.Influence = Mathf.Lerp(_legIK.Influence, _legGoal, k);
            _legIK.Active = _legIK.Influence > 0.01f;
        }
        if (_lookIK != null)
        {
            _lookIK.Influence = Mathf.Lerp(_lookIK.Influence, _lookGoal, k * 0.6f);
            _lookIK.Active = _lookIK.Influence > 0.01f;
        }
    }

    private void EnsureLimbs()
    {
        if (_armIK != null)
            return;
        _ikTargetsRoot = new Node3D { Name = "IKTargets" };
        AddChild(_ikTargetsRoot);
        _handTargetL = Marker("HandL", new Vector3(-0.3f, 1.0f, -0.3f));
        _handTargetR = Marker("HandR", new Vector3(0.3f, 1.0f, -0.3f));
        _footTargetL = Marker("FootL", new Vector3(-0.12f, 0.08f, 0f));
        _footTargetR = Marker("FootR", new Vector3(0.12f, 0.08f, 0f));
        // Polos: cotovelos para fora e para trás, joelhos para a frente (nossa frente é -Z).
        Node3D elbowL = Marker("PoleElbowL", new Vector3(-0.7f, 0.9f, 0.6f));
        Node3D elbowR = Marker("PoleElbowR", new Vector3(0.7f, 0.9f, 0.6f));
        Node3D kneeL = Marker("PoleKneeL", new Vector3(-0.2f, 0.6f, -1.2f));
        Node3D kneeR = Marker("PoleKneeR", new Vector3(0.2f, 0.6f, -1.2f));

        // Ordem dos filhos do Skeleton3D = ordem do processamento: FootPlacer antes do IK das pernas.
        _footPlacer = new FootPlacer { Name = "FootPlacer", Enabled = false };
        _skeleton.AddChild(_footPlacer);
        _footPlacer.Setup(this, _footTargetL, _footTargetR);

        _armIK = NewTwoBone("ArmIK");
        AddSetting(_armIK, 0, "LeftArm", "LeftForeArm", "LeftHand", _handTargetL, elbowL);
        AddSetting(_armIK, 1, "RightArm", "RightForeArm", "RightHand", _handTargetR, elbowR);
        _legIK = NewTwoBone("LegIK");
        AddSetting(_legIK, 0, "LeftUpLeg", "LeftLeg", "LeftFoot", _footTargetL, kneeL);
        AddSetting(_legIK, 1, "RightUpLeg", "RightLeg", "RightFoot", _footTargetR, kneeR);
    }

    private Node3D Marker(string name, Vector3 local)
    {
        var m = new Node3D { Name = name, Position = local };
        _ikTargetsRoot.AddChild(m);
        return m;
    }

    private TwoBoneIK3D NewTwoBone(string name)
    {
        var ik = new TwoBoneIK3D { Name = name, Active = false, Influence = 0f };
        _skeleton.AddChild(ik);
        ik.SettingCount = 2;
        return ik;
    }

    private static void AddSetting(TwoBoneIK3D ik, int i, string root, string middle, string end, Node3D target, Node3D pole)
    {
        ik.SetRootBoneName(i, root);
        ik.SetMiddleBoneName(i, middle);
        ik.SetEndBoneName(i, end);
        ik.SetTargetNode(i, ik.GetPathTo(target));
        ik.SetPoleNode(i, ik.GetPathTo(pole));
    }

    private static void PointSetting(TwoBoneIK3D ik, int i, Node3D target)
    {
        if (target != null && target.IsInsideTree() && ik.IsInsideTree())
            ik.SetTargetNode(i, ik.GetPathTo(target));
    }

    private void EnsureLook()
    {
        if (_lookIK != null || _boneHead < 0)
            return;
        _lookIK = new LookAtModifier3D
        {
            Name = "HeadLook",
            BoneName = "Head",
            ForwardAxis = SkeletonModifier3D.BoneAxis.PlusY, // conferido em render: o eixo +Y do osso Head aponta para o rosto
            PrimaryRotationAxis = Vector3.Axis.Y,
            UseSecondaryRotation = true,
            UseAngleLimitation = true,
            SymmetryLimitation = true,
            PrimaryLimitAngle = Mathf.DegToRad(140f),
            SecondaryLimitAngle = Mathf.DegToRad(50f),
            Duration = 0.35f,
            TransitionType = Tween.TransitionType.Sine,
            EaseType = Tween.EaseType.InOut,
            Active = false,
            Influence = 0f,
        };
        _skeleton.AddChild(_lookIK);
    }
}
