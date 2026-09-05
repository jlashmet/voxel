using System;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Playable;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Input.Api;
using Game.Progression.Api;
using Game.SessionOrchestration.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Hightown;
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
    /// Player-facing Kentridge world/presentation realization. Application owns input and session
    /// lifecycle; this component supplies Kentridge content/session-graph composition and consumes
    /// those public production capabilities after the application starts a run.
    /// </summary>
    [AddComponentMenu("Game/Kentridge Playable Slice")]
    [DefaultExecutionOrder(-15000)]
    public sealed class KentridgePlayableSlice : MonoBehaviour, IShowcaseMeasurementDriver
    {
        private const float DecimetresToMetres = 0.1f;
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);

        [Header("World")]
        [SerializeField] private uint m_Seed = 0x4B454E54u;

        [Tooltip("Emit Hightown's buildings as voxels. On by default: the slice intentionally " +
                 "exercises two generated settlements in one world. Disable only when isolating " +
                 "the corridor, road and themed country between Kentridge and Hightown.")]
        [SerializeField] private bool m_RealizeHightownBuildings = true;
        [SerializeField] private int m_BrickPoolCapacity = 262144;

        [Header("Streaming")]
        [SerializeField] private int m_LoadRadiusRegions = 3;
        [SerializeField] private int m_UnloadRadiusRegions = 4;
        [SerializeField] private float m_GenerateBudgetMs = 3f;

        [Tooltip("Milliseconds per frame spent generating while the opening cutscene holds " +
                 "control. Loading is the only thing happening then, so it may have the frame.")]
        [SerializeField] private float m_LoadingGenerateBudgetMs = 24f;

        [Tooltip("Scales the LOD hand-over distances. 1 keeps the finest step out to 96 m; lower " +
                 "draws far fewer chunks and meshes more of the mid distance at half resolution. " +
                 "This is the only lever that materially reduces draw submission.")]
        [SerializeField] private float m_DetailBandScale = 0.6f;

        [Header("Player")]
        [SerializeField] private float m_WalkSpeed = 5.5f;
        [SerializeField] private float m_LookSensitivity = 2.5f;
        [SerializeField] private float m_InteractionRangeMetres = 2.5f;

        private ShowcaseWorld _world;
        private KentridgeCharacterHost _motor;
        private KentridgeCharacterHost _actors;
        private SlicePresentation _presentation;
        private KentridgeCampaignSession _session;
        private IGameSessionControl _sessionControl;
        private KentridgeSessionRuntimeGraphFactory _sessionFactory;
        private KentridgeSessionRuntimeGraph _sessionGraph;
        private IPlayerInputReader _inputReader;
        private IInputActionStateReader _inputActions;
        private KentridgeGameplayHudInstaller _hudInstaller;
        private KentridgeGameplaySiteAccess _pubAccess;
        private RegionThemeMap _themes;
        private RegionCorridorPlan _corridorPlan;
        private VoxelFarTerrain _farTerrain;
        private KentridgeFarFeatureRuntime _farFeatures;
        private Camera _sceneCamera;
        private SettlementPlan _kentridgePlan;
        private SettlementPlan _hightownPlan;
        private KentridgeRegionLife _life;
        private GameObject _lifeHost;
        private ObjectiveRef _travelObjective;
        private NpcRef _destinationNpc;
        private CutsceneRef _introCutscene;
        private CutsceneRef _destinationCutscene;
        private bool _spawned;
        private bool _hasExitedPub;
        private bool _cutsceneOwnedControl;
        private bool _openingGameplayReleased;
        private bool _openingStarted;
        private bool _openingPresentationReady;
        private bool _openingCameraReady;
        private bool _openingCutsceneCameraActive;
        private Vector3 _openingCameraPosition;
        private Quaternion _openingCameraRotation;
        private Vector3 _openingCameraFocus;
        private bool _mouseLook = true;
        private float _yaw;
        private float _pitch;
        private float _surveyCycleStartedAt = -1f;
        private int _loggedSurveyRole = -1;

        internal KentridgeSessionRuntimeGraphFactory SessionFactory => _sessionFactory;
        internal KentridgeSessionRuntimeGraph SessionGraph => _sessionGraph;
        internal KentridgeCampaignSession CampaignSession => _session;
        public bool ProductionInputBound => _inputReader != null && _inputActions != null;
        public bool SessionControlBound => _sessionControl != null;
        public GameSessionLifecycle SessionLifecycle =>
            _sessionControl == null ? GameSessionLifecycle.Uninitialized : _sessionControl.Snapshot.Lifecycle;
        public bool GameplayControlEnabled =>
            _sessionControl != null
            && _sessionControl.Snapshot.Lifecycle == GameSessionLifecycle.Running
            && _session != null
            && _openingStarted
            && !_session.Runtime.HasActiveCutscene;
        public bool HasExitedPub => _hasExitedPub;
        public bool OpeningCutsceneStarted => _openingStarted;
        public bool OpeningPresentationReady => _openingPresentationReady;
        public bool OpeningCutsceneCameraActive => _openingCutsceneCameraActive;
        public Vector3 OpeningCutsceneCameraFocus => _openingCameraFocus;
        public float InteractionRangeMetres => m_InteractionRangeMetres;
        internal IProgressionQuery ProgressionQuery => _session?.Runtime.Progression;
        internal string TravelObjectiveId => _travelObjective.ToString();
        public bool TravelObjectiveActive =>
            _session != null && _session.Runtime.IsObjectiveActive(_travelObjective);
        public bool TravelObjectiveCompleted =>
            _session != null && _session.Runtime.IsObjectiveCompleted(_travelObjective);
        public bool DestinationCutsceneActive =>
            _session != null
            && _session.Runtime.HasActiveCutscene
            && _session.Runtime.ActiveCutscene.Equals(_destinationCutscene);

        public void BindProductionInput(
            IPlayerInputReader inputReader,
            IInputActionStateReader inputActions)
        {
            if (_spawned)
                throw new InvalidOperationException(
                    "Kentridge production input must be bound before world/session composition.");
            _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
            _inputActions = inputActions ?? throw new ArgumentNullException(nameof(inputActions));
        }

        public void BindSessionControl(IGameSessionControl sessionControl)
        {
            if (!_spawned || _sessionFactory == null)
                throw new InvalidOperationException(
                    "Kentridge session control cannot bind before the world/content factory is ready.");
            if (_sessionControl != null && !ReferenceEquals(_sessionControl, sessionControl))
                throw new InvalidOperationException("Kentridge session control is already bound.");
            _sessionControl = sessionControl ?? throw new ArgumentNullException(nameof(sessionControl));
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!ProductionInputBound)
                throw new InvalidOperationException(
                    "Kentridge playable slice requires Application-owned production input before OnEnable.");

            long tierBytes = DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity;
            int capacity = VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget(
                m_BrickPoolCapacity,
                tierBytes);

            FeatureCatalogue catalogue = default(FeatureCatalogue);
            try
            {
                var destinationSpeaker = new CutsceneActorId("destination-npc");
                KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                    DialogueOnly("destination-conversation", destinationSpeaker),
                    (scene, roles) => scene.Bind(destinationSpeaker, roles.DestinationNpc));
                _travelObjective = content.TravelObjective;
                _destinationNpc = content.DestinationNpc;
                _introCutscene = content.IntroCutscene;
                _destinationCutscene = content.DestinationCutscene;

                SettlementPlan settlement = KentridgeDefinition.Build(m_Seed);
                SettlementPlan hightown = HightownDefinition.Build(m_Seed);
                _kentridgePlan = settlement;
                _hightownPlan = hightown;
                KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                    content.Blueprint,
                    settlement);
                var realizationFacts = new KentridgeCampaignRealizationFacts(
                    new KentridgeVoxelSiteRealizationFacts(settlement, 1));
                KentridgeCampaignWorldRealization openingWorld =
                    KentridgeCampaignWorldRealizationBoundary.Realize(generation, realizationFacts);

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
                    tierBytes);
                FeatureCatalogue kentridgeCatalogue = KentridgeCombinedVoxelCatalogue.Build(
                    settlement,
                    BuildSettings(kentridge: true),
                    generation.HiddenSpaces,
                    Allocator.Temp);
                FeatureCatalogue hightownCatalogue = m_RealizeHightownBuildings
                    ? HightownVoxelCatalogue.Build(
                        hightown, BuildSettings(kentridge: false), Allocator.Temp)
                    : default(FeatureCatalogue);
                FeatureCatalogue corridorCatalogue = RegionCorridorCatalogue.Build(
                    m_Seed,
                    BuildSettings(kentridge: true),
                    settlement.CentreDm,
                    hightown.CentreDm,
                    Allocator.Temp);
                try
                {
                    catalogue = hightownCatalogue.IsCreated
                        ? SettlementCatalogueCombiner.Combine(
                            Allocator.Persistent,
                            kentridgeCatalogue, hightownCatalogue, corridorCatalogue)
                        : SettlementCatalogueCombiner.Combine(
                            Allocator.Persistent, kentridgeCatalogue, corridorCatalogue);
                }
                finally
                {
                    kentridgeCatalogue.Dispose();
                    if (hightownCatalogue.IsCreated) hightownCatalogue.Dispose();
                    corridorCatalogue.Dispose();
                }
                _world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default(FeatureCatalogue);

                _sceneCamera = GetComponent<Camera>();
                if (_sceneCamera == null) _sceneCamera = Camera.main;
                _farFeatures = new KentridgeFarFeatureRuntime(
                    transform,
                    _world.FarFeaturePresentation,
                    _world.FarFeaturePresentationCount,
                    ShowcaseWorld.VoxelSize,
                    _sceneCamera);

                RegionCorridorPlan corridorPlan = RegionCorridorCatalogue.Plan(
                    m_Seed, BuildSettings(kentridge: true),
                    settlement.CentreDm, hightown.CentreDm);
                _themes = RegionThemeMap.ForKentridgeHightown(
                    settlement.CentreDm.Y, hightown.CentreDm.Y, corridorPlan.CrossingZDm);
                _corridorPlan = corridorPlan;

                _motor = new KentridgeCharacterHost(m_WalkSpeed);
                _actors = _motor;
                KentridgeGameplayAudioIntegration audioPresentation =
                    GetComponent<KentridgeGameplayAudioIntegration>()
                    ?? gameObject.AddComponent<KentridgeGameplayAudioIntegration>();
                _presentation = new SlicePresentation(ApplyCutsceneCamera, audioPresentation);
                KentridgeForestBanditEncounter forestSessionExtension =
                    GetComponent<KentridgeForestBanditEncounter>()
                    ?? throw new InvalidOperationException(
                        "Kentridge scene is missing its explicit forest session extension composition.");
                if (!forestSessionExtension.ProductionInputBound)
                    throw new InvalidOperationException(
                        "Kentridge forest extension is missing Application-owned production input.");

                _hudInstaller = GetComponent<KentridgeGameplayHudInstaller>();
                if (_hudInstaller == null)
                    throw new InvalidOperationException(
                        "Kentridge scene is missing its explicit gameplay HUD composition.");
                if (!_hudInstaller.InputBound)
                    throw new InvalidOperationException(
                        "Kentridge HUD is missing Application-owned production input.");

                _sessionFactory = new KentridgeSessionRuntimeGraphFactory(
                    content.Blueprint,
                    generation,
                    realizationFacts,
                    _actors,
                    _presentation,
                    extensionFactory: forestSessionExtension);

                RenderingComposition.ResetSurfacePassDiagnostics("kentridge-playable-slice-enabled");
                RenderingComposition.SetSurfaceBuildEnabled(false);
                RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
                RenderingComposition.SetVoxelRingRadiusMetres(
                    m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
                RenderingComposition.SetVoxelDetailBandScale(m_DetailBandScale);
                float streamedMetres = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
                _farTerrain = VoxelFarTerrain.Create(
                    transform, m_Seed, streamedMetres, 12000f);
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

                CutsceneStageBinding openingStage = FindOpeningStage(openingWorld, content.IntroCutscene);
                PrepareOpeningCamera(openingStage);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
                GenerateAt(_pubAccess.Entrance);
                GenerateAt(_pubAccess.InteriorApproach);
                GenerateAt(_pubAccess.ExteriorApproach);
                RenderingComposition.SetSurfaceBuildEnabled(true);

                _lifeHost = new GameObject("Kentridge Region Life");
                _life = _lifeHost.AddComponent<KentridgeRegionLife>();
                _life.Populate(
                    _world,
                    _themes,
                    _corridorPlan.RoadXDm * DecimetresToMetres,
                    (settlement.CentreDm.Y + 700) * DecimetresToMetres,
                    (hightown.CentreDm.Y - 700) * DecimetresToMetres,
                    halfWidthMetres: 90f);

                Vector3 openingLeadStart = ToMetres(
                    openingStage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                _motor.Position = openingLeadStart;
                _motor.Velocity = Vector3.zero;
                _actors.Player.SetCutsceneBodyVisible(false);
                ApplyOpeningCameraPose();

                _spawned = true;
                _cutsceneOwnedControl = false;
                _openingGameplayReleased = false;
                _openingStarted = false;
                _openingPresentationReady = false;
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
            if (_sessionControl != null &&
                _sessionControl.Snapshot.Lifecycle != GameSessionLifecycle.Uninitialized &&
                _sessionControl.Snapshot.Lifecycle != GameSessionLifecycle.Stopped)
            {
                Debug.LogError(
                    "Kentridge world is disposing before Application completed ordered session teardown.");
            }

            _sessionGraph = null;
            _session = null;
            _sessionControl = null;
            _sessionFactory = null;

            if (_lifeHost != null) Destroy(_lifeHost);
            _lifeHost = null;
            _life = null;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _farFeatures?.Dispose();
            _farFeatures = null;
            _sceneCamera = null;

            if (_farTerrain != null)
            {
                _farTerrain.Structures = null;
                Destroy(_farTerrain.gameObject);
                _farTerrain = null;
            }

            _actors?.Dispose();
            _actors = null;
            _presentation = null;
            _world?.Dispose();
            _world = null;
            _kentridgePlan = null;
            _hightownPlan = null;
            _motor = null;
            _travelObjective = default;
            _destinationNpc = default;
            _introCutscene = default;
            _destinationCutscene = default;
            _spawned = false;
            _hasExitedPub = false;
            _cutsceneOwnedControl = false;
            _openingGameplayReleased = false;
            _openingStarted = false;
            _openingPresentationReady = false;
            _openingCameraReady = false;
            _openingCutsceneCameraActive = false;
            _openingCameraPosition = default;
            _openingCameraRotation = Quaternion.identity;
            _openingCameraFocus = default;
            _surveyCycleStartedAt = -1f;
            _loggedSurveyRole = -1;
            _hudInstaller = null;
        }

        private void Update()
        {
            if (!Application.isPlaying || !_spawned || _world == null) return;

            float dt = Time.deltaTime;
            _actors.Tick(dt);

            if (!_openingPresentationReady)
            {
                TickOpeningPreload();
                return;
            }

            SynchronizeSessionGraph();
            if (_sessionControl == null || _session == null || _sessionGraph == null)
            {
                StreamWorld(m_LoadingGenerateBudgetMs);
                return;
            }

            if (!_openingStarted)
            {
                if (_sessionControl.Snapshot.Lifecycle != GameSessionLifecycle.Running)
                {
                    StreamWorld(m_LoadingGenerateBudgetMs);
                    return;
                }
                if (_sessionGraph.LastNewGameMatchedCount == 0 || !_session.Runtime.HasActiveCutscene)
                    throw new InvalidOperationException(
                        "Application started Kentridge without the authored New Game opening cutscene.");
                _openingStarted = true;
                _cutsceneOwnedControl = true;
            }

            GameSessionOperationResult tick = _sessionControl.Tick(
                Mathf.Max(0, Mathf.RoundToInt(dt * 1000f)));
            if (!tick.Succeeded)
                throw new InvalidOperationException(
                    "Kentridge session update failed: " + tick.Failure + " " + tick.Diagnostic);

            bool hasActiveCutscene = _session.Runtime.HasActiveCutscene;
            if (_cutsceneOwnedControl
                && !hasActiveCutscene
                && !_openingGameplayReleased
                && _session.Runtime.IsCutsceneCompleted(_introCutscene))
            {
                ReleasePlayerForGameplay();
                _openingGameplayReleased = true;
            }
            _cutsceneOwnedControl = hasActiveCutscene;

            if (AutoSurvey)
            {
                StepAutoSurvey(dt);
            }
            else if (AutoRecede)
            {
                StepAutoRecede(dt);
            }
            else if (AutoWalk)
            {
                if (hasActiveCutscene) ReleaseForScriptedWalk();
                StepAutoWalk(dt);
                UpdateExitedPub();
            }
            else if (hasActiveCutscene)
            {
                TryAdvanceDialogue();
                if (!_openingCutsceneCameraActive) ApplyPlayerCameraFacing();
            }
            else
            {
                HandleKeys();
                if (_session.Runtime.HasActiveCutscene)
                {
                    _cutsceneOwnedControl = true;
                    if (!_openingCutsceneCameraActive) ApplyPlayerCameraFacing();
                }
                else
                {
                    if (_mouseLook) HandleLook();
                    MovePlayer(dt);
                    UpdateExitedPub();
                }
            }

            if (_openingCutsceneCameraActive)
                ApplyOpeningCameraPose();
            else
                transform.position = _motor.EyePosition;

            StreamWorld(hasActiveCutscene ? m_LoadingGenerateBudgetMs : m_GenerateBudgetMs);
        }

        private void TickOpeningPreload()
        {
            ApplyOpeningCameraPose();
            StreamWorld(m_LoadingGenerateBudgetMs);
            if (RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
                _openingPresentationReady = true;
        }

        private void StreamWorld(float budget)
        {
            _farFeatures?.Update(_sceneCamera, transform.position);
            _world.StepStreaming(_motor.EyePosition, budget);
            if (_farTerrain != null)
            {
                float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
                _farTerrain.HoleRadiusMetres = Mathf.Max(
                    _world.ResidentGroundRadiusMetres(_motor.EyePosition), streamed);
            }
        }

        private void SynchronizeSessionGraph()
        {
            KentridgeSessionRuntimeGraph current = _sessionFactory?.Current;
            if (ReferenceEquals(current, _sessionGraph)) return;
            _sessionGraph = current;
            _session = current?.Session;
            _openingStarted = false;
            _openingGameplayReleased = false;
            _cutsceneOwnedControl = false;
        }

        private void HandleKeys()
        {
            if (_inputActions.WasPressed(LocalPlayer, StandardInputActions.Cancel))
                SetCursorLocked(!_mouseLook);
            if (_inputActions.WasPressed(LocalPlayer, StandardInputActions.Interact))
                TryInteractWithNearbyNpc();
        }

        public bool TryInteractWithNearbyNpc()
        {
            if (_session == null || _sessionGraph == null || _actors == null || _motor == null) return false;
            if (_session.Runtime.HasActiveCutscene) return false;
            if (!TryFindNearbyConversationNpc(out NpcRef npc, out _)) return false;

            _sessionGraph.InteractWithNpc(npc);
            if (_session.Runtime.HasActiveCutscene)
                _cutsceneOwnedControl = true;
            return true;
        }

        public bool TryGetDestinationNpcWorldPosition(out Vector3 position)
        {
            if (_actors != null && _actors.TryGetNpcPosition(_destinationNpc, out position))
                return true;
            position = default;
            return false;
        }

        private bool TryFindNearbyConversationNpc(out NpcRef npc, out Vector3 position)
        {
            npc = default;
            position = default;
            if (_session == null || _actors == null || _motor == null) return false;

            float maxDistance = Mathf.Max(0f, m_InteractionRangeMetres);
            float bestDistanceSquared = maxDistance * maxDistance;
            bool found = false;

            for (int i = 0; i < _session.Blueprint.Npcs.Count; i++)
            {
                NpcSpec candidate = _session.Blueprint.Npcs[i];
                if (!candidate.RequiresConversation) continue;
                if (!_actors.TryGetNpcPosition(candidate.Ref, out Vector3 candidatePosition)) continue;

                float distanceSquared = (candidatePosition - _motor.Position).sqrMagnitude;
                if (distanceSquared > bestDistanceSquared) continue;

                bestDistanceSquared = distanceSquared;
                npc = candidate.Ref;
                position = candidatePosition;
                found = true;
            }

            return found;
        }

        private void ReleasePlayerForGameplay()
        {
            Vector3 interior = ToMetres(_pubAccess.InteriorApproach);
            Vector3 exterior = ToMetres(_pubAccess.ExteriorApproach);
            Vector3 facing = exterior - interior;
            facing.y = 0f;

            _openingCutsceneCameraActive = false;
            _actors?.Player.SetCutsceneBodyVisible(false);
            _motor.SnapToGround(_world, interior);
            _hasExitedPub = false;
            transform.position = _motor.EyePosition;
            if (facing.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                _yaw = transform.rotation.eulerAngles.y;
                _pitch = 0f;
            }
        }

        public void RescuePlayerToY100()
        {
            if (_motor == null) return;
            Vector3 position = _motor.Position;
            position.y = 100f;
            _motor.Position = position;
            _motor.Velocity = Vector3.zero;
            transform.position = _motor.EyePosition;
        }

        private void DrawDialogue()
        {
            DialogueLine line = _presentation?.Pending;
            if (line == null) return;

            const float margin = 40f;
            float width = Screen.width - margin * 2f;

            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true,
                padding = new RectOffset(18, 18, 12, 12),
            };
            float textHeight = body.CalcHeight(new GUIContent(line.Text), width - 36f);
            float height = textHeight + 74f;
            var box = new Rect(margin, Screen.height - height - margin, width, height);

            GUI.Box(box, GUIContent.none);

            var speaker = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(18, 18, 8, 0),
            };
            GUI.Label(new Rect(box.x, box.y, width, 26f), line.Speaker, speaker);
            GUI.Label(new Rect(box.x, box.y + 26f, width, textHeight), line.Text, body);

            var prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 22, 0, 8),
            };
            GUI.Label(new Rect(box.x, box.yMax - 26f, width, 20f),
                      "click to continue", prompt);
        }

        private bool TryAdvanceDialogue()
        {
            DialogueLine pending = _presentation?.Pending;
            if (pending == null) return false;

            PlayerInputSnapshot input = _inputReader.Read(LocalPlayer);
            bool advanced = input.PrimaryPressed || input.ConfirmPressed;
            bool timedOut = s_AutoAdvanceSeconds > 0f
                         && Time.realtimeSinceStartup - pending.ShownAt >= s_AutoAdvanceSeconds;

            if (!advanced && !timedOut) return false;

            _presentation.DismissPending();
            return true;
        }

        private static readonly float s_AutoAdvanceSeconds = ReadAutoAdvanceSeconds();
        private static readonly string s_SurveyTown = ReadArgument("-voxel-survey-town");
        private static readonly int s_SurveyRole = ReadSurveyRole();
        private static readonly float s_SurveyCycleSeconds = ReadPositiveFloatArgument(
            "-voxel-survey-cycle-seconds");

        private static float ReadAutoAdvanceSeconds()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-voxel-auto-dialogue"
                    && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture,
                                      out float seconds))
                    return seconds;
            return 0f;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        private static float ReadPositiveFloatArgument(string name)
        {
            string value = ReadArgument(name);
            return float.TryParse(
                       value,
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out float result)
                   && result > 0f
                ? result
                : 0f;
        }

        private static int ReadSurveyRole()
        {
            string value = ReadArgument("-voxel-survey-role");
            return int.TryParse(value, out int role) && role >= 0 && role <= 16 ? role : -1;
        }

        public bool AutoWalk { get; set; }
        public bool AutoSurvey { get; set; }
        public bool AutoRecede { get; set; }
        public float SurveyHeightMetres { get; set; } = 55f;
        public float SurveySpinDegreesPerSecond { get; set; } = 30f;
        public float SurveyPitchDegrees { get; set; } = 28f;
        public float RecedeSpeedMetresPerSecond { get; set; } = 8f;
        public float RecedeMaxDistanceMetres { get; set; } = 360f;

        public float DistanceToLandmarkMetres
        {
            get
            {
                Vector3 delta = transform.position - LandmarkWorldPosition();
                delta.y = 0f;
                return delta.magnitude;
            }
        }

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

        private Vector3 LandmarkWorldPosition()
        {
            SettlementPlan plan = string.Equals(
                s_SurveyTown, HightownDefinition.Id, StringComparison.OrdinalIgnoreCase)
                ? _hightownPlan
                : _kentridgePlan;

            Int2 point = plan != null ? plan.CentreDm : KentridgeDefinition.TownCentreDm;
            int authoredGround = VoxelEngine.Terrain.Api.TerrainQuery.HeightAt(
                point.X, point.Y, m_Seed);
            int surveyRole = CurrentSurveyRole();
            if (plan != null && surveyRole >= 0)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if (plot.RoleId != surveyRole) continue;
                    Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                    Int2 centre = RotatedFootprintCentre(footprint, plot.Frontage);
                    point = new Int2(
                        plot.PositionDm.X + centre.X,
                        plot.PositionDm.Y + centre.Y);
                    authoredGround = KentridgeVerticalProfile.PlotSurfaceY(
                        plan, plot, m_Seed, 1);
                    break;
                }
            }

            int x = point.X;
            int z = point.Y;
            return new Vector3(
                x * DecimetresToMetres,
                authoredGround * DecimetresToMetres,
                z * DecimetresToMetres);
        }

        private int CurrentSurveyRole()
        {
            if (s_SurveyRole < 0) return -1;
            if (s_SurveyCycleSeconds <= 0f || _surveyCycleStartedAt < 0f)
                return s_SurveyRole;

            int elapsedRoles = Mathf.FloorToInt(
                (Time.realtimeSinceStartup - _surveyCycleStartedAt) / s_SurveyCycleSeconds);
            return (s_SurveyRole + elapsedRoles) % 17;
        }

        private static Int2 RotatedFootprintCentre(
            Int3 footprint,
            FrontageDirection frontage)
        {
            int x = footprint.X / 2;
            int z = footprint.Z / 2;
            switch ((byte)frontage & 3)
            {
                case 1: return new Int2(footprint.Z - 1 - z, x);
                case 2: return new Int2(footprint.X - 1 - x, footprint.Z - 1 - z);
                case 3: return new Int2(z, footprint.X - 1 - x);
                default: return new Int2(x, z);
            }
        }

        private void StepAutoSurvey(float dt)
        {
            if (_surveyCycleStartedAt < 0f)
                _surveyCycleStartedAt = Time.realtimeSinceStartup;
            int surveyRole = CurrentSurveyRole();
            if (surveyRole != _loggedSurveyRole)
            {
                _loggedSurveyRole = surveyRole;
                _yaw = SurveyFrontageYawDegrees(surveyRole);
                Debug.Log($"SURVEYROLE town={s_SurveyTown ?? KentridgeDefinition.Id} "
                        + $"role={surveyRole} name={(KentridgeRole)surveyRole}");
            }

            Vector3 landmark = LandmarkWorldPosition();
            _yaw += SurveySpinDegreesPerSecond * dt;
            float radians = _yaw * Mathf.Deg2Rad;
            float radius = SurveyOrbitRadiusMetres();
            transform.position = landmark + new Vector3(
                Mathf.Sin(radians) * radius,
                SurveyHeightMetres,
                -Mathf.Cos(radians) * radius);
            Vector3 focus = landmark + Vector3.up * SurveyFocusHeightMetres();
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
            _motor.Velocity = Vector3.zero;
        }

        private float SurveyOrbitRadiusMetres()
        {
            SettlementPlan plan = string.Equals(
                s_SurveyTown, HightownDefinition.Id, StringComparison.OrdinalIgnoreCase)
                ? _hightownPlan
                : _kentridgePlan;
            int surveyRole = CurrentSurveyRole();
            if (plan != null && surveyRole >= 0)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if (plot.RoleId != surveyRole) continue;
                    Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                    float footprintRadius = Mathf.Max(footprint.X, footprint.Z)
                                          * DecimetresToMetres * 0.75f;
                    return Mathf.Max(16f, footprintRadius + 8f);
                }
            }

            return Mathf.Max(16f, SurveyHeightMetres * 1.35f);
        }

        private float SurveyFrontageYawDegrees(int surveyRole)
        {
            SettlementPlan plan = string.Equals(
                s_SurveyTown, HightownDefinition.Id, StringComparison.OrdinalIgnoreCase)
                ? _hightownPlan
                : _kentridgePlan;
            if (plan != null && surveyRole >= 0)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if (plot.RoleId != surveyRole) continue;
                    return -90f * ((byte)plot.Frontage & 3);
                }
            }

            return 0f;
        }

        private float SurveyFocusHeightMetres()
        {
            SettlementPlan plan = string.Equals(
                s_SurveyTown, HightownDefinition.Id, StringComparison.OrdinalIgnoreCase)
                ? _hightownPlan
                : _kentridgePlan;
            int surveyRole = CurrentSurveyRole();
            if (plan != null && surveyRole >= 0)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if (plot.RoleId != surveyRole) continue;
                    Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                    return Mathf.Clamp(
                        footprint.Y * DecimetresToMetres * 0.45f,
                        2.5f,
                        10f);
                }
            }

            return Mathf.Min(8f, SurveyHeightMetres * 0.35f);
        }

        private void ReleaseForScriptedWalk()
        {
            _presentation?.DismissPending();
            GameSessionOperationResult tick = _sessionControl.Tick(0);
            if (!tick.Succeeded)
                throw new InvalidOperationException(
                    "Kentridge scripted-walk session update failed: " + tick.Failure + " " + tick.Diagnostic);
            if (_session.Runtime.HasActiveCutscene) return;

            if (!_openingGameplayReleased)
            {
                ReleasePlayerForGameplay();
                _openingGameplayReleased = true;
            }
            _cutsceneOwnedControl = false;
        }

        private void StepAutoWalk(float dt)
        {
            const float DegreesPerSecond = 18f;

            _yaw += DegreesPerSecond * dt;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(_motor.Position))) return;

            Vector3 wish = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            _motor.Step(_world, wish, sprint: false, jumpHeld: false, dt);
        }

        private void StepAutoRecede(float dt)
        {
            Vector3 landmark = LandmarkWorldPosition();
            float travelled = RecedeSpeedMetresPerSecond * dt;
            Vector3 next = transform.position + Vector3.forward * travelled;

            int vx = (int)Mathf.Floor(next.x / 0.1f);
            int vz = (int)Mathf.Floor(next.z / 0.1f);
            float ground = _world != null ? _world.SurfaceHeight(vx, vz) * 0.1f : landmark.y;

            transform.position = new Vector3(next.x, ground + 26f, next.z);
            _pitch = 16f;
            _yaw = 0f;
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _motor.Position = transform.position - Vector3.up * _motor.EyeHeight;
            _motor.Velocity = Vector3.zero;
        }

        private void HandleLook()
        {
            PlayerInputSnapshot input = _inputReader.Read(LocalPlayer);
            _yaw += input.PointerX * m_LookSensitivity;
            _pitch = Mathf.Clamp(
                _pitch - input.PointerY * m_LookSensitivity,
                -89f,
                89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void MovePlayer(float dt)
        {
            if (!_world.IsGenerated(ShowcaseWorld.RegionAt(_motor.Position))) return;

            PlayerInputSnapshot input = _inputReader.Read(LocalPlayer);
            bool sprint = _inputActions.IsHeld(LocalPlayer, StandardInputActions.Sprint);

            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 wish = flatForward * input.MoveY + flatRight * input.MoveX;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool jumpHeld = _inputActions.IsHeld(LocalPlayer, StandardInputActions.Jump);
            _motor.Step(_world, wish, sprint, jumpHeld, dt);
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

        private void PrepareOpeningCamera(CutsceneStageBinding stage)
        {
            Vector3 leadStart = ToMetres(stage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
            Vector3 leadStage = ToMetres(stage.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
            Vector3 madeline = ToMetres(stage.Resolve(KentridgeOpeningCutscene.MadelineStage).Position);
            Vector3 steven = ToMetres(stage.Resolve(KentridgeOpeningCutscene.StevenStage).Position);

            Vector3 floorFocus = (leadStage + madeline + steven) / 3f;
            Vector3 approach = floorFocus - leadStart;
            approach.y = 0f;
            if (approach.sqrMagnitude < 1e-6f)
                approach = new Vector3(_pubAccess.Inward.X, 0f, _pubAccess.Inward.Y);
            if (approach.sqrMagnitude < 1e-6f) approach = Vector3.forward;
            approach.Normalize();

            float groupRadius = Mathf.Max(
                HorizontalDistance(floorFocus, leadStage),
                Mathf.Max(
                    HorizontalDistance(floorFocus, madeline),
                    HorizontalDistance(floorFocus, steven)));

            float groundFloorHeight =
                KentridgeDefinition.Theme.FloorHeightDm * DecimetresToMetres;
            float maximumInteriorHeight = groundFloorHeight - 0.6f;
            if (maximumInteriorHeight <= 2.2f)
                throw new InvalidOperationException(
                    "Generated Kentridge pub has insufficient ground-floor clearance for the opening camera.");
            float height = Mathf.Clamp(
                2.5f + groupRadius * 0.12f,
                2.2f,
                maximumInteriorHeight);
            float back = Mathf.Clamp(2.6f + groupRadius * 0.25f, 2.6f, 3.6f);

            _openingCameraFocus = floorFocus + Vector3.up * 0.9f;
            _openingCameraPosition = floorFocus - approach * back + Vector3.up * height;
            _openingCameraRotation = Quaternion.LookRotation(
                _openingCameraFocus - _openingCameraPosition,
                Vector3.up);
            _openingCameraReady = true;
        }

        private void ApplyOpeningCameraPose()
        {
            if (!_openingCameraReady) return;
            _openingCutsceneCameraActive = true;
            transform.position = _openingCameraPosition;
            transform.rotation = _openingCameraRotation;
        }

        private void ApplyCutsceneCamera(CutsceneCueId cue)
        {
            if (!_openingCameraReady
                || !string.Equals(
                    cue.Value,
                    KentridgeOpeningCutscene.EstablishingCamera.Value,
                    StringComparison.Ordinal))
                return;

            ApplyOpeningCameraPose();
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static CutsceneStageBinding FindOpeningStage(
            KentridgeCampaignWorldRealization world,
            CutsceneRef intro)
        {
            for (int i = 0; i < world.CutsceneStages.Count; i++)
            {
                CutsceneStageRealization stage = world.CutsceneStages[i];
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
            if (!Application.isPlaying || !_spawned) return;

            if (!_openingPresentationReady)
            {
                DrawOpeningLoadingCover();
                return;
            }

            if (_session == null || !_openingStarted || AutoSurvey || AutoRecede) return;
            DrawDialogue();
        }

        private static void DrawOpeningLoadingCover()
        {
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = previous;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            GUI.Label(
                new Rect(0f, Screen.height * 0.5f - 20f, Screen.width, 40f),
                "Loading Kentridge…",
                style);
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

        private static VoxelWorldGenSettings BuildSettings(bool kentridge)
        {
            var materials = new VoxelMaterialMap(
                foundationStone: kentridge ? (byte)20 : (byte)6,
                masonry: kentridge ? (byte)18 : (byte)1,
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

        private static CutsceneDefinition DialogueOnly(string id, CutsceneActorId speaker) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(speaker, new CutsceneCueId(id + ".dialogue")) });

        private sealed class DialogueLine : ICutsceneOperation
        {
            public string Speaker { get; }
            public string Text { get; }
            private bool _dismissed;

            public DialogueLine(string speaker, string text)
            {
                Speaker = speaker;
                Text = text;
            }

            public bool IsComplete => _dismissed;
            public void Dismiss() => _dismissed = true;
            public float ShownAt { get; set; }
        }

        private sealed class SlicePresentation : ICutscenePresentation
        {
            private readonly Action<CutsceneCueId> _camera;
            private readonly ICutsceneSoundCueRuntime _sound;

            public string LastCue { get; private set; } = string.Empty;
            public DialogueLine Pending { get; private set; }

            public SlicePresentation(
                Action<CutsceneCueId> camera,
                ICutsceneSoundCueRuntime sound)
            {
                _camera = camera ?? throw new ArgumentNullException(nameof(camera));
                _sound = sound ?? throw new ArgumentNullException(nameof(sound));
            }

            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue)
            {
                LastCue = cameraCue.Value;
                _camera(cameraCue);
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue)
            {
                LastCue = dialogueCue.Value;
                Pending = new DialogueLine(
                    SpeakerName(speaker), KentridgeOpeningScript.LineFor(dialogueCue))
                {
                    ShownAt = Time.realtimeSinceStartup,
                };
                return Pending;
            }

            public void DismissPending()
            {
                Pending?.Dismiss();
                Pending = null;
            }

            private static string SpeakerName(CutsceneActorId speaker)
            {
                string id = speaker.Value;
                if (string.IsNullOrEmpty(id)) return string.Empty;
                return char.ToUpperInvariant(id[0]) + id.Substring(1);
            }

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue)
            {
                LastCue = soundCue.Value;
                return _sound.Execute(soundCue)
                       ?? throw new InvalidOperationException(
                           "Kentridge sound runtime returned no operation for cue '" + soundCue + "'.");
            }
        }
    }
}
