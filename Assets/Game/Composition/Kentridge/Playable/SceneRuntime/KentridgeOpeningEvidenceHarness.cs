using System;
using System.Collections.Generic;
using System.Text;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Opt-in built-player acceptance probe for the recovered opening story. Ordinary gameplay does
    /// not install this component. The exact SceneIssue replay drives the live Logan opening through
    /// the normal playable-slice runtime, then validates the production Awon/Medrare story rules,
    /// recovered dialogue, choreography, effects, and replay suppression before publishing PASS.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class KentridgeOpeningEvidenceHarness : MonoBehaviour
    {
        private const string TargetIssueId =
            "20260828-213647-000-KentridgeAwonMedrareOpeningCutscenes";
        private const ulong ExpectedMedrareDialogueHash = 0xaf88eb792eee83b6UL;
        private const int ValidationFailureExitCode = 42;
        private const int IncompleteValidationExitCode = 43;

        private KentridgePlayableSlice _slice;
        private bool _attempted;
        private bool _passed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!ShouldInstall()) return;

            KentridgePlayableSlice slice = UnityEngine.Object.FindFirstObjectByType<KentridgePlayableSlice>(
                FindObjectsInactive.Include);
            if (slice == null)
            {
                Debug.LogError("KENTRIDGE_OPENING result=FAIL reason=no-playable-slice");
                Environment.ExitCode = ValidationFailureExitCode;
                UnityEngine.Application.Quit(ValidationFailureExitCode);
                return;
            }

            var root = new GameObject("Kentridge Opening Evidence Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            var harness = root.AddComponent<KentridgeOpeningEvidenceHarness>();
            harness._slice = slice;
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log("KENTRIDGE_OPENING armed waiting-for-live-logan-opening");
        }

        private void Update()
        {
            if (_attempted || _slice == null) return;

            // This is exactly the same unattended release seam already used by the Kentridge
            // landmark evidence harness. While the live Logan cutscene owns control, AutoWalk lets
            // the production slice dismiss pending dialogue and tick its real CampaignRuntime. No
            // teleport or alternate cutscene runner is introduced.
            if (!_slice.GameplayControlEnabled)
            {
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                return;
            }

            _slice.AutoWalk = false;
            _attempted = true;
            try
            {
                ValidateProductionOpening();
                _passed = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("KENTRIDGE_OPENING result=FAIL reason=" + Sanitize(ex.Message));
                Environment.ExitCode = ValidationFailureExitCode;
                UnityEngine.Application.Quit(ValidationFailureExitCode);
            }
        }

        private void OnApplicationQuit()
        {
            if (_passed) return;
            if (!_attempted)
                Debug.LogError("KENTRIDGE_OPENING result=FAIL reason=player-exited-before-validation");
            if (Environment.ExitCode == 0)
                Environment.ExitCode = IncompleteValidationExitCode;
        }

        private static void ValidateProductionOpening()
        {
            var destination = new CutsceneDefinition(
                "built-player.kentridge-opening-evidence.destination",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(destination);
            var state = new EvidenceState();
            var effects = new EvidenceEffects();

            Require(
                KentridgeOpeningScript.LineFor(KentridgeOpeningScript.CueForOriginalLine(27)) ==
                "There's a few things I have to do first though.  First, my father wanted me to stop by the house to show me something.",
                "logan-continuation-text");
            Require(DispatchNpc(content, content.Medrare, state, effects) == 0, "medrare-fired-before-awon");
            Require(DispatchSite(content, content.MedrareHouseSite, state, effects) == 0, "first-spell-fired-before-awon");

            // GameplayControlEnabled above proves that this built application's real campaign
            // runtime has completed the Logan opening. Mirror only that completion into the rule
            // probe so the remaining production progression can be exercised deterministically.
            state.Complete(content.IntroCutscene);
            Require(DispatchNpc(content, content.Awon, state, effects) == 1, "awon-did-not-fire-after-logan");
            Require(effects.LastCutscene.Equals(content.AwonOpeningCutscene), "awon-cutscene-mismatch");
            Require(KentridgeOpeningProgressionCutscenes.AwonDefinition.Steps.Count == 22, "awon-line-count");
            state.Complete(content.AwonOpeningCutscene);

            Require(DispatchNpc(content, content.Medrare, state, effects) == 1, "medrare-join-did-not-fire-after-awon");
            Require(effects.LastCutscene.Equals(content.MedrareJoinCutscene), "medrare-join-cutscene-mismatch");

            CutsceneDefinition join = KentridgeOpeningProgressionCutscenes.MedrareJoinDefinition;
            Require(join.Steps.Count == 20, "medrare-step-count");
            Require(join.Steps[0].Type == CutsceneStepType.Camera, "medrare-camera-step");
            Require(join.Steps[0].Cue.Equals(KentridgeOpeningProgressionCutscenes.MedrareJoinZoomHalf), "medrare-camera-cue");
            Require(join.Steps[1].Type == CutsceneStepType.Wait && join.Steps[1].DurationMilliseconds == 1500, "medrare-wait-step");
            Require(join.Steps[2].Type == CutsceneStepType.MoveActor, "medrare-move-step");
            Require(join.Steps[2].Actor.Equals(KentridgeOpeningProgressionCutscenes.Medrare), "medrare-move-actor");
            Require(join.Steps[2].StagePoint.Equals(KentridgeOpeningProgressionCutscenes.MedrareApproachPoint), "medrare-move-target");
            Require(join.Steps[2].DurationMilliseconds == 2000, "medrare-move-duration");

            ulong dialogueHash = HashMedrareDialogue(join);
            Require(dialogueHash == ExpectedMedrareDialogueHash, "medrare-dialogue-hash");

            state.Complete(content.MedrareJoinCutscene);
            Require(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.CutsceneCompleted(content.MedrareJoinCutscene),
                    state,
                    effects) == 1,
                "medrare-join-effect");
            Require(effects.JoinedPartyMembers.Contains("Medrare"), "medrare-party-membership");

            Require(DispatchSite(content, content.MedrareHouseSite, state, effects) == 1, "first-spell-did-not-fire");
            Require(effects.LastCutscene.Equals(content.MedrareFirstSpellCutscene), "first-spell-cutscene-mismatch");
            state.Complete(content.MedrareFirstSpellCutscene);
            Require(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.CutsceneCompleted(content.MedrareFirstSpellCutscene),
                    state,
                    effects) == 2,
                "first-spell-effects");
            Require(effects.GrantedSpells.Contains("Flame"), "flame-not-granted");
            Require(effects.LastCutscene.Equals(content.MedrareToChurchCutscene), "church-continuation-missing");

            Require(DispatchNpc(content, content.Medrare, state, effects) == 0, "medrare-join-replayed");

            Debug.Log(
                "KENTRIDGE_OPENING result=PASS sequence=logan>awon>medrare " +
                "awonLines=22 medrareLines=17 dialogueHash=" + dialogueHash.ToString("x16") +
                " party=Medrare flame=True replaySuppressed=True");
        }

        private static ulong HashMedrareDialogue(CutsceneDefinition join)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 3; i < join.Steps.Count; i++)
            {
                CutsceneStep step = join.Steps[i];
                Require(step.Type == CutsceneStepType.Dialogue, "medrare-non-dialogue-step-" + i);
                string line = KentridgeOpeningScript.LineFor(step.Cue);
                Require(!string.IsNullOrEmpty(line), "medrare-empty-dialogue-step-" + i);
                AddHash(ref hash, step.Actor.Value);
                AddByte(ref hash, 0);
                AddHash(ref hash, line);
                AddByte(ref hash, (byte)'\n');
            }
            return hash;
        }

        private static void AddHash(ref ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            for (int i = 0; i < bytes.Length; i++) AddByte(ref hash, bytes[i]);
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }

        private static int DispatchNpc(
            KnownOpeningCampaignContent content,
            NpcRef npc,
            EvidenceState state,
            EvidenceEffects effects) =>
            StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.NpcInteracted(npc), state, effects);

        private static int DispatchSite(
            KnownOpeningCampaignContent content,
            SiteRef site,
            EvidenceState state,
            EvidenceEffects effects) =>
            StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteProximityEntered(site), state, effects);

        private static bool ShouldInstall()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-voxel-kentridge-opening-evidence", StringComparison.Ordinal))
                    return true;

                bool issueSwitch = string.Equals(args[i], "-voxel-scene-issue", StringComparison.Ordinal)
                    || string.Equals(args[i], "-voxelIssue", StringComparison.Ordinal);
                if (!issueSwitch || i + 1 >= args.Length) continue;
                if ((args[i + 1] ?? string.Empty).IndexOf(TargetIssueId, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        private static string Sanitize(string value) =>
            (value ?? "unknown").Replace('\r', ' ').Replace('\n', ' ');

        private sealed class EvidenceState : IStoryStateView
        {
            private readonly HashSet<CutsceneRef> _completed = new HashSet<CutsceneRef>();
            public void Complete(CutsceneRef cutscene) => _completed.Add(cutscene);
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => _completed.Contains(cutscene);
        }

        private sealed class EvidenceEffects : IStoryProgressEffectSink
        {
            public CutsceneRef LastCutscene { get; private set; }
            public HashSet<string> JoinedPartyMembers { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> GrantedSpells { get; } = new HashSet<string>(StringComparer.Ordinal);
            public void StartObjective(ObjectiveRef objective) { }
            public void StartQuest(QuestRef quest) { }
            public void PlayCutscene(CutsceneRef cutscene) => LastCutscene = cutscene;
            public void JoinPartyMember(string memberId) => JoinedPartyMembers.Add(memberId);
            public void GrantSpell(string spellId) => GrantedSpells.Add(spellId);
        }
    }
}
