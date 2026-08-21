using System.Collections.Generic;
using Game.Composition.Materials;
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
    public sealed class VoxelShowcase : MonoBehaviour, IShowcaseMeasurementDriver
    {
        [Header("World")]
        [Tooltip("Deterministic world seed. The same seed produces the same world everywhere.")]
        [SerializeField] private uint m_Seed = 0x5EED1234;

        [Tooltip("Mixed-brick pool capacity. Bounded by configuration, never by world size. " +
                 "Each slot currently costs 2112 B; runtime clamps this to the device tier.")]
        [SerializeField] private int m_BrickPoolCapacity = 2367424;

        [Header("Streaming")]
        [Tooltip("Regions kept resident around the player. One region is 51.2 m across.")]
        [SerializeField] private int m_LoadRadiusRegions = 8;

        [Tooltip("Regions evicted past this radius. The gap above the load radius is the " +
                 "hysteresis that stops a region thrashing on a boundary.")]
        [SerializeField] private int m_UnloadRadiusRegions = 11;

        [Tooltip("Milliseconds per frame spent generating terrain. Work resumes mid-region.")]
        [SerializeField] private float m_GenerateBudgetMs = 3f;

        [Tooltip("Full builds terrain, castle and the Kentridge town. CastleOnly builds terrain "
               + "and the castle, for a scene whose geometry is one landmark rather than a town.")]
        [SerializeField] private ShowcaseFeatureContent m_Features = ShowcaseFeatureContent.Full;

        [Tooltip("Bake restores the offline startup image. Generate builds the world during the "
               + "scene on the ordinary per-frame budget, which only suits a small radius.")]
        [SerializeField] private ShowcaseStartupSource m_Startup = ShowcaseStartupSource.Bake;

        [Tooltip("Scales the LOD hand-over distances. 1 keeps the shipped layout (finest step to "
               + "96 m). Lower values draw far fewer chunks and stop the arena being permanently "
               + "oversubscribed, at the cost of coarser mid-distance terrain.")]
        [SerializeField] private float m_DetailBandScale = 0.6f;

        [Tooltip("Mesh everything at the finest step instead of using coarse LOD rings. Only "
               + "viable for a small streamed radius; it removes the seam where bands meet.")]
        [SerializeField] private bool m_DisableLod;

        /// <summary>Which authored content this scene builds. Content keyed to the castle — its
        /// vegetation, for one — must not be published into a scene that has no castle.</summary>
        public ShowcaseFeatureContent Features => m_Features;

        /// <summary>
        /// Whether the world keeps streaming new regions as the player moves.
        ///
        /// A bounded showcase can be built in full up front, after which movement needs no new
        /// terrain at all. Freezing streaming then measures what moving through finished world
        /// actually costs, instead of measuring first-time generation of ground the player has
        /// just walked into.
        /// </summary>
        public bool StreamingEnabled { get; set; } = true;

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
        private bool _castleLightsPublished;
        private int _loggedCastleStage = -1;
        private bool _loggedCastleComplete;
        private int _loggedFeatureInstances;
        private int _loggedRegions = -1;
        private int _lastStreamingLogFrame;

        private bool _mouseLook = true;
        private bool _hadFocus = true;
        private int _relockFrames;
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
            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity, tierBytes);

            // Pass the tier budget down as well as the capacity it produced. Storage applies its
            // own ceiling before the eager BrickPool allocation; without the tier budget it would
            // fall back to the conservative backstop and halve a pool this scene already sized.
            _world = new ShowcaseWorld(
                m_Seed, capacity, m_LoadRadiusRegions, m_UnloadRadiusRegions,
                GameMaterialComposition.SimulationDefinitions(), tierBytes, m_Features, m_Startup);
            _gpuDebris = new GpuDebrisSystem();
            _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };

            // Keep the production surface scheduler live from the first rendered frame. The
            // castle is published incrementally, and ready chunk geometry already remains visible
            // until replacements upload. Disabling this pass used to accumulate all terrain and
            // castle work, then dump the entire backlog into the renderer when the landmark
            // finished — producing the exact castle-arrival cliff this showcase is meant to avoid.
            RenderingComposition.ResetSurfacePassDiagnostics("showcase-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);

            // Terrain past the streaming radius. The voxel world only makes a few hundred
            // metres resident, so without this the mountains simply are not in the scene to be
            // seen. Inner radius sits just inside the loaded region ring so the two overlap
            // rather than leaving a gap at the handover.
            float streamedMetres = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;

            // Voxel rings may not claim further than the world actually streams. Left at the
            // default they cover 410 m regardless, so a scene with a smaller load radius meshed
            // bands with no resident regions and the far field broke into floating slabs.
            RenderingComposition.SetVoxelRingRadiusMetres(streamedMetres);
            RenderingComposition.SetVoxelLodEnabled(!m_DisableLod);
            // Explicit, because the scheduler holds this statically: without it this scene would
            // inherit whatever band scale the previously loaded scene happened to set.
            RenderingComposition.SetVoxelDetailBandScale(m_DetailBandScale);

            // The far field begins where the voxel rings end. It used to start inside them, so
            // the two overlapped by design — which is only harmless if one of them is not drawn,
            // and both are.
            _farTerrain = VoxelFarTerrain.Create(transform, m_Seed, streamedMetres, 12000f);
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
            _hadFocus = Application.isFocused;
        }

        private void OnDisable()
        {
            // The castle worker borrows this world's read-only material catalogue even though its
            // heavy mutation target is private. Cancel/join it while this world is still alive;
            // a global render clear must never be responsible for another world's task lifetime.
            _world?.StopBackgroundWork();

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

            // The far field is dynamically created by OnEnable and owns Persistent NativeArrays,
            // meshes, and a reference to this world's FarFieldStructureStore. Leaving the child
            // alive across a component disable lets it draw against a disposed world and creates
            // a second clipmap on the next enable. Sever the world reference before deferred
            // GameObject destruction, then let VoxelFarTerrain.OnDestroy retire its job/caches.
            if (_farTerrain != null)
            {
                _farTerrain.Structures = null;
                Destroy(_farTerrain.gameObject);
                _farTerrain = null;
            }

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
        /// <summary>
        /// Moves the player, and therefore streaming, to a new place in the world.
        ///
        /// Streaming and residency follow this component's transform, not the camera's, so a
        /// caller that only moves the camera views a world that is still streamed around the
        /// spawn — every distant chunk reads as missing because it was never requested.
        /// </summary>
        public void TeleportTo(Vector3 metres)
        {
            _motor.SnapToGround(_world, metres);
            transform.position = _motor.EyePosition;
        }

        private void Spawn()
        {
            // The landmark is owned by the origin region. Build it first even though the safe
            // approach spawn is just south of that region; landmark construction also preloads
            // every neighbouring region its terrain sculpt can touch.
            _world.GenerateCastleOriginBlocking();

            var spawn = _world.SpawnPosition();

            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));
            _motor.SnapToGround(_world, spawn);

            RenderingComposition.SetLocalLights(
                _world.CastlePresentationLights, _world.CastlePresentationLightColours);

            transform.position = _motor.EyePosition;
            var castleTarget = new Vector3(ShowcaseWorld.RegionVoxelEdge * 0.5f * 0.1f,
                                           transform.position.y + 5.0f,
                                           (ShowcaseWorld.RegionVoxelEdge * 0.5f + 120f) * 0.1f);
            Vector3 toCastle = castleTarget - transform.position;
            _yaw = Mathf.Atan2(toCastle.x, toCastle.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Atan2(toCastle.y,
                                  new Vector2(toCastle.x, toCastle.z).magnitude) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _spawned = true;
        }

        /// <summary>
        /// Reports castle authoring as it happens.
        ///
        /// The world tracks stage and timing but never said anything, which is invisible in the
        /// baked scene — the castle is already finished on frame one — and leaves a generated
        /// world looking hung while it authors tens of millions of voxels. Logged on transitions
        /// only, so a multi-minute build costs a handful of lines.
        /// </summary>
        private void ReportCastleProgress()
        {
            int stage = _world.CastleBuildStage;
            if (stage != _loggedCastleStage)
            {
                _loggedCastleStage = stage;
                if (stage != 0)
                    Debug.Log($"Showcase castle: stage {stage} started "
                            + $"(previous stage {_world.LastCastleStage} took "
                            + $"{_world.LastCastleStageMs:0.0} ms; worst "
                            + $"{_world.MaxCastleStageMs:0.0} ms at stage {_world.MaxCastleStage}); "
                            + $"regions generated {_world.RegionsGenerated}, "
                            + $"terrain {_world.GenerationProgress * 100f:0}%");
            }

            // A house-only showcase never builds a castle, so without this the scene would report
            // nothing at all while its landmark went up — the same blind spot the castle logging
            // above exists to close.
            // Streaming state, periodically, while the world is still filling in. Region
            // generation is the slowest stage and reported nothing at all, so an unfinished world
            // was indistinguishable from a broken one.
            if (_world.PendingRegionLoads > 0 || _world.RegionsGenerated != _loggedRegions)
            {
                if (Time.frameCount - _lastStreamingLogFrame >= 120)
                {
                    _lastStreamingLogFrame = Time.frameCount;
                    _loggedRegions = _world.RegionsGenerated;
                    Debug.Log($"Showcase streaming: {_loggedRegions} regions generated, "
                            + $"{_world.PendingRegionLoads} pending, "
                            + $"terrain {_world.GenerationProgress * 100f:0}%");
                }
            }

            if (_world.FeatureInstancesBuilt != _loggedFeatureInstances)
            {
                _loggedFeatureInstances = _world.FeatureInstancesBuilt;
                Debug.Log($"Showcase features: {_loggedFeatureInstances} instance(s), "
                        + $"{_world.FeatureVoxelsBuilt:N0} voxels; "
                        + $"regions generated {_world.RegionsGenerated}, "
                        + $"terrain {_world.GenerationProgress * 100f:0}%");
            }

            if (!_loggedCastleComplete && _world.CastleVoxels > 0)
            {
                _loggedCastleComplete = true;
                Debug.Log($"Showcase castle complete: {_world.CastleVoxels:N0} voxels in "
                        + $"{Time.realtimeSinceStartup:0.0} s since load; worst stage "
                        + $"{_world.MaxCastleStage} at {_world.MaxCastleStageMs:0.0} ms; "
                        + $"regions generated {_world.RegionsGenerated}, "
                        + $"terrain {_world.GenerationProgress * 100f:0}%");
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || _world == null) return;

            {
                if (!_spawned) Spawn();

                // Spawn publishes the castle's lights, but a generated world has no castle yet at
                // that point. Publish once more when the landmark actually lands.
                if (!_castleLightsPublished && _world.CastleVoxels > 0)
                {
                    RenderingComposition.SetLocalLights(
                        _world.CastlePresentationLights, _world.CastlePresentationLightColours);
                    _castleLightsPublished = true;
                }

                ReportCastleProgress();

                HandleKeys();
                if (_mouseLook && _relockFrames == 0) HandleLook();
                MovePlayer();
                UpdateFlashlight();
                HandleEdits();
                StepTornadoes(Time.deltaTime);
                _gpuDebris?.Step(_world, Time.deltaTime);

                // This is an interactive showcase even while the landmark worker is active.
                // Never switch into the old 12 ms "loading" slice: castle authoring, voxel
                // streaming and surface extraction must share the frame without a startup cliff.
                if (StreamingEnabled)
                    _world.StepStreaming(transform.position, m_GenerateBudgetMs);

                // The far field's hole has to follow what streaming has actually finished, not
                // the radius it was configured with. Set after StepStreaming so a region that
                // completed this frame closes the gap on this frame rather than the next.
                if (_farTerrain != null)
                {
                    // Open the hole to where voxel terrain actually reaches, not to the last
                    // fully-generated region shell. That shell collapses to the camera's own
                    // region whenever any column in the next one is still filling, which left the
                    // clipmap drawing from a few metres out — over the near ground and through
                    // whatever was standing on it. The hole setter refuses to open at all until
                    // near coverage is complete, so this cannot uncover a genuine hole.
                    float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
                    _farTerrain.HoleRadiusMetres = Mathf.Max(
                        _world.ResidentGroundRadiusMetres(transform.position), streamed);
                }
            }

        }

        // -- movement ------------------------------------------------------------

        /// <summary>
        /// Far-field state, for diagnostics. The far mesh lifts its vertices over built content,
        /// so a hole that fails to open draws a smooth structure proxy on top of the voxel
        /// building it is supposed to stand in for — which looks like a LOD bug and is not one.
        /// </summary>
        public string DescribeFarTerrain()
        {
            if (_farTerrain == null) return "FAR none";
            float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
            return $"FAR hole={_farTerrain.HoleRadiusMetres:0.#}m "
                 + $"inner={_farTerrain.InnerRadiusMetres:0.#}m streamed={streamed:0.#}m "
                 + $"residentGround={_world.ResidentGroundRadiusMetres(transform.position):0.#}m "
                 + $"coverage={RenderingComposition.HasCompletePublishedNearSurfaceCoverage()} "
                 + $"structures={(_farTerrain.Structures != null)}";
        }

        /// <summary>
        /// Walks the player on a scripted loop instead of from the keyboard.
        ///
        /// Frame cost while moving is the number that matters and it was previously produced by a
        /// human driving a window, which is neither repeatable nor separable from the stationary
        /// measurement in the same log. This substitutes synthetic input at the top of the
        /// existing movement path — the same wish vector, the same motor step, the same streaming
        /// — rather than teleporting the transform, because a teleport helper skips exactly the
        /// per-frame work being measured.
        /// </summary>
        public bool AutoWalk { get; set; }

        /// <summary>
        /// Flies straight back from the landmark while keeping it centred, so every LOD ring
        /// boundary is crossed in view.
        ///
        /// Popping is a transition between rings, and a ring boundary is only crossed by changing
        /// distance to a fixed object. The circular walk keeps distance roughly constant and so
        /// cannot show it at all; only receding can. Distance is logged with the frame line so a
        /// visible pop can be matched to the ring cut that produced it.
        /// </summary>
        public bool AutoRecede { get; set; }

        public float RecedeSpeedMetresPerSecond { get; set; } = 8f;
        public float RecedeMaxDistanceMetres { get; set; } = 360f;

        /// <summary>Metres from the player to the landmark this world was built around.</summary>
        public float DistanceToLandmarkMetres
        {
            get
            {
                Vector3 landmark = LandmarkWorldPosition();
                Vector3 delta = transform.position - landmark;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        private Vector3 LandmarkWorldPosition()
        {
            int groundVoxels = _world.SurfaceHeight(
                ShowcaseWorld.LandmarkCentreX, ShowcaseWorld.LandmarkCentreZ);
            return new Vector3(ShowcaseWorld.LandmarkCentreX * 0.1f,
                               groundVoxels * 0.1f,
                               ShowcaseWorld.LandmarkCentreZ * 0.1f);
        }

        /// <summary>
        /// Backs away from the landmark at a fixed rate, aimed at it throughout.
        ///
        /// This flies rather than walks. Walking backwards over hundreds of metres of unsculpted
        /// terrain ends up climbing hillsides and looking at the ground, which is a test of the
        /// character motor rather than of the renderer.
        /// </summary>
        /// <summary>
        /// Stands on top of the landmark and turns slowly, looking down at the surrounding
        /// ground.
        ///
        /// Holes in terrain are reported from up here and are invisible from the ground, because
        /// at eye level the near ground occludes everything a missing chunk would expose. The
        /// vantage has to be high, aimed down, and turning, or the defect cannot be captured.
        /// </summary>
        public bool AutoSurvey { get; set; }

        public float SurveyHeightMetres { get; set; } = 55f;
        public float SurveyPitchDegrees { get; set; } = 28f;

        /// <summary>
        /// Degrees a second the survey turns. Zero holds a heading, which separates "this chunk
        /// has not been built yet" from "this chunk cannot be built": a turning camera
        /// continuously brings unbuilt ground into view, so a standing backlog under rotation is
        /// not the same defect as one that persists while still.
        /// </summary>
        public float SurveySpinDegreesPerSecond { get; set; } = 30f;

        private void StepAutoSurvey()
        {
            Vector3 landmark = LandmarkWorldPosition();
            transform.position = new Vector3(landmark.x,
                                             landmark.y + SurveyHeightMetres,
                                             landmark.z);
            _yaw += SurveySpinDegreesPerSecond * Time.deltaTime;
            _pitch = SurveyPitchDegrees;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
            _motor.Velocity = Vector3.zero;
        }

        private void StepAutoRecede()
        {
            Vector3 landmark = LandmarkWorldPosition();
            Vector3 away = transform.position - landmark;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-3f) away = Vector3.back;
            float distance = away.magnitude;

            if (distance < RecedeMaxDistanceMetres)
            {
                Vector3 direction = away / distance;
                float travelled = RecedeSpeedMetresPerSecond * Time.deltaTime;
                // Rise with distance so the castle stays in frame rather than sinking behind the
                // terrain between here and it.
                float height = landmark.y + 12f + distance * 0.16f;
                Vector3 next = landmark + direction * (distance + travelled);
                transform.position = new Vector3(next.x, height, next.z);
            }

            Vector3 toLandmark = landmark - transform.position;
            if (toLandmark.sqrMagnitude > 1e-4f)
            {
                Quaternion look = Quaternion.LookRotation(toLandmark.normalized, Vector3.up);
                Vector3 euler = look.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
            _motor.Velocity = Vector3.zero;
        }

        private float _autoWalkElapsed;

        private void HandleLook()
        {
            _yaw += Input.GetAxisRaw("Mouse X") * m_LookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * m_LookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        /// <summary>
        /// Turns steadily while walking forward, so the path is a wide circle. Straight-line
        /// travel leaves the small map; a circle keeps streaming and surface extraction working
        /// continuously without the run degenerating into a teleport back to the middle.
        /// </summary>
        private void StepAutoWalk()
        {
            const float DegreesPerSecond = 24f;
            _autoWalkElapsed += Time.deltaTime;
            _yaw += DegreesPerSecond * Time.deltaTime;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void MovePlayer()
        {
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            if (AutoSurvey)
            {
                StepAutoSurvey();
                return;
            }

            if (AutoRecede)
            {
                StepAutoRecede();
                return;
            }

            if (AutoWalk)
            {
                StepAutoWalk();
                forward = 1f;
                strafe = 0f;
            }

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
            _relockFrames = 0;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>
        /// Reacquires mouse capture after focus comes back, which the editor otherwise never does.
        ///
        /// Losing focus releases the mouse — alt-tab, a click into the Inspector, any dialog —
        /// but leaves <see cref="Cursor.lockState"/> reading <c>Locked</c>. So the state this
        /// component believes in and the state the editor is actually enforcing disagree, and
        /// because assigning a property its current value does nothing, no amount of setting
        /// <c>Locked</c> on the way back re-captures anything. The result is a live pointer over
        /// a character that will not turn.
        ///
        /// The transition is what the editor acts on, so this drives one: release on the frame
        /// focus returns, capture on the next. Both halves have to be real assignments, and they
        /// have to land in different frames — collapsed into one frame the editor coalesces them
        /// back into the no-op this exists to avoid.
        /// </summary>
        private void SyncCursorLock()
        {
            bool focused = Application.isFocused;
            bool regained = focused && !_hadFocus;
            _hadFocus = focused;

            // `_mouseLook` is the intent, so a cursor deliberately released with Escape stays
            // released, and an unfocused editor never has the mouse snatched back out from under
            // whatever window the developer is actually working in.
            if (regained && _mouseLook) _relockFrames = 2;
            if (_relockFrames == 0 || !focused || !_mouseLook) return;

            if (--_relockFrames > 0)
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // The pointer was free for a frame and travelled. Left in the axes, that distance
            // arrives as one enormous delta and snaps the view the instant capture resumes.
            Input.ResetInputAxes();
        }

        // -- input ---------------------------------------------------------------

        private void HandleKeys()
        {
            SyncCursorLock();

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
