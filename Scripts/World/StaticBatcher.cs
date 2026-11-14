// Scripts/World/StaticBatcher.cs
// Junta as caixinhas e cilindros estáticos de um quarteirão em poucas malhas (v0.10, desempenho no celular).
// A cidade é montada com milhares de MeshInstance3D pequenos (AddBox/AddCylinder): cada um vira uma chamada
// de desenho, e é isso que derruba o FPS no celular. Aqui, depois que o quarteirão fica pronto:
//  - cores lisas (StandardMaterial3D sem textura, sem emissão e opaco) viram cor de vértice numa malha só
//    por faixa de tamanho;
//  - os outros materiais padrão são juntados por material (o mesmo objeto de material continua valendo,
//    então piscas e letreiros que mudam a emissão continuam funcionando).
// Fica de fora tudo o que pode se mexer ou ser procurado depois: nós com nome próprio, com script, em grupo,
// com filhos, com ShaderMaterial (uniformes por instância do prédio) ou embaixo de um nó com script.
// As colisões não mudam (StaticBody3D e CollisionShape3D continuam no lugar).
using System.Collections.Generic;
using Godot;

namespace DeadFrequency;

public static class StaticBatcher
{
    /// <summary>Desliga a junção (para comparar ou depurar): --no-batch na linha de comando.</summary>
    public static readonly bool Enabled = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-batch") < 0;

    /// <summary>Marque um nó com este meta para ele (e o que tem dentro) nunca ser juntado.</summary>
    public const string SkipMeta = "no_batch";

    /// <summary>Totais desde o início (F3/depuração): malhas juntadas e malhas criadas.</summary>
    public static int MergedCount { get; private set; }
    public static int BatchCount { get; private set; }

