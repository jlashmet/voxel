using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Collision;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Driver for the showcase scene: owns a <see cref="ShowcaseWorld"/>, walks a character over
    /// it, streams regions around them, turns clicks into edits, and reports what the engine is
    /// doing underneath.
    ///
    /// What the scene demonstrates, in the order it becomes visible:
    ///
    ///   Collision         — the character stands on voxels through <see cref="CharacterMotor"/>,
    ///                       which reads the same storage the surface mesh is built from.
    ///   Streaming         — walk in any direction and regions generate ahead and evict behind.
    ///                       Generation and meshing are time-budgeted, so the cost is a slice
    ///                       per frame rather than a stall per region.
    ///   Sparse storage    — the mixed-brick count tracks surface area, not world volume.
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
    /// Play mode only, deliberately. An earlier version ran with <c>ExecuteAlways</c> so the
    /// scene view would show the world without pressing Play. That is not survivable: OnEnable
    /// allocates a ~150 MB brick pool and does seconds of blocking generation and meshing, and
    /// the editor re-runs OnEnable on every domain reload — every script compile, every entry
    /// and exit from play mode. Reloads then queue faster than the work completes and the editor
    /// is gone. Nothing in this component may allocate or generate outside play mode.
    /// </remarks>
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
        [Tooltip("Regions kept resident around the player. One region is 51.2 m across.")]
        [SerializeField] private int m_LoadRadiusRegions = 3;

        [Tooltip("Regions evicted past this radius. The gap above the load radius is the " +
                 "hysteresis that stops a region thrashing on a boundary.")]
        [SerializeField] private int m_UnloadRadiusRegions = 5;

        [Tooltip("Milliseconds per frame spent generating terrain. Work resumes mid-region.")]
        [SerializeField] private float m_GenerateBudgetMs = 3f;

        [Tooltip("Milliseconds per frame spent building surface meshes. Work resumes mid-region.")]
        [SerializeField] private float m_MeshBudgetMs = 4f;

        [Header("Character")]
        [SerializeField] private bool m_FlyMode;
        [SerializeField] private float m_WalkSpeed = 5.5f;
        [SerializeField] private float m_FlySpeed = 18f;
        [SerializeField] private float m_LookSensitivity = 2.5f;

        [Tooltip("How far the player can reach to edit, in metres.")]
        [SerializeField] private float m_ReachMetres = 12f;

        [Header("Editing")]
        [SerializeField] private int m_BrushRadius = 12;
        [SerializeField] private int m_MinBrushRadius = 2;
        [SerializeField] private int m_MaxBrushRadius = 40;

        [Header("Renderer")]
        [Tooltip("Raymarch the brickmap on the GPU (the engine path). Off falls back to the " +
                 "demo's mesh builder, which is only kept for A/B comparison.")]
        [SerializeField] private bool m_UseRaymarch = true;

        [Header("Presentation")]
        [Tooltip("Presentation-only. Shadows across a streamed world are expensive.")]
        [SerializeField] private bool m_CastShadows;

        private ShowcaseWorld _world;
        private VoxelSurfaceRenderer _renderer;
        private CharacterMotor _motor;
        private bool _spawned;

        private int _materialSlot;
        private bool _showHud = true;
        private bool _mouseLook = true;
        private float _yaw, _pitch;
        private double _lastEditMs;
        private string _lastEditLabel = "—";
        private bool _hasAim;
        private int3 _aimVoxel, _placeVoxel;

        private float _smoothedMs;
        private float _worstMs;
        private float _worstWindowStart;
        private float _displayWorstMs;

        // -- lifecycle -----------------------------------------------------------

        // Built in OnEnable rather than Awake so an editor domain reload rebuilds them; the
        // world holds Persistent native collections and must not outlive the component.
        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            // Clamped rather than trusted: the pool is 576 bytes per slot, so a mistyped
            // inspector value is hundreds of megabytes before anything reports a problem.
            int capacity = Mathf.Clamp(m_BrickPoolCapacity, 4096, 262144);

            _world = new ShowcaseWorld(m_Seed, capacity,
                                       m_LoadRadiusRegions, m_UnloadRadiusRegions);
            _renderer = new VoxelSurfaceRenderer { CastShadows = m_CastShadows };
            _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };

            // Hand the world to the render feature. URP owns the feature and constructs it, so
            // the world registers itself rather than being injected.
            VoxelRenderBridge.RegionsNeedingUpload = _world.RegionsNeedingUpload;
            VoxelRenderBridge.Source = () => new VoxelWorldView
            {
                Table = _world.Table,
                Pool = _world.Pool,
                CameraRegion = ShowcaseWorld.RegionAt(transform.position),
            };
            _spawned = false;

            Spawn();
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            VoxelRenderBridge.Source = null;
            VoxelRenderBridge.RegionsNeedingUpload = null;

            _renderer?.Dispose();
            _renderer = null;
            _world?.Dispose();
            _world = null;
        }

        /// <summary>
        /// Puts the character on the ground with the region beneath it fully built. This one
        /// generation is deliberately blocking: a non-resident region reads as empty, so
        /// spawning before it exists drops the character through the world.
        /// </summary>
        private void Spawn()
        {
            var spawn = _world.SpawnPosition();

            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));
            _motor.SnapToGround(_world, spawn);

            transform.position = _motor.EyePosition;
            transform.rotation = Quaternion.Euler(5f, 0f, 0f);
            _yaw = 0f;
            _pitch = 5f;
            _spawned = true;
        }

        private void Update()
        {
            if (!Application.isPlaying || _world == null || _renderer == null) return;

            {
                if (!_spawned) Spawn();

                TrackFrameTime();
                HandleKeys();
                if (_mouseLook) HandleLook();
                MovePlayer();
                UpdateAim();
                HandleEdits();

                _world.StepStreaming(transform.position, m_GenerateBudgetMs);
            }

            if (m_UseRaymarch)
            {
                // The raymarch reads the brickmap directly, so the mesh path is not just
                // unnecessary — leaving it on would draw the world twice.
                // Do not clear RegionsNeedingUpload here. It is the world telling the renderer
                // which pointer grids changed, and the renderer clears it once consumed. The
                // driver clearing it every frame meant a region's pointers were uploaded once,
                // while it was still generating, and never refreshed — so the GPU held pointers
                // to pool slots that had since been freed and reused, and the raymarch drew
                // nothing at all once generation completed.
                _renderer.SetVisible(false);
            }
            else
            {
                _renderer.SetVisible(true);
                _renderer.CastShadows = m_CastShadows;
                _renderer.Sync(_world, m_MeshBudgetMs);
            }
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

        // -- movement ------------------------------------------------------------

        private void HandleLook()
        {
            _yaw += Input.GetAxisRaw("Mouse X") * m_LookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * m_LookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void MovePlayer()
        {
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            if (m_FlyMode)
            {
                var move = transform.forward * forward + transform.right * strafe;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

                if (move.sqrMagnitude > 1e-6f)
                {
                    float speed = m_FlySpeed * (sprint ? 3f : 1f);
                    transform.position += move.normalized * (speed * Time.deltaTime);
                }

                _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
                _motor.Velocity = Vector3.zero;
                return;
            }

            // Walking. Movement is flattened to the ground plane so looking up does not slow
            // you down, and held while the region under the character is still generating —
            // an ungenerated region reads as empty and would drop the player through it.
            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(_motor.Position)))
            {
                transform.position = _motor.EyePosition;
                return;
            }

            var flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            var wish = flatForward * forward + flatRight * strafe;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            _motor.Step(_world, wish, sprint, Input.GetKey(KeyCode.Space), Time.deltaTime);
            transform.position = _motor.EyePosition;
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

            if (Input.GetKeyDown(KeyCode.F))
            {
                m_FlyMode = !m_FlyMode;
                if (!m_FlyMode)
                {
                    _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
                    _motor.Velocity = Vector3.zero;
                }
            }

            if (Input.GetKeyDown(KeyCode.R)) Spawn();

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
        /// Resolves what the player is looking at, in two stages.
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
            var direction = math.normalize((float3)transform.forward);
            var originBrick = originVoxel / VoxelDimensions.BrickEdge;

            if (!VoxelRaycast.Raycast(in _world.Table, in _world.Pool, originBrick, direction, out var hit))
                return;

            int reachVoxels = Mathf.CeilToInt(m_ReachMetres / VoxelSurfaceRenderer.VoxelSize);
            int3 startVoxel = (int3)math.floor(originVoxel);
            int3 endVoxel = startVoxel + (int3)math.round(direction * reachVoxels);

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
            if (!Application.isPlaying || !_showHud || _world == null || _renderer == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false };

            GUI.Box(new Rect(10, 10, 430, 384), GUIContent.none);
            GUILayout.BeginArea(new Rect(22, 20, 410, 368));

            byte material = ShowcaseWorld.BuildableMaterials[_materialSlot];
            int poolBytes = _world.Pool.AllocatedCount * VoxelDimensions.BytesPerMixedBrick;
            float fps = _smoothedMs > 0f ? 1000f / _smoothedMs : 0f;
            var rc = ShowcaseWorld.RegionAt(_motor.Position);

            GUILayout.Label("<b>Voxel engine showcase</b>", style);
            GUILayout.Label($"voxel {VoxelSurfaceRenderer.VoxelSize:0.00} m   " +
                            $"brick {VoxelSurfaceRenderer.VoxelSize * VoxelDimensions.BrickEdge:0.0} m   " +
                            $"region {ShowcaseWorld.RegionMetres:0.#} m   seed 0x{_world.Seed:X}", style);
            GUILayout.Space(6);

            GUILayout.Label($"<b>Frame</b>   {fps:0} fps   {_smoothedMs:0.0} ms   " +
                            $"worst 1s {_displayWorstMs:0.0} ms", style);
            GUILayout.Space(6);

            GUILayout.Label($"<b>Player</b>   {(m_FlyMode ? "flying" : _motor.Grounded ? "grounded" : "airborne")}", style);
            GUILayout.Label($"position   {_motor.Position.x:0.0}, {_motor.Position.y:0.0}, {_motor.Position.z:0.0} m" +
                            $"   region {rc.x}, {rc.z}", style);
            GUILayout.Space(6);

            GUILayout.Label("<b>Streaming</b>", style);
            GUILayout.Label($"resident regions   {_world.Table.ResidentCount}" +
                            $"   (load r{_world.LoadRadiusRegions} / unload r{_world.UnloadRadiusRegions})", style);
            GUILayout.Label($"generated / evicted {_world.RegionsGenerated} / {_world.RegionsEvicted}" +
                            $"   queued {_world.PendingRegionLoads}   building {_world.GenerationProgress * 100f:0}%", style);
            GUILayout.Label($"budgets   generate {_world.LastGenerateMs:0.0} / {m_GenerateBudgetMs:0.#} ms" +
                            $"   mesh {_renderer.LastRebuildMs:0.0} / {m_MeshBudgetMs:0.#} ms" +
                            $"   queue {_renderer.PendingRebuilds}", style);
            GUILayout.Space(6);

            GUILayout.Label("<b>Storage</b>", style);
            GUILayout.Label($"mixed bricks       {_world.Pool.AllocatedCount:N0} / {_world.Pool.Capacity:N0}" +
                            $"   ({poolBytes / (1024f * 1024f):0.0} MB)", style);
            GUILayout.Label(m_UseRaymarch
                ? "renderer           GPU brickmap raymarch (no geometry)"
                : $"renderer           mesh: {_renderer.FaceCount:N0} quads, {_renderer.VertexCount:N0} verts", style);
            GUILayout.Space(6);

            GUILayout.Label($"<b>Last edit</b>   {_lastEditLabel}   ({_lastEditMs:0.0} ms)", style);
            GUILayout.Space(4);

            GUILayout.Label("WASD move   space jump   shift sprint   F fly   R respawn", style);
            GUILayout.Label($"LMB blast   RMB build   wheel radius   1-4 material   " +
                            $"r{m_BrushRadius} <b>{ShowcaseWorld.MaterialNames[material]}</b>", style);
            GUILayout.Label("T shadows   F1 hide HUD   esc release cursor", style);

            GUILayout.EndArea();
        }
    }
}
