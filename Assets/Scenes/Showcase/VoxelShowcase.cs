using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Collision;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Driver for the showcase scene: owns a <see cref="ShowcaseWorld"/>, streams regions
    /// around the camera, turns clicks into edits, and reports what the engine is doing.
    ///
    /// What the scene demonstrates, in the order it becomes visible:
    ///
    ///   Streaming         — fly in any direction and regions generate ahead of you and evict
    ///                       behind. The HUD's resident count and generate time are the show.
    ///   Sparse storage    — the mixed-brick count tracks surface area, not world volume.
    ///                       Underground rock and open sky both cost zero pool slots.
    ///   Shared traversal  — picking runs through <see cref="VoxelRaycast"/> and
    ///                       <see cref="DdaTraversal"/>, the same walk the renderer will march.
    ///   Material behaviour— one blast leaves a clean crater in sand, a ragged one in stone,
    ///                       and no mark at all on bedrock.
    ///   Bounded memory    — fill a crater back in and watch allocated bricks fall as bricks
    ///                       collapse to uniform.
    ///
    /// A demo harness, not engine code: it lives in its own assembly and nothing references it.
    /// </summary>
    /// <remarks>
    /// <see cref="ExecuteAlways"/> so the scene view shows the world without entering play
    /// mode. Input, camera movement, and streaming stay play-mode only.
    /// </remarks>
    [ExecuteAlways]
    [AddComponentMenu("VoxelEngine/Voxel Showcase")]
    public sealed class VoxelShowcase : MonoBehaviour
    {
        [Header("World")]
        [Tooltip("Deterministic world seed. The same seed produces the same world everywhere.")]
        [SerializeField] private uint m_Seed = 0x5EED1234;

        [Tooltip("Mixed-brick pool capacity. Bounded by configuration, never by world size. " +
                 "Each slot is 576 B, so 262144 slots is about 151 MB.")]
        [SerializeField] private int m_BrickPoolCapacity = 262144;

        [Header("Streaming")]
        [Tooltip("Regions kept resident around the camera. One region is 51.2 m across.")]
        [SerializeField] private int m_LoadRadiusRegions = 2;

        [Tooltip("Regions are evicted past this radius. Must exceed the load radius — the gap " +
                 "is the hysteresis that stops a region thrashing on a boundary.")]
        [SerializeField] private int m_UnloadRadiusRegions = 4;

        [Tooltip("Regions generated per frame. Generation is the demo's biggest single cost, " +
                 "so it is budgeted rather than done all at once.")]
        [SerializeField] private int m_RegionsPerFrame = 1;

        [Tooltip("Region meshes rebuilt per frame.")]
        [SerializeField] private int m_MeshesPerFrame = 1;

        [Header("Editing")]
        [SerializeField] private int m_BrushRadius = 12;
        [SerializeField] private int m_MinBrushRadius = 2;
        [SerializeField] private int m_MaxBrushRadius = 40;

        [Header("Camera")]
        [SerializeField] private float m_MoveSpeed = 18f;
        [SerializeField] private float m_FastMultiplier = 6f;
        [SerializeField] private float m_LookSensitivity = 2.5f;

        [Header("Presentation")]
        [Tooltip("Presentation-only. Shadows across a streamed world are expensive.")]
        [SerializeField] private bool m_CastShadows;

        private ShowcaseWorld _world;
        private VoxelSurfaceRenderer _renderer;

        private int _materialSlot;
        private bool _showHud = true;
        private bool _mouseLook = true;
        private float _yaw, _pitch;
        private double _lastEditMs;
        private string _lastEditLabel = "—";
        private bool _hasAim;
        private int3 _aimVoxel, _placeVoxel;

        // Frame timing.
        private float _smoothedMs;
        private float _worstMs;
        private float _worstWindowStart;
        private float _displayWorstMs;

        // -- lifecycle -----------------------------------------------------------

        // Built in OnEnable rather than Awake so an editor domain reload rebuilds them; the
        // world holds Persistent native collections and must not outlive the component.
        private void OnEnable()
        {
            _world = new ShowcaseWorld(m_Seed, m_BrickPoolCapacity,
                                       m_LoadRadiusRegions, m_UnloadRadiusRegions);
            _renderer = new VoxelSurfaceRenderer { CastShadows = m_CastShadows };

            if (Application.isPlaying)
            {
                PositionCamera();
                SetCursorLocked(true);
            }

            // Seed the world around the spawn point so the first frame is not empty. In edit
            // mode this is the only generation that happens — streaming needs a running camera.
            _world.UpdateStreaming(transform.position, EditModeWarmupRegions);
            _renderer.Sync(_world, EditModeWarmupRegions * 5);
        }

        /// <summary>Regions generated up front, before any streaming has run.</summary>
        private const int EditModeWarmupRegions = 4;

        private void OnDisable()
        {
            _renderer?.Dispose();
            _renderer = null;
            _world?.Dispose();
            _world = null;
        }

        private void Update()
        {
            if (_world == null || _renderer == null) return;

            if (Application.isPlaying)
            {
                TrackFrameTime();
                HandleKeys();
                if (_mouseLook) HandleLook();
                HandleMove();
                UpdateAim();
                HandleEdits();

                _world.UpdateStreaming(transform.position, m_RegionsPerFrame);
            }

            _renderer.CastShadows = m_CastShadows;
            _renderer.Sync(_world, m_MeshesPerFrame);
        }

        private void TrackFrameTime()
        {
            float ms = Time.unscaledDeltaTime * 1000f;

            // Exponential smoothing for the headline number, plus the worst frame in a rolling
            // one-second window — an average alone hides exactly the hitches worth seeing here.
            _smoothedMs = _smoothedMs <= 0f ? ms : Mathf.Lerp(_smoothedMs, ms, 0.05f);
            _worstMs = Mathf.Max(_worstMs, ms);

            if (Time.unscaledTime - _worstWindowStart >= 1f)
            {
                _displayWorstMs = _worstMs;
                _worstMs = 0f;
                _worstWindowStart = Time.unscaledTime;
            }
        }

        // -- camera --------------------------------------------------------------

        private void PositionCamera()
        {
            var spawn = _world.SpawnPosition();
            transform.position = spawn;
            transform.LookAt(spawn + new Vector3(0f, -0.25f, 1f));

            var e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        private void HandleLook()
        {
            _yaw += Input.GetAxisRaw("Mouse X") * m_LookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * m_LookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void HandleMove()
        {
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            if (move == Vector3.zero) return;

            float speed = m_MoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? m_FastMultiplier : 1f);
            transform.position += move.normalized * (speed * Time.deltaTime);
        }

        private void SetCursorLocked(bool locked)
        {
            _mouseLook = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // -- input ---------------------------------------------------------------

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(!_mouseLook);
            if (Input.GetKeyDown(KeyCode.F1)) _showHud = !_showHud;
            if (Input.GetKeyDown(KeyCode.T)) m_CastShadows = !m_CastShadows;

            for (int i = 0; i < ShowcaseWorld.BuildableMaterials.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) _materialSlot = i;

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                m_BrushRadius = Mathf.Clamp(m_BrushRadius + (scroll > 0f ? 2 : -2),
                                            m_MinBrushRadius, m_MaxBrushRadius);
        }

        private void HandleEdits()
        {
            if (!_hasAim) return;

            if (Input.GetMouseButtonDown(0))
            {
                var start = Time.realtimeSinceStartupAsDouble;
                int changed = _world.Explode(_aimVoxel, (ushort)m_BrushRadius);
                _lastEditMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;
                _lastEditLabel = $"blast r{m_BrushRadius}: {changed:N0} voxels";
            }
            else if (Input.GetMouseButtonDown(1))
            {
                byte material = ShowcaseWorld.BuildableMaterials[_materialSlot];
                var start = Time.realtimeSinceStartupAsDouble;
                int changed = _world.Place(_placeVoxel, (ushort)m_BrushRadius, material);
                _lastEditMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;
                _lastEditLabel = $"place {ShowcaseWorld.MaterialNames[material]} r{m_BrushRadius}: {changed:N0} voxels";
            }
        }

        // -- picking -------------------------------------------------------------

        /// <summary>
        /// Resolves what the camera points at, in two stages.
        ///
        /// <see cref="VoxelRaycast"/> walks brick coordinates and rejects empty space a brick at
        /// a time; the refinement then walks voxels through the hit brick using the same
        /// <see cref="DdaTraversal.Cursor"/>. Two scales, one traversal — a second line-walk
        /// here is exactly how a picker drifts away from what the renderer draws.
        /// </summary>
        private void UpdateAim()
        {
            _hasAim = false;

            var originVoxel = (float3)(transform.position / VoxelSurfaceRenderer.VoxelSize);
            var direction = (float3)transform.forward;
            var originBrick = originVoxel / VoxelDimensions.BrickEdge;

            if (!VoxelRaycast.Raycast(in _world.Table, in _world.Pool, originBrick, direction, out var hit))
                return;

            int3 startVoxel = (int3)math.floor(originVoxel);
            int3 endVoxel = hit.Position * VoxelDimensions.BrickEdge
                          + (int3)math.round(math.normalize(direction) * (VoxelDimensions.BrickEdge * 3));

            var cursor = DdaTraversal.Cursor.Between(startVoxel, endVoxel);
            int3 previous = startVoxel;

            while (cursor.MoveNext())
            {
                int3 v = cursor.Current;
                if (VoxelAccess.IsSolid(ref _world.Table, in _world.Pool, v))
                {
                    _aimVoxel = v;
                    _placeVoxel = previous;
                    _hasAim = true;
                    return;
                }

                previous = v;
            }
        }

        // -- HUD -----------------------------------------------------------------

        private void OnGUI()
        {
            if (!_showHud || _world == null || _renderer == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };

            GUI.Box(new Rect(10, 10, 420, 366), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 20, 400, 350));

            byte material = ShowcaseWorld.BuildableMaterials[_materialSlot];
            int poolBytes = _world.Pool.AllocatedCount * VoxelDimensions.BytesPerMixedBrick;
            float fps = _smoothedMs > 0f ? 1000f / _smoothedMs : 0f;
            var regionCoord = new int3(
                Mathf.FloorToInt(transform.position.x / ShowcaseWorld.RegionMetres), 0,
                Mathf.FloorToInt(transform.position.z / ShowcaseWorld.RegionMetres));

            GUILayout.Label("<b>Voxel engine showcase</b>", style);
            GUILayout.Label($"seed 0x{_world.Seed:X}   voxel {VoxelSurfaceRenderer.VoxelSize:0.00} m   " +
                            $"region {ShowcaseWorld.RegionMetres:0.#} m", style);
            GUILayout.Space(6);

            GUILayout.Label($"<b>Frame</b>   {fps:0} fps   {_smoothedMs:0.0} ms   " +
                            $"worst 1s {_displayWorstMs:0.0} ms", style);
            GUILayout.Space(6);

            GUILayout.Label("<b>Streaming</b>", style);
            GUILayout.Label($"camera region      {regionCoord.x}, {regionCoord.z}", style);
            GUILayout.Label($"resident regions   {_world.Table.ResidentCount}" +
                            $"   (load r{_world.LoadRadiusRegions} / unload r{_world.UnloadRadiusRegions})", style);
            GUILayout.Label($"generated / evicted {_world.RegionsGenerated} / {_world.RegionsEvicted}" +
                            $"   pending {_world.PendingRegionLoads}", style);
            GUILayout.Label($"last generate      {_world.LastGenerateMs:0.0} ms" +
                            $"   mesh {_renderer.LastRebuildMs:0.0} ms (queue {_renderer.PendingRebuilds})", style);
            GUILayout.Space(6);

            GUILayout.Label("<b>Storage</b>", style);
            GUILayout.Label($"mixed bricks       {_world.Pool.AllocatedCount:N0} / {_world.Pool.Capacity:N0}" +
                            $"   ({poolBytes / (1024f * 1024f):0.0} MB)", style);
            GUILayout.Label($"surface geometry   {_renderer.FaceCount:N0} quads" +
                            $"   {_renderer.VertexCount:N0} verts   {_renderer.RegionMeshCount} meshes", style);
            GUILayout.Space(6);

            GUILayout.Label($"<b>Last edit</b>   {_lastEditLabel}   ({_lastEditMs:0.0} ms)", style);
            GUILayout.Space(6);

            GUILayout.Label("<b>Controls</b>", style);
            GUILayout.Label("WASD/QE fly, shift boost, mouse look, esc release", style);
            GUILayout.Label("LMB blast   RMB build   wheel radius   1-4 material", style);
            GUILayout.Label("T shadows   F1 hide HUD", style);
            GUILayout.Label($"brush r{m_BrushRadius}   material <b>{ShowcaseWorld.MaterialNames[material]}</b>", style);

            GUILayout.EndArea();
        }
    }
}
