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
    /// Bounded close-up renderer for authoritative voxel structures. Material profiles select the
    /// density blur/recovery and feature preservation used to derive presentation geometry.
    /// </summary>
    public sealed class VoxelHeroSurfaceRenderer : IDisposable
    {
        public const float VoxelSize = 0.1f;
        private const int BrickEdge = VoxelDimensions.BrickEdge;
        private const int MaterialCount = 18;
        private const int SamplesPerVoxel = 2;
        private const float SampleStepVoxels = 1f / SamplesPerVoxel;
        private const int MaxBlurPasses = VoxelSurfaceProfile.MaxSupportedBlurPasses;
        private const float MinCoverageGradient = 0.05f;

        private static readonly int3[] CubeCorners =
        {
            new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0),
            new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)
        };

        private static readonly int[,] Tetrahedra =
        {
            { 0, 5, 1, 6 }, { 0, 1, 2, 6 }, { 0, 2, 3, 6 },
            { 0, 3, 7, 6 }, { 0, 7, 4, 6 }, { 0, 4, 5, 6 }
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
            _surfaceProfiles = surfaceProfiles ?? VoxelSurfaceProfileSet.Canonical();
            _root = new GameObject("Voxel Hero Material-Aware Surface") { hideFlags = HideFlags.DontSave };

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
                    PolygoniseTetra(world, Tetrahedra[t, 0], Tetrahedra[t, 1],
                        Tetrahedra[t, 2], Tetrahedra[t, 3], cornerPosition, cornerDensity);
            }

            UploadMeshes();
            _baseDensity = null;
            _baseMaterials = null;
            _density = null;
        }

        /// <summary>
        /// Builds raw/one-pass/two-pass occupancy fields once, then lets each material select its
        /// own field before distance recovery. Stone choosing zero blur is therefore never melted by
        /// terrain smoothing. Empty samples inherit the nearest solid profile at an iso crossing.
        /// </summary>
        private void BuildBaseDensity(ShowcaseWorld world)
        {
            int margin = MaxBlurPasses + 2;
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
                int index = BaseIndex(x, y, z);
                _baseMaterials[index] = material;
                original[index] = material != VoxelDimensions.MaterialEmpty ? -1f : 1f;
            }

            var blurLevels = new float[MaxBlurPasses + 1][];
            blurLevels[0] = original;
            var scratch = new float[count];
            for (int pass = 1; pass <= MaxBlurPasses; pass++)
            {
                float[] level = new float[count];
                Array.Copy(blurLevels[pass - 1], level, count);
                BlurSeparable(level, scratch);
                blurLevels[pass] = level;
            }

            for (int z = 0; z < _baseSize.z; z++)
            for (int y = 0; y < _baseSize.y; y++)
            for (int x = 0; x < _baseSize.x; x++)
            {
                int index = BaseIndex(x, y, z);
                VoxelSurfaceProfile profile = _surfaceProfiles.Get(ProfileMaterialAt(x, y, z));
                float raw = original[index];
                float filtered = blurLevels[profile.BlurPasses][index];

                if (profile.DistanceRecovery > 0.00001f)
                {
                    float distanceRecovered = DistanceRecoveredDensity(
                        x, y, z, raw, profile.CurveRecovery);
                    filtered = math.lerp(filtered, distanceRecovered, profile.DistanceRecovery);
                }

                float shaped = math.lerp(raw, filtered, profile.Smoothing);
                shaped -= profile.DensityBias;
                shaped -= SurfaceModification(_baseMin + new int3(x, y, z), in profile);
                _baseDensity[index] = math.clamp(shaped, -1.5f, 1.5f);
            }
        }

        private float DistanceRecoveredDensity(int centreX, int centreY, int centreZ,
                                               float raw, float curveRecovery)
        {
            float tight = DistanceRecoveredDensityRadiusOne(centreX, centreY, centreZ, raw);
            if (curveRecovery <= 0.00001f) return tight;
            float wide = DistanceRecoveredDensityRadiusTwo(centreX, centreY, centreZ, tight);
            return math.lerp(tight, wide, curveRecovery);
        }

        private float DistanceRecoveredDensityRadiusOne(int centreX, int centreY, int centreZ,
                                                        float raw)
        {
            float coverage = 0f;
            float3 gradient = float3.zero;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float occupied = OccupiedAt(centreX + x, centreY + y, centreZ + z);
                float wx = x == 0 ? 2f : 1f;
                float wy = y == 0 ? 2f : 1f;
                float wz = z == 0 ? 2f : 1f;
                coverage += occupied * wx * wy * wz;
                gradient += occupied * new float3(x * wy * wz, wx * y * wz, wx * wy * z);
            }
            coverage *= 1f / 64f;
            gradient *= 1f / 32f;
            return RecoveredDensity(coverage, gradient, raw);
        }

        private float DistanceRecoveredDensityRadiusTwo(int centreX, int centreY, int centreZ,
                                                        float fallback)
        {
            float coverage = 0f;
            float3 gradient = float3.zero;
            for (int z = -2; z <= 2; z++)
            for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                float occupied = OccupiedAt(centreX + x, centreY + y, centreZ + z);
                float wx = BinomialRadiusTwo(x);
                float wy = BinomialRadiusTwo(y);
                float wz = BinomialRadiusTwo(z);
                float dx = BinomialDerivativeRadiusTwo(x);
                float dy = BinomialDerivativeRadiusTwo(y);
                float dz = BinomialDerivativeRadiusTwo(z);
                coverage += occupied * wx * wy * wz;
                gradient += occupied * new float3(dx * wy * wz, wx * dy * wz, wx * wy * dz);
            }
            coverage *= 1f / 4096f;
            gradient *= 1f / 2048f;
            return RecoveredDensity(coverage, gradient, fallback);
        }

        private float OccupiedAt(int x, int y, int z)
        {
            x = math.clamp(x, 0, _baseSize.x - 1);
            y = math.clamp(y, 0, _baseSize.y - 1);
            z = math.clamp(z, 0, _baseSize.z - 1);
            return _baseMaterials[BaseIndex(x, y, z)] != VoxelDimensions.MaterialEmpty ? 1f : 0f;
        }

        private static float RecoveredDensity(float coverage, float3 gradient, float fallback)
        {
            float gradientMagnitude = math.length(gradient);
            if (gradientMagnitude <= MinCoverageGradient) return fallback;
            float signedVoxels = (coverage - 0.5f) / gradientMagnitude;
            return math.clamp(-signedVoxels, -1.5f, 1.5f);
        }

        private static float BinomialRadiusTwo(int offset)
        {
            int a = math.abs(offset);
            return a == 0 ? 6f : a == 1 ? 4f : 1f;
        }

        private static float BinomialDerivativeRadiusTwo(int offset) => offset switch
        {
            -2 => -1f, -1 => -2f, 0 => 0f, 1 => 2f, 2 => 1f, _ => 0f
        };

        private byte ProfileMaterialAt(int x, int y, int z)
        {
            byte material = _baseMaterials[BaseIndex(x, y, z)];
            if (material != VoxelDimensions.MaterialEmpty) return material;

            float bestDistance = float.PositiveInfinity;
            byte bestMaterial = VoxelDimensions.MaterialEmpty;
            for (int dz = -MaxBlurPasses; dz <= MaxBlurPasses; dz++)
            for (int dy = -MaxBlurPasses; dy <= MaxBlurPasses; dy++)
            for (int dx = -MaxBlurPasses; dx <= MaxBlurPasses; dx++)
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
                float3 p = (float3)_minVoxel + new float3(x, y, z) * SampleStepVoxels;
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
            float3 materialNormal = math.normalizesafe(na + nb + nc, new float3(0f, 1f, 0f));

            byte material = SurfaceMaterial(world, centroid, materialNormal);
            if (material == VoxelDimensions.MaterialEmpty || material >= MaterialCount) material = 1;
            VoxelSurfaceProfile profile = _surfaceProfiles.Get(material);

            int maskA = FeatureSnap(ref a, na, in profile);
            int maskB = FeatureSnap(ref b, nb, in profile);
            int maskC = FeatureSnap(ref c, nc, in profile);

            float3 faceNormal = math.normalizesafe(math.cross(b - a, c - a), materialNormal);
            if (math.dot(faceNormal, materialNormal) < 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
                (maskB, maskC) = (maskC, maskB);
                faceNormal = -faceNormal;
            }

            int featureMask = maskA | maskB | maskC;
            if (featureMask != 0 && profile.NormalPlanarization > 0.00001f)
            {
                float faceDominance = math.max(math.abs(faceNormal.x),
                    math.max(math.abs(faceNormal.y), math.abs(faceNormal.z)));
                float faceWeight = PlanarWeight(faceDominance, profile.PlanarizationThreshold)
                                 * profile.NormalPlanarization;
                if (faceWeight > 0.00001f)
                {
                    na = math.normalizesafe(math.lerp(na, faceNormal, faceWeight), faceNormal);
                    nb = math.normalizesafe(math.lerp(nb, faceNormal, faceWeight), faceNormal);
                    nc = math.normalizesafe(math.lerp(nc, faceNormal, faceWeight), faceNormal);
                }
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

        /// <summary>
        /// Snaps only axes that both look planar in the gradient and are already close to an exact
        /// half-voxel boundary. Multiple axes may snap at a true edge/corner. A circular SDF point
        /// away from those planes is untouched, so curve recovery is not undone by planarization.
        /// Returns a bit mask of axes that participated.
        /// </summary>
        private static int FeatureSnap(ref float3 position, float3 normal,
                                       in VoxelSurfaceProfile profile)
        {
            if (profile.Planarization <= 0.00001f) return 0;
            int mask = 0;
            float3 absNormal = math.abs(normal);

            for (int axis = 0; axis < 3; axis++)
            {
                float component = axis == 0 ? absNormal.x : axis == 1 ? absNormal.y : absNormal.z;
                if (component <= profile.PlanarizationThreshold) continue;

                float value = axis == 0 ? position.x : axis == 1 ? position.y : position.z;
                float target = math.round(value - 0.5f) + 0.5f;
                float distance = math.abs(value - target);
                if (distance > profile.PlanarSnapDistanceVoxels) continue;

                float directional = PlanarWeight(component, profile.PlanarizationThreshold);
                float proximity = 1f - distance / profile.PlanarSnapDistanceVoxels;
                proximity *= proximity;
                float strength = profile.Planarization * directional * proximity;
                float snapped = math.lerp(value, target, strength);

                if (axis == 0) position.x = snapped;
                else if (axis == 1) position.y = snapped;
                else position.z = snapped;
                mask |= 1 << axis;
            }
            return mask;
        }

        private static float PlanarWeight(float component, float threshold)
        {
            float range = math.max(0.0001f, 1f - threshold);
            float weight = math.saturate((component - threshold) / range);
            return weight * weight;
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
                    name = $"Voxel Hero material {material}",
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
            1 => "stone", 2 => "wood", 3 => "sand", 4 => "glass", 5 => "bedrock",
            6 => "darkstone", 7 => "slate", 8 => "roof", 9 => "cloth", 10 => "grass",
            11 => "water", 12 => "gold", 13 => "dirt", 14 => "moss", 15 => "window",
            16 => "cascade", 17 => "crystal", _ => $"material-{material}"
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
