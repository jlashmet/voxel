using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Collision.Api;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Tiering.Api;

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
    ///   Shared traversal  — tornado collision runs through <see cref="DdaTraversal"/>, the
    ///                       same deterministic walk used by the rest of the voxel engine.
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
                 "Each slot currently costs 2112 B; runtime clamps this to the device tier.")]
        [SerializeField] private int m_BrickPoolCapacity = 262144;

        [Header("Streaming")]
        [Tooltip("Regions kept resident around the player. One region is 51.2 m across.")]
        [SerializeField] private int m_LoadRadiusRegions = 8;

        [Tooltip("Regions evicted past this radius. The gap above the load radius is the " +
                 "hysteresis that stops a region thrashing on a boundary.")]
        [SerializeField] private int m_UnloadRadiusRegions = 11;

        [Tooltip("Milliseconds per frame spent generating terrain. Work resumes mid-region.")]
        [SerializeField] private float m_GenerateBudgetMs = 3f;

        [Header("Character")]
        [SerializeField] private bool m_FlyMode;
        [SerializeField] private float m_WalkSpeed = 5.5f;
        [SerializeField] private float m_FlySpeed = 18f;
        [SerializeField] private float m_LookSensitivity = 2.5f;

        [Header("Editing")]
        [SerializeField] private int m_BrushRadius = 12;
        [SerializeField] private int m_MinBrushRadius = 2;
        [SerializeField] private int m_MaxBrushRadius = 40;

        private ShowcaseWorld _world;
        private GpuDebrisSystem _gpuDebris;
        private CharacterMotor _motor;
        private bool _spawned;

        private bool _mouseLook = true;
        private bool _flashlightEnabled;
        private float _yaw, _pitch;
        private double _lastEditMs;
        private string _lastEditLabel = "—";

        private sealed class TornadoShot
        {
            public GameObject Root;
            public LineRenderer[] Spirals;
            public Transform Core;
            public Mesh CoreMesh;
            public Vector3 Position;
            public Vector3 Direction;
            public float Age;
            public float Phase;
            public int ImpactRadius;
        }

        private readonly List<TornadoShot> _tornadoes = new();
        private Material _tornadoMaterial;
        private VoxelFarTerrain _farTerrain;

        private const float TornadoSpeed = 28f;
        private const float TornadoLifetime = 3f;
        private const int MaxActiveTornadoes = 8;

        public int ActiveTornadoCount => _tornadoes.Count;
        public bool FlashlightEnabled => _flashlightEnabled;

        // -- lifecycle -----------------------------------------------------------

        // Built in OnEnable rather than Awake so an editor domain reload rebuilds them; the
        // world holds Persistent native collections and must not outlive the component.
        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            // Clamp by bytes, not an obsolete slot count. Sidecars change per-slot cost; tier
            // budgets remain the authority and cannot silently be exceeded by an inspector value.
            int tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity, tierBytes);

            _world = new ShowcaseWorld(m_Seed, capacity,
                                       m_LoadRadiusRegions, m_UnloadRadiusRegions);
            _gpuDebris = new GpuDebrisSystem();
            _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };

            // Hand stable world capabilities to Composition; URP still owns the concrete render
            // feature, but the showcase no longer reaches into Rendering.Runtime globals.
            RenderingComposition.ResetSurfacePassDiagnostics("showcase-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);

            // Terrain past the streaming radius. The voxel world only makes a few hundred
            // metres resident, so without this the mountains simply are not in the scene to be
            // seen. Inner radius sits just inside the loaded region ring so the two overlap
            // rather than leaving a gap at the handover.
            float streamedMetres = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
            _farTerrain = VoxelFarTerrain.Create(transform, m_Seed,
                                                 streamedMetres * 0.85f, 12000f);
            _farTerrain.Structures = _world.FarField;
            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld, _world.Changes, _world.Seed, farFieldEnabled: true);
            _spawned = false;

            Spawn();
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _gpuDebris?.Dispose();
            _gpuDebris = null;
            for (int i = 0; i < _tornadoes.Count; i++)
                DestroyTornado(_tornadoes[i]);
            _tornadoes.Clear();
            if (_tornadoMaterial != null) Destroy(_tornadoMaterial);
            _tornadoMaterial = null;
            _world?.Dispose();
            _world = null;
        }

        /// <summary>
        /// Enables a presentation-only section box. Intended for fixed showcase cameras; it
        /// never writes to the voxel world and is reset automatically when this driver disables.
        /// </summary>
        public void SetCutawayPresentation(bool enabled, Vector3 minVoxel = default,
                                           Vector3 maxVoxel = default)
        {
            RenderingComposition.SetCutaway(enabled, minVoxel, maxVoxel);
        }

        /// <summary>
        /// Puts the character on the ground with the region beneath it fully built. This one
        /// generation is deliberately blocking: a non-resident region reads as empty, so
        /// spawning before it exists drops the character through the world.
        /// </summary>
        private void Spawn()
        {
            // The landmark is owned by the origin region. Build it first so castle planning has
            // resolved the primary approach before choosing the spawn column; landmark construction
            // also preloads every neighbouring region its terrain sculpt can touch.
            _world.GenerateRegionBlocking(int3.zero);

            var spawn = _world.CastleSpawnPosition();

            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));
            _motor.SnapToGround(_world, spawn);

            RenderingComposition.SetLocalLights(
                _world.CastlePresentationLights, _world.CastlePresentationLightColours);

            transform.position = _motor.EyePosition;
            Vector3 castleTarget = _world.CastleLookTargetPosition();
            Vector3 toCastle = castleTarget - transform.position;
            _yaw = Mathf.Atan2(toCastle.x, toCastle.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Atan2(toCastle.y,
                                  new Vector2(toCastle.x, toCastle.z).magnitude) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _spawned = true;
        }

        private void Update()
        {
            if (!Application.isPlaying || _world == null) return;

            {
                if (!_spawned) Spawn();

                HandleKeys();
                if (_mouseLook) HandleLook();
                MovePlayer();
                UpdateFlashlight();
                HandleEdits();
                StepTornadoes(Time.deltaTime);
                _gpuDebris?.Step(_world, Time.deltaTime);

                // Startup may spend a loading budget until the atomic landmark is committed.
                // Afterwards streaming returns to the interactive 3 ms slice.
                double streamingBudget = _world.CastleVoxels == 0
                    ? Mathf.Max(m_GenerateBudgetMs, 12f) : m_GenerateBudgetMs;
                _world.StepStreaming(transform.position, streamingBudget);
                if (_world.CastleVoxels > 0) RenderingComposition.SetSurfaceBuildEnabled(true);

                // The far field's hole has to follow what streaming has actually finished, not
                // the radius it was configured with. Set after StepStreaming so a region that
                // completed this frame closes the gap on this frame rather than the next.
                if (_farTerrain != null)
                    _farTerrain.HoleRadiusMetres =
                        _world.ResidentGroundRadiusMetres(transform.position);
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

            if (Input.GetKeyDown(KeyCode.E)) TryInteract();

            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
                m_BrushRadius = Mathf.Clamp(m_BrushRadius + (scroll > 0f ? 2 : -2),
                                            m_MinBrushRadius, m_MaxBrushRadius);
        }

        /// <summary>Whether the player-facing E prompt should currently be visible.</summary>
        public bool InteractionPromptVisible =>
            _world != null && _motor != null
            && (_world.CanOpenCastleFrontGate(_motor.Position)
                || _world.CanOpenCastleTrapdoor(_motor.Position));

        /// <summary>
        /// Performs the interaction bound to E. Keeping this as a callable driver operation lets
        /// tests exercise the same motor-position gate and feedback path as keyboard input rather
        /// than bypassing the showcase and calling the world mutation directly.
        /// </summary>
        public bool TryInteract()
        {
            if (_world == null || _motor == null) return false;

            _lastEditMs = 0.0;
            if (_world.TryOpenCastleFrontGate(_motor.Position))
            {
                _lastEditLabel = "castle front gate opened";
                return true;
            }
            if (_world.TryOpenCastleTrapdoor(_motor.Position))
            {
                _lastEditLabel = "secret cellar trapdoor opened";
                return true;
            }
            return false;
        }

        private void HandleEdits()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 hand = transform.position + transform.forward * 0.65f
                             + transform.right * 0.34f - transform.up * 0.24f;
                LaunchTornado(hand, transform.forward, m_BrushRadius);
                _lastEditMs = 0.0;
                _lastEditLabel = $"tornado launched r{m_BrushRadius}";
            }
            else if (Input.GetMouseButtonDown(1)) ToggleFlashlight();
        }

        public void ToggleFlashlight()
        {
            _flashlightEnabled = !_flashlightEnabled;
            UpdateFlashlight();
        }

        private void UpdateFlashlight()
        {
            RenderingComposition.SetFlashlight(
                _flashlightEnabled, transform.position, transform.forward);
        }

        /// <summary>Launches a visible corkscrew projectile; impact remains world-authoritative.</summary>
        public void LaunchTornado(Vector3 origin, Vector3 direction, int impactRadius)
        {
            if (_tornadoes.Count >= MaxActiveTornadoes)
            {
                DestroyTornado(_tornadoes[0]);
                _tornadoes.RemoveAt(0);
            }

            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : transform.forward;
            EnsureTornadoMaterial();

            var root = new GameObject("Tornado projectile");
            root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var shot = new TornadoShot
            {
                Root = root,
                Spirals = new LineRenderer[3],
                Position = origin,
                Direction = direction,
                ImpactRadius = Mathf.Clamp(impactRadius, m_MinBrushRadius, m_MaxBrushRadius),
            };

            Color[] colours =
            {
                new(0.72f, 0.94f, 1f, 0.92f),
                new(0.36f, 0.72f, 1f, 0.78f),
                new(0.86f, 0.90f, 0.98f, 0.58f),
            };

            for (int i = 0; i < shot.Spirals.Length; i++)
            {
                var child = new GameObject($"spiral {i}");
                child.transform.SetParent(root.transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = _tornadoMaterial;
                line.useWorldSpace = false;
                line.positionCount = 40;
                line.widthMultiplier = 0.075f;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.sortingOrder = 50;
                line.startColor = colours[i];
                line.endColor = new Color(colours[i].r, colours[i].g, colours[i].b, 0.08f);
                shot.Spirals[i] = line;
            }

            CreateTornadoCore(shot);

            UpdateTornadoVisual(shot);
            _tornadoes.Add(shot);
        }

        private void StepTornadoes(float deltaTime)
        {
            int impactsThisFrame = 0;
            for (int i = _tornadoes.Count - 1; i >= 0; i--)
            {
                var shot = _tornadoes[i];
                Vector3 previous = shot.Position;
                float distance = TornadoSpeed * deltaTime;
                shot.Position += shot.Direction * distance;
                shot.Age += deltaTime;
                shot.Phase += deltaTime * 13f;

                if (TryTornadoImpact(previous, shot.Position, out int3 hit, out bool semanticTreeHit))
                {
                    // Structural classification is CPU-authoritative. Serialize impacts so a
                    // shotgun burst cannot schedule several large connectivity walks in one frame.
                    if (impactsThisFrame > 0)
                    {
                        shot.Position = previous;
                        shot.Root.transform.position = previous;
                        continue;
                    }
                    impactsThisFrame++;

                    var start = Time.realtimeSinceStartupAsDouble;
                    int changed = 0;
                    if (!semanticTreeHit)
                        changed = _world.Explode(hit, (ushort)shot.ImpactRadius,
                                                 (float3)shot.Direction);
                    if (semanticTreeHit || changed > 0)
                    {
                        float3 impactMetres = (float3)hit * ShowcaseWorld.VoxelSize;
                        VegetationComposition.TreeDamage.ApplyBlast(
                            impactMetres, shot.ImpactRadius * ShowcaseWorld.VoxelSize,
                            (float3)shot.Direction);
                    }
                    _lastEditMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;
                    _lastEditLabel = $"tornado impact r{shot.ImpactRadius}: {changed:N0} voxels, " +
                                     $"{(_gpuDebris?.ActiveVoxels ?? 0):N0} falling";
                    SpawnImpactBurst((Vector3)((float3)hit * ShowcaseWorld.VoxelSize));
                    DestroyTornado(shot);
                    _tornadoes.RemoveAt(i);
                    continue;
                }

                if (shot.Age >= TornadoLifetime)
                {
                    DestroyTornado(shot);
                    _tornadoes.RemoveAt(i);
                    continue;
                }

                shot.Root.transform.SetPositionAndRotation(
                    shot.Position, Quaternion.LookRotation(shot.Direction));
                UpdateTornadoVisual(shot);
            }
        }

        private bool TryTornadoImpact(Vector3 from, Vector3 to, out int3 hit, out bool semanticTreeHit)
        {
            Vector3 travel = to - from;
            Vector3 forward = travel.sqrMagnitude > 1e-8f ? travel.normalized : transform.forward;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(forward, Vector3.right);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, forward).normalized;
            const float sweepRadius = 0.28f;
            const float diagonal = sweepRadius * 0.70710678f;
            bool found = false;
            float nearestDistance = float.MaxValue;
            hit = default;
            semanticTreeHit = false;
            ConsiderTornadoLine(from, to, Vector3.zero, ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, right * sweepRadius,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, -right * sweepRadius,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, up * sweepRadius,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, -up * sweepRadius,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, (right + up) * diagonal,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, (right - up) * diagonal,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, (-right + up) * diagonal,
                                ref found, ref nearestDistance, ref hit);
            ConsiderTornadoLine(from, to, (-right - up) * diagonal,
                                ref found, ref nearestDistance, ref hit);
            if (VegetationComposition.TreeDamage.TrySweepImpact(
                    (float3)from, (float3)to, sweepRadius,
                    out float3 treeHitMetres, out _))
            {
                float treeDistance = math.lengthsq(treeHitMetres - (float3)from);
                if (!found || treeDistance < nearestDistance)
                {
                    hit = (int3)math.round(treeHitMetres / ShowcaseWorld.VoxelSize);
                    semanticTreeHit = true;
                    found = true;
                }
            }
            return found;
        }

        private void ConsiderTornadoLine(Vector3 from, Vector3 to, Vector3 offset,
                                         ref bool found, ref float nearestDistance, ref int3 hit)
        {
            if (!TryTornadoLineImpact(from + offset, to + offset, out int3 candidate)) return;
            float distance = math.lengthsq((float3)candidate * ShowcaseWorld.VoxelSize
                                           - (float3)from);
            if (distance >= nearestDistance) return;
            nearestDistance = distance;
            hit = candidate;
            found = true;
        }

        private bool TryTornadoLineImpact(Vector3 from, Vector3 to, out int3 hit)
        {
            int3 start = (int3)math.floor((float3)from / ShowcaseWorld.VoxelSize);
            int3 end = (int3)math.floor((float3)to / ShowcaseWorld.VoxelSize);
            var cursor = DdaTraversal.Cursor.Between(start, end);

            while (cursor.MoveNext())
            {
                int3 voxel = cursor.Current;
                if (!_world.SurfaceQuery.TryRead(voxel, out VoxelCell cell) ||
                    cell.BaseMaterialId == VoxelGrid.MaterialEmpty) continue;
                hit = voxel;
                return true;
            }

            hit = default;
            return false;
        }

        private void UpdateTornadoVisual(TornadoShot shot)
        {
            const int points = 40;
            for (int strand = 0; strand < shot.Spirals.Length; strand++)
            {
                var line = shot.Spirals[strand];
                for (int p = 0; p < points; p++)
                {
                    float t = p / (float)(points - 1);
                    float z = Mathf.Lerp(-1.9f, 0.25f, t);
                    float radius = Mathf.Lerp(0.62f, 0.06f, t)
                                 * (0.86f + Mathf.Sin(t * 18f + shot.Phase) * 0.14f);
                    float angle = shot.Phase + strand * Mathf.PI * 2f / shot.Spirals.Length
                                + t * Mathf.PI * 8f;
                    line.SetPosition(p, new Vector3(Mathf.Cos(angle) * radius,
                                                    Mathf.Sin(angle) * radius, z));
                }
            }

            if (shot.Core != null)
                shot.Core.localRotation = Quaternion.Euler(0f, 0f, shot.Phase * Mathf.Rad2Deg);
        }

        private void CreateTornadoCore(TornadoShot shot)
        {
            const int rings = 9;
            const int sides = 16;
            var vertices = new Vector3[rings * sides];
            var triangles = new int[(rings - 1) * sides * 6];

            for (int ring = 0; ring < rings; ring++)
            {
                float t = ring / (float)(rings - 1);
                float z = Mathf.Lerp(-1.85f, 0.18f, t);
                float radius = Mathf.Lerp(0.38f, 0.055f, t)
                             * (1f + Mathf.Sin(t * Mathf.PI * 5f) * 0.12f);
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides + t * Mathf.PI * 2f;
                    vertices[ring * sides + side] =
                        new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
                }
            }

            int triangle = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + next;
                int c = (ring + 1) * sides + side;
                int d = (ring + 1) * sides + next;
                triangles[triangle++] = a; triangles[triangle++] = b; triangles[triangle++] = c;
                triangles[triangle++] = b; triangles[triangle++] = d; triangles[triangle++] = c;
            }

            shot.CoreMesh = new Mesh { name = "Runtime tornado funnel" };
            shot.CoreMesh.vertices = vertices;
            shot.CoreMesh.triangles = triangles;
            shot.CoreMesh.RecalculateNormals();
            shot.CoreMesh.RecalculateBounds();

            var core = new GameObject("funnel core");
            core.transform.SetParent(shot.Root.transform, false);
            core.AddComponent<MeshFilter>().sharedMesh = shot.CoreMesh;
            var renderer = core.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _tornadoMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            shot.Core = core.transform;
        }

        private static void DestroyTornado(TornadoShot shot)
        {
            if (shot.CoreMesh != null) Destroy(shot.CoreMesh);
            if (shot.Root != null) Destroy(shot.Root);
        }

        private void EnsureTornadoMaterial()
        {
            if (_tornadoMaterial != null) return;
            // UI/Default is a late transparent vertex-colour shader. The custom voxel pass
            // fills the opaque target immediately before transparents, so the effect stays in the
            // late transparent queue and composes over extracted voxel geometry.
            Shader shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            _tornadoMaterial = new Material(shader) { name = "Runtime Tornado" };
            if (_tornadoMaterial.HasProperty("_Color"))
                _tornadoMaterial.SetColor("_Color", new Color(0.55f, 0.86f, 1f, 0.48f));
            if (_tornadoMaterial.HasProperty("_BaseColor"))
                _tornadoMaterial.SetColor("_BaseColor", new Color(0.55f, 0.86f, 1f, 0.48f));
        }

        private void SpawnImpactBurst(Vector3 position)
        {
            var root = new GameObject("Tornado impact");
            root.transform.position = position;
            var particles = root.AddComponent<ParticleSystem>();
            // A newly-added ParticleSystem starts immediately. Stop it before configuring
            // duration and bursts; Unity rejects those mutations while it is playing.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = 0.18f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.42f, 0.78f, 1f, 0.9f), Color.white);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 110) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.24f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _tornadoMaterial;
            particles.Play();
            Destroy(root, 1.2f);
        }

        // -- interaction prompt --------------------------------------------------

        private void OnGUI()
        {
            if (!Application.isPlaying || _world == null) return;

            // Keep only contextual gameplay guidance; the persistent diagnostics overlay is gone.
            if (InteractionPromptVisible)
            {
                var prompt = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                GUI.Box(new Rect(Screen.width * 0.5f - 120f, Screen.height - 96f, 240f, 40f),
                        _world.CanOpenCastleFrontGate(_motor.Position)
                            ? "E  OPEN CASTLE GATE" : "E  OPEN TRAPDOOR", prompt);
            }
        }

    }
}
