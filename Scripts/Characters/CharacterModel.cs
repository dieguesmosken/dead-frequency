// Scripts/Characters/CharacterModel.cs
// Personagem dos outros jogadores, pedestres, comerciantes, passageiros e da prévia do inventário.
// v0.9.4: usa os modelos animados da Quaternius (Man/Woman Animated, CC0) em Assets/Quaternius/Characters.
// A textura deles é uma paleta 32x32 em faixas (pele, olhos, cabelo, camisa, calça, sapato), então a
// roupa de cada boneco é só uma paleta gerada aqui. Se o modelo não existir, cai no boneco de caixas antigo.
// Equipamentos (óculos, luvas, mochila, radar) seguem os ossos da cabeça, das mãos e das costas.
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public partial class CharacterModel : Node3D
{
    public const string ManPath = "res://Assets/Quaternius/Characters/man.fbx";
    public const string WomanPath = "res://Assets/Quaternius/Characters/woman.fbx";
    /// <summary>Os modelos vêm com ~5,2 unidades de altura; 0,34 deixa com ~1,77 m.</summary>
    private const float ModelScale = 0.34f;

    public Color Shirt { get; private set; } = new(0.3f, 0.35f, 0.4f);
    public Color Pants { get; private set; } = new(0.15f, 0.15f, 0.2f);
    public Color Skin { get; private set; } = new(0.78f, 0.6f, 0.45f);
    /// <summary>Colete refletivo de entregador (jogadores).</summary>
    public Color? Vest { get; private set; }
    public bool IsWoman { get; private set; }
    public Color Hair { get; private set; }

    /// <summary>Ponto da cabeça (óculos e outros itens de cabeça ficam aqui).</summary>
    public Node3D Head { get; private set; }
    private Node3D _leftHand, _rightHand, _leftForearm, _back, _leftFoot, _rightFoot;
    private Node3D _equipmentRoot;
    private readonly Dictionary<string, Node3D> _equipment = new();
    private string _equipmentKey = "";
    private float _headPitch;

    /// <summary>Fala com o jogador quando ele passa perto (pedestres, comerciantes).</summary>
    public bool Chatty { get; set; }
    /// <summary>Frases próprias (ex.: o comerciante oferecendo produtos); vazio = frases da calçada.</summary>
    public string[] Lines { get; set; }
    private float _talkCheck, _talkCooldown;
    private bool _mountsReady;

    // Modelo animado
    private Skeleton3D _skeleton;
    private AnimationPlayer _anim;
    private string _idle, _walk, _run, _work;
    private string _current = "";
    private int _boneHead = -1, _boneHandL = -1, _boneHandR = -1, _boneForearmL = -1, _boneSpine = -1, _boneFootL = -1, _boneFootR = -1;

    // Boneco de caixas (reserva)
    private Node3D _leftArm, _rightArm, _leftLeg, _rightLeg, _torso;
    private float _walkPhase;
    private bool _boxModel;

    private static BoxMesh _box;
    private static BoxMesh Box => _box ??= new BoxMesh { Size = Vector3.One };
    private static readonly Dictionary<string, PackedScene> Scenes = new();

    private static readonly Color[] HairColors =
    {
        new(0.08f, 0.06f, 0.05f), new(0.2f, 0.12f, 0.07f), new(0.36f, 0.22f, 0.1f),
        new(0.55f, 0.38f, 0.18f), new(0.75f, 0.6f, 0.32f), new(0.45f, 0.45f, 0.45f), new(0.5f, 0.16f, 0.08f),
    };

    /// <summary>Cria um boneco. variant escolhe homem/mulher e cabelo; -1 = pelas cores.</summary>
    /// <param name="variant">Escolhe homem/mulher e cabelo. -1 = sorteado pelas cores (sempre o mesmo para as mesmas cores).</param>
    public static CharacterModel Create(Color shirt, Color pants, Color skin, Color? vest = null, int variant = -1)
    {
        var model = new CharacterModel { Name = "CharacterModel" };
        model.Setup(shirt, pants, skin, vest ?? Colors.Transparent, variant);
        return model;
    }

    /// <summary>Monta o boneco (usado pelo Create; vest transparente = sem colete).</summary>
    public void Setup(Color shirt, Color pants, Color skin, Color vest, int variant = -1)
    {
        if (variant < 0)
            variant = (int)((uint)(shirt.ToRgba32() * 2654435761u ^ pants.ToRgba32() * 40503u ^ skin.ToRgba32()) >> 3);
        Shirt = shirt;
        Pants = pants;
        Skin = skin;
        Vest = vest.A > 0f ? vest : null;
        IsWoman = variant % 2 == 1;
        Hair = HairColors[(variant / 2) % HairColors.Length];
        Build();
    }

    private static PackedScene LoadScene(string path)
    {
        if (Scenes.TryGetValue(path, out PackedScene s))
            return s;
        s = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        Scenes[path] = s;
        return s;
    }

    private void Build()
    {
        Head = new Node3D { Name = "Head", Position = new Vector3(0f, 1.6f, 0f) };
        AddChild(Head);
        _equipmentRoot = new Node3D { Name = "Equipment" };
        AddChild(_equipmentRoot);

        if (LoadScene(IsWoman ? WomanPath : ManPath)?.Instantiate() is Node3D body)
        {
            body.Name = "Body";
            body.Scale = Vector3.One * ModelScale;
            body.Rotation = new Vector3(0f, Mathf.Pi, 0f); // o modelo olha para +Z; o nosso "frente" é -Z
            AddChild(body);
            _skeleton = body.FindChild("Skeleton3D", true, false) as Skeleton3D;
            _anim = body.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            ApplyPalette(body);
            SetupAnimations();
            if (_skeleton != null)
            {
                _boneHead = _skeleton.FindBone("Head");
                _boneHandL = _skeleton.FindBone("LeftHand");
                _boneHandR = _skeleton.FindBone("RightHand");
                _boneForearmL = _skeleton.FindBone("LeftForeArm");
                _boneSpine = _skeleton.FindBone("Spine2");
                _boneFootL = _skeleton.FindBone("LeftFoot");
                _boneFootR = _skeleton.FindBone("RightFoot");
            }
        }
        else
        {
            BuildBoxModel();
        }

        _leftHand = Mount("HandL");
        _rightHand = Mount("HandR");
        _leftForearm = Mount("ForearmL");
        _back = Mount("Back");
        // O modelo masculino é descalço na textura: um par de tênis segue os pés.
        if (!_boxModel && !IsWoman && _boneFootL >= 0)
        {
            _leftFoot = Mount("FootL");
            _rightFoot = Mount("FootR");
            StandardMaterial3D shoe = Mat(new Color(0.07f, 0.07f, 0.08f));
            foreach (Node3D f in new[] { _leftFoot, _rightFoot })
                Part(f, new Vector3(0f, 0.1f, 0.005f), new Vector3(0.11f, 0.27f, 0.1f), shoe);
        }
        if (_boxModel)
        {
            _leftHand.Position = new Vector3(0.33f, 1.0f, 0f);
            _rightHand.Position = new Vector3(-0.33f, 1.0f, 0f);
            _leftForearm.Position = new Vector3(0.35f, 1.15f, 0f);
            _back.Position = new Vector3(0f, 1.25f, 0f);
        }
        UpdateMounts();
    }

    private Node3D Mount(string name)
    {
        var n = new Node3D { Name = name };
        AddChild(n);
        return n;
    }

    // ---------------- Paleta (roupa) ----------------

    private void ApplyPalette(Node3D body)
    {
        Color shirt = Vest ?? Shirt;
        Color shoes = new(0.07f, 0.07f, 0.07f);
        // Faixas (de cima para baixo na textura): pele, olhos, cabelo, camisa, calça, [sapato].
        float[] cuts = IsWoman ? new[] { 0.19f, 0.34f, 0.53f, 0.72f, 0.88f, 1f } : new[] { 0.19f, 0.34f, 0.53f, 0.72f, 1f };
        Color[] colors = IsWoman ? new[] { Skin, new Color(0.08f, 0.07f, 0.07f), Hair, shirt, Pants, shoes } : new[] { Skin, new Color(0.08f, 0.07f, 0.07f), Hair, shirt, Pants };

        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgb8);
        for (int y = 0; y < 32; y++)
        {
            float v = (y + 0.5f) / 32f;
            int band = 0;
            while (band < cuts.Length - 1 && v > cuts[band])
                band++;
            for (int x = 0; x < 32; x++)
            {
                img.SetPixel(x, y, colors[band]);
            }
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Roughness = 0.9f,
        };
        foreach (Node n in body.FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).MaterialOverride = mat;
    }

    private void SetupAnimations()
    {
        if (_anim == null)
            return;
        foreach (string a in _anim.GetAnimationList())
        {
            string s = a.Contains('|') ? a[(a.IndexOf('|') + 1)..] : a;
            switch (s)
            {
                case "Idle": _idle = a; break;
                case "Walk" or "Walking": _walk = a; break;
                case "Run" or "Running": _run = a; break;
                case "Working" or "PickUp": _work = a; break;
                case "SitIdle" or "SittingIdle" or "Sit" or "Sitting" or "Sit_Idle": _sit = a; break;
                default: continue;
            }
            _anim.GetAnimation(a).LoopMode = Animation.LoopModeEnum.Linear;
        }
        Play(_idle, 0f);
        // Cada boneco começa num ponto diferente da animação (pedestres não ficam sincronizados).
        if (_idle != null)
            _anim.Seek(GD.Randf() * (float)_anim.GetAnimation(_idle).Length, true);
    }

    private void Play(string name, float blend)
    {
        if (_anim == null || name == null || name == _current)
            return;
        _current = name;
        _anim.Play(name, blend);
    }

    /// <summary>Lista de equipamentos montados (para trocar de boneco sem perder).</summary>
    public string EquipmentKey => _equipmentKey;

    private bool _seated;
    private string _sit;

    /// <summary>Sentado num banco (motorista/carona): pose de sentado ou, sem ela, o corpo afunda no banco.</summary>
    public void SetSeated(bool seated)
    {
        if (_seated == seated)
            return;
        _seated = seated;
        if (GetNodeOrNull<Node3D>("Body") is Node3D body)
            body.Position = new Vector3(0f, seated && _sit == null ? -0.45f : (seated ? 0.05f : 0f), seated ? 0.1f : 0f);
        if (seated && _sit != null)
            Play(_sit, 0.2f);
        else if (!seated)
            Play(_idle, 0.2f);
    }

    /// <summary>Animação de trabalho (comerciante, repositor...). Volta ao normal no próximo Animate com velocidade.</summary>
    public void PlayWork() => Play(_work ?? _idle, 0.3f);

    // ---------------- Animação ----------------

    /// <summary>Caminhada/corrida pela velocidade em m/s (0 = parado).</summary>
    public void Animate(float delta, float speed)
    {
        if (_boxModel)
        {
            AnimateBoxes(delta, speed);
            return;
        }
        if (_anim == null)
            return;
        if (_seated)
            return;
        if (speed < 0.25f)
        {
            if (_current != _work)
                Play(_idle, 0.25f);
            _anim.SpeedScale = 1f;
        }
        else if (speed < 3.6f || _run == null)
        {
            Play(_walk ?? _idle, 0.2f);
            _anim.SpeedScale = Mathf.Clamp(speed / 1.45f, 0.5f, 2.2f);
        }
        else
        {
            Play(_run, 0.2f);
            _anim.SpeedScale = Mathf.Clamp(speed / 5f, 0.7f, 1.6f);
        }
    }

    public void SetHeadPitch(float pitch)
    {
        _headPitch = Mathf.Clamp(pitch, -0.8f, 0.8f);
        if (_boxModel)
            Head.Rotation = new Vector3(_headPitch, 0f, 0f);
    }

    public override void _Process(double delta)
    {
        if (Chatty)
            UpdateTalk((float)delta);
        if (!_boxModel && (_equipment.Count > 0 || _leftFoot != null || !_mountsReady))
        {
            _mountsReady = true;
            UpdateMounts();
        }
    }

    private void UpdateTalk(float dt)
    {
        _talkCooldown -= dt;
        _talkCheck -= dt;
        if (_talkCheck > 0f || _talkCooldown > 0f || !Visible)
            return;
        _talkCheck = 0.4f;
        PlayerController player = ProfessionManager.Instance?.Player;
        if (player == null || !IsInstanceValid(player) || player.State != PlayerState.Walking)
            return;
        if (player.GlobalPosition.DistanceSquaredTo(GlobalPosition) > 3.6f * 3.6f || !SidewalkTalk.CanTalkNow())
            return;
        _talkCooldown = (float)GD.RandRange(18.0, 35.0);
        if (GD.Randf() > 0.7f)
            return; // às vezes a pessoa só passa calada
        string line = Lines != null && Lines.Length > 0 && GD.Randf() < 0.6f ? Lines[GD.Randi() % (uint)Lines.Length] : SidewalkTalk.LineFor(player);
        SidewalkTalk.Say(this, line, 2.05f * Scale.Y);
    }

    /// <summary>Leva os pontos de encaixe para onde os ossos estão neste quadro.</summary>
    private void UpdateMounts()
    {
        if (_skeleton == null || !IsInsideTree())
            return;
        Transform3D toLocal = GlobalTransform.AffineInverse() * _skeleton.GlobalTransform;
        Vector3 BonePos(int b) => (toLocal * _skeleton.GetBoneGlobalPose(b)).Origin;

        if (_boneHead >= 0)
        {
            // A cabeça fica "em pé" (só a inclinação de olhar para cima/baixo); centro ~10 cm acima do osso.
            Head.Position = BonePos(_boneHead) + new Vector3(0f, 0.13f, 0f);
            Head.Rotation = new Vector3(_headPitch, 0f, 0f);
        }
        if (_boneSpine >= 0)
        {
            // As costas seguem o tronco inteiro (inclina junto quando anda/corre). O eixo Y do osso aponta
            // ao longo da coluna; montamos uma base com esse "cima" e o +Z do boneco (= costas).
            Transform3D t = toLocal * _skeleton.GetBoneGlobalPose(_boneSpine);
            // Metade da inclinação do osso: na corrida a coluna dobra muito e a bag tamparia a cabeça.
            Vector3 up = (t.Basis.Y.Normalized() + Vector3.Up).Normalized();
            Vector3 back = (Vector3.Back - up * up.Dot(Vector3.Back)).Normalized();
            Vector3 side = up.Cross(back).Normalized();
            _back.Transform = new Transform3D(new Basis(side, up, back), t.Origin);
        }
        FollowBone(_leftHand, _boneHandL, toLocal);
        FollowBone(_rightHand, _boneHandR, toLocal);
        FollowBone(_leftForearm, _boneForearmL, toLocal);
        if (_leftFoot != null)
        {
            FollowBone(_leftFoot, _boneFootL, toLocal);
            FollowBone(_rightFoot, _boneFootR, toLocal);
        }
    }

    private void FollowBone(Node3D mount, int bone, Transform3D toLocal)
    {
        if (bone < 0)
            return;
        Transform3D t = toLocal * _skeleton.GetBoneGlobalPose(bone);
        mount.Transform = new Transform3D(t.Basis.Orthonormalized(), t.Origin);
    }

    // ---------------- Equipamentos ----------------

    /// <summary>Recebe "radar,gloves,..." e monta só o que mudou.</summary>
    public void SetEquipment(string csv)
    {
        csv ??= "";
        if (csv == _equipmentKey)
            return;
        _equipmentKey = csv;
        var wanted = new HashSet<string>(csv.Split(',', System.StringSplitOptions.RemoveEmptyEntries));

        foreach (string id in new List<string>(_equipment.Keys))
        {
            if (!wanted.Contains(id))
            {
                _equipment[id].QueueFree();
                _equipment.Remove(id);
            }
        }
        foreach (string id in wanted)
        {
            if (!_equipment.ContainsKey(id))
            {
                Node3D item = BuildItem(id);
                if (item != null)
                    _equipment[id] = item;
            }
        }
        UpdateMounts();
    }

    private Node3D BuildItem(string id)
    {
        ItemDef def = ItemCatalog.Get(id);
        if (def == null)
            return null;
        StandardMaterial3D color = Mat(def.Color);
        StandardMaterial3D dark = Mat(new Color(0.08f, 0.08f, 0.08f));
        var root = new Node3D { Name = id };

        switch (id)
        {
            case ItemCatalog.NightVision:
                Head.AddChild(root);
                Part(root, new Vector3(0f, 0.03f, -0.13f), new Vector3(0.22f, 0.08f, 0.06f), dark);
                var lens = Mat(def.Color);
                lens.EmissionEnabled = true;
                lens.Emission = def.Color;
                lens.EmissionEnergyMultiplier = 1.5f;
                Part(root, new Vector3(0.05f, 0.03f, -0.18f), new Vector3(0.06f, 0.06f, 0.07f), lens);
                Part(root, new Vector3(-0.05f, 0.03f, -0.18f), new Vector3(0.06f, 0.06f, 0.07f), lens);
                Part(root, new Vector3(0f, 0.06f, -0.01f), new Vector3(0.24f, 0.03f, 0.25f), dark); // tira
                break;
            case ItemCatalog.Gloves:
                _equipmentRoot.AddChild(root);
                // O osso da mão aponta para os dedos (+Y local).
                Part(_leftHand, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.11f, 0.06f), color).Name = "GloveL";
                Part(_rightHand, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.11f, 0.06f), color).Name = "GloveR";
                root.TreeExiting += () =>
                {
                    _leftHand.GetNodeOrNull("GloveL")?.QueueFree();
                    _rightHand.GetNodeOrNull("GloveR")?.QueueFree();
                };
                break;
            case ItemCatalog.Backpack:
            {
                // Bag de entregador (v0.9.7): caixa térmica quadrada, encostada nas costas, com alças
                // que passam por cima do ombro, tampa com zíper e faixa refletiva.
                _back.AddChild(root);
                StandardMaterial3D strip = Mat(new Color(0.85f, 0.85f, 0.8f));
                strip.EmissionEnabled = true;
                strip.Emission = new Color(0.4f, 0.4f, 0.38f);
                const float s = 0.44f, d = 0.38f, back = 0.11f;
                Vector3 c = new(0f, -0.14f, back + d * 0.5f);
                Part(root, c, new Vector3(s, s, d), color);
                Part(root, c + new Vector3(0f, s * 0.5f + 0.012f, 0f), new Vector3(s + 0.02f, 0.025f, d + 0.02f), dark); // tampa
                Part(root, c + new Vector3(0f, s * 0.5f - 0.03f, 0f), new Vector3(s + 0.01f, 0.02f, d + 0.01f), dark); // zíper
                Part(root, c + new Vector3(0f, -0.06f, d * 0.5f + 0.004f), new Vector3(s * 0.8f, 0.05f, 0.01f), strip); // refletivo
                Part(root, c + new Vector3(0f, -s * 0.5f, 0f), new Vector3(s + 0.01f, 0.02f, d + 0.01f), dark); // fundo
                foreach (float x in new[] { -0.11f, 0.11f })
                {
                    // Alças: descem pelas costas, passam por cima do ombro e voltam na frente do peito.
                    Part(root, new Vector3(x, 0.02f, back - 0.005f), new Vector3(0.045f, 0.4f, 0.02f), dark);
                    Part(root, new Vector3(x, 0.2f, -0.02f), new Vector3(0.045f, 0.02f, 0.18f), dark);
                    Part(root, new Vector3(x, 0.06f, -0.11f), new Vector3(0.045f, 0.28f, 0.02f), dark);
                }
                break;
            }
            case ItemCatalog.Radar:
                _leftForearm.AddChild(root);
                Part(root, new Vector3(0f, 0.17f, 0f), new Vector3(0.09f, 0.08f, 0.09f), dark);
                var screen = Mat(def.Color);
                screen.EmissionEnabled = true;
                screen.Emission = def.Color;
                screen.EmissionEnergyMultiplier = 2f;
                Part(root, new Vector3(0f, 0.17f, 0.05f), new Vector3(0.07f, 0.06f, 0.01f), screen);
                Part(root, new Vector3(0f, 0.17f, -0.05f), new Vector3(0.07f, 0.06f, 0.01f), screen);
                break;
            default:
                _equipmentRoot.AddChild(root);
                break;
        }
        return root;
    }

    private static MeshInstance3D Part(Node3D parent, Vector3 position, Vector3 size, Material material)
    {
        var mesh = new MeshInstance3D { Mesh = Box, Scale = size, Position = position, MaterialOverride = material };
        parent.AddChild(mesh);
        return mesh;
    }

    private static StandardMaterial3D Mat(Color color) => new() { AlbedoColor = color, Roughness = 0.85f };

    // ---------------- Boneco de caixas (reserva, se o modelo não carregar) ----------------

    private void BuildBoxModel()
    {
        _boxModel = true;
        StandardMaterial3D shirt = Mat(Shirt), pants = Mat(Pants), skin = Mat(Skin), shoes = Mat(new Color(0.08f, 0.08f, 0.08f));
        _torso = new Node3D { Position = new Vector3(0f, 0.95f, 0f) };
        AddChild(_torso);
        Part(_torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 0.28f), Vest.HasValue ? Mat(Vest.Value) : shirt);
        Head.Reparent(_torso, false);
        Head.Position = new Vector3(0f, 0.85f, 0f);
        Part(Head, Vector3.Zero, new Vector3(0.28f, 0.28f, 0.28f), skin);
        Part(Head, new Vector3(0f, 0.16f, 0.02f), new Vector3(0.3f, 0.07f, 0.3f), Mat(Hair));
        _leftArm = Limb(_torso, new Vector3(0.33f, 0.58f, 0f), new Vector3(0.14f, 0.56f, 0.14f), shirt, skin);
        _rightArm = Limb(_torso, new Vector3(-0.33f, 0.58f, 0f), new Vector3(0.14f, 0.56f, 0.14f), shirt, skin);
        _leftLeg = Limb(this, new Vector3(0.13f, 0.95f, 0f), new Vector3(0.18f, 0.9f, 0.2f), pants, shoes);
        _rightLeg = Limb(this, new Vector3(-0.13f, 0.95f, 0f), new Vector3(0.18f, 0.9f, 0.2f), pants, shoes);
    }

    private static Node3D Limb(Node3D parent, Vector3 pivot, Vector3 size, Material main, Material tip)
    {
        var joint = new Node3D { Position = pivot };
        parent.AddChild(joint);
        Part(joint, new Vector3(0f, -size.Y * 0.45f, 0f), new Vector3(size.X, size.Y * 0.9f, size.Z), main);
        Part(joint, new Vector3(0f, -size.Y * 0.95f, -size.Z * 0.15f), new Vector3(size.X * 1.05f, size.Y * 0.12f, size.Z * 1.3f), tip);
        return joint;
    }

    private void AnimateBoxes(float delta, float speed)
    {
        float amount = Mathf.Clamp(speed / 4f, 0f, 1f);
        _walkPhase += delta * (4f + speed * 2.2f);
        float swing = Mathf.Sin(_walkPhase) * 0.6f * amount;
        _leftLeg.Rotation = new Vector3(swing, 0f, 0f);
        _rightLeg.Rotation = new Vector3(-swing, 0f, 0f);
        _leftArm.Rotation = new Vector3(-swing * 0.8f, 0f, 0f);
        _rightArm.Rotation = new Vector3(swing * 0.8f, 0f, 0f);
        _torso.Position = new Vector3(0f, 0.95f + Mathf.Abs(Mathf.Cos(_walkPhase)) * 0.04f * amount, 0f);
    }
}
