using Game.Composition.Materials;
using Game.Composition.WorldObjects.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Collision.Api;
using VoxelEngine.Composition;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Lightweight play-mode driver for the curated worldbuilding gallery. It deliberately uses
    /// the production showcase world, renderer, streaming, collision motor, structure authorers,
    /// and world-object presentation runtime instead of Unity placeholder architecture.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("VoxelEngine/Worldbuilding Gallery Showcase")]
    public sealed class WorldbuildingGalleryShowcase : MonoBehaviour, IShowcaseMeasurementDriver
    {
        [Header("World")]
        [SerializeField] private uint m_Seed = 0x5EED1234u;
        [SerializeField] private int m_BrickPoolCapacity = 262144;
        [SerializeField] private int m_LoadRadiusRegions = 8;
        [SerializeField] private int m_UnloadRadiusRegions = 11;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

        [Tooltip("Scales the LOD hand-over distances. 1 keeps the finest step out to 96 m; lower "
               + "draws far fewer chunks and meshes more of the mid distance at half resolution. "
               + "This is the only lever that materially reduces draw submission.")]
        [SerializeField] private float m_DetailBandScale = 0.6f;

        [Tooltip("Bake restores the offline startup image. Generate authors the castle, exhibits, "
               + "promenade, cave and guild houses during the scene, which is what the bake exists "
               + "to avoid — use it only to produce a new bake.")]
        [SerializeField] private ShowcaseStartupSource m_Startup = ShowcaseStartupSource.Bake;

        [Header("Movement")]
        [SerializeField] private float m_WalkSpeed = 5.5f;
        [SerializeField] private float m_FlySpeed = 18f;
        [SerializeField] private float m_LookSensitivity = 2.5f;
        [SerializeField] private bool m_FlyMode;

        private ShowcaseWorld _world;
        private CharacterMotor _motor;
        private VoxelFarTerrain _farTerrain;
        private WorldObjectRuntimeComposition _worldObjects;
        private GameObject _worldObjectHost;
        private GalleryLifePopulation _life;
        private bool _mouseLook;
        private float _yaw;
        private float _pitch;
        private int _tourStopIndex = -1;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 15000f;
            if (GetComponent<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity,
                tierBytes);

            _world = new ShowcaseWorld(
                m_Seed,
                capacity,
                m_LoadRadiusRegions,
                m_UnloadRadiusRegions,
                GameMaterialComposition.SimulationDefinitions(),
                tierBytes,
                ShowcaseFeatureContent.Full,
                m_Startup);
            _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };

            RenderingComposition.ResetSurfacePassDiagnostics("worldbuilding-gallery-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);

            float streamedMetres = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;

            // The voxel LOD rings default to 409.6 m regardless of what the world streams. This
            // scene streams less than that, so without this the renderer spent every frame
            // considering thousands of chunks over ground no region will ever exist for: the
            // arena filled with geometry for terrain that was never coming, leases started
            // failing, and several hundred chunks the camera could actually see were left
            // undrawn. That is the hole in the terrain, and it is a renderer configuration
            // mistake rather than a streaming one.
            RenderingComposition.SetVoxelRingRadiusMetres(streamedMetres);

            // Set explicitly rather than inherited. This is a static on the scheduler, so a scene
            // that leaves it alone runs with whatever the previously loaded scene chose.
            RenderingComposition.SetVoxelDetailBandScale(m_DetailBandScale);

            // The far field begins where the voxel rings end, not inside them. At 0.85 the two
            // overlapped by nearly forty metres, which is only harmless if one of them is not
            // drawn — and both are.
            _farTerrain = VoxelFarTerrain.Create(
                transform,
                m_Seed,
                streamedMetres,
                12000f);
            _farTerrain.Structures = _world.FarField;

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld,
                _world.Changes,
                _world.Seed,
                farFieldEnabled: true);

            _worldObjectHost = new GameObject("Worldbuilding Gallery World Objects");
            _worldObjects = _worldObjectHost.AddComponent<WorldObjectRuntimeComposition>();

            _world.StartWorldbuildingGalleryBlocking(_worldObjects);

            // Scatter after the world is populated and before the player is placed. Both systems
            // read the built surface to decide where not to go, so running them against a world
            // that is still filling would put grass through the promenade.
            _life = _worldObjectHost.AddComponent<GalleryLifePopulation>();
            _life.Populate(_world, _world.WorldbuildingGalleryCentreMetres());

            Spawn();
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            _world?.StopBackgroundWork();

            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            if (_worldObjectHost != null)
                Destroy(_worldObjectHost);
            _worldObjectHost = null;
            _worldObjects = null;
            _life = null;

            if (_farTerrain != null)
                Destroy(_farTerrain.gameObject);
            _farTerrain = null;

            _world?.Dispose();
            _world = null;
            _motor = null;
            _tourStopIndex = -1;

            SetCursorLocked(false);
        }

        private void Spawn()
        {
            if (_world == null || _motor == null) return;

            float3 spawn = _world.WorldbuildingGallerySpawnPosition();
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));
            m_FlyMode = false;
            _motor.SnapToGround(_world, spawn);
            _motor.Velocity = Vector3.zero;
            transform.position = _motor.EyePosition;
            _tourStopIndex = -1;

            // The opening frame is the one every viewer sees, and it came up inside solid masonry.
            // Report what the placement actually resolved to, so a buried spawn is a number in the
            // log rather than something to reverse-engineer from a screenshot.
            int spawnX = (int)math.floor(spawn.x / ShowcaseWorld.VoxelSize);
            int spawnZ = (int)math.floor(spawn.z / ShowcaseWorld.VoxelSize);
            Debug.Log($"Gallery spawn: eye={transform.position} feet={_motor.Position} "
                    + $"terrain={_world.SurfaceHeight(spawnX, spawnZ)}v "
                    + $"occupied={_world.OccupiedSurfaceHeight(spawnX, spawnZ)}v "
                    + $"built={_world.HasBuiltContentAbove(spawnX, spawnZ)}");

            AimAt(_world.WorldbuildingGalleryLookTarget());

            RenderingComposition.SetLocalLights(
                _world.CastlePresentationLights,
                _world.CastlePresentationLightColours);
        }

        private void JumpToTourStop(int index)
        {
            if (_world == null || _motor == null) return;

            int count = _world.WorldbuildingGalleryTourStopCount;
            if (count <= 0) return;

            index %= count;
            if (index < 0) index += count;

            float3 spawn = _world.WorldbuildingGalleryTourSpawnPosition(index);
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(spawn));

            m_FlyMode = false;
            _motor.SnapToGround(_world, spawn);
            _motor.Velocity = Vector3.zero;
            transform.position = _motor.EyePosition;
            _tourStopIndex = index;

            AimAt(_world.WorldbuildingGalleryTourLookTarget(index));
        }

        private void CycleTour(int direction)
        {
            if (_world == null) return;

            int count = _world.WorldbuildingGalleryTourStopCount;
            if (count <= 0) return;

            int next = _tourStopIndex < 0
                ? (direction >= 0 ? 0 : count - 1)
                : _tourStopIndex + direction;
            JumpToTourStop(next);
        }

        private void AimAt(Vector3 target)
        {
            Vector3 toTarget = target - transform.position;
            _yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            _pitch = -Mathf.Atan2(
                toTarget.y,
                new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void Update()
        {
            if (!Application.isPlaying || _world == null || _motor == null) return;

            HandleKeys();
            if (_mouseLook) HandleLook();
            MovePlayer();

            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
            if (_farTerrain != null)
                _farTerrain.HoleRadiusMetres =
                    _world.ResidentGroundRadiusMetres(transform.position);
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                SetCursorLocked(!_mouseLook);

            if (Input.GetKeyDown(KeyCode.F))
            {
                m_FlyMode = !m_FlyMode;
                if (!m_FlyMode)
                {
                    _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
                    _motor.Velocity = Vector3.zero;
                }
            }

            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.G))
                Spawn();

            if (Input.GetKeyDown(KeyCode.T))
            {
                bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                CycleTour(reverse ? -1 : 1);
            }

            int tourHotkey = TourHotkeyIndex();
            if (tourHotkey >= 0 && tourHotkey < _world.WorldbuildingGalleryTourStopCount)
                JumpToTourStop(tourHotkey);

            if (Input.GetKeyDown(KeyCode.E))
            {
                // The original castle remains part of the gallery and keeps its real gate and
                // trapdoor interactions. Generated world-object scenes tick independently through
                // WorldObjectRuntimeComposition and expose their collider/light/particle state.
                if (!_world.TryOpenCastleFrontGate(_motor.Position))
                    _world.TryOpenCastleTrapdoor(_motor.Position);
            }
        }

        private static int TourHotkeyIndex()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha8)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha9)) return 8;
            return -1;
        }

        private void HandleLook()
        {
            _yaw += Input.GetAxisRaw("Mouse X") * m_LookSensitivity;
            _pitch = Mathf.Clamp(
                _pitch - Input.GetAxisRaw("Mouse Y") * m_LookSensitivity,
                -89f,
                89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // -- scripted measurement camera -----------------------------------------

        /// <inheritdoc />
        public bool AutoWalk { get; set; }

        /// <inheritdoc />
        public bool AutoSurvey { get; set; }

        /// <inheritdoc />
        public bool AutoRecede { get; set; }

        public float SurveyHeightMetres { get; set; } = 55f;
        public float SurveySpinDegreesPerSecond { get; set; } = 30f;
        public float SurveyPitchDegrees { get; set; } = 28f;
        public float RecedeSpeedMetresPerSecond { get; set; } = 8f;
        public float RecedeMaxDistanceMetres { get; set; } = 360f;

        /// <inheritdoc />
        public float DistanceToLandmarkMetres
        {
            get
            {
                Vector3 delta = transform.position - LandmarkWorldPosition();
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        /// <inheritdoc />
        public string DescribeFarTerrain()
        {
            if (_farTerrain == null || _world == null) return "FAR none";
            float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
            return $"FAR hole={_farTerrain.HoleRadiusMetres:0.#}m "
                 + $"inner={_farTerrain.InnerRadiusMetres:0.#}m streamed={streamed:0.#}m "
                 + $"residentGround={_world.ResidentGroundRadiusMetres(transform.position):0.#}m "
                 + $"coverage={RenderingComposition.HasCompletePublishedNearSurfaceCoverage()} "
                 + $"structures={(_farTerrain.Structures != null)}";
        }

        private Vector3 LandmarkWorldPosition()
        {
            float3 centre = _world.WorldbuildingGalleryCentreMetres();
            return new Vector3(centre.x, centre.y, centre.z);
        }

        /// <summary>
        /// Turns steadily while walking forward, so the path is a wide circle around the gallery
        /// rather than a straight line out of it. The exhibits span barely a hundred metres, so a
        /// straight walk leaves the authored district within seconds and spends the rest of the
        /// run measuring empty procedural terrain.
        /// </summary>
        private void StepAutoWalk()
        {
            const float DegreesPerSecond = 24f;
            _yaw += DegreesPerSecond * Time.deltaTime;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

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
                // Rise with distance so the district stays in frame rather than sinking behind
                // whatever terrain lies between here and it.
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

        private void MovePlayer()
        {
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) -
                            (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) -
                           (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            // Scripted modes substitute input at the top of the ordinary movement path rather than
            // driving the transform directly, so a measured run exercises the same motor step and
            // the same streaming work a played one does.
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
                Vector3 move = transform.forward * forward + transform.right * strafe;
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

            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(_motor.Position)))
            {
                transform.position = _motor.EyePosition;
                return;
            }

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 wish = flatForward * forward + flatRight * strafe;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            _motor.Step(
                _world,
                wish,
                sprint,
                Input.GetKey(KeyCode.Space),
                Time.deltaTime);
            transform.position = _motor.EyePosition;
        }

        private void SetCursorLocked(bool locked)
        {
            _mouseLook = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            const int width = 560;
            GUILayout.BeginArea(new Rect(18, 18, width, 205), GUI.skin.box);
            GUILayout.Label("WORLD BUILDING GALLERY");
            GUILayout.Label("Structures • Decorations • Interactables • Cave • Castle • Guild houses");
            GUILayout.Space(4);
            GUILayout.Label("WASD move   Shift sprint   Space jump   F fly   E interact");
            GUILayout.Label("1-9 jump to exhibit   T / Shift+T next/previous   G or R overview");
            GUILayout.Label("Fly: Space up / Ctrl down   Esc releases mouse");

            if (_world != null)
            {
                string stop = _tourStopIndex >= 0
                    ? $"{_tourStopIndex + 1}/{_world.WorldbuildingGalleryTourStopCount}  " +
                      _world.WorldbuildingGalleryTourStopName(_tourStopIndex)
                    : "Overview promenade";
                GUILayout.Label($"Tour: {stop}");
            }

            if (_worldObjects != null)
                GUILayout.Label($"World-object scenes: {_worldObjects.LoadedSceneCount}   presented: {_worldObjects.PresentedSceneCount}");
            if (_world != null)
                GUILayout.Label($"Resident regions: {_world.RegionsGenerated} generated   {_world.PendingRegionLoads} pending");
            GUILayout.EndArea();
        }
    }
}
