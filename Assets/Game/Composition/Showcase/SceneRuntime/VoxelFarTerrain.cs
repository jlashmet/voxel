using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Composition.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Draws terrain beyond the voxel streaming radius as a geometric clipmap sampled straight
    /// from <see cref="TerrainSampler"/> and amended by permanent authored surface metadata.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VoxelFarTerrain : MonoBehaviour
    {
        [Tooltip("Where the voxel world stops and this takes over, in metres.")]
        [SerializeField] private float m_InnerRadiusMetres = 220f;

        [Tooltip("Outermost extent, in metres.")]
        [SerializeField] private float m_OuterRadiusMetres = 4000f;

        [Tooltip("Vertices per axis in each clipmap ring. Higher is denser but costs the same per ring, so this is the main quality dial.")]
        [SerializeField] private int m_Resolution = 96;

        [SerializeField] private Material m_Material;

        [Tooltip("Terrain seed. Must match the voxel world or the two will disagree.")]
        [SerializeField] private uint m_Seed = 1;

        public FarFieldStructureStore Structures { get; set; }
        public ShowcaseMaterialSet MaterialRoles { get; set; }

        private readonly List<Mesh> _ringMeshes = new();
        private readonly List<int> _ringSpacing = new();
        private readonly List<int2> _ringOrigin = new();
        private readonly List<NativeArray<int>> _ringHeights = new();
        private readonly List<bool> _ringHeightValid = new();
        private readonly List<int> _ringBuiltStructureVersion = new();
        private readonly List<float> _ringBuiltTopologyHoleMetres = new();
        private readonly List<int> _indicesScratch = new();
        private readonly List<Vector3> _startupFallbackPositions = new();
        private readonly List<Color> _startupFallbackColours = new();
        private readonly List<int> _startupFallbackIndices = new();
        private Vector3[] _positionsScratch;
        private Color[] _coloursScratch;
        private MeshRenderer _renderer;
        private Camera _camera;
        private bool _ownsMaterial;
        private bool _coverageFailureLogged;

        private JobHandle _heightJobHandle;
        private bool _heightJobScheduled;
        private int _heightJobRing = -1;
        private int2 _heightJobOrigin;
        private int _ringWorkCursor;
        private ulong _topologyRebuildCount;

        private const int StartupFallbackSpacingMultiplier = 4;
        private bool _startupFallbackInitialized;
        private int _startupFallbackRing = -1;
        private int _startupFallbackCoverageRing = -1;
        private Vector2 _startupFallbackCameraXZ = new(float.PositiveInfinity, float.PositiveInfinity);
        private bool _requirePublishedNearCoverage;

        public float InnerRadiusMetres => m_InnerRadiusMetres;
        public float OuterRadiusMetres => m_OuterRadiusMetres;
        public uint Seed { get => m_Seed; set => m_Seed = value; }
        public bool StartupFallbackActive => _startupFallbackRing >= 0;

        /// <summary>Worst-case cardinal radius currently guaranteed by the configured outer ring.</summary>
        public float GuaranteedAuthoritativeRadiusMetres
        {
            get
            {
                FarTerrainCoverageMath.TryCalculateRequiredRingCount(
                    m_InnerRadiusMetres,
                    m_OuterRadiusMetres,
                    m_Resolution,
                    out int rings,
                    out _);
                return FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                    m_InnerRadiusMetres,
                    m_Resolution,
                    rings - 1);
            }
        }

        public static byte ResolveFarSurfaceMaterial(
            ShowcaseMaterialSet materialRoles,
            bool isStructure,
            bool hasAuthoredTerrain,
            byte authoredTerrainMaterial,
            int height)
        {
            if (isStructure) return materialRoles.FarStructure;
            if (hasAuthoredTerrain && authoredTerrainMaterial != 0)
                return authoredTerrainMaterial;
            return materialRoles.SurfaceAt(height, ShowcaseWorld.BaseHeightVoxels);
        }

        public ulong TopologyRebuildCount => _topologyRebuildCount;

        public bool HasSampledHeightsForEveryRing
        {
            get
            {
                if (_ringMeshes.Count == 0 || _startupFallbackRing >= 0) return false;
                for (int ring = 0; ring < _ringHeightValid.Count; ring++)
                    if (!_ringHeightValid[ring]) return false;
                return true;
            }
        }

        public float HoleRadiusMetres
        {
            get => _holeRadiusMetres;
            set
            {
                float requested = Mathf.Clamp(value, 0f, m_InnerRadiusMetres);
                _requestedHoleRadiusMetres = requested;
                if (_requirePublishedNearCoverage)
                {
                    if (!RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
                    {
                        _holeRadiusMetres = 0f;
                        return;
                    }

                    requested = GroundProjectedNearRadius(requested);
                    float snapCellMetres = SpacingForRing(0) * 0.1f;
                    float snapDiagonalGuard = snapCellMetres * Mathf.Sqrt(2f);
                    _holeRadiusMetres = Mathf.Max(0f, requested - snapDiagonalGuard);
                    return;
                }

                _holeRadiusMetres = requested;
            }
        }

        private float _holeRadiusMetres;
        private float _requestedHoleRadiusMetres;
        private float _builtHoleRadiusMetres = -1f;
        private Vector3 _groundProjectionCameraPosition = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        private float _groundProjectionNearRadius = -1f;
        private float _cachedGroundProjectedNearRadius;

        private float GroundProjectedNearRadius(float nearRadiusMetres)
        {
            if (_camera == null || nearRadiusMetres <= 0f) return nearRadiusMetres;

            Vector3 cameraPosition = _camera.transform.position;
            if (Mathf.Approximately(nearRadiusMetres, _groundProjectionNearRadius)
                && (cameraPosition - _groundProjectionCameraPosition).sqrMagnitude < 0.25f)
                return _cachedGroundProjectedNearRadius;

            const int angularSamples = 16;
            float radialStep = Mathf.Max(SpacingForRing(0) * 0.2f, 1.6f);
            float nearRadiusSq = nearRadiusMetres * nearRadiusMetres;
            float safeRadius = 0f;
            for (float radius = radialStep; radius <= nearRadiusMetres; radius += radialStep)
            {
                bool shellFits = true;
                for (int sample = 0; sample < angularSamples; sample++)
                {
                    float angle = sample * (Mathf.PI * 2f / angularSamples);
                    float worldX = cameraPosition.x + Mathf.Cos(angle) * radius;
                    float worldZ = cameraPosition.z + Mathf.Sin(angle) * radius;
                    int voxelX = Mathf.FloorToInt(worldX / 0.1f);
                    int voxelZ = Mathf.FloorToInt(worldZ / 0.1f);
                    int terrainHeight = TerrainSampler.HeightAt(voxelX, voxelZ, m_Seed);
                    if (Structures != null)
                    {
                        int authored = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);
                        if (authored != int.MinValue) terrainHeight = authored;
                    }
                    float terrainY = terrainHeight * 0.1f;
                    float vertical = cameraPosition.y - terrainY;
                    if (radius * radius + vertical * vertical <= nearRadiusSq) continue;
                    shellFits = false;
                    break;
                }

                if (!shellFits) break;
                safeRadius = radius;
            }

            _groundProjectionCameraPosition = cameraPosition;
            _groundProjectionNearRadius = nearRadiusMetres;
            _cachedGroundProjectedNearRadius = safeRadius;
            return safeRadius;
        }

        public int RingCount
        {
            get
            {
                bool covered = FarTerrainCoverageMath.TryCalculateRequiredRingCount(
                    m_InnerRadiusMetres,
                    m_OuterRadiusMetres,
                    m_Resolution,
                    out int rings,
                    out float guaranteedCoverageMetres);
                if (covered)
                {
                    _coverageFailureLogged = false;
                    return rings;
                }

                if (!_coverageFailureLogged)
                {
                    Debug.LogError($"VoxelFarTerrain: requested {m_OuterRadiusMetres:F1} m far radius cannot be guaranteed with MaxRings={FarTerrainCoverageMath.MaxRings}; worst-case snapped coverage is {guaranteedCoverageMetres:F1} m.");
                    _coverageFailureLogged = true;
                }
                return rings;
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null) _renderer.enabled = false;
            MaterialRoles = Game.Composition.Materials.GameMaterialComposition.ShowcaseMaterials;
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (m_Material != null) return;
            Shader shader = Shader.Find("VoxelEngine/FarTerrain");
            if (shader == null)
            {
                Debug.LogError("VoxelFarTerrain: VoxelEngine/FarTerrain shader is missing. Distant terrain will fall back to an unlit flat colour.");
                shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }
            if (shader == null) return;
            m_Material = new Material(shader) { name = "FarTerrainDefault" };
            _ownsMaterial = true;
            if (m_Material.HasProperty("_Smoothness")) m_Material.SetFloat("_Smoothness", 0.05f);
        }

        public static VoxelFarTerrain Create(Transform parent, uint seed, float innerRadiusMetres, float outerRadiusMetres)
        {
            var go = new GameObject("VoxelFarTerrain");
            if (parent != null) go.transform.SetParent(parent, false);
            var far = go.AddComponent<VoxelFarTerrain>();
            far.m_Seed = seed;
            far.m_InnerRadiusMetres = innerRadiusMetres;
            far.m_OuterRadiusMetres = outerRadiusMetres;
            far._requirePublishedNearCoverage = true;
            return far;
        }

        private void OnDestroy()
        {
            if (_heightJobScheduled)
            {
                _heightJobHandle.Complete();
                _heightJobScheduled = false;
            }
            for (int i = 0; i < _ringHeights.Count; i++) if (_ringHeights[i].IsCreated) _ringHeights[i].Dispose();
            _ringHeights.Clear();
            for (int i = 0; i < _ringMeshes.Count; i++) if (_ringMeshes[i] != null) Destroy(_ringMeshes[i]);
            _ringMeshes.Clear();
            _ringBuiltTopologyHoleMetres.Clear();
            if (_ownsMaterial && m_Material != null) Destroy(m_Material);
            if (_ownsMaterial) m_Material = null;
            _ownsMaterial = false;
        }

        public int SpacingForRing(int ring)
        {
            return FarTerrainCoverageMath.RingSpacingVoxels(m_InnerRadiusMetres, m_Resolution, ring);
        }

        private void LateUpdate()
        {
            if (m_Material == null) return;
            _camera = _camera != null ? _camera : Camera.main;
            if (_camera == null) return;
            EnsureRings();
            Vector3 cameraPosition = _camera.transform.position;
            int structureVersion = Structures?.Version ?? 0;
            bool rebuiltThisFrame = false;

            if (_heightJobScheduled && _heightJobHandle.IsCompleted)
            {
                _heightJobHandle.Complete();
                int ring = _heightJobRing;
                _heightJobScheduled = false;
                _heightJobRing = -1;
                _ringHeightValid[ring] = true;
                _ringOrigin[ring] = _heightJobOrigin;
                bool completingFallback = ring == _startupFallbackRing;
                float requestedHole = _holeRadiusMetres;
                bool staleCriticalPublication = ring == 0 && !OriginFor(cameraPosition, _ringSpacing[0]).Equals(_heightJobOrigin);
                if (!completingFallback)
                {
                    if (staleCriticalPublication) _holeRadiusMetres = 0f;
                    RebuildRingFromCachedHeights(ring, _ringOrigin[ring], _ringSpacing[ring]);
                    if (staleCriticalPublication) _holeRadiusMetres = requestedHole;
                    _ringBuiltStructureVersion[ring] = structureVersion;
                }
                if (_startupFallbackRing >= 0 && ring != _startupFallbackRing && ring == _startupFallbackCoverageRing + 1)
                {
                    _startupFallbackCoverageRing = ring;
                    BuildStartupFallback(_ringMeshes[_startupFallbackRing], _startupFallbackCoverageRing);
                }
                bool retiredFallback = TryRetireStartupFallback(cameraPosition, structureVersion);
                if (ring == 0) _builtHoleRadiusMetres = staleCriticalPublication ? 0f : _holeRadiusMetres;
                _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                rebuiltThisFrame = !completingFallback || retiredFallback;
            }

            if (_startupFallbackRing >= 0)
            {
                float recenterMetres = Mathf.Max(_ringSpacing[0] * 0.1f, 1f);
                Vector2 cameraXZ = new(cameraPosition.x, cameraPosition.z);
                if ((cameraXZ - _startupFallbackCameraXZ).sqrMagnitude >= recenterMetres * recenterMetres)
                    BuildStartupFallback(_ringMeshes[_startupFallbackRing], _startupFallbackCoverageRing);
            }

            int criticalSpacing = _ringSpacing[0];
            int2 criticalOrigin = OriginFor(cameraPosition, criticalSpacing);
            bool criticalOriginStale = _ringHeightValid[0] && !criticalOrigin.Equals(_ringOrigin[0]);
            if (criticalOriginStale && _ringBuiltTopologyHoleMetres[0] > 0.05f)
            {
                CloseRingZeroHoleTopology();
                _builtHoleRadiusMetres = 0f;
                rebuiltThisFrame = true;
            }

            if (!rebuiltThisFrame && RingNeedsPresentationRefresh(0, cameraPosition, structureVersion))
            {
                RebuildRingFromCachedHeights(0, _ringOrigin[0], _ringSpacing[0]);
                _ringBuiltStructureVersion[0] = structureVersion;
                _builtHoleRadiusMetres = _holeRadiusMetres;
                _ringWorkCursor = _ringMeshes.Count > 1 ? 1 : 0;
                rebuiltThisFrame = true;
            }

            if (!rebuiltThisFrame)
            {
                for (int offset = 0; offset < _ringMeshes.Count; offset++)
                {
                    int ring = (_ringWorkCursor + offset) % _ringMeshes.Count;
                    if (ring == 0 || !RingNeedsPresentationRefresh(ring, cameraPosition, structureVersion)) continue;
                    RebuildRingFromCachedHeights(ring, _ringOrigin[ring], _ringSpacing[ring]);
                    _ringBuiltStructureVersion[ring] = structureVersion;
                    _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                    rebuiltThisFrame = true;
                    break;
                }
            }

            if (!_heightJobScheduled)
            {
                bool criticalNeedsSample = !_ringHeightValid[0] || !criticalOrigin.Equals(_ringOrigin[0]);
                if (criticalNeedsSample)
                {
                    ScheduleHeightJob(0, criticalOrigin, criticalSpacing);
                    _ringWorkCursor = _ringMeshes.Count > 1 ? 1 : 0;
                }
                else
                {
                    for (int offset = 0; offset < _ringMeshes.Count; offset++)
                    {
                        int ring = (_ringWorkCursor + offset) % _ringMeshes.Count;
                        if (ring == 0) continue;
                        int spacing = _ringSpacing[ring];
                        int2 targetOrigin = OriginFor(cameraPosition, spacing);
                        if (_ringHeightValid[ring] && targetOrigin.Equals(_ringOrigin[ring])) continue;
                        ScheduleHeightJob(ring, targetOrigin, spacing);
                        _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                        break;
                    }
                }
            }

            for (int ring = 0; ring < _ringMeshes.Count; ring++)
            {
                if (!_ringHeightValid[ring] && ring != _startupFallbackRing) continue;
                Graphics.DrawMesh(_ringMeshes[ring], Matrix4x4.identity, m_Material, gameObject.layer, _camera);
            }
        }

        private bool RingNeedsPresentationRefresh(int ring, Vector3 cameraPosition, int structureVersion)
        {
            if (ring < 0 || ring >= _ringMeshes.Count || !_ringHeightValid[ring]) return false;
            if (ring == _startupFallbackRing) return false;
            if (_heightJobScheduled && _heightJobRing == ring) return false;
            int2 targetOrigin = OriginFor(cameraPosition, _ringSpacing[ring]);
            if (!targetOrigin.Equals(_ringOrigin[ring])) return false;
            if (_ringBuiltStructureVersion[ring] != structureVersion) return true;
            return ring == 0 && Mathf.Abs(_holeRadiusMetres - _builtHoleRadiusMetres) >= 1f;
        }

        private int CurrentAuthoritativePrefixRingCount(Vector3 cameraPosition)
        {
            int count = 0;
            for (int ring = 0; ring < _ringHeightValid.Count; ring++)
            {
                if (!_ringHeightValid[ring]) break;
                int spacing = _ringSpacing[ring];
                if (!OriginFor(cameraPosition, spacing).Equals(_ringOrigin[ring])) break;
                count++;
            }
            return count;
        }

        private bool TryRetireStartupFallback(Vector3 cameraPosition, int structureVersion)
        {
            if (_startupFallbackRing < 0) return false;
            int authoritativePrefix = CurrentAuthoritativePrefixRingCount(cameraPosition);
            if (!FarTerrainCoverageMath.CanRetireStartupFallback(authoritativePrefix, m_InnerRadiusMetres, m_OuterRadiusMetres, m_Resolution)) return false;
            int fallbackRing = _startupFallbackRing;
            RebuildRingFromCachedHeights(fallbackRing, _ringOrigin[fallbackRing], _ringSpacing[fallbackRing]);
            _ringBuiltStructureVersion[fallbackRing] = structureVersion;
            _startupFallbackRing = -1;
            _startupFallbackCoverageRing = -1;
            return true;
        }

        private int2 OriginFor(Vector3 cameraPosition, int spacing)
        {
            int centreX = Mathf.FloorToInt(cameraPosition.x / 0.1f);
            int centreZ = Mathf.FloorToInt(cameraPosition.z / 0.1f);
            int half = spacing * m_Resolution / 2;
            return new int2(FloorTo(centreX, spacing) - half, FloorTo(centreZ, spacing) - half);
        }

        private void ScheduleHeightJob(int ring, int2 origin, int spacing)
        {
            int verts = m_Resolution + 1;
            NativeArray<int> heights = _ringHeights[ring];
            _heightJobHandle = new FarTerrainHeightJob { Origin = origin, Spacing = spacing, VertsPerAxis = verts, Seed = m_Seed, Heights = heights }.Schedule(verts * verts, 64);
            _heightJobRing = ring;
            _heightJobOrigin = origin;
            _heightJobScheduled = true;
        }

        private static int FloorTo(int value, int step)
        {
            int quotient = value / step;
            if (value % step != 0 && value < 0) quotient--;
            return quotient * step;
        }

        private void EnsureRings()
        {
            int wanted = RingCount;
            int verts = m_Resolution + 1;
            int sampleCount = verts * verts;
            if (_positionsScratch == null || _positionsScratch.Length != sampleCount)
            {
                _positionsScratch = new Vector3[sampleCount];
                _coloursScratch = new Color[sampleCount];
                _indicesScratch.Clear();
                _indicesScratch.Capacity = Math.Max(_indicesScratch.Capacity, m_Resolution * m_Resolution * 6);
            }
            while (_ringMeshes.Count < wanted)
            {
                int ring = _ringMeshes.Count;
                var mesh = new Mesh { name = $"FarTerrainRing{ring}" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.MarkDynamic();
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (m_OuterRadiusMetres * 4f));
                _ringMeshes.Add(mesh);
                _ringSpacing.Add(SpacingForRing(ring));
                _ringOrigin.Add(new int2(int.MinValue, int.MinValue));
                _ringHeights.Add(new NativeArray<int>(sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory));
                _ringHeightValid.Add(false);
                _ringBuiltStructureVersion.Add(int.MinValue);
                _ringBuiltTopologyHoleMetres.Add(float.NaN);
            }
            if (!_startupFallbackInitialized && _ringMeshes.Count > 0)
            {
                _startupFallbackInitialized = true;
                BuildCriticalStartupFallback();
                if (_ringMeshes.Count > 1)
                {
                    _startupFallbackRing = _ringMeshes.Count - 1;
                    _startupFallbackCoverageRing = 0;
                    BuildStartupFallback(_ringMeshes[_startupFallbackRing], _startupFallbackCoverageRing);
                }
            }
        }

        private void BuildCriticalStartupFallback()
        {
            const int ring = 0;
            int spacing = _ringSpacing[ring];
            Vector3 cameraPosition = _camera != null ? _camera.transform.position : transform.position;
            int2 origin = OriginFor(cameraPosition, spacing);
            int verts = m_Resolution + 1;
            new FarTerrainHeightJob { Origin = origin, Spacing = spacing, VertsPerAxis = verts, Seed = m_Seed, Heights = _ringHeights[ring] }.Run(verts * verts);
            _ringHeightValid[ring] = true;
            _ringOrigin[ring] = origin;
            RebuildRingFromCachedHeights(ring, origin, spacing);
            _ringBuiltStructureVersion[ring] = Structures?.Version ?? 0;
            _builtHoleRadiusMetres = _holeRadiusMetres;
            _ringWorkCursor = _ringMeshes.Count > 1 ? 1 : 0;
        }

        private void BuildStartupFallback(Mesh mesh, int coverageRing)
        {
            Vector3 cameraPosition = _camera != null ? _camera.transform.position : transform.position;
            _startupFallbackCameraXZ = new Vector2(cameraPosition.x, cameraPosition.z);
            float radius = Mathf.Max(m_OuterRadiusMetres, m_InnerRadiusMetres);
            float desiredMinX = cameraPosition.x - radius;
            float desiredMaxX = cameraPosition.x + radius;
            float desiredMinZ = cameraPosition.z - radius;
            float desiredMaxZ = cameraPosition.z + radius;
            int coverageSpacing = _ringSpacing[coverageRing];
            int2 coverageOrigin = _ringOrigin[coverageRing];
            float innerMinX = coverageOrigin.x * 0.1f;
            float innerMaxX = (coverageOrigin.x + coverageSpacing * m_Resolution) * 0.1f;
            float innerMinZ = coverageOrigin.y * 0.1f;
            float innerMaxZ = (coverageOrigin.y + coverageSpacing * m_Resolution) * 0.1f;
            float criticalHalfExtent = _ringSpacing[0] * m_Resolution * 0.05f;
            innerMinX = Mathf.Min(innerMinX, cameraPosition.x - criticalHalfExtent);
            innerMaxX = Mathf.Max(innerMaxX, cameraPosition.x + criticalHalfExtent);
            innerMinZ = Mathf.Min(innerMinZ, cameraPosition.z - criticalHalfExtent);
            innerMaxZ = Mathf.Max(innerMaxZ, cameraPosition.z + criticalHalfExtent);
            innerMinX = Mathf.Clamp(innerMinX, desiredMinX, desiredMaxX);
            innerMaxX = Mathf.Clamp(innerMaxX, desiredMinX, desiredMaxX);
            innerMinZ = Mathf.Clamp(innerMinZ, desiredMinZ, desiredMaxZ);
            innerMaxZ = Mathf.Clamp(innerMaxZ, desiredMinZ, desiredMaxZ);
            _startupFallbackPositions.Clear();
            _startupFallbackColours.Clear();
            _startupFallbackIndices.Clear();
            float currentMinX = innerMinX;
            float currentMaxX = innerMaxX;
            float currentMinZ = innerMinZ;
            float currentMaxZ = innerMaxZ;
            for (int ring = coverageRing + 1; ring < _ringMeshes.Count; ring++)
            {
                int spacing = _ringSpacing[ring];
                float halfExtent = spacing * m_Resolution * 0.05f;
                float targetMinX = Mathf.Max(desiredMinX, cameraPosition.x - halfExtent);
                float targetMaxX = Mathf.Min(desiredMaxX, cameraPosition.x + halfExtent);
                float targetMinZ = Mathf.Max(desiredMinZ, cameraPosition.z - halfExtent);
                float targetMaxZ = Mathf.Min(desiredMaxZ, cameraPosition.z + halfExtent);
                float nextMinX = Mathf.Min(currentMinX, targetMinX);
                float nextMaxX = Mathf.Max(currentMaxX, targetMaxX);
                float nextMinZ = Mathf.Min(currentMinZ, targetMinZ);
                float nextMaxZ = Mathf.Max(currentMaxZ, targetMaxZ);
                float targetStep = Mathf.Max(_ringSpacing[0] * 0.1f, spacing * 0.1f * StartupFallbackSpacingMultiplier);
                AddStartupFallbackBand(nextMinX, nextMaxX, nextMinZ, nextMaxZ, currentMinX, currentMaxX, currentMinZ, currentMaxZ, targetStep);
                currentMinX = nextMinX;
                currentMaxX = nextMaxX;
                currentMinZ = nextMinZ;
                currentMaxZ = nextMaxZ;
            }
            if (currentMinX > desiredMinX || currentMaxX < desiredMaxX || currentMinZ > desiredMinZ || currentMaxZ < desiredMaxZ)
            {
                float targetStep = Mathf.Max(_ringSpacing[0] * 0.1f, _ringSpacing[_ringSpacing.Count - 1] * 0.1f * StartupFallbackSpacingMultiplier);
                AddStartupFallbackBand(desiredMinX, desiredMaxX, desiredMinZ, desiredMaxZ, currentMinX, currentMaxX, currentMinZ, currentMaxZ, targetStep);
            }
            mesh.Clear(false);
            mesh.SetVertices(_startupFallbackPositions);
            mesh.SetColors(_startupFallbackColours);
            mesh.SetTriangles(_startupFallbackIndices, 0, false);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(cameraPosition.x, 0f, cameraPosition.z), new Vector3(radius * 2f, 20000f, radius * 2f));
        }

        private void AddStartupFallbackBand(float outerMinX, float outerMaxX, float outerMinZ, float outerMaxZ, float innerMinX, float innerMaxX, float innerMinZ, float innerMaxZ, float targetStep)
        {
            AddStartupFallbackRect(outerMinX, outerMaxX, outerMinZ, innerMinZ, targetStep);
            AddStartupFallbackRect(outerMinX, outerMaxX, innerMaxZ, outerMaxZ, targetStep);
            AddStartupFallbackRect(outerMinX, innerMinX, innerMinZ, innerMaxZ, targetStep);
            AddStartupFallbackRect(innerMaxX, outerMaxX, innerMinZ, innerMaxZ, targetStep);
        }

        private void AddStartupFallbackRect(float minX, float maxX, float minZ, float maxZ, float targetStep)
        {
            float width = maxX - minX;
            float depth = maxZ - minZ;
            if (width <= 0.01f || depth <= 0.01f) return;
            int xSegments = Mathf.Max(1, Mathf.CeilToInt(width / targetStep));
            int zSegments = Mathf.Max(1, Mathf.CeilToInt(depth / targetStep));
            int row = xSegments + 1;
            int baseVertex = _startupFallbackPositions.Count;
            for (int z = 0; z <= zSegments; z++)
            {
                float worldZ = Mathf.Lerp(minZ, maxZ, z / (float)zSegments);
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = Mathf.Lerp(minX, maxX, x / (float)xSegments);
                    SampleStartupFallbackVertex(worldX, worldZ, out Vector3 position, out Color colour);
                    _startupFallbackPositions.Add(position);
                    _startupFallbackColours.Add(colour);
                }
            }
            for (int z = 0; z < zSegments; z++)
            for (int x = 0; x < xSegments; x++)
            {
                int i = baseVertex + x + z * row;
                _startupFallbackIndices.Add(i);
                _startupFallbackIndices.Add(i + row);
                _startupFallbackIndices.Add(i + 1);
                _startupFallbackIndices.Add(i + 1);
                _startupFallbackIndices.Add(i + row);
                _startupFallbackIndices.Add(i + row + 1);
            }
        }

        private void SampleStartupFallbackVertex(float worldX, float worldZ, out Vector3 position, out Color colour)
        {
            int voxelX = Mathf.FloorToInt(worldX / 0.1f);
            int voxelZ = Mathf.FloorToInt(worldZ / 0.1f);
            int height = TerrainSampler.HeightAt(voxelX, voxelZ, m_Seed);
            bool hasAuthoredTerrain = false;
            byte authoredTerrainMaterial = 0;
            if (Structures != null)
            {
                int authoredTerrain = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);
                if (authoredTerrain != int.MinValue)
                {
                    height = authoredTerrain;
                    hasAuthoredTerrain = true;
                    authoredTerrainMaterial = Structures.AuthoredTerrainMaterialAt(voxelX, voxelZ);
                }
            }
            bool isStructure = false;
            if (Structures != null)
            {
                int built = Structures.HeightAt(voxelX, voxelZ);
                if (built != int.MinValue && built > height)
                {
                    height = built;
                    isStructure = true;
                }
            }
            position = new Vector3(worldX, height * 0.1f, worldZ);
            byte material = ResolveFarSurfaceMaterial(MaterialRoles, isStructure, hasAuthoredTerrain, authoredTerrainMaterial, height);
            Vector4 albedo = RenderingComposition.GetMaterialAlbedo(material);
            colour = new Color(albedo.x, albedo.y, albedo.z, 1f);
        }

        private void CloseRingZeroHoleTopology()
        {
            const int ring = 0;
            int verts = m_Resolution + 1;
            _indicesScratch.Clear();
            for (int z = 0; z < m_Resolution; z++)
            for (int x = 0; x < m_Resolution; x++)
            {
                int i = x + z * verts;
                _indicesScratch.Add(i);
                _indicesScratch.Add(i + verts);
                _indicesScratch.Add(i + 1);
                _indicesScratch.Add(i + 1);
                _indicesScratch.Add(i + verts);
                _indicesScratch.Add(i + verts + 1);
            }
            _ringMeshes[ring].SetTriangles(_indicesScratch, 0, false);
            _ringBuiltTopologyHoleMetres[ring] = 0f;
            _topologyRebuildCount++;
        }

        private void RebuildRingFromCachedHeights(int ring, int2 origin, int spacing)
        {
            int verts = m_Resolution + 1;
            NativeArray<int> heights = _ringHeights[ring];
            Vector3[] positions = _positionsScratch;
            Color[] colours = _coloursScratch;
            Vector3 centre = new((origin.x + spacing * m_Resolution / 2) * 0.1f, 0f, (origin.y + spacing * m_Resolution / 2) * 0.1f);
            float structureProxyHoleSq = _requestedHoleRadiusMetres * _requestedHoleRadiusMetres;
            for (int z = 0; z < verts; z++)
            for (int x = 0; x < verts; x++)
            {
                int i = x + z * verts;
                int voxelX = origin.x + x * spacing;
                int voxelZ = origin.y + z * spacing;
                int height = heights[i];
                bool hasAuthoredTerrain = false;
                byte authoredTerrainMaterial = 0;
                if (Structures != null)
                {
                    int authoredTerrain = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);
                    if (authoredTerrain != int.MinValue)
                    {
                        height = authoredTerrain;
                        hasAuthoredTerrain = true;
                        authoredTerrainMaterial = Structures.AuthoredTerrainMaterialAt(voxelX, voxelZ);
                    }
                }
                bool isStructure = false;
                float worldX = voxelX * 0.1f;
                float worldZ = voxelZ * 0.1f;
                float proxyDx = worldX - centre.x;
                float proxyDz = worldZ - centre.z;
                bool insideRequestedNearFootprint = ring == 0 && proxyDx * proxyDx + proxyDz * proxyDz < structureProxyHoleSq;
                if (Structures != null && !insideRequestedNearFootprint)
                {
                    int built = Structures.HeightAt(voxelX, voxelZ);
                    if (built != int.MinValue && built > height)
                    {
                        height = built;
                        isStructure = true;
                    }
                }
                positions[i] = new Vector3(worldX, height * 0.1f, worldZ);
                byte material = ResolveFarSurfaceMaterial(MaterialRoles, isStructure, hasAuthoredTerrain, authoredTerrainMaterial, height);
                Vector4 albedo = RenderingComposition.GetMaterialAlbedo(material);
                colours[i] = new Color(albedo.x, albedo.y, albedo.z, 1f);
            }
            bool circularHole = ring == 0;
            float holeMetres;
            if (circularHole) holeMetres = _holeRadiusMetres;
            else
            {
                float childHalfExtent = SpacingForRing(ring - 1) * m_Resolution * 0.5f * 0.1f;
                float parentCellGuard = spacing * 0.1f;
                holeMetres = Mathf.Max(0f, childHalfExtent - parentCellGuard);
            }
            float builtTopologyHole = _ringBuiltTopologyHoleMetres[ring];
            bool topologyDirty = float.IsNaN(builtTopologyHole) || !Mathf.Approximately(builtTopologyHole, holeMetres);
            if (topologyDirty)
            {
                _indicesScratch.Clear();
                for (int z = 0; z < m_Resolution; z++)
                for (int x = 0; x < m_Resolution; x++)
                {
                    int i = x + z * verts;
                    float dx = Mathf.Max(Mathf.Abs(positions[i].x - centre.x), Mathf.Abs(positions[i + verts + 1].x - centre.x));
                    float dz = Mathf.Max(Mathf.Abs(positions[i].z - centre.z), Mathf.Abs(positions[i + verts + 1].z - centre.z));
                    bool inHole = circularHole ? dx * dx + dz * dz < holeMetres * holeMetres : Mathf.Max(dx, dz) < holeMetres;
                    if (inHole) continue;
                    _indicesScratch.Add(i);
                    _indicesScratch.Add(i + verts);
                    _indicesScratch.Add(i + 1);
                    _indicesScratch.Add(i + 1);
                    _indicesScratch.Add(i + verts);
                    _indicesScratch.Add(i + verts + 1);
                }
            }
            Mesh mesh = _ringMeshes[ring];
            mesh.vertices = positions;
            mesh.colors = colours;
            if (topologyDirty)
            {
                mesh.SetTriangles(_indicesScratch, 0, false);
                _ringBuiltTopologyHoleMetres[ring] = holeMetres;
                _topologyRebuildCount++;
            }
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(centre, new Vector3(spacing * m_Resolution * 0.1f, 20000f, spacing * m_Resolution * 0.1f));
        }
    }
}
