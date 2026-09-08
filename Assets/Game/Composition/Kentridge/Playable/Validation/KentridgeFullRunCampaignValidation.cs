using System;
using System.Collections.Generic;
using Game.Application.Api;
using Game.Application.Runtime;
using Game.Characters.Api;
using Game.Composition.Campaign.Content;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using Game.Input.Api;
using KentridgePlayableFullRunBootstrap = Game.Kentridge.PlayableSlice.KentridgePlayableFullRunBootstrap;
using Game.Outcomes.Api;
using Game.Persistence.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;

namespace Game.Composition.Kentridge.Playable.Validation
{
    /// <summary>
    /// Slow module-local built-player proof for System26. It enters through the shipped Kentridge
    /// full-run bootstrap, Application and SessionOrchestration, then drives only public campaign and
    /// Encounter semantic facts through the authored Rorik/Moordell/Rossdam/Logan route. No private
    /// progression state, test-only campaign graph or alternate outcome authority is used.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class KentridgeFullRunCampaignValidation : MonoBehaviour
    {
        private const uint Seed = 0x4B454E54u;
        private const string SuccessMarker = "KENTRIDGE_FULL_RUN_VALIDATION PASS";
        private string _status = "System26 authored full-run validation: starting";

        private void Awake()
        {
            EnsureValidationCamera();
        }

        private void Start()
        {
            try
            {
                RunValidation();
                _status = "PASS  Kentridge -> Rorik -> Moordell -> Rossdam -> Logan -> System15 -> frontend";
                Debug.Log(SuccessMarker);
            }
            catch (Exception exception)
            {
                _status = "FAIL  " + exception.Message;
                Debug.LogException(exception);
                throw;
            }
        }

        private static void RunValidation()
        {
            var destinationSpeaker = new CutsceneActorId("destination-npc");
            CutsceneDefinition destinationCutscene = new CutsceneDefinition(
                "destination-conversation",
                CutsceneStageSetupDefinition.Empty,
                new[]
                {
                    CutsceneStep.Dialogue(
                        destinationSpeaker,
                        new CutsceneCueId("destination-conversation.dialogue"))
                });
            SettlementPlan settlement = Game.Kentridge.PlayableSlice.KentridgeDefinition.Build(Seed);
            KentridgePlayableFullRunBootstrap bootstrap =
                KentridgePlayableFullRunBootstrap.Compose(
                    destinationCutscene,
                    destinationSpeaker,
                    Seed,
                    settlement,
                    voxelsPerDecimetre: 1);

            AuthoredFullRunCampaignContent content = bootstrap.Content;
            Require(
                bootstrap.Composition.PhysicalWorld.Graph.HierarchyPlan.Settlements.Count > 1,
                "Shipped player bootstrap did not consume a multi-settlement physical hierarchy.");
            Require(
                bootstrap.Composition.Generation.Sites.IsResolved,
                "Shipped player bootstrap did not resolve authored sites into physical world facts.");
            Require(
                ContainsNpc(bootstrap.Composition.World.Npcs, content.Rorik)
                && ContainsNpc(bootstrap.Composition.World.Npcs, content.MoordellContact)
                && ContainsNpc(bootstrap.Composition.World.Npcs, content.RossdamContact)
                && ContainsNpc(bootstrap.Composition.World.Npcs, content.KentridgeMayor),
                "Hierarchy-aware world realization omitted a continuation NPC.");
            Require(
                ContainsAllRequiredStages(
                    content.Blueprint.Cutscenes,
                    bootstrap.Composition.World.CutsceneStages),
                "Hierarchy-aware world realization omitted a cutscene stage required by authored choreography.");
            Debug.Log(
                "KENTRIDGE_FULL_RUN_MILESTONE physical-world-ready settlements="
                + bootstrap.Composition.PhysicalWorld.Graph.HierarchyPlan.Settlements.Count
                + " npcs=" + bootstrap.Composition.World.Npcs.Count);

            var actors = new ValidationActorHost();
            var presentation = new ImmediatePresentation();
            KentridgeSessionRuntimeGraphFactory factory =
                bootstrap.Composition.CreateSessionFactory(actors, presentation);
            var orchestrator = new GameSessionOrchestrator(factory);
            var app = new ApplicationFlowCoordinator(
                orchestrator,
                new EmptySaveCatalog(),
                new InertFormation(),
                new InertPartyPresentation(),
                new InertPartyIntent(),
                bootstrap.Composition.OutcomeQuery,
                new ValidationInputContexts(),
                new ValidationBindings(),
                new MemoryPreferences(),
                new ValidationAudio(),
                new ValidationExit(),
                new ValidationPlans());

            try
            {
                Require(app.CompleteBoot().Succeeded, "Application boot did not reach the frontend.");
                ApplicationOperationResult start = app.RequestNewGame(
                    new ApplicationSessionDescriptor(
                        "main-campaign",
                        Game.Kentridge.PlayableSlice.KentridgeDefinition.Id,
                        "system26-built-player-validation",
                        "kentridge-authored-full-run"));
                Require(start.Succeeded, "Application failed to start the authored full-run session: " + start.Detail);
                Require(
                    app.Snapshot.Lifecycle == ApplicationLifecycle.InGame
                    && app.Snapshot.GameplayReady,
                    "Application did not reach gameplay readiness through SessionOrchestration.");

                KentridgeSessionRuntimeGraph graph = factory.Current;
                Require(graph != null, "SessionOrchestration did not retain the Kentridge runtime graph.");
                KentridgeCampaignSession session = graph.Session;
                Require(session != null && session.IsFullRun, "Player bootstrap composed an opening-only campaign session.");
                CampaignRuntime runtime = session.Runtime;
                var encounters = new EncounterRegistry(new EmptyCharacters());

                CompleteCutscene(orchestrator, runtime, content.IntroCutscene, "new-game");
                LogMilestone("opening-intro-completed");

                graph.InteractWithNpc(content.OpeningRoles.Awon.Ref);
                CompleteActiveCutscene(orchestrator, runtime, "opening-intro-completed");
                LogMilestone("awon-opening-completed");

                Require(
                    runtime.EnterSite(content.OpeningRoles.MedrareSite.Ref) > 0,
                    "Medrare site entry did not advance the authored route.");
                CompleteActiveCutscene(orchestrator, runtime, "awon-opening-completed");
                LogMilestone("see-medrare-completed");

                graph.InteractWithNpc(content.OpeningRoles.Medrare.Ref);
                CompleteActiveCutscene(orchestrator, runtime, "see-medrare-completed");
                Require(runtime.IsPartyMemberJoined("Medrare"), "Medrare did not join through authored Story effects.");
                LogMilestone("medrare-joined");

                Require(
                    runtime.EnterSite(content.OpeningRoles.MedrareHouseSite.Ref) > 0,
                    "Medrare house entry did not advance the authored route.");
                CompleteActiveCutscene(orchestrator, runtime, "medrare-joined");
                Require(runtime.HasSpell("Flame"), "First-spell consequence was not present.");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.MedrareToChurchCutscene,
                    "medrare-first-spell-completed");
                Require(runtime.IsObjectiveActive(content.ChurchObjective), "Church objective did not become active.");
                LogMilestone("church-objective-active");

                graph.InteractWithNpc(content.Angel);
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.AngelGiveQuestCutscene,
                    "church-objective-active");
                Require(runtime.IsObjectiveActive(content.RorikObjective), "Rorik objective did not become active.");
                LogMilestone("rorik-objective-active");

                graph.InteractWithNpc(content.Rorik);
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.RorikChallengeCutscene,
                    "rorik-objective-active");
                ResolveEncounter(encounters, runtime, content.RorikEncounter, "rorik");
                Require(runtime.IsObjectiveActive(content.MoordellObjective), "Moordell objective did not become active.");
                LogMilestone("rorik-encounter-completed");

