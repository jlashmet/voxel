using System.Text;
using Game.Input.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Read-only exact-player diagnostic for the repeated System24 destination-interaction isolate.
    /// It is installed only for the System24 command-line validation and never issues gameplay commands.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class KentridgeSystem24InteractionDiagnostic : MonoBehaviour
    {
        private static readonly LocalPlayerId LocalPlayer = new LocalPlayerId(0);

        private KentridgeProductionCompositionRoot _root;
        private KentridgePlayableSlice _slice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!KentridgeSystem24VerticalSliceDriver.IsRequested) return;
            KentridgeProductionCompositionRoot root = FindFirstObjectByType<KentridgeProductionCompositionRoot>();
            if (root == null || root.GetComponent<KentridgeSystem24InteractionDiagnostic>() != null) return;
            root.gameObject.AddComponent<KentridgeSystem24InteractionDiagnostic>();
        }

        private void Awake()
        {
            _root = GetComponent<KentridgeProductionCompositionRoot>();
            _slice = GetComponent<KentridgePlayableSlice>();
        }

        private void Update()
        {
            if (_root == null || _root.InputActions == null || _slice == null || _slice.CharacterHost == null)
                return;
            if (!_root.InputActions.WasPressed(LocalPlayer, StandardInputActions.Interact)) return;

            Vector3 player = _slice.CharacterHost.Position;
            bool hasDestination = _slice.TryGetDestinationNpcWorldPosition(out Vector3 destination);
            float distance = hasDestination ? Vector3.Distance(player, destination) : -1f;
            Debug.Log(
                "SYSTEM24_INTERACTION_DIAGNOSTIC edge=true" +
                " position=" + Format(player) +
                " destination=" + (hasDestination ? Format(destination) : "unavailable") +
                " distance=" + (hasDestination ? distance.ToString("0.000") : "unavailable") +
                " range=" + _slice.InteractionRangeMetres.ToString("0.000") +
                " objectiveActive=" + _slice.TravelObjectiveActive +
                " destinationCutscene=" + _slice.DestinationCutsceneActive +
                " candidates=" + DescribeConversationCandidates(player));
        }

        private string DescribeConversationCandidates(Vector3 player)
        {
            var session = _slice.CampaignSession;
            var host = _slice.CharacterHost;
            if (session == null || host == null) return "unavailable";

            float range = Mathf.Max(0f, _slice.InteractionRangeMetres);
            float rangeSquared = range * range;
            float bestDistanceSquared = rangeSquared;
            string best = "none";
            var builder = new StringBuilder();

            for (int i = 0; i < session.Blueprint.Npcs.Count; i++)
            {
                var candidate = session.Blueprint.Npcs[i];
                if (!candidate.RequiresConversation) continue;
                if (!host.TryGetNpcPosition(candidate.Ref, out Vector3 position)) continue;

                float distanceSquared = (position - player).sqrMagnitude;
                if (builder.Length > 0) builder.Append('|');
                builder.Append(candidate.Ref)
                    .Append('@')
                    .Append(Mathf.Sqrt(distanceSquared).ToString("0.000"));

                if (distanceSquared > bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                best = candidate.Ref + "@" + Mathf.Sqrt(distanceSquared).ToString("0.000");
            }

            return "nearest=" + best + ";all=" + builder;
        }

        private static string Format(Vector3 value) =>
            value.x.ToString("0.000") + "," + value.y.ToString("0.000") + "," + value.z.ToString("0.000");
    }
}
