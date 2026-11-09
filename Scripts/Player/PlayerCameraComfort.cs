// Camera de conforto e representação visual do jogador local.
using Godot;

namespace DeadFrequency;

public partial class PlayerController
{
    private SpringArm3D _comfortSpring;
    private Camera3D _comfortCamera;
    private CharacterModel _localCharacter;
    private bool _thirdPerson = true;
    private const float ComfortCameraDistance = 4.2f;
    private const float ComfortCameraHeight = 1.35f;
    private const float ComfortCameraSmooth = 10f;

    private void InitializeComfortCamera()
    {
        _localCharacter = CharacterLook.Local.Build();
        _localCharacter.Name = "LocalCharacter";
        AddChild(_localCharacter);
        _localCharacter.Visible = _thirdPerson;

        _comfortSpring = new SpringArm3D
        {
            Name = "ComfortCameraSpring",
            SpringLength = ComfortCameraDistance,
            Margin = 0.18f,
            CollisionMask = 1 | 4 | 8 | 16,
            Position = new Vector3(0f, ComfortCameraHeight, 0f)
        };
        AddChild(_comfortSpring);

        _comfortCamera = new Camera3D
        {
            Name = "ComfortCamera",
            Current = false,
            Fov = _camera.Fov,
            Near = 0.08f,
            Far = _camera.Far
        };
        _comfortSpring.AddChild(_comfortCamera);
        SetThirdPerson(_thirdPerson, false);
    }

    private Camera3D GetAimCamera()
    {
        return _thirdPerson && _comfortCamera != null ? _comfortCamera : _camera;
    }

    private void HandleComfortCameraInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.V })
        {
            SetThirdPerson(!_thirdPerson, true);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetThirdPerson(bool enabled, bool notify)
    {
        _thirdPerson = enabled;
        if (_localCharacter != null)
            _localCharacter.Visible = enabled;
        Camera3D active = enabled ? _comfortCamera : _camera;
        active?.MakeCurrent();

        if (_flashlight != null && IsInstanceValid(_flashlight))
        {
            Node oldParent = _flashlight.GetParent();
            if (oldParent != active)
            {
                oldParent?.RemoveChild(_flashlight);
                active?.AddChild(_flashlight);
                _flashlight.Position = new Vector3(0.12f, -0.08f, -0.08f);
            }
        }

        if (notify)
            GameHud.Toast(this, enabled ? "Câmera em terceira pessoa (V)" : "Câmera em primeira pessoa (V)", UiStyle.Phosphor, 2.2);
    }

    private void UpdateComfortCamera(float delta)
    {
        if (_comfortCamera == null || _comfortSpring == null)
            return;

        _comfortCamera.Fov = _camera.Fov;
        _comfortSpring.SpringLength = Mathf.Lerp(_comfortSpring.SpringLength, ComfortCameraDistance, 1f - Mathf.Exp(-ComfortCameraSmooth * delta));

        float yaw = State == PlayerState.Walking
            ? GlobalRotation.Y
            : (CurrentVehicle != null ? CurrentVehicle.GlobalRotation.Y : GlobalRotation.Y);
        float pitch = Mathf.Clamp(_head.Rotation.X, -0.55f, 0.55f);
        Vector3 targetPosition = GlobalPosition + Vector3.Up * ComfortCameraHeight;
        Transform3D target = new(Basis.FromEuler(new Vector3(pitch, yaw, 0f)), targetPosition);
        _comfortSpring.GlobalTransform = _comfortSpring.GlobalTransform.InterpolateWith(target, 1f - Mathf.Exp(-ComfortCameraSmooth * delta));

        if (_thirdPerson && _comfortCamera.Current == false)
            _comfortCamera.MakeCurrent();
        else if (!_thirdPerson && _camera.Current == false)
            _camera.MakeCurrent();
    }

    private void UpdateLocalCharacter(float delta)
    {
        if (_localCharacter == null || !IsInstanceValid(_localCharacter))
            return;

        float speed = State == PlayerState.Walking
            ? new Vector2(Velocity.X, Velocity.Z).Length()
            : (CurrentVehicle != null ? CurrentVehicle.LinearVelocity.Length() : 0f);
        _localCharacter.Animate(delta, State == PlayerState.Walking ? speed : 0f);
        _localCharacter.SetHeadPitch(_head.Rotation.X);
        _localCharacter.SetSeated(State == PlayerState.Driving || State == PlayerState.Riding);
    }
}
