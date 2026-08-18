using System;
using System.Collections.Generic;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.Materials;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Tiering.Api;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// First player-facing integration of the generated Kentridge world and authored opening campaign.
    /// The pub and town are one continuous voxel world: once the opening cutscene releases control,
    /// the player walks through the generated pub doorway directly into generated Kentridge.
    /// </summary>
    [AddComponentMenu("Game/Kentridge Playable Slice")]
    public sealed class KentridgePlayableSlice : MonoBehaviour
    {
        private const float DecimetresToMetres = 0.1f;

        [Header("World")]
        [SerializeField] private uint m_Seed = 0x4B454E54u;
        [SerializeField] private int m_BrickPoolCapacity = 262144;

        [Header("Streaming")]
        [SerializeField] private int m_LoadRadiusRegions = 3;
        [SerializeField] private int m_UnloadRadiusRegions = 4;
        [SerializeField] private float m_GenerateBudgetMs = 3f;

        [Header("Player")]
        [SerializeField] private float m_WalkSpeed = 5.5f;
        [SerializeField] private float m_LookSensitivity = 2.5f;

        private ShowcaseWorld _world;
        private CharacterMotor _motor;
        private ActorHost _actors;
        private SlicePresentation _presentation;
        private KentridgeCampaignSession _session;
        private KentridgeGameplaySiteAccess _pubAccess;
        private bool _spawned;
        private bool _hasExitedPub;
        private bool _mouseLook = true;
        private float _yaw;
        private float _pitch;

        public bool GameplayControlEnabled => _session != null && !_session.Runtime.HasActiveCutscene;
        public bool HasExitedPub => _hasExitedPub;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity,
                tierBytes);

            FeatureCatalogue catalogue = default(FeatureCatalogue);
            try
            {
                KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                    DialogueOnly("destination-conversation"));
                SettlementPlan settlement = KentridgeDefinition.Build(m_Seed);
                KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                    content.Blueprint,
                    settlement);

                if (!KentridgeGameplaySiteAccessResolver.TryResolve(
                        settlement,
                        (int)KentridgeRole.Pub,
                        1,
                        out _pubAccess))
                    throw new InvalidOperationException(
                        "Generated Kentridge pub did not expose a physical public entrance.");

                _world = new ShowcaseWorld(
                    m_Seed,
                    capacity,
                    m_LoadRadiusRegions,
                    m_UnloadRadiusRegions,
                    GameMaterialComposition.SimulationDefinitions(),
                    tierBytes);
                catalogue = KentridgeCombinedVoxelCatalogue.Build(
                    settlement,
                    BuildSettings(),
                    generation.HiddenSpaces,
                    Allocator.Persistent);
                _world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default(FeatureCatalogue); // ownership transferred to ShowcaseWorld

                _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };
                _actors = new ActorHost(_motor);
                _presentation = new SlicePresentation();
                _session = KentridgeCampaignSessionBootstrap.CreateSession(
                    content.Blueprint,
                    generation,
                    new KentridgeVoxelSiteRealizationFacts(settlement, 1),
                    _actors,
                    _presentation);

                RenderingComposition.ResetSurfacePassDiagnostics("kentridge-playable-slice-enabled");
                RenderingComposition.SetSurfaceBuildEnabled(false);
                RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
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
                    farFieldEnabled: false);

                CutsceneStageBinding openingStage = FindOpeningStage(content.IntroCutscene);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
                GenerateAt(_pubAccess.Entrance);
                GenerateAt(_pubAccess.InteriorApproach);
                GenerateAt(_pubAccess.ExteriorApproach);
                RenderingComposition.SetSurfaceBuildEnabled(true);

                int matched = _session.StartNewGame();
                if (matched == 0 || !_session.Runtime.HasActiveCutscene)
                    throw new InvalidOperationException(
                        "New Game did not start the authored Kentridge opening cutscene.");

                ApplyPlayerCameraFacing();
                transform.position = _motor.EyePosition;
                _spawned = true;
                SetCursorLocked(true);
            }
            catch
            {
                if (catalogue.IsCreated) catalogue.Dispose();
                DisposeRuntime();
                throw;
            }
        }

        private void OnDisable()
        {
            DisposeRuntime();
            if (Application.isPlaying) SetCursorLocked(false);
        }

        private void DisposeRuntime()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _actors?.Dispose();
            _actors = null;
            _session = null;
            _presentation = null;
            _world?.Dispose();
            _world = null;
            _motor = null;
            _spawned = false;
            _hasExitedPub = false;
        }

        private void Update()
        {
            if (!Application.isPlaying || !_spawned || _world == null || _session == null) return;

            float dt = Time.deltaTime;
            _actors.Tick(dt);
            _session.Runtime.Tick(Mathf.Max(0, Mathf.RoundToInt(dt * 1000f)));

            if (_session.Runtime.HasActiveCutscene)
            {
                ApplyPlayerCameraFacing();
            }
            else
            {
                HandleKeys();
                if (_mouseLook) HandleLook();
                MovePlayer(dt);
                UpdateExitedPub();
            }

            transform.position = _motor.EyePosition;
            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(!_mouseLook);
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

        private void MovePlayer(float dt)
        {
            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(_motor.Position))) return;

            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f)
                          - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            float strafe = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            bool sprint = Input.GetKey(KeyCode.LeftShift);

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 wish = flatForward * forward + flatRight * strafe;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            _motor.Step(_world, wish, sprint, Input.GetKey(KeyCode.Space), dt);
        }

        private void UpdateExitedPub()
        {
            Vector3 entrance = ToMetres(_pubAccess.Entrance);
            Vector3 inward = new Vector3(_pubAccess.Inward.X, 0f, _pubAccess.Inward.Y);
            float signedDepth = Vector3.Dot(_motor.Position - entrance, inward);
            if (signedDepth <= -0.75f) _hasExitedPub = true;
        }

        private void ApplyPlayerCameraFacing()
        {
            Vector3 facing = _actors.Player.Facing;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector3.forward;
            facing.Normalize();
            transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            Vector3 euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private CutsceneStageBinding FindOpeningStage(CutsceneRef intro)
        {
            for (int i = 0; i < _session.World.CutsceneStages.Count; i++)
            {
                CutsceneStageRealization stage = _session.World.CutsceneStages[i];
                if (stage.Cutscene.Equals(intro)) return stage.Binding;
            }
            throw new InvalidOperationException("Kentridge opening cutscene has no realized stage.");
        }

        private void GenerateAt(CutsceneInt3 point) =>
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(ToMetres(point)));

        private void GenerateAt(RealizedWorldPoint point) =>
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(ToMetres(point)));

        private void SetCursorLocked(bool locked)
        {
            _mouseLook = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !_spawned || _session == null) return;

            string state = _session.Runtime.HasActiveCutscene
                ? "Opening cutscene"
                : _hasExitedPub
                    ? "Kentridge town"
                    : "Player control — walk out through the pub door";
            GUI.Box(new Rect(16f, 16f, 420f, 82f), state);
            GUI.Label(new Rect(30f, 44f, 390f, 24f),
                _session.Runtime.HasActiveCutscene
                    ? _presentation.LastCue
                    : "WASD move • mouse look • Shift sprint • Space jump");
        }

        private static Vector3 ToMetres(CutsceneInt3 point) =>
            new Vector3(
                point.X * DecimetresToMetres,
                point.Y * DecimetresToMetres,
                point.Z * DecimetresToMetres);

        private static Vector3 ToMetres(RealizedWorldPoint point)
        {
            float scale = DecimetresToMetres / point.UnitsPerDecimetre;
            return new Vector3(
                point.Position.X * scale,
                point.Position.Y * scale,
                point.Position.Z * scale);
        }

        private static CutsceneInt3 ToCutscene(Vector3 metres) =>
            new CutsceneInt3(
                Mathf.RoundToInt(metres.x / DecimetresToMetres),
                Mathf.RoundToInt(metres.y / DecimetresToMetres),
                Mathf.RoundToInt(metres.z / DecimetresToMetres));

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 1,
                darkMasonry: 6,
                timber: 2,
                glass: 4,
                warmWindow: 15,
                roofTile: 8,
                slate: 7,
                cloth: 9,
                moss: 14,
                water: 11,
                roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });

        private sealed class PlayerActor : ICutsceneActorRuntime
        {
            private readonly CharacterMotor _motor;
            private PlayerMoveOperation _move;

            public Vector3 Facing { get; private set; } = Vector3.forward;
            public CutsceneInt3 Position => ToCutscene(_motor.Position);

            public PlayerActor(CharacterMotor motor) =>
                _motor = motor ?? throw new ArgumentNullException(nameof(motor));

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _move = null;
                _motor.Position = ToMetres(destination.Position);
                _motor.Velocity = Vector3.zero;
                SetFacing(destination.Forward);
            }

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                _move = new PlayerMoveOperation(
                    this,
                    _motor.Position,
                    ToMetres(destination.Position),
                    durationHintMilliseconds * 0.001f);
                return _move;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition)
            {
                Vector3 direction = ToMetres(targetPosition) - _motor.Position;
                if (direction.sqrMagnitude > 1e-6f) Facing = direction.normalized;
                return CompletedCutsceneOperation.Instance;
            }

            public void Tick(float dt)
            {
                _move?.Tick(dt);
                if (_move != null && _move.IsComplete) _move = null;
            }

            private void SetFacing(CutsceneInt3 facing)
            {
                Vector3 direction = new Vector3(facing.X, facing.Y, facing.Z);
                if (direction.sqrMagnitude > 1e-6f) Facing = direction.normalized;
            }

            private sealed class PlayerMoveOperation : ICutsceneOperation
            {
                private readonly PlayerActor _actor;
                private readonly Vector3 _start;
                private readonly Vector3 _destination;
                private readonly float _duration;
                private float _elapsed;

                public bool IsComplete { get; private set; }

                public PlayerMoveOperation(
                    PlayerActor actor,
                    Vector3 start,
                    Vector3 destination,
                    float duration)
                {
                    _actor = actor;
                    _start = start;
                    _destination = destination;
                    _duration = Mathf.Max(0f, duration);
                    if (_duration == 0f)
                    {
                        _actor._motor.Position = _destination;
                        IsComplete = true;
                    }
                }

                public void Tick(float dt)
                {
                    if (IsComplete) return;
                    _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, dt));
                    float t = _duration <= 0f ? 1f : _elapsed / _duration;
                    _actor._motor.Position = Vector3.Lerp(_start, _destination, t);
                    _actor._motor.Velocity = Vector3.zero;
                    if (_elapsed >= _duration) IsComplete = true;
                }
            }
        }

        private sealed class NpcActor : ICutsceneActorRuntime
        {
            private readonly GameObject _root;
            private CutsceneInt3 _position;

            public CutsceneInt3 Position => _position;

            public NpcActor(string name, CutsceneInt3 position)
            {
                _root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _root.name = name;
                _root.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                SetPosition(position);
            }

            public void PlaceAt(CutsceneStagePoint destination) => SetPosition(destination.Position);

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                SetPosition(destination.Position);
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition)
            {
                Vector3 direction = ToMetres(targetPosition) - ToMetres(_position);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                    _root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return CompletedCutsceneOperation.Instance;
            }

            public void Dispose() => UnityEngine.Object.Destroy(_root);

            private void SetPosition(CutsceneInt3 position)
            {
                _position = position;
                _root.transform.position = ToMetres(position) + Vector3.up * 0.9f;
            }
        }

        private sealed class ActorHost : IKentridgeCampaignActorHost, IDisposable
        {
            private readonly Dictionary<NpcRef, NpcActor> _npcs = new Dictionary<NpcRef, NpcActor>();
            public PlayerActor Player { get; }

            public ActorHost(CharacterMotor motor) => Player = new PlayerActor(motor);

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                foreach (NpcActor actor in _npcs.Values) actor.Dispose();
                _npcs.Clear();
                for (int i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    Int3 point = placement.Position.Position;
                    _npcs.Add(
                        placement.Npc,
                        new NpcActor(
                            placement.Npc.ToString(),
                            new CutsceneInt3(point.X, point.Y, point.Z)));
                }
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                NpcActor value;
                bool found = _npcs.TryGetValue(npc, out value);
                actor = value;
                return found;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = playerSlot == 0 ? Player : null;
                return actor != null;
            }

            public void Tick(float dt) => Player.Tick(dt);

            public void Dispose()
            {
                foreach (NpcActor actor in _npcs.Values) actor.Dispose();
                _npcs.Clear();
            }
        }

        private sealed class SlicePresentation : ICutscenePresentation
        {
            public string LastCue { get; private set; } = string.Empty;

            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue)
            {
                LastCue = cameraCue.Value;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue)
            {
                LastCue = dialogueCue.Value;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue)
            {
                LastCue = soundCue.Value;
                return CompletedCutsceneOperation.Instance;
            }
        }
    }
}
