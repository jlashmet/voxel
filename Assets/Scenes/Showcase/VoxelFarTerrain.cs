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
    ///
    /// <para><b>Why this is not voxels.</b> A <c>Region</c> costs 1 MB of brick pointers no
    /// matter what it contains, and a region spans 51.2 m. Covering a 4 km view radius with
    /// resident regions is thousands of them — gigabytes of pointers before a single voxel is
    /// stored. Distant terrain is therefore never made resident; it is meshed directly from the
    /// height function, which costs nothing to keep and nothing to stream.</para>
    ///
    /// <para><b>Why it stays consistent with the voxels.</b> Both representations read the same
    /// <see cref="TerrainSampler.HeightAt"/>. Permanent generated sculpts that depart from that
    /// analytic field are supplied by <see cref="FarFieldStructureStore"/> at coarse far-field
    /// resolution. Runtime destruction remains near-field detail and is intentionally omitted.</para>
    ///
    /// <para><b>Clipmap layout.</b> Concentric square rings centred on the camera, each ring
    /// twice the sample spacing of the one inside it. Every ring holds the same vertex count, so
    /// cost is flat per ring and total cost grows with the logarithm of view distance rather
    /// than its square. Rings snap to their own sample spacing so vertices never slide between
    /// frames, which is what stops distant ridgelines from shimmering as the camera moves.</para>
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class VoxelFarTerrain : MonoBehaviour
    {
        [Tooltip("Where the voxel world stops and this takes over, in metres.")]
        [SerializeField] private float m_InnerRadiusMetres = 220f;

        [Tooltip("Outermost extent, in metres.")]
        [SerializeField] private float m_OuterRadiusMetres = 4000f;

        [Tooltip("Vertices per axis in each clipmap ring. Higher is denser but costs the same "
               + "per ring, so this is the main quality dial.")]
        [SerializeField] private int m_Resolution = 96;

        [SerializeField] private Material m_Material;

        [Tooltip("Terrain seed. Must match the voxel world or the two will disagree.")]
        [SerializeField] private uint m_Seed = 1;

        /// <summary>
        /// Permanent authored far-field data. It can lower the analytic terrain for generated
        /// sculpts and raise it for distant structure silhouettes.
        /// </summary>
        public FarFieldStructureStore Structures { get; set; }

        /// <summary>
        /// Opaque application-owned material roles shared with the near-field showcase world.
        /// The far renderer decides only whether a sample is terrain or built structure.
        /// </summary>
        public ShowcaseMaterialSet MaterialRoles { get; set; }

        private readonly List<Mesh> _ringMeshes = new();
        private readonly List<int> _ringSpacing = new();
        private readonly List<int2> _ringOrigin = new();
        private readonly List<NativeArray<int>> _ringHeights = new();
        private readonly List<bool> _ringHeightValid = new();
        private readonly List<int> _ringBuiltStructureVersion = new();
        private readonly List<float> _ringBuiltTopologyHoleMetres = new();
        private readonly List<int> _indicesScratch = new();
        private Vector3[] _positionsScratch;
        private Color[] _coloursScratch;
        private Vector2[] _materialIdsScratch;
        private MeshRenderer _renderer;
        private Camera _camera;
        private bool _ownsMaterial;

        // Height sampling is deliberately single-flight. The old implementation scheduled a Burst
        // job and immediately Complete()d it in LateUpdate, then could repeat that for every ring
        // in the same frame. One worker job at a time is enough for a visual far field, and the
        // previous mesh remains valid while a snapped replacement is being sampled.
        private JobHandle _heightJobHandle;
        private bool _heightJobScheduled;
        private int _heightJobRing = -1;
        private int2 _heightJobOrigin;
        private int _ringWorkCursor;
        private ulong _topologyRebuildCount;

        // Ring zero is sampled synchronously once so nearby terrain has the correct silhouette on
        // the first rendered frame. The remaining clipmap has no completed height cache to draw,
        // so keep one zero-sampling emergency mesh in the outer-ring slot until that ring receives
        // its first authoritative async sample. It is deliberately not marked height-valid:
        // normal single-flight admission still visits every outer ring in order, while DrawMesh
        // provides continuous horizon coverage. The real outer ring replaces it on publication.
        private bool _startupFallbackInitialized;
        private int _startupFallbackRing = -1;

        // Showcase-created far terrain uses the renderer's publication state as part of the
        // near/far ownership contract. Isolated clipmap instances (tests/lookdev) keep direct
        // control of HoleRadiusMetres so topology can still be exercised without a live renderer.
        private bool _requirePublishedNearCoverage;

        public float InnerRadiusMetres => m_InnerRadiusMetres;
        public float OuterRadiusMetres => m_OuterRadiusMetres;
        public uint Seed { get => m_Seed; set => m_Seed = value; }

        /// <summary>
        /// Diagnostic count of clipmap index-buffer rebuilds. Camera movement and structure-only
        /// presentation refreshes must not advance this once a ring topology has been established.
        /// Correctness-driven ring-0 fallback closure is intentionally counted because it really
        /// does replace the index topology and therefore remains visible to performance tests.
        /// </summary>
        public ulong TopologyRebuildCount => _topologyRebuildCount;

        /// <summary>
        /// True once every ring carries heights sampled from the terrain height field, so the far
        /// mesh is the height field at a coarser rate rather than a placeholder.
        ///
        /// Until the first asynchronous height job for the outermost ring lands, that ring is the
        /// flat base-height square published by <see cref="BuildStartupFallback"/>. That square is
        /// deliberate — it keeps the horizon covered without sampling terrain on the player frame —
        /// but its vertices do not follow the height field, so anything comparing far vertices with
        /// <see cref="TerrainSampler.HeightAt"/> has to wait for this rather than for a frame count.
        /// </summary>
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

        /// <summary>
        /// Radius of ring 0's actual published hole, in metres.
        ///
        /// Generated Storage residency is only an upper bound. Showcase-created far terrain keeps
        /// the hole closed while the asynchronous near renderer is dirty, building, awaiting
        /// publication, or still reports visible holes. Once near coverage is complete, the hole
        /// remains one maximum ring-0 snap diagonal smaller than that coverage. The near renderer
        /// follows the player continuously while the far lattice is floor-snapped, so using the
        /// full near radius would let the snapped hole protrude outside near coverage near a cell
        /// corner even though both renderers were individually healthy.
        /// </summary>
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
                        // Near coverage is a per-view fact. A hole that was safe at street level
                        // is not safe after the camera rises and looks down: the new frustum can
                        // expose hundreds of chunks which have no published geometry yet. Keeping
                        // the old hole in that state shows the sky clear colour through the world.
                        // Close it until the current view is complete. Ring zero suppresses coarse
                        // structure proxies inside the requested near footprint while closed, so
                        // fallback terrain fills absent pixels without drawing a smooth castle or
                        // house over its detailed near-field replacement.
                        _holeRadiusMetres = 0f;
                        return;
                    }

                    // Voxel LOD bands are spherical in camera space, while this hole is a circle
                    // projected onto the ground. Looking down during a descent can therefore have
                    // a handful of fully built chunks directly below the camera and zero reported
                    // missing work, even though those chunks cover only a tiny fraction of the
                    // configured horizontal radius. Project the spherical near radius onto the
                    // local terrain plane before opening the ground hole.
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
        private Vector3 _groundProjectionCameraPosition =
            new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        private float _groundProjectionNearRadius = -1f;
        private float _cachedGroundProjectedNearRadius;

        private float GroundProjectedNearRadius(float nearRadiusMetres)
        {
            if (_camera == null || nearRadiusMetres <= 0f) return nearRadiusMetres;

            Vector3 cameraPosition = _camera.transform.position;
            if (Mathf.Approximately(nearRadiusMetres, _groundProjectionNearRadius)
                && (cameraPosition - _groundProjectionCameraPosition).sqrMagnitude < 0.25f)
                return _cachedGroundProjectedNearRadius;

            // A single height directly below the camera is insufficient on sloped terrain. The
            // near renderer admits surface chunks inside a 3D camera-space sphere; the far hole is
            // safe only through the last complete radial shell whose real terrain surface fits in
            // that sphere in every direction.
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

        /// <summary>Ring count needed to reach the outer radius by successive doubling.</summary>
        public int RingCount
        {
            get
            {
                int rings = 1;
                float reach = m_InnerRadiusMetres * 2f;
                while (reach < m_OuterRadiusMetres && rings < 12) { reach *= 2f; rings++; }
                return rings;
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            // The mesh is drawn with Graphics.DrawMesh, not through this renderer; the
            // component is required only so the object carries sane bounds in the editor.
            if (_renderer != null) _renderer.enabled = false;
            // The global ShowcaseMaterialComposition binding was removed: material
            // identity is application-owned and supplied explicitly now.
            MaterialRoles = Game.Composition.Materials.GameMaterialComposition.ShowcaseMaterials;
            EnsureMaterial();
        }

        /// <summary>
        /// Builds a default lit material when none is assigned, so dropping this component on an
        /// object is enough to see distant terrain. An unassigned material would otherwise fail
        /// silently — the component would run, mesh correctly, and draw nothing.
        /// </summary>
        private void EnsureMaterial()
        {
            if (m_Material != null) return;
            // VoxelEngine/FarTerrain, not URP/Lit: the rings carry their material in vertex
            // colour and URP/Lit ignores that channel entirely, which is how the whole far field
            // came to render in one flat grey.
            Shader shader = Shader.Find("VoxelEngine/FarTerrain");
            if (shader == null)
            {
                Debug.LogError("VoxelFarTerrain: VoxelEngine/FarTerrain shader is missing. "
                             + "Distant terrain will fall back to an unlit flat colour.");
                shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }
            if (shader == null) return;
            m_Material = new Material(shader) { name = "FarTerrainDefault" };
            _ownsMaterial = true;
            if (m_Material.HasProperty("_Smoothness"))
                m_Material.SetFloat("_Smoothness", 0.05f);
        }

        /// <summary>
        /// Creates a far-terrain object configured to take over exactly where the voxel
        /// streaming radius ends. Called by the showcase so nothing has to be wired by hand.
        /// </summary>
        public static VoxelFarTerrain Create(Transform parent, uint seed,
                                             float innerRadiusMetres, float outerRadiusMetres)
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
            // Teardown is a lifecycle boundary, so it is the one place where joining an in-flight
            // far-terrain job is correct: its Persistent target must not be disposed underneath it.
            if (_heightJobScheduled)
            {
                _heightJobHandle.Complete();
                _heightJobScheduled = false;
            }

            for (int i = 0; i < _ringHeights.Count; i++)
                if (_ringHeights[i].IsCreated) _ringHeights[i].Dispose();
            _ringHeights.Clear();

            for (int i = 0; i < _ringMeshes.Count; i++)
                if (_ringMeshes[i] != null) Destroy(_ringMeshes[i]);
            _ringMeshes.Clear();
            _ringBuiltTopologyHoleMetres.Clear();

            // A serialized/shared material is owned by its asset or caller. Only release the
            // runtime fallback allocated by EnsureMaterial; otherwise destroying this component
            // could invalidate another renderer's shared presentation asset.
            if (_ownsMaterial && m_Material != null)
                Destroy(m_Material);
            if (_ownsMaterial) m_Material = null;
            _ownsMaterial = false;
        }

        /// <summary>
        /// Voxel spacing between samples for a ring. Ring 0 covers the inner radius at the
        /// coarsest spacing that still resolves it; each ring outward doubles.
        /// </summary>
        public int SpacingForRing(int ring)
        {
            float innerVoxels = m_InnerRadiusMetres / 0.1f;
            int spacing = Mathf.Max(1, Mathf.NextPowerOfTwo(
                Mathf.CeilToInt(innerVoxels * 2f / m_Resolution)));
            return spacing << ring;
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

            // Poll only. Never call Complete() for unfinished far-terrain work from the player
            // frame. Once IsCompleted is true, ownership transfer is non-blocking and we publish
            // at most one mesh this frame while every other ring keeps drawing its old mesh.
            if (_heightJobScheduled && _heightJobHandle.IsCompleted)
            {
                _heightJobHandle.Complete();
                int ring = _heightJobRing;
                _heightJobScheduled = false;
                _heightJobRing = -1;
                _ringHeightValid[ring] = true;
                _ringOrigin[ring] = _heightJobOrigin;

                // The player can cross another snap while this single-flight job is running. If
                // the completed ring-0 sample is already stale, publish it as full fallback rather
                // than briefly reopening a hole around the old lattice point before scheduling the
                // next sample. Height data are still useful; only the stale ownership hole closes.
                float requestedHole = _holeRadiusMetres;
                bool staleCriticalPublication = ring == 0
                    && !OriginFor(cameraPosition, _ringSpacing[0]).Equals(_heightJobOrigin);
                if (staleCriticalPublication) _holeRadiusMetres = 0f;
                RebuildRingFromCachedHeights(ring, _ringOrigin[ring], _ringSpacing[ring]);
                if (staleCriticalPublication) _holeRadiusMetres = requestedHole;

                if (ring == _startupFallbackRing) _startupFallbackRing = -1;
                _ringBuiltStructureVersion[ring] = structureVersion;
                if (ring == 0)
                    _builtHoleRadiusMetres = staleCriticalPublication ? 0f : _holeRadiusMetres;
                _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                rebuiltThisFrame = true;
            }

            // A published ring can lag the camera while its replacement sample is still queued or
            // running. Its old vertex heights remain valid fallback terrain, but an open hole at
            // the old snap centre does not. Close only the index hole immediately; do not touch the
            // NativeArray height cache that a worker may be writing and do not recalculate vertices
            // or normals on this correctness path.
            int criticalSpacing = _ringSpacing[0];
            int2 criticalOrigin = OriginFor(cameraPosition, criticalSpacing);
            bool criticalOriginStale = _ringHeightValid[0]
                && !criticalOrigin.Equals(_ringOrigin[0]);
            if (criticalOriginStale
                && _ringBuiltTopologyHoleMetres[0] > 0.05f)
            {
                CloseRingZeroHoleTopology();
                _builtHoleRadiusMetres = 0f;
                rebuiltThisFrame = true;
            }

            // Hole changes and new far-field structures do not require resampling the terrain.
            // Re-use the persistent height cache and refresh only one mesh per frame. Ring 0 gets
            // first refusal because a stale residency hole is the only update that can expose a
            // near/far coverage mismatch directly around the player.
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
                    if (ring == 0 || !RingNeedsPresentationRefresh(
                            ring, cameraPosition, structureVersion))
                        continue;

                    RebuildRingFromCachedHeights(
                        ring, _ringOrigin[ring], _ringSpacing[ring]);
                    _ringBuiltStructureVersion[ring] = structureVersion;
                    _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                    rebuiltThisFrame = true;
                    break;
                }
            }

            // One single-flight height job updates a moved ring. Ring 0 owns the correctness
            // boundary around the camera, so a moved/invalid ring 0 always gets first refusal.
            // Once it is current, the remaining rings retain round-robin admission so ordinary
            // movement does not abandon outer coverage work.
            if (!_heightJobScheduled)
            {
                bool criticalNeedsSample = !_ringHeightValid[0]
                    || !criticalOrigin.Equals(_ringOrigin[0]);
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
                        if (_ringHeightValid[ring] && targetOrigin.Equals(_ringOrigin[ring]))
                            continue;

                        ScheduleHeightJob(ring, targetOrigin, spacing);
                        _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                        break;
                    }
                }
            }

            for (int ring = 0; ring < _ringMeshes.Count; ring++)
            {
                // The startup fallback deliberately has no valid height cache. It is a published
                // emergency mesh only, so allow that one slot to draw while ordinary rings still
                // require an authoritative completed sample.
                if (!_ringHeightValid[ring] && ring != _startupFallbackRing) continue;
                Graphics.DrawMesh(_ringMeshes[ring], Matrix4x4.identity, m_Material,
                                  gameObject.layer, _camera);
            }
        }

        private bool RingNeedsPresentationRefresh(
            int ring, Vector3 cameraPosition, int structureVersion)
        {
            if (ring < 0 || ring >= _ringMeshes.Count || !_ringHeightValid[ring])
                return false;

            // A scheduled height job owns this ring's persistent cache until Complete() transfers
            // ownership back to the main thread. Keep drawing the existing mesh, but never rebuild
            // presentation from a NativeArray while the worker may still be writing it.
            if (_heightJobScheduled && _heightJobRing == ring)
                return false;

            int2 targetOrigin = OriginFor(cameraPosition, _ringSpacing[ring]);
            if (!targetOrigin.Equals(_ringOrigin[ring])) return false;
            if (_ringBuiltStructureVersion[ring] != structureVersion) return true;
            return ring == 0 && Mathf.Abs(_holeRadiusMetres - _builtHoleRadiusMetres) >= 1f;
        }

        private int2 OriginFor(Vector3 cameraPosition, int spacing)
        {
            // Snap the ring's origin to its own sample spacing. Floor, not truncate: integer
            // division rounds toward zero, which otherwise makes west/north axis crossings jump.
            int centreX = Mathf.FloorToInt(cameraPosition.x / 0.1f);
            int centreZ = Mathf.FloorToInt(cameraPosition.z / 0.1f);
            int half = spacing * m_Resolution / 2;
            return new int2(FloorTo(centreX, spacing) - half,
                            FloorTo(centreZ, spacing) - half);
        }

        private void ScheduleHeightJob(int ring, int2 origin, int spacing)
        {
            int verts = m_Resolution + 1;
            NativeArray<int> heights = _ringHeights[ring];
            _heightJobHandle = new FarTerrainHeightJob
            {
                Origin = origin,
                Spacing = spacing,
                VertsPerAxis = verts,
                Seed = m_Seed,
                Heights = heights,
            }.Schedule(verts * verts, 64);
            _heightJobRing = ring;
            _heightJobOrigin = origin;
            _heightJobScheduled = true;
        }

        /// <summary>Floor division onto a positive lattice step, correct for negatives.</summary>
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
                _materialIdsScratch = new Vector2[sampleCount];
                _indicesScratch.Clear();
                _indicesScratch.Capacity = Math.Max(
                    _indicesScratch.Capacity, m_Resolution * m_Resolution * 6);
            }

            while (_ringMeshes.Count < wanted)
            {
                int ring = _ringMeshes.Count;
                var mesh = new Mesh { name = $"FarTerrainRing{ring}" };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.MarkDynamic();
                // The clipmap is re-centred every time the camera crosses a sample, so Unity
                // must not cull it against a stale bound.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (m_OuterRadiusMetres * 4f));
                _ringMeshes.Add(mesh);
                _ringSpacing.Add(SpacingForRing(ring));
                _ringOrigin.Add(new int2(int.MinValue, int.MinValue));
                _ringHeights.Add(new NativeArray<int>(
                    sampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory));
                _ringHeightValid.Add(false);
                _ringBuiltStructureVersion.Add(int.MinValue);
                _ringBuiltTopologyHoleMetres.Add(float.NaN);
            }

            if (!_startupFallbackInitialized && _ringMeshes.Count > 0)
            {
                _startupFallbackInitialized = true;
                BuildCriticalStartupFallback();

                // With more than one ring, retain a cheap full-square horizon behind the sampled
                // critical ring while the outer height jobs publish. A one-ring clipmap is already
                // completely covered by the synchronously sampled ring zero.
                if (_ringMeshes.Count > 1)
                {
                    _startupFallbackRing = _ringMeshes.Count - 1;
                    BuildStartupFallback(_ringMeshes[_startupFallbackRing]);
                }
            }
        }

        /// <summary>
        /// Publishes authoritative nearby terrain before the first frame can be presented.
        ///
        /// A flat startup plane covers base height but not the silhouette of a hill. While near
        /// chunks are still publishing, that leaves sky visible through the missing part of the
        /// hill. Ring zero is only one fixed-size sample lattice, so paying for this one Burst job
        /// synchronously at creation establishes the visual coverage invariant without turning the
        /// ordinary moving-camera path back into a blocking one.
        /// </summary>
        private void BuildCriticalStartupFallback()
        {
            const int ring = 0;
            int spacing = _ringSpacing[ring];
            Vector3 cameraPosition = _camera != null
                ? _camera.transform.position
                : transform.position;
            int2 origin = OriginFor(cameraPosition, spacing);
            int verts = m_Resolution + 1;

            new FarTerrainHeightJob
            {
                Origin = origin,
                Spacing = spacing,
                VertsPerAxis = verts,
                Seed = m_Seed,
                Heights = _ringHeights[ring],
            }.Run(verts * verts);

            _ringHeightValid[ring] = true;
            _ringOrigin[ring] = origin;
            RebuildRingFromCachedHeights(ring, origin, spacing);
            _ringBuiltStructureVersion[ring] = Structures?.Version ?? 0;
            _builtHoleRadiusMetres = _holeRadiusMetres;
            _ringWorkCursor = _ringMeshes.Count > 1 ? 1 : 0;
        }

        /// <summary>
        /// Publishes a zero-sampling full-square fallback before any asynchronous far height cache
        /// has completed. It intentionally uses the showcase base height rather than touching the
        /// terrain sampler on the player frame. The normal outer-ring height job later overwrites
        /// this mesh atomically through <see cref="RebuildRingFromCachedHeights"/>.
        /// </summary>
        private void BuildStartupFallback(Mesh mesh)
        {
            Vector3 cameraPosition = _camera != null ? _camera.transform.position : transform.position;
            float radius = Mathf.Max(m_OuterRadiusMetres, m_InnerRadiusMetres);
            float y = ShowcaseWorld.BaseHeightVoxels * 0.1f;
            float minX = cameraPosition.x - radius;
            float maxX = cameraPosition.x + radius;
            float minZ = cameraPosition.z - radius;
            float maxZ = cameraPosition.z + radius;

            mesh.vertices = new[]
            {
                new Vector3(minX, y, minZ),
                new Vector3(minX, y, maxZ),
                new Vector3(maxX, y, minZ),
                new Vector3(maxX, y, maxZ),
            };

            byte material = MaterialRoles.SurfaceAt(
                ShowcaseWorld.BaseHeightVoxels, ShowcaseWorld.BaseHeightVoxels);
            Vector4 albedo = RenderingComposition.GetMaterialAlbedo(material);
            Color colour = new(albedo.x, albedo.y, albedo.z, 1f);
            Vector2 materialData = new(material, 0f);
            mesh.colors = new[] { colour, colour, colour, colour };
            mesh.uv2 = new[] { materialData, materialData, materialData, materialData };
            mesh.SetTriangles(new[] { 0, 1, 2, 2, 1, 3 }, 0, false);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(
                new Vector3(cameraPosition.x, y, cameraPosition.z),
                new Vector3(radius * 2f, 2f, radius * 2f));
        }

        /// <summary>
        /// Replaces ring 0's annulus with the full square using only its already-published vertex
        /// buffer. This is the fallback transition used while the ring's snapped height sample is
        /// stale. It deliberately does not read the persistent height cache, so it is safe even
        /// when the single-flight worker is currently writing ring 0.
        /// </summary>
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

        /// <summary>
        /// Rebuilds one ring from its already-completed persistent height cache. This method does
        /// no job waiting and reuses managed scratch buffers. Ring topology is retained across
        /// camera moves and structure-only presentation refreshes, avoiding a full index rebuild,
        /// mesh clear, and index-buffer upload on the ordinary frame path.
        /// </summary>
        private void RebuildRingFromCachedHeights(int ring, int2 origin, int spacing)
        {
            int verts = m_Resolution + 1;
            NativeArray<int> heights = _ringHeights[ring];
            Vector3[] positions = _positionsScratch;
            Color[] colours = _coloursScratch;
            Vector2[] materialIds = _materialIdsScratch;
            Vector3 centre = new((origin.x + spacing * m_Resolution / 2) * 0.1f, 0f,
                                 (origin.y + spacing * m_Resolution / 2) * 0.1f);
            float structureProxyHoleSq = _requestedHoleRadiusMetres
                                       * _requestedHoleRadiusMetres;
            for (int z = 0; z < verts; z++)
            for (int x = 0; x < verts; x++)
            {
                int i = x + z * verts;
                int voxelX = origin.x + x * spacing;
                int voxelZ = origin.y + z * spacing;

                // Start with the Burst-sampled analytic field, then apply the permanent authored
                // terrain surface before considering positive structure silhouettes. The lowering
                // stays active inside ring zero's requested near footprint because that is exactly
                // where closed-hole fallback must reproduce a gorge/moat while near chunks publish.
                int height = heights[i];
                if (Structures != null)
                {
                    int authoredTerrain = Structures.AuthoredTerrainHeightAt(voxelX, voxelZ);
                    if (authoredTerrain != int.MinValue) height = authoredTerrain;
                }

                bool isStructure = false;
                float worldX = voxelX * 0.1f;
                float worldZ = voxelZ * 0.1f;
                float proxyDx = worldX - centre.x;
                float proxyDz = worldZ - centre.z;
                bool insideRequestedNearFootprint = ring == 0
                    && proxyDx * proxyDx + proxyDz * proxyDz < structureProxyHoleSq;
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

                // Material identity comes from the same application-owned role set as the near
                // world. Rendering only resolves the opaque index to the installed presentation.
                byte material = isStructure
                    ? MaterialRoles.FarStructure
                    : MaterialRoles.SurfaceAt(height, ShowcaseWorld.BaseHeightVoxels);
                Vector4 albedo = RenderingComposition.GetMaterialAlbedo(material);
                colours[i] = new Color(albedo.x, albedo.y, albedo.z, 1f);
                materialIds[i] = new Vector2(material, 0f);
            }

            // Every ring is a full square centred on its independently snapped sample lattice.
            // Ring 0's hole is the voxel world's actual Euclidean footprint. Outer rings reserve
            // one parent-cell guard band inside the finer ring's nominal half-extent: without it,
            // two valid published snap states can meet at only an edge (or leave a narrow strip)
            // even though both rings individually have correct topology.
            bool circularHole = ring == 0;
            float holeMetres;
            if (circularHole)
            {
                holeMetres = _holeRadiusMetres;
            }
            else
            {
                float childHalfExtent = SpacingForRing(ring - 1)
                                      * m_Resolution * 0.5f * 0.1f;
                float parentCellGuard = spacing * 0.1f;
                holeMetres = Mathf.Max(0f, childHalfExtent - parentCellGuard);
            }

            float builtTopologyHole = _ringBuiltTopologyHoleMetres[ring];
            bool topologyDirty = float.IsNaN(builtTopologyHole)
                              || !Mathf.Approximately(builtTopologyHole, holeMetres);
            if (topologyDirty)
            {
                _indicesScratch.Clear();
                for (int z = 0; z < m_Resolution; z++)
                for (int x = 0; x < m_Resolution; x++)
                {
                    int i = x + z * verts;
                    // Test the quad's far corner against the hole so the ring's inner edge closes
                    // over the finer ring's outer edge rather than leaving a gap between them.
                    float dx = Mathf.Max(Mathf.Abs(positions[i].x - centre.x),
                                         Mathf.Abs(positions[i + verts + 1].x - centre.x));
                    float dz = Mathf.Max(Mathf.Abs(positions[i].z - centre.z),
                                         Mathf.Abs(positions[i + verts + 1].z - centre.z));
                    bool inHole = circularHole
                        ? dx * dx + dz * dz < holeMetres * holeMetres
                        : Mathf.Max(dx, dz) < holeMetres;
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
            // Do not Clear(): vertex count is invariant for a ring and clearing also invalidates
            // the index buffer we deliberately retain between presentation refreshes.
            mesh.vertices = positions;
            mesh.colors = colours;
            mesh.uv2 = materialIds;
            if (topologyDirty)
            {
                mesh.SetTriangles(_indicesScratch, 0, false);
                _ringBuiltTopologyHoleMetres[ring] = holeMetres;
                _topologyRebuildCount++;
            }
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(centre,
                new Vector3(spacing * m_Resolution * 0.1f, 20000f,
                            spacing * m_Resolution * 0.1f));
        }
    }
}
