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
    /// from <see cref="TerrainSampler"/>.
    ///
    /// <para><b>Why this is not voxels.</b> A <c>Region</c> costs 1 MB of brick pointers no
    /// matter what it contains, and a region spans 51.2 m. Covering a 4 km view radius with
    /// resident regions is thousands of them — gigabytes of pointers before a single voxel is
    /// stored. Distant terrain is therefore never made resident; it is meshed directly from the
    /// height function, which costs nothing to keep and nothing to stream.</para>
    ///
    /// <para><b>Why it stays consistent with the voxels.</b> Both representations read the same
    /// <see cref="TerrainSampler.HeightAt"/>. The far mesh is not an approximation of the voxel
    /// world authored separately — it is the same integer height field at a coarser sample rate,
    /// so a mountain does not change shape as you approach it. What changes is only detail, and
    /// destruction, which the far field does not show.</para>
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
        /// Built content to raise the far mesh over. Without it the far field shows bare
        /// terrain and every structure appears only once you are inside the voxel radius.
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
        private readonly List<int> _indicesScratch = new();
        private Vector3[] _positionsScratch;
        private Color[] _coloursScratch;
        private MeshRenderer _renderer;
        private Camera _camera;

        // Height sampling is deliberately single-flight. The old implementation scheduled a Burst
        // job and immediately Complete()d it in LateUpdate, then could repeat that for every ring
        // in the same frame. One worker job at a time is enough for a visual far field, and the
        // previous mesh remains valid while a snapped replacement is being sampled.
        private JobHandle _heightJobHandle;
        private bool _heightJobScheduled;
        private int _heightJobRing = -1;
        private int2 _heightJobOrigin;
        private int _ringWorkCursor;

        public float InnerRadiusMetres => m_InnerRadiusMetres;
        public float OuterRadiusMetres => m_OuterRadiusMetres;
        public uint Seed { get => m_Seed; set => m_Seed = value; }

        /// <summary>
        /// Radius of ring 0's hole, in metres — the disc the voxel world is currently covering.
        ///
        /// Driven every frame from <c>ShowcaseWorld.ResidentGroundRadiusMetres</c> rather than
        /// from <see cref="InnerRadiusMetres"/>, which is only the configured ceiling. A hole
        /// sized from configuration is blind to streaming: it opens at full width on the first
        /// frame, before any region exists to fill it, and the player watches terrain appear
        /// inside it. Starting closed and opening as regions land means something is always
        /// drawn.
        /// </summary>
        public float HoleRadiusMetres
        {
            get => _holeRadiusMetres;
            set => _holeRadiusMetres = Mathf.Clamp(value, 0f, m_InnerRadiusMetres);
        }

        private float _holeRadiusMetres;
        private float _builtHoleRadiusMetres = -1f;

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
                RebuildRingFromCachedHeights(ring, _ringOrigin[ring], _ringSpacing[ring]);
                _ringBuiltStructureVersion[ring] = structureVersion;
                if (ring == 0) _builtHoleRadiusMetres = _holeRadiusMetres;
                _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
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

            // One single-flight height job updates a moved ring. Round-robin admission prevents a
            // constantly moving near ring from starving the outer clipmap indefinitely.
            if (!_heightJobScheduled)
            {
                for (int offset = 0; offset < _ringMeshes.Count; offset++)
                {
                    int ring = (_ringWorkCursor + offset) % _ringMeshes.Count;
                    int spacing = _ringSpacing[ring];
                    int2 targetOrigin = OriginFor(cameraPosition, spacing);
                    if (_ringHeightValid[ring] && targetOrigin.Equals(_ringOrigin[ring]))
                        continue;

                    ScheduleHeightJob(ring, targetOrigin, spacing);
                    _ringWorkCursor = (ring + 1) % _ringMeshes.Count;
                    break;
                }
            }

            for (int ring = 0; ring < _ringMeshes.Count; ring++)
            {
                if (!_ringHeightValid[ring]) continue;
                Graphics.DrawMesh(_ringMeshes[ring], Matrix4x4.identity, m_Material,
                                  gameObject.layer, _camera);
            }
        }

        private bool RingNeedsPresentationRefresh(
            int ring, Vector3 cameraPosition, int structureVersion)
        {
            if (ring < 0 || ring >= _ringMeshes.Count || !_ringHeightValid[ring])
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
            }
        }

        /// <summary>
        /// Rebuilds one ring from its already-completed persistent height cache. This method does
        /// no job waiting and reuses managed scratch buffers, so moving the camera no longer
        /// creates a TempJob allocation, two arrays and a large List for every ring rebuild.
        /// </summary>
        private void RebuildRingFromCachedHeights(int ring, int2 origin, int spacing)
        {
            int verts = m_Resolution + 1;
            NativeArray<int> heights = _ringHeights[ring];
            Vector3[] positions = _positionsScratch;
            Color[] colours = _coloursScratch;
            for (int z = 0; z < verts; z++)
            for (int x = 0; x < verts; x++)
            {
                int i = x + z * verts;
                int voxelX = origin.x + x * spacing;
                int voxelZ = origin.y + z * spacing;

                // Built content stands above the terrain the height field describes, so the
                // far mesh takes whichever is higher. This is what puts the castle on the
                // horizon instead of having it appear at the streaming radius.
                int height = heights[i];
                bool isStructure = false;
                if (Structures != null)
                {
                    int built = Structures.HeightAt(voxelX, voxelZ);
                    if (built != int.MinValue && built > height) { height = built; isStructure = true; }
                }

                positions[i] = new Vector3(voxelX * 0.1f, height * 0.1f, voxelZ * 0.1f);

                // Material identity comes from the same application-owned role set as the near
                // world. Rendering only resolves the opaque index to the installed presentation.
                byte material = isStructure
                    ? MaterialRoles.FarStructure
                    : MaterialRoles.SurfaceAt(height, ShowcaseWorld.BaseHeightVoxels);
                Vector4 albedo = RenderingComposition.GetMaterialAlbedo(material);
                colours[i] = new Color(albedo.x, albedo.y, albedo.z, 1f);
            }

            // Every ring is a full square centred on the camera, so without a hole each one
            // redraws all the ground the rings inside it already cover. Ring 0's hole is the
            // voxel world's actual Euclidean footprint; outer rings nest as square annuli.
            bool circularHole = ring == 0;
            float holeMetres = circularHole
                ? _holeRadiusMetres
                : SpacingForRing(ring - 1) * m_Resolution / 2f * 0.1f;

            Vector3 centre = new((origin.x + spacing * m_Resolution / 2) * 0.1f, 0f,
                                 (origin.y + spacing * m_Resolution / 2) * 0.1f);

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

            Mesh mesh = _ringMeshes[ring];
            mesh.Clear();
            mesh.vertices = positions;
            mesh.colors = colours;
            mesh.SetTriangles(_indicesScratch, 0, false);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(centre,
                new Vector3(spacing * m_Resolution * 0.1f, 20000f,
                            spacing * m_Resolution * 0.1f));
        }
    }
}