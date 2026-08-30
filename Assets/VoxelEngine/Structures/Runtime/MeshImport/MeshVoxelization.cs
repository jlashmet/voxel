using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime.MeshImport
{
    /// <summary>One indexed source triangle and its opaque voxel material.</summary>
    public readonly struct MeshVoxelTriangle
    {
        public readonly int A;
        public readonly int B;
        public readonly int C;
        public readonly byte Material;

        public MeshVoxelTriangle(int a, int b, int c, byte material)
        {
            A = a;
            B = b;
            C = c;
            Material = material;
        }
    }

    /// <summary>
    /// Engine-independent triangle input for the deterministic voxelizer. Vertices are local-space
    /// points and <see cref="Transform"/> is applied before grid quantization, so authoring adapters
    /// can flatten Unity mesh hierarchies without leaking GameObjects into Structures.Runtime.
    /// </summary>
    public readonly struct MeshVoxelizationSource
    {
        public readonly float3[] Vertices;
        public readonly MeshVoxelTriangle[] Triangles;
        public readonly float4x4 Transform;

        public MeshVoxelizationSource(
            float3[] vertices,
            MeshVoxelTriangle[] triangles,
            float4x4 transform)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Triangles = triangles ?? throw new ArgumentNullException(nameof(triangles));
            Transform = transform;
        }
    }

    /// <summary>Bounded authoring policy. Dimensions are in output voxels.</summary>
    public readonly struct MeshVoxelizationSettings
    {
        public readonly float VoxelSize;
        public readonly bool FillInterior;
        public readonly byte FallbackMaterial;
        public readonly int3 MaxDimensions;
        public readonly int MaxDenseCells;
        public readonly int ThinFeaturePaddingVoxels;

        public MeshVoxelizationSettings(
            float voxelSize,
            bool fillInterior,
            byte fallbackMaterial,
            int3 maxDimensions,
            int maxDenseCells,
            int thinFeaturePaddingVoxels)
        {
            VoxelSize = voxelSize;
            FillInterior = fillInterior;
            FallbackMaterial = fallbackMaterial;
            MaxDimensions = maxDimensions;
            MaxDenseCells = maxDenseCells;
            ThinFeaturePaddingVoxels = thinFeaturePaddingVoxels;
        }
    }

    /// <summary>One local sparse authored voxel.</summary>
    public readonly struct BakedVoxelCell : IEquatable<BakedVoxelCell>
    {
        public readonly int3 Position;
        public readonly byte Material;

        public BakedVoxelCell(int3 position, byte material)
        {
            Position = position;
            Material = material;
        }

        public bool Equals(BakedVoxelCell other) =>
            math.all(Position == other.Position) && Material == other.Material;
        public override bool Equals(object obj) => obj is BakedVoxelCell other && Equals(other);
        public override int GetHashCode() => Position.GetHashCode() * 397 ^ Material;
    }

    /// <summary>
    /// Deterministic sparse structure emitted by mesh authoring. Cell positions are local to
    /// <see cref="GridOrigin"/> and sorted x/y/z for stable serialization and replay.
    /// </summary>
    public sealed class BakedVoxelStructure
    {
        public float VoxelSize { get; }
        public int3 GridOrigin { get; }
        public int3 Size { get; }
        public BakedVoxelCell[] Cells { get; }
        public int SourceTriangleCount { get; }
        public double VoxelizationMilliseconds { get; }

        public BakedVoxelStructure(
            float voxelSize,
            int3 gridOrigin,
            int3 size,
            BakedVoxelCell[] cells,
            int sourceTriangleCount,
            double voxelizationMilliseconds)
        {
            if (!(voxelSize > 0f) || !IsFinite(voxelSize))
                throw new ArgumentOutOfRangeException(nameof(voxelSize));
            if (math.any(size <= 0)) throw new ArgumentOutOfRangeException(nameof(size));
            VoxelSize = voxelSize;
            GridOrigin = gridOrigin;
            Size = size;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            SourceTriangleCount = sourceTriangleCount;
            VoxelizationMilliseconds = voxelizationMilliseconds;
        }

        /// <summary>
        /// Replays only sparse authored cells through the canonical structure authoring API.
        /// GridOrigin describes source-space provenance; callers choose the gameplay placement
        /// origin explicitly so the same bake is reusable at arbitrary world positions.
        /// </summary>
        public int ReplayTo(IStructureAuthoringSession authoring, int3 worldOrigin)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            for (int i = 0; i < Cells.Length; i++)
            {
                BakedVoxelCell cell = Cells[i];
                int3 p = worldOrigin + cell.Position;
                authoring.Set(p.x, p.y, p.z, cell.Material);
                if (authoring.BudgetExceeded)
                    throw new InvalidOperationException(
                        $"Mesh structure replay exceeded the authoring budget after {i + 1} cells.");
            }
            return Cells.Length;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Deterministic CPU triangle-mesh voxelizer used at authoring time. Surface coverage is a
    /// triangle/AABB separating-axis test, not vertex quantization. Optional interior fill is a
    /// bounded exterior flood fill over the already-preflighted dense working grid.
    /// </summary>
    public static class MeshVoxelizer
    {
        private const float BoundsEpsilon = 1e-5f;
        private const float AxisEpsilonSq = 1e-12f;

        public static BakedVoxelStructure Voxelize(
            in MeshVoxelizationSource source,
            in MeshVoxelizationSettings settings)
        {
            ValidateSettings(in settings);
            ValidateSource(in source);
            var stopwatch = Stopwatch.StartNew();

            float invVoxel = 1f / settings.VoxelSize;
            var gridVertices = new float3[source.Vertices.Length];
            float3 min = new float3(float.PositiveInfinity);
            float3 max = new float3(float.NegativeInfinity);
            for (int i = 0; i < source.Vertices.Length; i++)
            {
                float3 world = math.transform(source.Transform, source.Vertices[i]);
                if (!AllFinite(world))
                    throw new ArgumentException("Mesh transform produced a non-finite vertex.", nameof(source));
                float3 grid = world * invVoxel;
                gridVertices[i] = grid;
                min = math.min(min, grid);
                max = math.max(max, grid);
            }

            // Epsilon expands exact grid-plane extrema to both touching cells. This is deliberate:
            // conservative coverage must retain a zero-thickness membrane that lies on a cell face.
            int3 gridMin = (int3)math.floor(min - BoundsEpsilon);
            int3 gridMax = (int3)math.floor(max + BoundsEpsilon);
            int3 size = gridMax - gridMin + 1;
            PreflightSize(size, in settings);

            int denseCount = CheckedCellCount(size);
            var surfaceMaterial = new byte[denseCount];
            var surfaceOwned = new bool[denseCount];

            for (int triangleIndex = 0; triangleIndex < source.Triangles.Length; triangleIndex++)
            {
                MeshVoxelTriangle triangle = source.Triangles[triangleIndex];
                float3 a = gridVertices[triangle.A];
                float3 b = gridVertices[triangle.B];
                float3 c = gridVertices[triangle.C];
                float3 triMin = math.min(a, math.min(b, c));
                float3 triMax = math.max(a, math.max(b, c));
                int3 first = math.max(gridMin, (int3)math.floor(triMin - BoundsEpsilon));
                int3 last = math.min(gridMax, (int3)math.floor(triMax + BoundsEpsilon));
                byte material = triangle.Material != 0
                    ? triangle.Material
                    : settings.FallbackMaterial;

                for (int x = first.x; x <= last.x; x++)
                for (int y = first.y; y <= last.y; y++)
                for (int z = first.z; z <= last.z; z++)
                {
                    float3 centre = new float3(x + 0.5f, y + 0.5f, z + 0.5f);
                    if (!TriangleIntersectsUnitBox(a, b, c, centre)) continue;
                    int3 local = new int3(x, y, z) - gridMin;
                    int index = Index(local, size);
                    if (!surfaceOwned[index])
                    {
                        surfaceOwned[index] = true;
                        surfaceMaterial[index] = material;
                    }
                    else if (material < surfaceMaterial[index])
                    {
                        // Multiple triangles may own a boundary cell. Resolve without dependence on
                        // traversal/hash ordering so equivalent source orderings remain deterministic.
                        surfaceMaterial[index] = material;
                    }
                }
            }

            if (settings.ThinFeaturePaddingVoxels > 0)
                DilateSurface(surfaceOwned, surfaceMaterial, size, settings.ThinFeaturePaddingVoxels);

            byte[] finalMaterial = surfaceMaterial;
            bool[] finalOwned = surfaceOwned;
            if (settings.FillInterior)
                FillInterior(surfaceOwned, surfaceMaterial, size,
                             settings.FallbackMaterial, settings.MaxDenseCells,
                             out finalOwned, out finalMaterial);

            var cells = new List<BakedVoxelCell>();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                int3 local = new int3(x, y, z);
                int index = Index(local, size);
                if (!finalOwned[index]) continue;
                byte material = finalMaterial[index] != 0
                    ? finalMaterial[index]
                    : settings.FallbackMaterial;
                cells.Add(new BakedVoxelCell(local, material));
            }

            stopwatch.Stop();
            return new BakedVoxelStructure(
                settings.VoxelSize,
                gridMin,
                size,
                cells.ToArray(),
                source.Triangles.Length,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        private static void ValidateSettings(in MeshVoxelizationSettings settings)
        {
            if (!(settings.VoxelSize > 0f) || !IsFinite(settings.VoxelSize))
                throw new ArgumentOutOfRangeException(nameof(settings), "Voxel size must be finite and positive.");
            if (math.any(settings.MaxDimensions <= 0))
                throw new ArgumentOutOfRangeException(nameof(settings), "Maximum dimensions must be positive.");
            if (settings.MaxDenseCells <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings), "Dense-cell budget must be positive.");
            if (settings.ThinFeaturePaddingVoxels < 0 || settings.ThinFeaturePaddingVoxels > 8)
                throw new ArgumentOutOfRangeException(nameof(settings), "Thin-feature padding must be in [0,8].");
            if (settings.FallbackMaterial == 0)
                throw new ArgumentOutOfRangeException(nameof(settings), "Fallback material must be non-empty.");
        }

        private static void ValidateSource(in MeshVoxelizationSource source)
        {
            if (source.Vertices == null || source.Vertices.Length == 0)
                throw new ArgumentException("Mesh source requires vertices.", nameof(source));
            if (source.Triangles == null || source.Triangles.Length == 0)
                throw new ArgumentException("Mesh source requires triangles.", nameof(source));
            if (!AllFinite(source.Transform.c0) || !AllFinite(source.Transform.c1)
                || !AllFinite(source.Transform.c2) || !AllFinite(source.Transform.c3))
                throw new ArgumentException("Mesh transform must be finite.", nameof(source));

            for (int i = 0; i < source.Vertices.Length; i++)
                if (!AllFinite(source.Vertices[i]))
                    throw new ArgumentException($"Vertex {i} is non-finite.", nameof(source));

            for (int i = 0; i < source.Triangles.Length; i++)
            {
                MeshVoxelTriangle t = source.Triangles[i];
                if ((uint)t.A >= (uint)source.Vertices.Length
                    || (uint)t.B >= (uint)source.Vertices.Length
                    || (uint)t.C >= (uint)source.Vertices.Length)
                    throw new ArgumentException($"Triangle {i} contains an invalid vertex index.", nameof(source));
            }
        }

        private static void PreflightSize(int3 size, in MeshVoxelizationSettings settings)
        {
            if (math.any(size <= 0) || math.any(size > settings.MaxDimensions))
                throw new ArgumentOutOfRangeException(nameof(settings),
                    $"Voxelized bounds {size} exceed configured maximum {settings.MaxDimensions}.");
            int dense = CheckedCellCount(size);
            if (dense > settings.MaxDenseCells)
                throw new ArgumentOutOfRangeException(nameof(settings),
                    $"Voxelized dense working set {dense:N0} exceeds budget {settings.MaxDenseCells:N0}.");
            if (settings.FillInterior)
            {
                long padded = (long)(size.x + 2) * (size.y + 2) * (size.z + 2);
                if (padded > settings.MaxDenseCells)
                    throw new ArgumentOutOfRangeException(nameof(settings),
                        $"Interior-fill working set {padded:N0} exceeds budget {settings.MaxDenseCells:N0}.");
            }
        }

        private static int CheckedCellCount(int3 size)
        {
            long count = (long)size.x * size.y * size.z;
            if (count <= 0 || count > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(size), "Voxelized working set is not addressable.");
            return (int)count;
        }

        private static bool TriangleIntersectsUnitBox(float3 a, float3 b, float3 c, float3 centre)
        {
            float3 v0 = a - centre;
            float3 v1 = b - centre;
            float3 v2 = c - centre;
            float3 e0 = v1 - v0;
            float3 e1 = v2 - v1;
            float3 e2 = v0 - v2;
            const float half = 0.50001f;

            if (!AxisOverlap(new float3(1f, 0f, 0f), v0, v1, v2, half)
                || !AxisOverlap(new float3(0f, 1f, 0f), v0, v1, v2, half)
                || !AxisOverlap(new float3(0f, 0f, 1f), v0, v1, v2, half))
                return false;

            float3 normal = math.cross(e0, e1);
            if (!AxisOverlap(normal, v0, v1, v2, half)) return false;

            float3[] edges = { e0, e1, e2 };
            for (int i = 0; i < edges.Length; i++)
            {
                float3 edge = edges[i];
                if (!AxisOverlap(math.cross(edge, new float3(1f, 0f, 0f)), v0, v1, v2, half)
                    || !AxisOverlap(math.cross(edge, new float3(0f, 1f, 0f)), v0, v1, v2, half)
                    || !AxisOverlap(math.cross(edge, new float3(0f, 0f, 1f)), v0, v1, v2, half))
                    return false;
            }
            return true;
        }

        private static bool AxisOverlap(float3 axis, float3 v0, float3 v1, float3 v2, float half)
        {
            if (math.lengthsq(axis) < AxisEpsilonSq) return true;
            float p0 = math.dot(v0, axis);
            float p1 = math.dot(v1, axis);
            float p2 = math.dot(v2, axis);
            float min = math.min(p0, math.min(p1, p2));
            float max = math.max(p0, math.max(p1, p2));
            float radius = half * (math.abs(axis.x) + math.abs(axis.y) + math.abs(axis.z));
            return !(min > radius || max < -radius);
        }

        private static void DilateSurface(
            bool[] owned, byte[] materials, int3 size, int radius)
        {
            bool[] sourceOwned = (bool[])owned.Clone();
            byte[] sourceMaterials = (byte[])materials.Clone();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                int3 p = new int3(x, y, z);
                int sourceIndex = Index(p, size);
                if (!sourceOwned[sourceIndex]) continue;
                byte material = sourceMaterials[sourceIndex];
                for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (math.abs(dx) + math.abs(dy) + math.abs(dz) > radius) continue;
                    int3 q = p + new int3(dx, dy, dz);
                    if (math.any(q < 0) || math.any(q >= size)) continue;
                    int qi = Index(q, size);
                    if (!owned[qi] || material < materials[qi])
                    {
                        owned[qi] = true;
                        materials[qi] = material;
                    }
                }
            }
        }

        private static void FillInterior(
            bool[] surfaceOwned,
            byte[] surfaceMaterial,
            int3 size,
            byte fallbackMaterial,
            int maxDenseCells,
            out bool[] finalOwned,
            out byte[] finalMaterial)
        {
            int3 paddedSize = size + 2;
            int paddedCount = CheckedCellCount(paddedSize);
            if (paddedCount > maxDenseCells)
                throw new ArgumentOutOfRangeException(nameof(maxDenseCells));

            var blocked = new bool[paddedCount];
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                int3 local = new int3(x, y, z);
                if (!surfaceOwned[Index(local, size)]) continue;
                blocked[Index(local + 1, paddedSize)] = true;
            }

            var exterior = new bool[paddedCount];
            var queue = new Queue<int3>();
            EnqueueExterior(new int3(0), paddedSize, blocked, exterior, queue);
            int3[] neighbours =
            {
                new int3(1,0,0), new int3(-1,0,0),
                new int3(0,1,0), new int3(0,-1,0),
                new int3(0,0,1), new int3(0,0,-1),
            };
            while (queue.Count > 0)
            {
                int3 p = queue.Dequeue();
                for (int i = 0; i < neighbours.Length; i++)
                {
                    int3 q = p + neighbours[i];
                    if (math.any(q < 0) || math.any(q >= paddedSize)) continue;
                    EnqueueExterior(q, paddedSize, blocked, exterior, queue);
                }
            }

            finalOwned = (bool[])surfaceOwned.Clone();
            finalMaterial = (byte[])surfaceMaterial.Clone();
            for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
            for (int z = 0; z < size.z; z++)
            {
                int3 local = new int3(x, y, z);
                int index = Index(local, size);
                if (finalOwned[index]) continue;
                if (exterior[Index(local + 1, paddedSize)]) continue;
                finalOwned[index] = true;
                finalMaterial[index] = fallbackMaterial;
            }
        }

        private static void EnqueueExterior(
            int3 p, int3 size, bool[] blocked, bool[] exterior, Queue<int3> queue)
        {
            int index = Index(p, size);
            if (blocked[index] || exterior[index]) return;
            exterior[index] = true;
            queue.Enqueue(p);
        }

        private static int Index(int3 p, int3 size) =>
            (p.x * size.y + p.y) * size.z + p.z;

        private static bool AllFinite(float3 v) =>
            IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        private static bool AllFinite(float4 v) =>
            IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z) && IsFinite(v.w);
        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Stable text codec for source-controlled sparse bakes. Runtime parses only this artifact;
    /// mesh voxelization itself remains an authoring operation.
    /// </summary>
    public static class BakedVoxelStructureCodec
    {
        private const string Magic = "MVX1";

        public static string Encode(BakedVoxelStructure bake)
        {
            if (bake == null) throw new ArgumentNullException(nameof(bake));
            var sb = new StringBuilder(64 + bake.Cells.Length * 14);
            sb.Append(Magic).Append('|')
              .Append(bake.VoxelSize.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            AppendInt3(sb, bake.GridOrigin);
            sb.Append('|');
            AppendInt3(sb, bake.Size);
            sb.Append('|').Append(bake.SourceTriangleCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            for (int i = 0; i < bake.Cells.Length; i++)
            {
                if (i != 0) sb.Append(';');
                BakedVoxelCell cell = bake.Cells[i];
                sb.Append(cell.Position.x).Append(',')
                  .Append(cell.Position.y).Append(',')
                  .Append(cell.Position.z).Append(',')
                  .Append(cell.Material);
            }
            return sb.ToString();
        }

        public static BakedVoxelStructure Decode(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) throw new FormatException("Mesh voxel bake is empty.");
            string[] parts = encoded.Split('|');
            if (parts.Length != 6 || parts[0] != Magic)
                throw new FormatException("Mesh voxel bake header is invalid.");
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float voxelSize)
                || !(voxelSize > 0f) || !IsFinite(voxelSize))
                throw new FormatException("Mesh voxel bake has an invalid voxel size.");
            int3 origin = ParseInt3(parts[2], "origin");
            int3 size = ParseInt3(parts[3], "size");
            if (math.any(size <= 0)) throw new FormatException("Mesh voxel bake size must be positive.");
            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceTriangles)
                || sourceTriangles < 0)
                throw new FormatException("Mesh voxel bake triangle count is invalid.");

            var cells = new List<BakedVoxelCell>();
            int3 previous = new int3(-1);
            if (parts[5].Length != 0)
            {
                string[] rows = parts[5].Split(';');
                for (int i = 0; i < rows.Length; i++)
                {
                    string[] values = rows[i].Split(',');
                    if (values.Length != 4
                        || !int.TryParse(values[0], out int x)
                        || !int.TryParse(values[1], out int y)
                        || !int.TryParse(values[2], out int z)
                        || !byte.TryParse(values[3], out byte material))
                        throw new FormatException($"Mesh voxel bake cell {i} is invalid.");
                    int3 position = new int3(x, y, z);
                    if (material == 0 || math.any(position < 0) || math.any(position >= size))
                        throw new FormatException($"Mesh voxel bake cell {i} is out of bounds or empty.");
                    if (i > 0 && Compare(previous, position) >= 0)
                        throw new FormatException("Mesh voxel bake cells must be unique and lexicographically ordered.");
                    previous = position;
                    cells.Add(new BakedVoxelCell(position, material));
                }
            }

            return new BakedVoxelStructure(
                voxelSize, origin, size, cells.ToArray(), sourceTriangles, 0d);
        }

        private static void AppendInt3(StringBuilder sb, int3 value) =>
            sb.Append(value.x).Append(',').Append(value.y).Append(',').Append(value.z);

        private static int3 ParseInt3(string text, string label)
        {
            string[] values = text.Split(',');
            if (values.Length != 3
                || !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                || !int.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                throw new FormatException($"Mesh voxel bake {label} is invalid.");
            return new int3(x, y, z);
        }

        private static int Compare(int3 a, int3 b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