    private sealed class Source
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] Uvs;
        public int[] Indices;
    }

    private sealed class Bucket
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector3> Normals = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<Color> Colors = new();
        public readonly List<int> Indices = new();
        public Material Material;
        public int SizeClass;
        public bool VertexColor;
    }

    private static readonly Dictionary<Mesh, Source> Sources = new();
    private static readonly Dictionary<long, StandardMaterial3D> FlatMaterials = new();

    /// <summary>Junta as malhas estáticas debaixo de root. Chame depois de montar tudo e antes do OptimizeVisibility.</summary>
    /// <param name="singleRange">Maior que zero: tudo numa faixa só, visível até essa distância (objetos pequenos, como um carro).</param>
    public static void Merge(Node3D root, float singleRange = 0f)
    {
        if (!Enabled || root == null)
            return;
        var buckets = new Dictionary<(ulong, int), Bucket>();
        var merged = new List<MeshInstance3D>();
        Collect(root, Transform3D.Identity, root, buckets, merged, singleRange > 0f);
        if (merged.Count < 8)
            return; // não compensa

        foreach (Bucket b in buckets.Values)
        {
            if (b.Indices.Count == 0)
                continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = b.Vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = b.Normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = b.Uvs.ToArray();
            if (b.VertexColor)
                arrays[(int)Mesh.ArrayType.Color] = b.Colors.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = b.Indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            var instance = new MeshInstance3D { Name = "Batch" + b.SizeClass, Mesh = mesh, MaterialOverride = b.Material };
            // Mesmas faixas do OptimizeVisibility, com folga pelo tamanho do quarteirão (a malha é do quarteirão todo).
            if (singleRange > 0f)
                instance.VisibilityRangeEnd = singleRange;
            else if (b.SizeClass <= 1)
                instance.VisibilityRangeEnd = 150f;
            else if (b.SizeClass == 2)
                instance.VisibilityRangeEnd = 210f;
            if (instance.VisibilityRangeEnd > 0f)
                instance.VisibilityRangeEndMargin = 10f;
            if (b.SizeClass == 0 && singleRange <= 0f)
                instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            root.AddChild(instance);
            BatchCount++;
        }
        foreach (MeshInstance3D m in merged)
        {
            m.GetParent()?.RemoveChild(m);
            m.QueueFree();
        }
        MergedCount += merged.Count;
    }

    private static void Collect(Node node, Transform3D toRoot, Node3D root, Dictionary<(ulong, int), Bucket> buckets, List<MeshInstance3D> merged, bool single)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is not Node3D n3 || child.GetScript().VariantType != Variant.Type.Nil)
                continue; // nós com script (portões, vendedores, placas) ficam como estão, com tudo o que têm dentro
            if (!IsPlain(child) || child.HasMeta(SkipMeta))
                continue; // carros estacionados e vitrines podem sumir depois: ficam inteiros
            Transform3D xf = toRoot * n3.Transform;
            if (child is MeshInstance3D mi)
            {
                if (CanMerge(mi) && Append(mi, xf, buckets, single))
                    merged.Add(mi);
                continue;
            }
            Collect(child, xf, root, buckets, merged, single);
        }
    }

    /// <summary>Só atravessa agrupadores simples: Node3D e StaticBody3D puros, visíveis.</summary>
    private static bool IsPlain(Node n)
    {
        System.Type t = n.GetType();
        if (t != typeof(Node3D) && t != typeof(StaticBody3D) && t != typeof(MeshInstance3D))
            return false;
        return ((Node3D)n).Visible;
    }

    private static bool CanMerge(MeshInstance3D m)
    {
        if (m.Mesh == null || m.Mesh.GetSurfaceCount() != 1 || m.Mesh is not (PrimitiveMesh or ArrayMesh) || m.GetChildCount() > 0 || m.GetGroups().Count > 0 || m.HasMeta(SkipMeta))
            return false;
        if (!m.Name.ToString().StartsWith('@'))
            return false; // nome próprio = alguém pode procurar por ele depois
        if (m.VisibilityRangeEnd > 0f || m.Skeleton != new NodePath("..") && !m.Skeleton.IsEmpty)
            return false;
        if (m.MaterialOverride is not StandardMaterial3D mat)
            return false;
        // Triplanar local, billboard e afins dependem do nó: ficam de fora.
        if (mat.Uv1Triplanar && !mat.Uv1WorldTriplanar)
            return false;
        if (mat.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled || mat.NormalEnabled)
            return false;
        return true;
    }

    private static bool IsFlat(StandardMaterial3D m) =>
        m.AlbedoTexture == null && !m.EmissionEnabled && m.Transparency == BaseMaterial3D.TransparencyEnum.Disabled &&
        m.ShadingMode == BaseMaterial3D.ShadingModeEnum.PerPixel && m.CullMode == BaseMaterial3D.CullModeEnum.Back &&
        !m.Uv1Triplanar && m.MetallicTexture == null && m.RoughnessTexture == null && !m.VertexColorUseAsAlbedo &&
        m.DetailEnabled == false && m.RimEnabled == false && m.ClearcoatEnabled == false;

    /// <summary>Material único de cor de vértice para cada (aspereza, metal): a cor vai no vértice.</summary>
    private static StandardMaterial3D FlatMaterial(StandardMaterial3D m)
    {
        long key = Mathf.RoundToInt(m.Roughness * 20f) * 1000L + Mathf.RoundToInt(m.Metallic * 20f) * 10L + (m.MetallicSpecular < 0.25f ? 1 : 0);
        if (!FlatMaterials.TryGetValue(key, out StandardMaterial3D shared))
        {
            shared = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
                Roughness = Mathf.RoundToInt(m.Roughness * 20f) / 20f,
                Metallic = Mathf.RoundToInt(m.Metallic * 20f) / 20f,
                MetallicSpecular = m.MetallicSpecular,
            };
            FlatMaterials[key] = shared;
        }
        return shared;
    }

    private static Source GetSource(Mesh mesh)
    {
        if (Sources.TryGetValue(mesh, out Source s))
            return s;
        Godot.Collections.Array arrays = mesh.SurfaceGetArrays(0);
        s = new Source { Vertices = System.Array.Empty<Vector3>(), Normals = System.Array.Empty<Vector3>(), Uvs = System.Array.Empty<Vector2>(), Indices = System.Array.Empty<int>() };
        bool triangles = mesh is PrimitiveMesh || (mesh is ArrayMesh am && am.SurfaceGetPrimitiveType(0) == Mesh.PrimitiveType.Triangles);
        Variant v = arrays[(int)Mesh.ArrayType.Vertex], n = arrays[(int)Mesh.ArrayType.Normal], uv = arrays[(int)Mesh.ArrayType.TexUV], ix = arrays[(int)Mesh.ArrayType.Index];
        // Só malhas de triângulos com normais; sem índices, cada 3 vértices são um triângulo.
        if (triangles && v.VariantType == Variant.Type.PackedVector3Array && n.VariantType == Variant.Type.PackedVector3Array)
        {
            s.Vertices = v.AsVector3Array();
            s.Normals = n.AsVector3Array();
            if (uv.VariantType == Variant.Type.PackedVector2Array)
                s.Uvs = uv.AsVector2Array();
            if (ix.VariantType == Variant.Type.PackedInt32Array)
                s.Indices = ix.AsInt32Array();
            else
            {
                s.Indices = new int[s.Vertices.Length / 3 * 3];
                for (int i = 0; i < s.Indices.Length; i++)
                    s.Indices[i] = i;
            }
            if (s.Normals.Length != s.Vertices.Length)
                s.Indices = System.Array.Empty<int>();
        }
        Sources[mesh] = s;
        return s;
    }

    private static bool Append(MeshInstance3D m, Transform3D xf, Dictionary<(ulong, int), Bucket> buckets, bool single)
    {
        var mat = (StandardMaterial3D)m.MaterialOverride;
        Source src = GetSource(m.Mesh);
        if (src.Indices.Length == 0)
            return false;

        Vector3 size = m.Mesh.GetAabb().Size * xf.Basis.Scale;
        float biggest = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        int sizeClass = biggest < 0.6f ? 0 : biggest < 1.6f ? 1 : biggest < 5f ? 2 : 3;
        if (m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off)
            sizeClass = Mathf.Min(sizeClass, 0);
        if (single)
            sizeClass = 3;

        bool flat = IsFlat(mat);
        Material target = flat ? FlatMaterial(mat) : mat;
        (ulong, int) key = (target.GetInstanceId(), sizeClass);
        if (!buckets.TryGetValue(key, out Bucket b))
        {
            b = new Bucket { Material = target, SizeClass = sizeClass, VertexColor = flat };
            buckets[key] = b;
        }

        int start = b.Vertices.Count;
        Basis normalBasis = xf.Basis.Inverse().Transposed();
        bool flipped = xf.Basis.Determinant() < 0f;
        Color color = mat.AlbedoColor;
        for (int i = 0; i < src.Vertices.Length; i++)
        {
            b.Vertices.Add(xf * src.Vertices[i]);
            b.Normals.Add((normalBasis * src.Normals[i]).Normalized());
            b.Uvs.Add(i < src.Uvs.Length ? src.Uvs[i] : Vector2.Zero);
            if (flat)
                b.Colors.Add(color);
        }
        for (int i = 0; i + 2 < src.Indices.Length; i += 3)
        {
            b.Indices.Add(start + src.Indices[i]);
            b.Indices.Add(start + src.Indices[flipped ? i + 2 : i + 1]);
            b.Indices.Add(start + src.Indices[flipped ? i + 1 : i + 2]);
        }
        return true;
    }
}
