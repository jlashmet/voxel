using System;
using System.Collections.Generic;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Hightown;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Characters.Runtime;
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
    public sealed class KentridgePlayableSlice : MonoBehaviour, IShowcaseMeasurementDriver
    {
        private const float DecimetresToMetres = 0.1f;

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
        private CharacterMotor _motor;
        private ActorHost _actors;
        private SlicePresentation _presentation;
        private KentridgeCampaignSession _session;
        private KentridgeGameplaySiteAccess _pubAccess;
        private RegionThemeMap _themes;
        private RegionCorridorPlan _corridorPlan;
        private VoxelFarTerrain _farTerrain;
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

        public bool GameplayControlEnabled =>
            _session != null && _openingStarted && !_session.Runtime.HasActiveCutscene;
        public bool HasExitedPub => _hasExitedPub;
        public bool OpeningCutsceneStarted => _openingStarted;
        public bool OpeningPresentationReady => _openingPresentationReady;
        public bool OpeningCutsceneCameraActive => _openingCutsceneCameraActive;
        public Vector3 OpeningCutsceneCameraFocus => _openingCameraFocus;
        public bool TravelObjectiveActive =>
            _session != null && _session.Runtime.IsObjectiveActive(_travelObjective);
        public bool TravelObjectiveCompleted =>
            _session != null && _session.Runtime.IsObjectiveCompleted(_travelObjective);
        public bool DestinationCutsceneActive =>
            _session != null
            && _session.Runtime.HasActiveCutscene
            && _session.Runtime.ActiveCutscene.Equals(_destinationCutscene);

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
                // Both towns, in one world. Kentridge carries the campaign — its authored buildings,
                // NPCs, hidden spaces and the opening cutscene — while Hightown is generated from
                // its own plan and theme. They are separate settlements sharing one coordinate
                // space, so the only thing that has to be joined is the catalogue the world takes.
                FeatureCatalogue kentridgeCatalogue = KentridgeCombinedVoxelCatalogue.Build(
                    settlement,
                    BuildSettings(kentridge: true),
                    generation.HiddenSpaces,
                    Allocator.Temp);
                FeatureCatalogue hightownCatalogue = m_RealizeHightownBuildings
                    ? HightownVoxelCatalogue.Build(
                        hightown, BuildSettings(kentridge: false), Allocator.Temp)
                    : default(FeatureCatalogue);

                // The country between them: the road joining the two towns, the river crossing it,
                // and the bridge carrying the road over the water. Generated together so the
                // crossing point is chosen once and all three agree on it.
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
                catalogue = default(FeatureCatalogue); // ownership transferred to ShowcaseWorld

                // The country between the towns is themed, and the theme is what decides what
                // grows and lives there. Both the forest band and the river crossing are derived
                // from the same corridor plan, so the pine forest is the wood the bridge stands in
                // rather than a separate scattering that happens to be nearby.
                RegionCorridorPlan corridorPlan = RegionCorridorCatalogue.Plan(
                    m_Seed, BuildSettings(kentridge: true),
                    settlement.CentreDm, hightown.CentreDm);
                _themes = RegionThemeMap.ForKentridgeHightown(
                    settlement.CentreDm.Y, hightown.CentreDm.Y, corridorPlan.CrossingZDm);
                _corridorPlan = corridorPlan;

                _motor = new CharacterMotor { WalkSpeed = m_WalkSpeed };
                _actors = new ActorHost(_motor);
                _presentation = new SlicePresentation(ApplyCutsceneCamera);
                _session = KentridgeCampaignSessionBootstrap.CreateSession(
                    content.Blueprint,
                    generation,
                    new KentridgeVoxelSiteRealizationFacts(settlement, 1),
                    _actors,
                    _presentation);

                RenderingComposition.ResetSurfacePassDiagnostics("kentridge-playable-slice-enabled");
                RenderingComposition.SetSurfaceBuildEnabled(false);
                RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);

                // Cap the LOD rings at what this scene actually streams. Left at the 409.6 m
                // default the renderer wants chunks for ground three times further out than any
                // region will ever exist for, so several hundred chunks are permanently "missing",
                // extraction never converges, and the build budget stays scaled up for visible
                // incompleteness — which is why the slice ran at 23 fps and never settled.
                RenderingComposition.SetVoxelRingRadiusMetres(
                    m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);

                // Set explicitly rather than inherited: this is a static on the scheduler, so a
                // scene that leaves it alone runs with whatever the previously loaded scene chose.
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

                CutsceneStageBinding openingStage = FindOpeningStage(content.IntroCutscene);
                PrepareOpeningCamera(openingStage);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                GenerateAt(openingStage.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
                GenerateAt(_pubAccess.Entrance);
                GenerateAt(_pubAccess.InteriorApproach);
                GenerateAt(_pubAccess.ExteriorApproach);
                RenderingComposition.SetSurfaceBuildEnabled(true);

                // Dress the country once the world is populated: both vegetation and wildlife read
                // the built surface to decide where not to go, so this has to follow generation.
                _lifeHost = new GameObject("Kentridge Region Life");
                _life = _lifeHost.AddComponent<KentridgeRegionLife>();
                _life.Populate(
                    _world,
                    _themes,
                    _corridorPlan.RoadXDm * DecimetresToMetres,
                    (settlement.CentreDm.Y + 700) * DecimetresToMetres,
                    (hightown.CentreDm.Y - 700) * DecimetresToMetres,
                    halfWidthMetres: 90f);

                // The semantic world and actor host are ready here, but surface
                // extraction/publication is asynchronous. Do not burn the story clock against a
                // bare terrain plane: park the player and stage camera, then start New Game only
                // after the renderer says the visible near set has actually been published.
                Vector3 openingLeadStart = ToMetres(
                    openingStage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                _motor.Position = openingLeadStart;
                _motor.Velocity = Vector3.zero;
                _actors.Player.SetCutsceneBodyVisible(false);
                ApplyOpeningCameraPose();

                _spawned = true;
                _cutsceneOwnedControl = true;
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
            if (_lifeHost != null) Destroy(_lifeHost);
            _lifeHost = null;
            _life = null;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            if (_farTerrain != null)
            {
                _farTerrain.Structures = null;
                Destroy(_farTerrain.gameObject);
                _farTerrain = null;
            }

            _actors?.Dispose();
            _actors = null;
            _session = null;
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
        }

        private void Update()
        {
            if (!Application.isPlaying || !_spawned || _world == null || _session == null) return;

            float dt = Time.deltaTime;
            _actors.Tick(dt);

            // New Game is deliberately presentation-gated. Storage generation is synchronous,
            // surface extraction/publication is not; the recovered opening must not begin until
            // the generated pub is actually drawable from its establishing camera.
            if (!_openingStarted)
            {
                TickOpeningPreload();
                return;
            }

            _session.Runtime.Tick(Mathf.Max(0, Mathf.RoundToInt(dt * 1000f)));

            bool hasActiveCutscene = _session.Runtime.HasActiveCutscene;
            if (_cutsceneOwnedControl
                && !hasActiveCutscene
                && !_openingGameplayReleased
                && _session.Runtime.IsCutsceneCompleted(_introCutscene))
            {
                // Only the opening owns a special gameplay spawn. Later cutscenes must return
                // control in-place; reusing this handoff for every cutscene teleports the player
                // all the way back inside the pub after a destination conversation.
                ReleasePlayerForGameplay();
                _openingGameplayReleased = true;
            }
            _cutsceneOwnedControl = hasActiveCutscene;

            // Scripted measurement modes deliberately take camera control even if the authored
            // opening has not finished. A standalone capture can spend tens of wall-clock seconds
            // streaming its first frame; making it wait for the whole cutscene meant the harness
            // logged "survey on" while every screenshot still came from the pub staging camera.
            // These flags are command-line opt-ins, so normal play still gives the cutscene first
            // refusal exactly as before.
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
                // Walking is the only mode that exercises streaming and collision from eye level,
                // which is where ground texturing is actually judged. It also has to release the
                // player from the cutscene first: the opening holds control indefinitely waiting
                // on a dialogue click that an unattended capture will never provide.
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
                    // An E interaction can start a cutscene in this very frame. Transfer control
                    // immediately instead of allowing one extra frame of player look/movement.
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

            // The opening's establishing camera is a stage camera, not the player's eyes. The
            // original game held that shot on the pub group while Weldon and then Logan entered
            // the frame. Once the opening releases, normal first-person follow resumes.
            if (!_openingCutsceneCameraActive)
                transform.position = _motor.EyePosition;

            // Spend far more of the frame on generation while the player does not have control.
            // The steady-state budget is sized so walking never stutters, but during the opening
            // cutscene nobody is walking — and at 3 ms a frame the roughly nine seconds of region
            // work this world needs takes the better part of a minute of wall clock, which is the
            // whole of the "takes forever to load" complaint. Once control is handed over the
            // budget drops back to the walking figure.
            float budget = hasActiveCutscene
                ? m_LoadingGenerateBudgetMs
                : m_GenerateBudgetMs;
            _world.StepStreaming(_motor.EyePosition, budget);
            if (_farTerrain != null)
            {
                float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
                _farTerrain.HoleRadiusMetres = Mathf.Max(
                    _world.ResidentGroundRadiusMetres(_motor.EyePosition), streamed);
            }
        }

        private void TickOpeningPreload()
        {
            ApplyOpeningCameraPose();

            // Residency follows Weldon's authored entrance mark, not the detached stage camera.
            _world.StepStreaming(_motor.EyePosition, m_LoadingGenerateBudgetMs);
            if (_farTerrain != null)
            {
                float streamed = m_LoadRadiusRegions * ShowcaseWorld.RegionMetres;
                _farTerrain.HoleRadiusMetres = Mathf.Max(
                    _world.ResidentGroundRadiusMetres(_motor.EyePosition), streamed);
            }

            if (!RenderingComposition.HasCompletePublishedNearSurfaceCoverage()) return;

            _openingPresentationReady = true;
            int matched = _session.StartNewGame();
            if (matched == 0 || !_session.Runtime.HasActiveCutscene)
                throw new InvalidOperationException(
                    "New Game did not start the authored Kentridge opening cutscene after pub presentation became ready.");

            _openingStarted = true;
            _cutsceneOwnedControl = true;
        }
        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(!_mouseLook);
            if (Input.GetKeyDown(KeyCode.F10)) RescuePlayerToY100();
            if (Input.GetKeyDown(KeyCode.E)) TryInteractWithNearbyNpc();
        }

        /// <summary>
        /// Performs the same semantic NPC interaction used by the player-facing E key. Eligibility
        /// comes from the campaign blueprint's RequiresConversation flag; distance comes from the
        /// authoritative spawned actor position; story/objective consequences stay in CampaignRuntime.
        /// </summary>
        public bool TryInteractWithNearbyNpc()
        {
            if (_session == null || _actors == null || _motor == null) return false;
            if (_session.Runtime.HasActiveCutscene) return false;
            if (!TryFindNearbyConversationNpc(out NpcRef npc, out _)) return false;

            _session.Runtime.InteractWithNpc(npc);
            if (_session.Runtime.HasActiveCutscene)
                _cutsceneOwnedControl = true;
            return true;
        }

        /// <summary>
        /// Exposes the currently realized destination actor position for navigation/objective-marker
        /// consumers without leaking the actor GameObject or making authored coordinates authoritative.
        /// </summary>
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

            // Blueprint order is deterministic and is the semantic source of which NPCs are
            // conversational. The actor host contributes only current physical positions.
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
            // Cutscene marks are performance positions, not gameplay-safe spawns. Hand control back
            // at the architecture-owned interior doorway approach instead. Snap that authored point
            // to the composed voxel floor so the full capsule is safe, then face the public exit.
            // The player must cross the generated doorway under normal CharacterMotor collision;
            // composition is not allowed to teleport across the world/gameplay boundary it claims
            // to integrate.
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

        /// <summary>
        /// Emergency player-facing escape hatch for malformed or partially streamed geometry.
        /// It intentionally preserves X/Z and sets the feet, rather than the camera, to world
        /// Y=100 so the character can steer while falling back toward the world.
        /// </summary>
        public void RescuePlayerToY100()
        {
            if (_motor == null) return;
            Vector3 position = _motor.Position;
            position.y = 100f;
            _motor.Position = position;
            _motor.Velocity = Vector3.zero;
            transform.position = _motor.EyePosition;
        }

        /// <summary>
        /// Draws the line currently being spoken and advances it on a click.
        ///
        /// Placed at the foot of the screen and sized to the text, so a long line is readable
        /// rather than clipped. The prompt is explicit because nothing else on screen tells the
        /// player that the scene is waiting on them rather than stalled.
        /// </summary>
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

        /// <summary>
        /// Advances dialogue. Checked before cutscene camera handling so a click during the scene
        /// is a reply rather than being swallowed by the locked cutscene camera.
        /// </summary>
        private bool TryAdvanceDialogue()
        {
            DialogueLine pending = _presentation?.Pending;
            if (pending == null) return false;

            bool clicked = Input.GetMouseButtonDown(0)
                        || Input.GetKeyDown(KeyCode.Space)
                        || Input.GetKeyDown(KeyCode.Return);

            // Waiting for a reader is correct for a player and fatal for an unattended capture:
            // nobody clicks, so the scene stalls on its first line and the world behind it is
            // never photographed. The flag exists only so an automated run can read at a fixed
            // pace; without it the line waits indefinitely, as it should.
            bool timedOut = s_AutoAdvanceSeconds > 0f
                         && Time.realtimeSinceStartup - pending.ShownAt >= s_AutoAdvanceSeconds;

            if (!clicked && !timedOut) return false;

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

        // -- scripted measurement camera -----------------------------------------
        //
        // The slice had no way to be driven without a person at the keyboard, so a player build of
        // it could not be photographed or checked. These are the same scripted modes the showcase
        // scenes expose; the landmark is Kentridge's town centre, so receding from it flies the
        // corridor toward Hightown and crosses the bridge on the way.

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
            // SurfaceHeight returns zero until the requested region is resident. At Hightown's
            // low plots that happened to be harmless; at Kentridge's high civic terrace it put
            // the survey camera beneath the world and produced an all-blue, upside-down capture.
            // The authored vertical profile is deterministic and available before streaming, so
            // it is the correct initial camera contract. Once resident, rendering still comes
            // entirely from the real world and its extracted surface.
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
                // Each inspection starts from the authored public frontage. Letting one global
                // orbit continue across role changes put the camera behind plots — and, in the
                // dense market, directly through neighbouring roofs — so the resulting real-player
                // screenshots could not actually be used to judge the requested facade.
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
                    // StepAutoSurvey places yaw zero south of the target. Frontage values are
                    // quarter-turns from the grammar's authored south-facing facade.
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

        /// <summary>
        /// Flies north up the corridor at a height that keeps the road, the river and the bridge
        /// in frame — the route a player would walk between the towns.
        /// </summary>
        /// <summary>
        /// Ends the opening so a scripted walk can start, without the dialogue beat it would
        /// otherwise block on. Nothing is skipped that a player would see: an unattended capture
        /// has nobody to click through the lines, so waiting is an indefinite stall rather than a
        /// pause. Only the command-line walk modes reach this.
        /// </summary>
        private void ReleaseForScriptedWalk()
        {
            _presentation?.DismissPending();
            _session.Runtime.Tick(0);
            if (_session.Runtime.HasActiveCutscene) return;

            if (!_openingGameplayReleased)
            {
                ReleasePlayerForGameplay();
                _openingGameplayReleased = true;
            }
            _cutsceneOwnedControl = false;
        }

        /// <summary>
        /// Walks a slow arc through synthetic input rather than moving the transform.
        ///
        /// Driving the motor is the point: a teleport skips collision, ground resolution and the
        /// streaming that a walk is being measured for, so a capture taken from one proves nothing
        /// about whether the world is walkable. The steady turn keeps new ground entering frame so
        /// a stall in extraction shows up as terrain arriving late rather than never being asked
        /// for.
        /// </summary>
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

        private void PrepareOpeningCamera(CutsceneStageBinding stage)
        {
            Vector3 leadStart = ToMetres(stage.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
            Vector3 leadStage = ToMetres(stage.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
            Vector3 madeline = ToMetres(stage.Resolve(KentridgeOpeningCutscene.MadelineStage).Position);
            Vector3 steven = ToMetres(stage.Resolve(KentridgeOpeningCutscene.StevenStage).Position);

            // Match the first game's staging rather than the gameplay camera: hold on the pub
            // conversation area while Weldon walks into it. A modest horizontal offset keeps the
            // entrance path readable; most of the separation is vertical so the result is the
            // overhead ensemble shot the original 2D camera naturally provided.
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
            float height = Mathf.Clamp(4.2f + groupRadius * 0.35f, 4.2f, 5.5f);
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

            if (!_openingStarted)
            {
                DrawOpeningLoadingCover();
                return;
            }

            // Automated review frames are evidence of the world, not of the debug HUD. Normal
            // players never set these modes and retain the authored dialogue and control prompt.
            if (AutoSurvey || AutoRecede) return;

            DrawDialogue();

            string state = _session.Runtime.HasActiveCutscene
                ? DestinationCutsceneActive ? "Destination conversation" : "Opening cutscene"
                : _hasExitedPub
                    ? "Kentridge town"
                    : "Player control — walk out through the pub door";
            GUI.Box(new Rect(16f, 16f, 420f, 82f), state);
            GUI.Label(new Rect(30f, 44f, 390f, 24f),
                _session.Runtime.HasActiveCutscene
                    ? _presentation.LastCue
                    : "WASD move • mouse look • E interact • Shift sprint • Space jump");

            if (!_session.Runtime.HasActiveCutscene)
            {
                if (TryFindNearbyConversationNpc(out NpcRef nearbyNpc, out _))
                    GUI.Label(new Rect(30f, 70f, 390f, 24f),
                              "E talk to " + nearbyNpc.ToString());

                if (GUI.Button(new Rect(16f, 106f, 235f, 34f),
                               "Rescue: move to Y = 100 m"))
                    RescuePlayerToY100();
                GUI.Label(new Rect(264f, 112f, 360f, 24f),
                          "F10 anytime • Esc releases the cursor");
            }
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

        private static CutsceneInt3 ToCutscene(Vector3 metres) =>
            new CutsceneInt3(
                Mathf.RoundToInt(metres.x / DecimetresToMetres),
                Mathf.RoundToInt(metres.y / DecimetresToMetres),
                Mathf.RoundToInt(metres.z / DecimetresToMetres));

        private static VoxelWorldGenSettings BuildSettings(bool kentridge)
        {
            // Kentridge's small warm masonry and Hightown's cool ashlar are separate material
            // projections of the same semantic roles. Previously both towns mapped Masonry to the
            // same neutral stone byte, so their authored themes differed on paper and collapsed to
            // one grey town in the player.
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

        private sealed class PlayerActor : ICutsceneActorRuntime
        {
            private readonly CharacterMotor _motor;
            private readonly GameObject _root;
            private readonly CharacterAnimationPolicy _animation;
            private PlayerMoveOperation _move;

            public Vector3 Facing { get; private set; } = Vector3.forward;
            public CutsceneInt3 Position => ToCutscene(_motor.Position);

            public PlayerActor(CharacterMotor motor)
            {
                _motor = motor ?? throw new ArgumentNullException(nameof(motor));
                _root = CutsceneCast.CreateBody("Weldon");
                _animation = _root.GetComponentInChildren<CharacterAnimationPolicy>();
                _root.SetActive(false);
            }

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _move = null;
                SetPosition(ToMetres(destination.Position));
                _motor.Velocity = Vector3.zero;
                SetFacing(destination.Forward);
                SetCutsceneBodyVisible(true);
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
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
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Facing = direction.normalized;
                    ApplyBodyFacing();
                }
                return CompletedCutsceneOperation.Instance;
            }

            public void Tick(float dt)
            {
                _move?.Tick(dt);
                if (_move != null && _move.IsComplete) _move = null;
                _animation?.Tick();
            }

            public void SetCutsceneBodyVisible(bool visible)
            {
                if (_root == null) return;
                _root.SetActive(visible);
                if (visible)
                {
                    _root.transform.position = _motor.Position;
                    ApplyBodyFacing();
                }
            }

            public void Dispose()
            {
                if (_root != null) UnityEngine.Object.Destroy(_root);
            }

            private void SetPosition(Vector3 position)
            {
                _motor.Position = position;
                _motor.Velocity = Vector3.zero;
                if (_root != null) _root.transform.position = position;
            }

            private void SetFacing(CutsceneInt3 facing)
            {
                Vector3 direction = new Vector3(facing.X, facing.Y, facing.Z);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    Facing = direction.normalized;
                    ApplyBodyFacing();
                }
            }

            private void ApplyBodyFacing()
            {
                if (_root == null || Facing.sqrMagnitude < 1e-6f) return;
                Vector3 flat = new Vector3(Facing.x, 0f, Facing.z).normalized;
                if (flat.sqrMagnitude > 1e-6f)
                    _root.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
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

                    Vector3 direction = _destination - _start;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 1e-6f)
                    {
                        _actor.Facing = direction.normalized;
                        _actor.ApplyBodyFacing();
                    }

                    if (_duration == 0f)
                    {
                        _actor.SetPosition(_destination);
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                    else
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Walk);
                    }
                }

                public void Tick(float dt)
                {
                    if (IsComplete) return;
                    _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, dt));
                    float t = _duration <= 0f ? 1f : _elapsed / _duration;
                    _actor.SetPosition(Vector3.Lerp(_start, _destination, t));
                    if (_elapsed >= _duration)
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                }
            }
        }

        private sealed class NpcActor : ICutsceneActorRuntime
        {
            private readonly GameObject _root;
            private CutsceneInt3 _position;
            private CharacterAnimationPolicy _animation;
            private NpcMoveOperation _move;

            public CutsceneInt3 Position => _position;

            public NpcActor(string name, CutsceneInt3 position)
            {
                _root = CutsceneCast.CreateBody(name);
                SetPosition(position);
                _animation = _root.GetComponentInChildren<CharacterAnimationPolicy>();
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
            }

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _move = null;
                SetPosition(destination.Position);
                SetFacing(destination.Forward);
                _animation?.SetLocomotion(CharacterLocomotionState.Idle);
            }

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                _move = new NpcMoveOperation(
                    this,
                    ToMetres(_position),
                    ToMetres(destination.Position),
                    durationHintMilliseconds * 0.001f);
                return _move;
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
                // Humanoid models are authored with their origin at the feet, unlike the capsule
                // that stood here before, which was centred.
                _root.transform.position = ToMetres(position);
            }

            private void SetPosition(Vector3 position)
            {
                _position = ToCutscene(position);
                _root.transform.position = position;
            }

            private void SetFacing(CutsceneInt3 facing)
            {
                Vector3 direction = new Vector3(facing.X, facing.Y, facing.Z);
                direction.y = 0f;
                if (direction.sqrMagnitude > 1e-6f)
                    _root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            public void Tick(float dt)
            {
                _move?.Tick(dt);
                if (_move != null && _move.IsComplete) _move = null;
                _animation?.Tick();
            }

            private sealed class NpcMoveOperation : ICutsceneOperation
            {
                private readonly NpcActor _actor;
                private readonly Vector3 _start;
                private readonly Vector3 _destination;
                private readonly float _duration;
                private float _elapsed;

                public bool IsComplete { get; private set; }

                public NpcMoveOperation(
                    NpcActor actor,
                    Vector3 start,
                    Vector3 destination,
                    float duration)
                {
                    _actor = actor;
                    _start = start;
                    _destination = destination;
                    _duration = Mathf.Max(0f, duration);

                    Vector3 direction = _destination - _start;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 1e-6f)
                        _actor._root.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

                    if (_duration == 0f)
                    {
                        _actor.SetPosition(_destination);
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                    else
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Walk);
                    }
                }

                public void Tick(float dt)
                {
                    if (IsComplete) return;
                    _elapsed = Mathf.Min(_duration, _elapsed + Mathf.Max(0f, dt));
                    float t = _duration <= 0f ? 1f : _elapsed / _duration;
                    _actor.SetPosition(Vector3.Lerp(_start, _destination, t));
                    if (_elapsed >= _duration)
                    {
                        _actor._animation?.SetLocomotion(CharacterLocomotionState.Idle);
                        IsComplete = true;
                    }
                }
            }
        }

        /// <summary>
        /// Bodies for cutscene actors.
        ///
        /// The opening was staged with capsules, which is enough to prove blocking works and
        /// nothing else: an actor that turns to face another actor reads as a shape rotating.
        /// The placeholder humanoids already carry a rig, an idle and a walk, so the choreography
        /// that was already correct now has something legible performing it.
        /// </summary>
        private static class CutsceneCast
        {
            private const string MalePrefab = "Characters/placeholder_male";
            private const string FemalePrefab = "Characters/placeholder_female";

            public static GameObject CreateBody(string name)
            {
                // Alternate the two available bodies so a group scene is not four copies of one
                // person. Keyed on the name so a given character is always the same body.
                string path = (Hash(name) & 1u) == 0u ? MalePrefab : FemalePrefab;
                var prefab = Resources.Load<GameObject>(path);

                if (prefab == null)
                {
                    // Better a visible shape than an invisible actor: a missing prefab would
                    // otherwise stage the scene with nobody in it.
                    Debug.LogError("Cutscene body prefab missing at Resources/" + path);
                    GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    fallback.name = name;
                    fallback.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                    return fallback;
                }

                GameObject body = UnityEngine.Object.Instantiate(prefab);
                body.name = name;
                return body;
            }

            private static uint Hash(string value)
            {
                uint h = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    h ^= value[i];
                    h *= 16777619u;
                }
                return h;
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

            public bool TryGetNpcPosition(NpcRef npc, out Vector3 position)
            {
                if (_npcs.TryGetValue(npc, out NpcActor actor))
                {
                    position = ToMetres(actor.Position);
                    return true;
                }

                position = default;
                return false;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = playerSlot == 0 ? Player : null;
                return actor != null;
            }

            public void Tick(float dt)
            {
                Player.Tick(dt);
                // Animated bodies need their policy advanced or they hold a single pose.
                foreach (NpcActor actor in _npcs.Values) actor.Tick(dt);
            }

            public void Dispose()
            {
                Player.Dispose();
                foreach (NpcActor actor in _npcs.Values) actor.Dispose();
                _npcs.Clear();
            }
        }

        /// <summary>
        /// One line of dialogue, waiting to be read.
        ///
        /// The cutscene runner advances when the operation it was handed reports complete, so a
        /// line that returns <see cref="CompletedCutsceneOperation"/> is over on the frame it
        /// started — which is why the opening played silently straight through. Holding the
        /// operation open until the player dismisses it is what makes dialogue a beat in the scene
        /// rather than an instant no-op.
        /// </summary>
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

            /// <summary>Realtime seconds since the line appeared, for unattended runs.</summary>
            public float ShownAt { get; set; }
        }

        private sealed class SlicePresentation : ICutscenePresentation
        {
            private readonly Action<CutsceneCueId> _camera;

            public string LastCue { get; private set; } = string.Empty;

            /// <summary>The line currently on screen, or null when nobody is speaking.</summary>
            public DialogueLine Pending { get; private set; }

            public SlicePresentation(Action<CutsceneCueId> camera) =>
                _camera = camera ?? throw new ArgumentNullException(nameof(camera));

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
                return CompletedCutsceneOperation.Instance;
            }
        }
    }
}
