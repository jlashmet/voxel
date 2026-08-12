using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Bounded close-up renderer for evaluating authored voxel structures at hero distance.
    ///
    /// The authoritative representation remains the ordinary 10 cm brickmap. This class does not
    /// accept presentation meshes or procedural structure parameters: it samples only RegionTable /
    /// BrickPool occupancy, builds a filtered scalar field, and polygonizes that field with
    /// marching tetrahedra. The half-voxel extraction lattice removes the staircase silhouette of
    /// the legacy showcase greedy mesher without changing destruction, networking, or storage.
    ///
    /// Surface filtering is material-aware. Terrain may keep the broad legacy smoothing while
    /// dressed masonry can recover a local signed-distance estimate from coverage/gradient, then
    /// optionally preserve strongly axis-aligned planes. Profiles affect presentation only; they
    /// never mutate authoritative voxels.
    ///
    /// This is intentionally bounded. A 5 cm visual lattice is appropriate for a close-up hero
    /// component, not for every resident world region. Shipping integration can promote the same
    /// idea into distance/tag-driven surface quality once the visual target is approved.
    /// </summary>
    public sealed class VoxelHeroSurfaceRenderer : IDisposable
    {
        public const float VoxelSize = 0.1f;
        private const int BrickEdge = VoxelDimensions.BrickEdge;
        private const int MaterialCount = 18;
        private const int SamplesPerVoxel = 2;
        private const float SampleStepVoxels = 1f / SamplesPerVoxel;
        private const int BlurPasses = 2;
        private const float MinCoverageGradient = 0.05f;

        private static readonly int3[] CubeCorners =
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)
        };

        private static readonly int[,] Tetrahedra =
        {
            { 0, 5, 1, 6 },
            { 0, 1, 2, 6 },
            { 0, 2, 3, 6 },
            { 0, 3, 7, 6 },
            { 0, 7, 4, 6 },
            { 0, 4, 5, 6 }
        };

        private readonly int3 _minVoxel;
        private readonly int3 _sizeVoxels;
        private readonly int3 _sampleSize;
        private readonly GameObject _root;
        private readonly VoxelSurfaceProfileSet _surfaceProfiles;
        private readonly List<Vector3>[] _vertices = new List<Vector3>[MaterialCount];
        private readonly List<Vector3>[] _normals = new List<Vector3>[MaterialCount];
        private readonly List<int>[] _triangles = new List<int>[MaterialCount];
        private readonly List<Mesh> _meshes = new();
        private readonly List<Material> _placeholderMaterials = new();

        private float[] _baseDensity;
        private byte[] _baseMaterials;
        private int3 _baseMin;
        private int3 _baseSize;
        private float[] _density;
        private bool _built;
        private bool _castShadows;

        public GameObject Root => _root;
        public int RegionMeshCount { get; private set; }
        public int VertexCount { get; private set; }
        public int PendingRebuilds => _built ? 0 : 1;

        public bool CastShadows
        {
            get => _castShadows;
            set
            {
                _castShadows = value;
                ShadowCastingMode mode = value ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (MeshRenderer renderer in _root.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.shadowCastingMode = mode;
            }
        }

        public VoxelHeroSurfaceRenderer(int3 minVoxel, int3 sizeVoxels,
                                        VoxelSurfaceProfileSet surfaceProfiles = null)
        {
            _minVoxel = minVoxel;
            _sizeVoxels = math.max(sizeVoxels, new int3(2));
            _sampleSize = _sizeVoxels * SamplesPerVoxel + 1;
            _surfaceProfiles = surfaceProfiles ?? new VoxelSurfaceProfileSet();
            _root = new GameObject("Voxel Hero Smooth Surface") { hideFlags = HideFlags.DontSave };

            for (int m = 1; m < MaterialCount; m++)
            {
                _vertices[m] = new List<Vector3>(16_384);
                _normals[m] = new List<Vector3>(16_384);
                _triangles[m] = new List<int>(24_576);
            }
        }

        public void Sync(ShowcaseWorld world, double budgetMs)
        {
            if (_built) return;
            Build(world);
            ConsumeIntersectingDirtyRegions(world);
            _built = true;
        }

        private void Build(ShowcaseWorld world)
        {
            BuildBaseDensity(world);
            BuildHalfVoxelDensity();

            int sx = _sampleSize.x;
            int sy = _sampleSize.y;
            int sz = _sampleSize.z;
            var cornerDensity = new float[8];
            var cornerPosition = new float3[8];

            for (int z = 0; z < sz - 1; z++)
            for (int y = 0; y < sy - 1; y++)
            for (int x = 0; x < sx - 1; x++)
            {
                bool anyInside = false;
                bool anyOutside = false;
                for (int c = 0; c < 8; c++)
                {
                    int3 o = CubeCorners[c];
                    float d = DensityAt(x + o.x, y + o.y, z + o.z);
                    cornerDensity[c] = d;
                    anyInside |= d < 0f;
                    anyOutside |= d >= 0f;
                    cornerPosition[c] = (float3)_minVoxel
                        + new float3(x + o.x, y + o.y, z + o.z) * SampleStepVoxels;
                }
                if (!anyInside || !anyOutside) continue;

                for (int t = 0; t < 6; t++)
                {
                    int a = Tetrahedra[t, 0];
                    int b = Tetrahedra[t, 1];
                    int c = Tetrahedra[t, 2];
                    int d = Tetrahedra[t, 3];
                    PolygoniseTetra(world, a, b, c, d, cornerPosition, cornerDensity);
                }
            }

            UploadMeshes();
            _baseDensity = null;
            _baseMaterials = null;
            _density = null;
        }

        /// <summary>
        /// Samples exact binary occupancy at the authoritative voxel lattice and builds the legacy
        /// two-pass filtered field. Material profiles can then replace that broad filter with the
        /// same radius-one coverage/gradient distance recovery used by the production density path.
        /// The legacy profile remains bit-for-bit equivalent to the previous hero renderer.
        /// </summary>
        private void BuildBaseDensity(ShowcaseWorld world)
        {
            int margin = BlurPasses + 2;
            _baseMin = _minVoxel - new int3(margin);
            _baseSize = _sizeVoxels + new int3(margin * 2 + 1);
            int count = _baseSize.x * _baseSize.y * _baseSize.z;
            _baseDensity = new float[count];
            _baseMaterials = new byte[count];
            var original = new float[count];

            ref RegionTable table = ref world.Table;
            ref BrickPool pool = ref world.Pool;

            for (int z = 0; z < _baseSize.z; z++)
            for (int y = 0; y < _baseSize.y; y++)
            for (int x = 0; x < _baseSize.x; x++)
            {
                int3 p = _baseMin + new int3(x, y, z);
                byte material = SampleMaterial(ref table, in pool, p);
                float value = material != VoxelDimensions.MaterialEmpty ? -1f : 1f;
                int index = BaseIndex(x, y, z);
                _baseMaterials[index] = material;
                _baseDensity[index] = value;
                original[index] = value;
            }

            var scratch = new float[count];
            for (int pass = 0; pass < BlurPasses; pass++)
                BlurSeparable(_baseDensity, scratch);

            for (int z = 0; z < _baseSize.z; z++)
            for (int y = 0; y < _baseSize.y; y++)
            for (int x = 0; x < _baseSize.x; x++)
            {
                int index = BaseIndex(x, y, z);
                byte profileMaterial = ProfileMaterialAt(x, y, z);
                VoxelSurfaceProfile profile = _surfaceProfiles.Get(profileMaterial);
                float filtered = _baseDensity[index];
                if (profile.DistanceRecovery > 0.00001f)
                {
                    float distanceRecovered = DistanceRecoveredDensity(x, y, z, original[index]);
                    filtered = math.lerp(filtered, distanceRecovered, profile.DistanceRecovery);
                }
                float shaped = math.lerp(original[index], filtered, profile.Smoothing);
                shaped -= profile.DensityBias;
                shaped -= SurfaceModification(_baseMin + new int3(x, y, z), in profile);
                _baseDensity[index] = math.clamp(shaped, -1.5f, 1.5f);
            }
        }

        /// <summary>
        /// Radius-one binomial coverage and its derivative recover signed distance from binary
        /// occupancy. For an axis-aligned half-space the estimate lands exactly half a voxel from
        /// the boundary; on a digital curve it infers a stable diagonal normal instead of simply
        /// rounding every stair step into a blob.
        /// </summary>
        private float DistanceRecoveredDensity(int centreX, int centreY, int centreZ, float raw)
        {
            float coverage = 0f;
            float3 gradient = float3.zero;

            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int qx = math.clamp(centreX + x, 0, _baseSize.x - 1);
                int qy = math.clamp(centreY + y, 0, _baseSize.y - 1);
                int qz = math.clamp(centreZ + z, 0, _baseSize.z - 1);
                float occupied = _baseMaterials[BaseIndex(qx, qy, qz)]
                    != VoxelDimensions.MaterialEmpty ? 1f : 0f;

                float wx = x == 0 ? 2f : 1f;
                float wy = y == 0 ? 2f : 1f;
                float wz = z == 0 ? 2f : 1f;
                float dx = x;
                float dy = y;
                float dz = z;

                coverage += occupied * wx * wy * wz;
                gradient += occupied * new float3(dx * wy * wz,
                                                   wx * dy * wz,
                                                   wx * wy * dz);
            }

            coverage *= 1f / 64f;
            gradient *= 1f / 32f;
            float gradientMagnitude = math.length(gradient);
            if (gradientMagnitude <= MinCoverageGradient) return raw;

            float signedVoxels = (coverage - 0.5f) / gradientMagnitude;
            return math.clamp(-signedVoxels, -1f, 1f);
        }

        /// <summary>
        /// Air participates in an iso-surface too, so it must use the adjacent solid's profile.
        /// Looking through the filter radius keeps both sides of a stone/air crossing on the same
        /// smoothing contract instead of blending stone against an unrelated default-air profile.
        /// </summary>
        private byte ProfileMaterialAt(int x, int y, int z)
        {
            byte material = _baseMaterials[BaseIndex(x, y, z)];
            if (material != VoxelDimensions.MaterialEmpty) return material;

            float bestDistance = float.PositiveInfinity;
            byte bestMaterial = VoxelDimensions.MaterialEmpty;
            for (int dz = -BlurPasses; dz <= BlurPasses; dz++)
            for (int dy = -BlurPasses; dy <= BlurPasses; dy++)
            for (int dx = -BlurPasses; dx <= BlurPasses; dx++)
            {
                int qx = x + dx;
                int qy = y + dy;
                int qz = z + dz;
                if (qx < 0 || qy < 0 || qz < 0
                    || qx >= _baseSize.x || qy >= _baseSize.y || qz >= _baseSize.z)
                    continue;

                byte candidate = _baseMaterials[BaseIndex(qx, qy, qz)];
                if (candidate == VoxelDimensions.MaterialEmpty) continue;
                float distance = dx * dx + dy * dy + dz * dz;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestMaterial = candidate;
            }
            return bestMaterial;
        }

        private static float SurfaceModification(int3 worldVoxel, in VoxelSurfaceProfile profile)
        {
            if (profile.ModificationStrength <= 0.00001f) return 0f;

            float3 q = (float3)worldVoxel / profile.ModificationScaleVoxels;
            float a = math.sin(q.x * 1.37f + q.y * 0.71f + q.z * 1.11f);
            float b = math.sin(q.x * -0.63f + q.y * 1.57f + q.z * 0.83f + 1.91f);
            float c = math.sin(q.x * 0.93f + q.y * -1.21f + q.z * 1.73f + 4.17f);
            return ((a + b + c) / 3f) * profile.ModificationStrength;
        }

        private void BlurSeparable(float[] values, float[] scratch)
        {
            int sx = _baseSize.x;
            int sy = _baseSize.y;
            int sz = _baseSize.z;

            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                int xm = math.max(0, x - 1);
                int xp = math.min(sx - 1, x + 1);
                scratch[BaseIndex(x, y, z)] =
                    (values[BaseIndex(xm, y, z)] + values[BaseIndex(x, y, z)] * 2f
                     + values[BaseIndex(xp, y, z)]) * 0.25f;
            }

            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                int ym = math.max(0, y - 1);
                int yp = math.min(sy - 1, y + 1);
                values[BaseIndex(x, y, z)] =
                    (scratch[BaseIndex(x, ym, z)] + scratch[BaseIndex(x, y, z)] * 2f
                     + scratch[BaseIndex(x, yp, z)]) * 0.25f;
            }

            for (int z = 0; z < sz; z++)
            for (int y = 0; y < sy; y++)
            for (int x = 0; x < sx; x++)
            {
                int zm = math.max(0, z - 1);
                int zp = math.min(sz - 1, z + 1);
                scratch[BaseIndex(x, y, z)] =
                    (values[BaseIndex(x, y, zm)] + values[BaseIndex(x, y, z)] * 2f
                     + values[BaseIndex(x, y, zp)]) * 0.25f;
            }

            Array.Copy(scratch, values, values.Length);
        }

        private void BuildHalfVoxelDensity()
        {
            _density = new float[_sampleSize.x * _sampleSize.y * _sampleSize.z];
            for (int z = 0; z < _sampleSize.z; z++)
            for (int y = 0; y < _sampleSize.y; y++)
            for (int x = 0; x < _sampleSize.x; x++)
            {
                float3 p = (float3)_minVoxel
                    + new float3(x, y, z) * SampleStepVoxels;
                _density[SampleIndex(x, y, z)] = SampleBaseDensity(p);
            }
        }

        private float SampleBaseDensity(float3 worldVoxel)
        {
            float3 local = worldVoxel - (float3)_baseMin;
            int3 i0 = (int3)math.floor(local);
            float3 f = math.frac(local);
            i0 = math.clamp(i0, int3.zero, _baseSize - new int3(2));
            int3 i1 = i0 + 1;

            float c000 = BaseDensityAt(i0.x, i0.y, i0.z);
            float c100 = BaseDensityAt(i1.x, i0.y, i0.z);
            float c010 = BaseDensityAt(i0.x, i1.y, i0.z);
            float c110 = BaseDensityAt(i1.x, i1.y, i0.z);
            float c001 = BaseDensityAt(i0.x, i0.y, i1.z);
            float c101 = BaseDensityAt(i1.x, i0.y, i1.z);
            float c011 = BaseDensityAt(i0.x, i1.y, i1.z);
            float c111 = BaseDensityAt(i1.x, i1.y, i1.z);

            float x00 = math.lerp(c000, c100, f.x);
            float x10 = math.lerp(c010, c110, f.x);
            float x01 = math.lerp(c001, c101, f.x);
            float x11 = math.lerp(c011, c111, f.x);
            return math.lerp(math.lerp(x00, x10, f.y), math.lerp(x01, x11, f.y), f.z);
        }

        private void PolygoniseTetra(ShowcaseWorld world, int a, int b, int c, int d,
            float3[] positions, float[] values)
        {
            int[] ids = { a, b, c, d };
            int[] inside = new int[4];
            int[] outside = new int[4];
            int insideCount = 0;
            int outsideCount = 0;

            for (int i = 0; i < 4; i++)
            {
                int id = ids[i];
                if (values[id] < 0f) inside[insideCount++] = id;
                else outside[outsideCount++] = id;
            }

            if (insideCount == 0 || insideCount == 4) return;

            if (insideCount == 1)
            {
                int i = inside[0];
                EmitTriangle(world,
                    Interpolate(positions[i], positions[outside[0]], values[i], values[outside[0]]),
                    Interpolate(positions[i], positions[outside[1]], values[i], values[outside[1]]),
                    Interpolate(positions[i], positions[outside[2]], values[i], values[outside[2]]));
                return;
            }

            if (insideCount == 3)
            {
                int o = outside[0];
                EmitTriangle(world,
                    Interpolate(positions[o], positions[inside[0]], values[o], values[inside[0]]),
                    Interpolate(positions[o], positions[inside[2]], values[o], values[inside[2]]),
                    Interpolate(positions[o], positions[inside[1]], values[o], values[inside[1]]));
                return;
            }

            int i0 = inside[0];
            int i1 = inside[1];
            int o0 = outside[0];
            int o1 = outside[1];
            float3 p00 = Interpolate(positions[i0], positions[o0], values[i0], values[o0]);
            float3 p01 = Interpolate(positions[i0], positions[o1], values[i0], values[o1]);
            float3 p10 = Interpolate(positions[i1], positions[o0], values[i1], values[o0]);
            float3 p11 = Interpolate(positions[i1], positions[o1], values[i1], values[o1]);
            EmitTriangle(world, p00, p01, p11);
            EmitTriangle(world, p00, p11, p10);
        }

        private static float3 Interpolate(float3 a, float3 b, float da, float db)
        {
            float denominator = da - db;
            float t = math.abs(denominator) > 1e-6f ? da / denominator : 0.5f;
            return math.lerp(a, b, math.clamp(t, 0f, 1f));
        }

        private void EmitTriangle(ShowcaseWorld world, float3 a, float3 b, float3 c)
        {
            float3 centroid = (a + b + c) / 3f;
            float3 na = Gradient(a);
            float3 nb = Gradient(b);
            float3 nc = Gradient(c);
            float3 averageNormal = math.normalizesafe(na + nb + nc, new float3(0f, 1f, 0f));

            byte material = SurfaceMaterial(world, centroid, averageNormal);
            if (material == VoxelDimensions.MaterialEmpty || material >= MaterialCount) material = 1;
            VoxelSurfaceProfile profile = _surfaceProfiles.Get(material);
            a = Planarize(a, na, in profile);
            b = Planarize(b, nb, in profile);
            c = Planarize(c, nc, in profile);

            if (math.dot(math.cross(b - a, c - a), averageNormal) < 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
            }

            List<Vector3> vertices = _vertices[material];
            List<Vector3> normals = _normals[material];
            List<int> triangles = _triangles[material];
            int start = vertices.Count;
            vertices.Add((Vector3)(a * VoxelSize));
            vertices.Add((Vector3)(b * VoxelSize));
            vertices.Add((Vector3)(c * VoxelSize));
            normals.Add((Vector3)na);
            normals.Add((Vector3)nb);
            normals.Add((Vector3)nc);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static float3 Planarize(float3 position, float3 normal,
                                        in VoxelSurfaceProfile profile)
        {
            if (profile.Planarization <= 0.00001f) return position;

            float3 n = math.abs(normal);
            float dominance = n.x;
            int axis = 0;
            if (n.y > dominance)
            {
                dominance = n.y;
                axis = 1;
            }
            if (n.z > dominance)
            {
                dominance = n.z;
                axis = 2;
            }

            if (dominance <= profile.PlanarizationThreshold) return position;
            float range = math.max(0.0001f, 1f - profile.PlanarizationThreshold);
            float axisWeight = math.saturate((dominance - profile.PlanarizationThreshold) / range);
            axisWeight *= axisWeight;
            float strength = profile.Planarization * axisWeight;

            if (axis == 0)
            {
                float target = math.round(position.x - 0.5f) + 0.5f;
                position.x = math.lerp(position.x, target, strength);
            }
            else if (axis == 1)
            {
                float target = math.round(position.y - 0.5f) + 0.5f;
                position.y = math.lerp(position.y, target, strength);
            }
            else
            {
                float target = math.round(position.z - 0.5f) + 0.5f;
                position.z = math.lerp(position.z, target, strength);
            }
            return position;
        }

        private float3 Gradient(float3 worldVoxel)
        {
            float3 local = (worldVoxel - (float3)_minVoxel) * SamplesPerVoxel;
            const float h = 0.90f;
            float dx = SampleDensity(local + new float3(h, 0f, 0f))
                     - SampleDensity(local - new float3(h, 0f, 0f));
            float dy = SampleDensity(local + new float3(0f, h, 0f))
                     - SampleDensity(local - new float3(0f, h, 0f));
            float dz = SampleDensity(local + new float3(0f, 0f, h))
                     - SampleDensity(local - new float3(0f, 0f, h));
            return math.normalizesafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        private float SampleDensity(float3 sampleCoordinate)
        {
            float3 clamped = math.clamp(sampleCoordinate, float3.zero,
                (float3)(_sampleSize - new int3(1)));
            int3 i0 = (int3)math.floor(clamped);
            int3 i1 = math.min(i0 + 1, _sampleSize - new int3(1));
            float3 f = math.frac(clamped);

            float c000 = DensityAt(i0.x, i0.y, i0.z);
            float c100 = DensityAt(i1.x, i0.y, i0.z);
            float c010 = DensityAt(i0.x, i1.y, i0.z);
            float c110 = DensityAt(i1.x, i1.y, i0.z);
            float c001 = DensityAt(i0.x, i0.y, i1.z);
            float c101 = DensityAt(i1.x, i0.y, i1.z);
            float c011 = DensityAt(i0.x, i1.y, i1.z);
            float c111 = DensityAt(i1.x, i1.y, i1.z);
            float x00 = math.lerp(c000, c100, f.x);
            float x10 = math.lerp(c010, c110, f.x);
            float x01 = math.lerp(c001, c101, f.x);
            float x11 = math.lerp(c011, c111, f.x);
            return math.lerp(math.lerp(x00, x10, f.y), math.lerp(x01, x11, f.y), f.z);
        }

        private byte SurfaceMaterial(ShowcaseWorld world, float3 position, float3 outwardNormal)
        {
            ref RegionTable table = ref world.Table;
            ref BrickPool pool = ref world.Pool;
            int3 centre = (int3)math.round(position - outwardNormal * 0.65f);
            byte material = SampleMaterial(ref table, in pool, centre);
            if (material != VoxelDimensions.MaterialEmpty) return material;

            float best = float.PositiveInfinity;
            byte bestMaterial = 0;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int3 q = centre + new int3(x, y, z);
                byte candidate = SampleMaterial(ref table, in pool, q);
                if (candidate == VoxelDimensions.MaterialEmpty) continue;
                float distance = x * x + y * y + z * z;
                if (distance >= best) continue;
                best = distance;
                bestMaterial = candidate;
            }
            return bestMaterial;
        }

        private static byte SampleMaterial(ref RegionTable table, in BrickPool pool, int3 voxel)
        {
            int3 worldBrick = new(FloorDiv(voxel.x, BrickEdge),
                                  FloorDiv(voxel.y, BrickEdge),
                                  FloorDiv(voxel.z, BrickEdge));
            int3 regionCoord = new(worldBrick.x >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.y >> VoxelDimensions.RegionEdgeLog2,
                                   worldBrick.z >> VoxelDimensions.RegionEdgeLog2);
            if (!table.TryGetRegion(regionCoord, out Region region))
                return VoxelDimensions.MaterialEmpty;

            int bx = worldBrick.x & VoxelDimensions.RegionEdgeMask;
            int by = worldBrick.y & VoxelDimensions.RegionEdgeMask;
            int bz = worldBrick.z & VoxelDimensions.RegionEdgeMask;
            BrickRef brick = region.GetBrick(bx, by, bz);
            if (brick.IsEmpty) return VoxelDimensions.MaterialEmpty;
            if (brick.IsUniform) return brick.UniformMaterial;

            int lx = FloorMod(voxel.x, BrickEdge);
            int ly = FloorMod(voxel.y, BrickEdge);
            int lz = FloorMod(voxel.z, BrickEdge);
            int voxelIndex = lx + BrickEdge * (ly + BrickEdge * lz);
            return pool.Voxels[pool.VoxelOffset(brick.PoolIndex) + voxelIndex];
        }

        private void UploadMeshes()
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            for (int material = 1; material < MaterialCount; material++)
            {
                if (_vertices[material].Count == 0) continue;

                var mesh = new Mesh
                {
                    name = $"Voxel Hero Smooth material {material}",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.SetVertices(_vertices[material]);
                mesh.SetNormals(_normals[material]);
                mesh.SetTriangles(_triangles[material], 0, true);
                mesh.RecalculateBounds();
                _meshes.Add(mesh);

                var go = new GameObject(MaterialName(material));
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                if (shader != null)
                {
                    var placeholder = new Material(shader) { name = $"Voxel Hero Placeholder {material}" };
                    renderer.sharedMaterial = placeholder;
                    _placeholderMaterials.Add(placeholder);
                }

                VertexCount += mesh.vertexCount;
                RegionMeshCount++;
            }
        }

        private void ConsumeIntersectingDirtyRegions(ShowcaseWorld world)
        {
            int regionEdge = ShowcaseWorld.RegionVoxelEdge;
            int3 maxVoxel = _minVoxel + _sizeVoxels;
            int3 minRegion = new(FloorDiv(_minVoxel.x, regionEdge), FloorDiv(_minVoxel.y, regionEdge),
                                 FloorDiv(_minVoxel.z, regionEdge));
            int3 maxRegion = new(FloorDiv(maxVoxel.x, regionEdge), FloorDiv(maxVoxel.y, regionEdge),
                                 FloorDiv(maxVoxel.z, regionEdge));
            for (int z = minRegion.z; z <= maxRegion.z; z++)
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int x = minRegion.x; x <= maxRegion.x; x++)
                world.DirtyRegions.Remove(new int3(x, y, z));
        }

        private float DensityAt(int x, int y, int z) => _density[SampleIndex(x, y, z)];
        private int SampleIndex(int x, int y, int z) => x + _sampleSize.x * (y + _sampleSize.y * z);
        private float BaseDensityAt(int x, int y, int z) => _baseDensity[BaseIndex(x, y, z)];
        private int BaseIndex(int x, int y, int z) => x + _baseSize.x * (y + _baseSize.y * z);

        private static string MaterialName(int material) => material switch
        {
            1 => "stone",
            2 => "wood",
            3 => "sand",
            4 => "glass",
            5 => "bedrock",
            6 => "darkstone",
            7 => "slate",
            8 => "roof",
            9 => "cloth",
            10 => "grass",
            11 => "water",
            12 => "gold",
            13 => "dirt",
            14 => "moss",
            15 => "window",
            16 => "cascade",
            17 => "crystal",
            _ => $"material-{material}"
        };

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            foreach (Mesh mesh in _meshes) DestroyObject(mesh);
            foreach (Material material in _placeholderMaterials) DestroyObject(material);
            DestroyObject(_root);
            _meshes.Clear();
            _placeholderMaterials.Clear();
            _baseDensity = null;
            _baseMaterials = null;
            _density = null;
        }
    }
}
