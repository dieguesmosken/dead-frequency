using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    private static AudioStreamWav _waterFlow;

    private void BuildStreetDrainage()
    {
        if (_useLogicalBlockPlans)
            return; // Drenos e bueiros são criados por chunk.

        var root = new Node3D { Name = "StreetDrainage" };
        _staticRoot.AddChild(root);

        StandardMaterial3D grateDark = Flat(new Color(0.025f, 0.03f, 0.03f), 0.65f);
        StandardMaterial3D grateMetal = Flat(new Color(0.36f, 0.38f, 0.36f), 0.55f);
        StandardMaterial3D manholeRim = Flat(new Color(0.28f, 0.28f, 0.26f), 0.75f);
        AudioStreamWav waterFlow = _waterFlow ??= CreateWaterFlow();

        for (int bx = -Radius; bx <= Radius; bx++)
        {
            for (int bz = -Radius; bz <= Radius; bz++)
            {
                Vector3 blockCenter = BlockCenter(bx, bz);
                int hash = AssetProps.Hash(blockCenter, 53);

                if (hash % 2 == 0)
                {
                    int sideIndex = hash / 3 % 4;
                    Vector3 outward = sideIndex switch
                    {
                        0 => Vector3.Right,
                        1 => Vector3.Left,
                        2 => Vector3.Back,
                        _ => Vector3.Forward,
                    };
                    Vector3 along = sideIndex switch
                    {
                        0 => Vector3.Back,
                        1 => Vector3.Forward,
                        2 => Vector3.Right,
                        _ => Vector3.Left,
                    };
                    float alongOffset = (hash / 12 % 11 - 5) * 1.1f;
                    Vector3 position = blockCenter
                        + outward * (BlockSize * 0.5f + 0.025f)
                        + along * alongOffset
                        + Vector3.Up * (CurbHeight * 0.5f);
                    BuildStormDrain(root, position, outward, grateDark, grateMetal, waterFlow);
                }

                float manholeOffset = (hash / 97 % 9 - 4) * 1.25f;
                Vector3 manholePosition = blockCenter
                    + Vector3.Back * (BlockSize * 0.5f + RoadWidth * 0.5f)
                    + Vector3.Right * manholeOffset;
                BuildManhole(root, manholePosition, manholeRim, grateDark);
            }
        }
    }

    private void BuildStreetDrainageForBlock(int bx, int bz)
    {
        var root = new Node3D { Name = $"StreetDrainage_{bx}_{bz}" };
        (_activeChunkRoot ?? _staticRoot).AddChild(root);

        StandardMaterial3D grateDark = Flat(new Color(0.025f, 0.03f, 0.03f), 0.65f);
        StandardMaterial3D grateMetal = Flat(new Color(0.36f, 0.38f, 0.36f), 0.55f);
        StandardMaterial3D manholeRim = Flat(new Color(0.28f, 0.28f, 0.26f), 0.75f);
        AudioStreamWav waterFlow = _waterFlow ??= CreateWaterFlow();

        Vector3 blockCenter = BlockCenter(bx, bz);
        int hash = AssetProps.Hash(blockCenter, 53);
        if (hash % 2 == 0)
        {
            int sideIndex = hash / 3 % 4;
            Vector3 outward = sideIndex switch
            {
                0 => Vector3.Right,
                1 => Vector3.Left,
                2 => Vector3.Back,
                _ => Vector3.Forward,
            };
            Vector3 along = sideIndex switch
            {
                0 => Vector3.Back,
                1 => Vector3.Forward,
                2 => Vector3.Right,
                _ => Vector3.Left,
            };
            float alongOffset = (hash / 12 % 11 - 5) * 1.1f;
            Vector3 position = blockCenter
                + outward * (BlockSize * 0.5f + 0.025f)
                + along * alongOffset
                + Vector3.Up * (CurbHeight * 0.5f);
            BuildStormDrain(root, position, outward, grateDark, grateMetal, waterFlow);
        }

        float manholeOffset = (hash / 97 % 9 - 4) * 1.25f;
        Vector3 manholePosition = blockCenter
            + Vector3.Back * (BlockSize * 0.5f + RoadWidth * 0.5f)
            + Vector3.Right * manholeOffset;
        BuildManhole(root, manholePosition, manholeRim, grateDark);
    }

    private void BuildStormDrain(Node3D parent, Vector3 position, Vector3 outward, Material dark, Material metal, AudioStreamWav waterFlow)
    {
        bool curbOnX = Mathf.Abs(outward.X) > 0.5f;
        Vector3 frameSize = curbOnX ? new Vector3(0.05f, 0.18f, 1.1f) : new Vector3(1.1f, 0.18f, 0.05f);
        Vector3 openingSize = curbOnX ? new Vector3(0.025f, 0.12f, 0.94f) : new Vector3(0.94f, 0.12f, 0.025f);
        AddBox(parent, position + outward * 0.025f, frameSize, metal, false);
        AddBox(parent, position + outward * 0.055f, openingSize, dark, false);

        for (int i = 0; i < 5; i++)
        {
            float heightOffset = (i - 2) * 0.025f;
            Vector3 barSize = curbOnX ? new Vector3(0.04f, 0.012f, 0.86f) : new Vector3(0.86f, 0.012f, 0.04f);
            AddBox(parent, position + outward * 0.08f + Vector3.Up * heightOffset, barSize, metal, false);
        }

        Vector3 along = curbOnX ? Vector3.Back : Vector3.Right;
        Vector3 apron = position + outward * 0.36f - Vector3.Up * (CurbHeight * 0.5f - 0.02f);
        Vector3 apronSize = curbOnX ? new Vector3(0.52f, 0.025f, 0.88f) : new Vector3(0.88f, 0.025f, 0.52f);
        AddBox(parent, apron, apronSize, dark, false);
        for (int i = 0; i < 4; i++)
        {
            float offset = (i - 1.5f) * 0.18f;
            Vector3 barSize = curbOnX ? new Vector3(0.44f, 0.03f, 0.025f) : new Vector3(0.025f, 0.03f, 0.44f);
            AddBox(parent, apron + along * offset + Vector3.Up * 0.015f, barSize, metal, false);
        }

        var sound = new AudioStreamPlayer3D
        {
            Stream = waterFlow,
            Bus = AudioManager.SfxBus,
            UnitSize = 3f,
            MaxDistance = 11f,
            VolumeDb = -17f,
            Position = position + Vector3.Up * 0.3f,
        };
        parent.AddChild(sound);
        sound.Play();
    }

    private void BuildManhole(Node3D parent, Vector3 position, Material rim, Material cover)
    {
        AddCylinder(parent, position + new Vector3(0f, 0.025f, 0f), 0.58f, 0.05f, rim);
        AddCylinder(parent, position + new Vector3(0f, 0.055f, 0f), 0.48f, 0.025f, cover);
        AddBox(parent, position + new Vector3(0f, 0.07f, 0f), new Vector3(0.38f, 0.01f, 0.035f), rim, false);
        AddBox(parent, position + new Vector3(0f, 0.07f, 0f), new Vector3(0.035f, 0.01f, 0.38f), rim, false);
    }

    private static AudioStreamWav CreateWaterFlow()
    {
        const int rate = 22050;
        const int sampleCount = rate * 3;
        var data = new byte[sampleCount * 2];
        var rng = new RandomNumberGenerator { Seed = 77123 };
        float lowNoise = 0f;
        float trickleNoise = 0f;
        int fadeSamples = rate / 20;

        for (int i = 0; i < sampleCount; i++)
        {
            float noise = rng.RandfRange(-1f, 1f);
            lowNoise = Mathf.Lerp(lowNoise, noise, 0.025f);
            trickleNoise = Mathf.Lerp(trickleNoise, noise, 0.22f);
            float ripple = Mathf.Sin(i * Mathf.Tau * 137f / rate) * 0.08f
                + Mathf.Sin(i * Mathf.Tau * 211f / rate) * 0.04f;
            float fade = Mathf.Min(1f, Mathf.Min((float)i / fadeSamples, (float)(sampleCount - 1 - i) / fadeSamples));
            float sample = (lowNoise * 0.8f + trickleNoise * 0.3f + ripple) * 0.5f * fade;
            short value = (short)(Mathf.Clamp(sample, -1f, 1f) * 24000f);
            data[i * 2] = (byte)(value & 0xff);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Data = data,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = sampleCount,
        };
    }
}