                graph.InteractWithNpc(content.MoordellContact);
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.MoordellDistributionCutscene,
                    "rorik-encounter-completed");
                Require(runtime.IsObjectiveActive(content.RossdamObjective), "Rossdam objective did not become active.");
                LogMilestone("moordell-distribution-completed");

                graph.InteractWithNpc(content.RossdamContact);
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.RossdamBattleStartCutscene,
                    "moordell-distribution-completed");
                ResolveEncounter(encounters, runtime, content.RossdamBattleEncounter, "rossdam");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.RossdamBattleEndCutscene,
                    "rossdam-encounter-completed");
                Require(runtime.IsObjectiveActive(content.MayorObjective), "Mayor objective did not become active.");
                LogMilestone("rossdam-battle-end-completed");

                graph.InteractWithNpc(content.KentridgeMayor);
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.MayorLoganLeadCutscene,
                    "rossdam-battle-end-completed");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.LoganBattleStartCutscene,
                    "mayor-logan-lead-completed");
                ResolveEncounter(encounters, runtime, content.LoganBattleEncounter, "logan");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.LoganBattleEndCutscene,
                    "logan-encounter-completed");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.LoganCastleBattleStartCutscene,
                    "logan-battle-end-completed");
                ResolveEncounter(encounters, runtime, content.LoganCastleLowerEncounter, "logan-castle-lower");
                CompleteCutscene(
                    orchestrator,
                    runtime,
                    content.LoganCastleHoleCutscene,
                    "logan-castle-lower-encounter-completed");
                LogMilestone("logan-castle-lower-logan-hole-completed");

                GameOutcomeSnapshot outcome = bootstrap.Composition.OutcomeQuery.Snapshot();
                Require(outcome.Lifecycle == GameOutcomeLifecycle.Resolved, "System15 did not resolve the authored full run.");
                Require(outcome.Disposition == GameOutcomeDisposition.Success, "System15 did not resolve a success disposition.");
                Require(outcome.Outcome == new OutcomeRef("main-campaign-complete"), "System15 resolved the wrong outcome.");
                Require(outcome.Revision == 1, "System15 terminal resolution was not exactly-once.");
                Debug.Log(
                    "KENTRIDGE_FULL_RUN_OUTCOME disposition=" + outcome.Disposition
                    + " outcome=" + outcome.Outcome
                    + " revision=" + outcome.Revision);

                Require(app.Update(0).Succeeded, "Application failed while observing the terminal System15 outcome.");
                Require(
                    app.Snapshot.Screen == ApplicationScreen.Outcome,
                    "Frontend did not project the resolved System15 outcome.");
                Require(
                    app.Snapshot.Detail.Contains("main-campaign-complete"),
                    "Frontend outcome detail did not contain the authored terminal outcome.");
                Require(
                    app.ReturnFromOutcome().Succeeded,
                    "Frontend outcome return did not use normal application/session teardown.");
                Require(
                    app.Snapshot.Lifecycle == ApplicationLifecycle.FrontEnd
                    && app.Snapshot.Screen == ApplicationScreen.MainMenu,
                    "Frontend aftermath did not return to the main menu.");
                Debug.Log("KENTRIDGE_FULL_RUN_APPLICATION_OUTCOME PASS");
            }
            finally
            {
                app.Dispose();
                if (orchestrator.Snapshot.Lifecycle != GameSessionLifecycle.Stopped)
                    orchestrator.Shutdown();
            }
        }

        private static void ResolveEncounter(
            EncounterRegistry encounters,
            CampaignRuntime runtime,
            EncounterId encounter,
            string milestone)
        {
            Require(
                encounters.Register(
                    new EncounterDefinition(encounter, EncounterCombatPolicy.Required, "system26-validation"),
                    out _) == EncounterMutationFailure.None,
                "Encounter registration failed after '" + milestone + "'.");
            Require(
                encounters.Activate(
                    new EncounterActivationRequest(encounter, "player-entered"),
                    out _) == EncounterMutationFailure.None,
                "Encounter activation failed after '" + milestone + "'.");
            Require(
                encounters.ApplyCombatResolved(
                    encounter,
                    new EncounterResolution(
                        EncounterResolutionResult.Completed,
                        "system26 authored full-run validation"),
                    out EncounterSnapshot snapshot) == EncounterMutationFailure.None,
                "Encounter completion failed after '" + milestone + "'.");
            Require(
                runtime.ObserveEncounter(snapshot) > 0,
                "Campaign did not consume encounter completion after '" + milestone + "'.");
        }

        private static void CompleteActiveCutscene(
            GameSessionOrchestrator orchestrator,
            CampaignRuntime runtime,
            string lastMilestone)
        {
            Require(
                runtime.HasActiveCutscene,
                "Authored route dead-ended after '" + lastMilestone + "': no active cutscene.");
            CompleteCutscene(orchestrator, runtime, runtime.ActiveCutscene, lastMilestone);
        }

        private static void CompleteCutscene(
            GameSessionOrchestrator orchestrator,
            CampaignRuntime runtime,
            CutsceneRef expected,
            string lastMilestone)
        {
            Require(
                runtime.HasActiveCutscene && runtime.ActiveCutscene.Equals(expected),
                "Authored route dead-ended after '" + lastMilestone
                + "': expected cutscene '" + expected + "'.");

            for (var i = 0; i < 128
                 && runtime.HasActiveCutscene
                 && runtime.ActiveCutscene.Equals(expected); i++)
            {
                GameSessionOperationResult tick = orchestrator.Tick(1000);
                Require(
                    tick.Succeeded,
                    "SessionOrchestration tick failed while completing '" + expected
                    + "': " + tick.Failure + " " + tick.Diagnostic);
            }

            Require(
                runtime.IsCutsceneCompleted(expected),
                "Cutscene '" + expected + "' did not complete within bounded semantic ticks.");
        }

        private static bool ContainsNpc(
            IReadOnlyList<ResolvedNpcWorldPlacement> placements,
            NpcRef npc)
        {
            for (var i = 0; i < placements.Count; i++)
                if (placements[i].Npc.Equals(npc))
                    return true;
            return false;
        }

        private static bool ContainsAllRequiredStages(
            IReadOnlyList<CutsceneSpec> cutscenes,
            IReadOnlyList<CutsceneStageRealization> stages)
        {
            for (var i = 0; i < cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = cutscenes[i];
                if (cutscene.Definition.StageRequirements.Count == 0)
                    continue;
                if (!ContainsStage(stages, cutscene.Ref))
                    return false;
            }
            return true;
        }

        private static bool ContainsStage(
            IReadOnlyList<CutsceneStageRealization> stages,
            CutsceneRef cutscene)
        {
            for (var i = 0; i < stages.Count; i++)
                if (stages[i].Cutscene.Equals(cutscene))
                    return true;
            return false;
        }

        private static void LogMilestone(string milestone)
        {
            Debug.Log("KENTRIDGE_FULL_RUN_MILESTONE " + milestone);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void EnsureValidationCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("System26 Full Run Validation Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.065f, 0.085f, 1f);
            camera.transform.position = new Vector3(0f, 2f, -6f);
        }

        private void OnGUI()
        {
            GUI.Box(
                new Rect(24f, 24f, Mathf.Max(360f, Screen.width - 48f), 82f),
                _status);
        }

        private sealed class ValidationActorHost : IKentridgeCampaignActorHost
        {
            private readonly Dictionary<NpcRef, ICutsceneActorRuntime> _npcs =
                new Dictionary<NpcRef, ICutsceneActorRuntime>();
            private readonly ICutsceneActorRuntime _player = new ImmediateActor();

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                _npcs.Clear();
                for (var i = 0; i < placements.Count; i++)
                    _npcs[placements[i].Npc] = new ImmediateActor();
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor) =>
                _npcs.TryGetValue(npc, out actor);

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = playerSlot == 0 ? _player : null;
                return actor != null;
            }
        }

        private sealed class ImmediateActor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public void PlaceAt(CutsceneStagePoint destination)
            {
                Position = destination.Position;
            }

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class ImmediatePresentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(
                CutsceneActorId speaker,
                CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class EmptyCharacters : ICharacterQuery
        {
            public IReadOnlyList<CharacterSnapshot> GetAll() =>
                Array.Empty<CharacterSnapshot>();

            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }

            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = default;
                return false;
            }
        }

        private sealed class EmptySaveCatalog : ISessionSaveCatalog
        {
            public IReadOnlyList<SessionSaveMetadata> ListSaves() =>
                Array.Empty<SessionSaveMetadata>();
        }

        private sealed class InertFormation : ISessionFormationService
        {
            public SessionFormationResult Host(HostSessionRequest request) =>
                SessionFormationResult.Success(
                    new GameSessionId("system26-unused-party"),
                    new PartyMemberId("system26-unused-host"));

            public SessionFormationResult Join(JoinSessionRequest request) =>
                SessionFormationResult.Success(
                    new GameSessionId("system26-unused-party"),
                    new PartyMemberId("system26-unused-joiner"));
        }

        private sealed class InertPartyPresentation : IPartyScreenPresentationQuery
        {
            public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId) =>
                new PartyScreenPresentationSnapshot(
                    new GameSessionId("system26-unused-party"),
                    4,
                    SessionPresentationLifecycle.WaitingForPlayers,
                    false,
                    Array.Empty<PartyMemberPresentationSnapshot>());
        }

        private sealed class InertPartyIntent : ISessionPresentationIntentRouter
        {
            public PartySessionCommandResult Request(SessionPresentationIntent intent) =>
                PartySessionCommandResult.Accept();
        }

        private sealed class ValidationInputContexts : IInputContextService
        {
            private readonly List<Entry> _entries = new List<Entry>();
            private int _next;

            public InputContextId ActiveContext =>
                _entries.Count == 0
                    ? InputContextId.Exploration
                    : _entries[_entries.Count - 1].Context;

            public IInputContextLease Push(InputContextId context)
            {
                int id = ++_next;
                _entries.Add(new Entry(id, context));
                return new Lease(this, id, context);
            }

            private void Remove(int id)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (_entries[i].Id != id) continue;
                    _entries.RemoveAt(i);
                    return;
                }
            }

            private readonly struct Entry
            {
                public readonly int Id;
                public readonly InputContextId Context;

                public Entry(int id, InputContextId context)
                {
                    Id = id;
                    Context = context;
                }
            }

            private sealed class Lease : IInputContextLease
            {
                private ValidationInputContexts _owner;
                private readonly int _id;
                public InputContextId Context { get; }

                public Lease(
                    ValidationInputContexts owner,
                    int id,
                    InputContextId context)
                {
                    _owner = owner;
                    _id = id;
                    Context = context;
                }

                public void Dispose()
                {
                    ValidationInputContexts owner = _owner;
                    if (owner == null) return;
                    _owner = null;
                    owner.Remove(_id);
                }
            }
        }

        private sealed class ValidationBindings : IInputBindingOverrideService
        {
            private readonly List<InputBindingOverride> _applied =
                new List<InputBindingOverride>();

            public IReadOnlyList<InputBindingOverride> SnapshotOverrides() => _applied;

            public bool TryApplyOverride(
                InputBindingOverride bindingOverride,
                out string error)
            {
                _applied.Add(bindingOverride);
                error = string.Empty;
                return true;
            }

            public void ClearOverrides()
            {
                _applied.Clear();
            }
        }

        private sealed class MemoryPreferences : IUserPreferencesStore
        {
            private UserPreferences _value;

            public bool TryLoad(out UserPreferences preferences)
            {
                preferences = _value;
                return _value != null;
            }

            public void Save(UserPreferences preferences)
            {
                _value = preferences;
            }
        }

        private sealed class ValidationAudio : IAudioPreferencesSink
        {
            public void Apply(UserPreferences preferences) { }
        }

        private sealed class ValidationExit : IApplicationExitPort
        {
            public void RequestExit() { }
        }

        private sealed class ValidationPlans : IApplicationSessionPlanProvider
        {
            public GameSessionStartRequest PlanNewGame(
                ApplicationSessionDescriptor descriptor) =>
                GameSessionStartRequest.NewGame(
                    new GameSessionIdentity(
                        descriptor.CampaignId,
                        descriptor.WorldId,
                        descriptor.SessionId,
                        descriptor.ConfigurationId));

            public GameSessionStartRequest PlanContinue(SessionSaveMetadata save) =>
                GameSessionStartRequest.Resume(
                    new GameSessionIdentity(
                        "main-campaign",
                        save.WorldId.Value,
                        save.SessionId,
                        "kentridge-authored-full-run"),
                    save.SaveId.Value);

            public GameSessionStartRequest PlanMultiplayer(
                SessionFormationResult formation) =>
                GameSessionStartRequest.NewGame(
                    new GameSessionIdentity(
                        "main-campaign",
                        Game.Kentridge.PlayableSlice.KentridgeDefinition.Id,
                        formation.SessionId.Value,
                        "kentridge-multiplayer"));
        }
    }
}