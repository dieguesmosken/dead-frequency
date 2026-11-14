// Scripts/Player/PlayerController.Climb.cs
// Escada de marinheiro (v0.10.1): em vez de teleportar, o jogador sobe/desce de verdade.
// W sobe, S desce, E (ou pular) solta no ponto mais perto (pé ou topo). Dá para olhar em volta lá de cima.
// O boneco (terceira pessoa e no online, st=3) usa o IK de escada do CharacterModel: mãos e pés nos degraus.
using Godot;

namespace DeadFrequency;

public partial class PlayerController
{
    private const float ClimbSpeed = 1.6f;

    /// <summary>Subindo/descendo uma escada agora.</summary>
    public bool IsClimbing { get; private set; }

    private Vector3 _climbBottom, _climbTop, _climbExitTop, _climbExitBottom;
    private float _climbYaw, _climbT, _climbExitYawTop, _climbExitYawBottom;
    private float _climbGrace;

    /// <summary>
    /// Começa a escada. bottom/top = onde o corpo fica no pé e no topo da escada (encostado nela);
    /// exitTop/exitBottom = onde sai andando; yaw = virado para a escada. fromTop = começa lá em cima.
    /// </summary>
    public void StartClimb(Vector3 bottom, Vector3 top, float yaw, Vector3 exitTop, float exitTopYaw,
        Vector3 exitBottom, float exitBottomYaw, bool fromTop)
    {
        if (State != PlayerState.Walking || _inTransition || IsClimbing || IsHolding)
            return;
        _climbBottom = bottom;
        _climbTop = top;
        _climbExitTop = exitTop;
        _climbExitBottom = exitBottom;
        _climbYaw = yaw;
        _climbExitYawTop = exitTopYaw;
        _climbExitYawBottom = exitBottomYaw;
        _climbT = fromTop ? 1f : 0f;
        _climbGrace = 0.35f;
        IsClimbing = true;
        Velocity = Vector3.Zero;
        Rotation = new Vector3(0f, yaw, 0f);
        GlobalPosition = fromTop ? top : bottom;
        ResetPhysicsInterpolation();
    }

    /// <summary>Roda no lugar do ProcessMovement enquanto está na escada.</summary>
    private void ProcessClimb(float dt)
    {
        float length = Mathf.Max(0.5f, _climbBottom.DistanceTo(_climbTop));
        float input = InputEnabled ? Input.GetAxis("move_back", "move_forward") : 0f;
        _climbT = Mathf.Clamp(_climbT + input * ClimbSpeed * dt / length, 0f, 1f);
        GlobalPosition = _climbBottom.Lerp(_climbTop, _climbT);
        Velocity = Vector3.Zero;
        UpdateNeeds(dt, false);

        _climbGrace -= dt;
        bool letGo = _climbGrace <= 0f && InputEnabled && (Input.IsActionJustPressed("interact") || Input.IsActionJustPressed("jump"));
        if (_climbT >= 1f && input > 0f)
            EndClimb(true);
        else if (_climbT <= 0f && input < 0f)
            EndClimb(false);
        else if (letGo)
            EndClimb(_climbT > 0.5f);
    }

    private void EndClimb(bool atTop)
    {
        IsClimbing = false;
        GlobalPosition = atTop ? _climbExitTop : _climbExitBottom;
        Rotation = new Vector3(0f, atTop ? _climbExitYawTop : _climbExitYawBottom, 0f);
        Velocity = Vector3.Zero;
        ResetPhysicsInterpolation();
    }

    /// <summary>Altura subida (o IK da escada usa para escolher os degraus).</summary>
    public float ClimbPhase => _climbBottom.Lerp(_climbTop, _climbT).Y - _climbBottom.Y;
}
