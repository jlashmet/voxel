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
    public sealed class WorldbuildingGalleryShowcase : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private uint m_Seed = 0x5EED1234u;
        [SerializeField] private int m_BrickPoolCapacity = 262144;
        [SerializeField] private int m_LoadRadiusRegions = 8;
        [SerializeField] private int m_UnloadRadiusRegions = 11;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

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

            int tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity,
                tierBytes);

            _world = new ShowcaseWorld(
                m_Seed,
                capacity,
                m_LoadRadiusRegions,
                m_UnloadRadiusRegions,
                GameMaterialComposition.SimulationDefinitions(),
                tierBytes);
            _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };

            RenderingComposition.ResetSurfacePassDiagnostics("worldbuilding-gallery-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);

            float streamedMetres = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
            _farTerrain = VoxelFarTerrain.Create(
                transform,
                m_Seed,
                streamedMetres * 0.85f,
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

            _world.GenerateWorldbuildingGalleryBlocking(_worldObjects);
            _world.GenerateWorldbuildingGalleryTourExpansionBlocking();
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

        private void MovePlayer()
        {
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) -
                            (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f) -
                           (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);

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
