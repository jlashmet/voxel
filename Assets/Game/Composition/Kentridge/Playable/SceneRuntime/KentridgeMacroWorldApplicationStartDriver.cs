using System;
using Game.Application.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    [DefaultExecutionOrder(-200)]
    internal sealed class KentridgeMacroWorldApplicationStartDriver : MonoBehaviour
    {
        private const float EvidenceDiscoveryTimeoutSeconds = 5f;

        private KentridgeProductionCompositionRoot _root;
        private bool _requestIssued;
        private float _installedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Kentridge Macro World Application Start")
            {
                hideFlags = HideFlags.DontSave
            };
            host.AddComponent<KentridgeMacroWorldApplicationStartDriver>();
        }

        private void Awake() => _installedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            // SceneIssue validation-profile components are installed after scene load and may not
            // exist yet when this companion receives its first Start/Update callback. The previous
            // implementation destroyed itself immediately in that window, leaving the real
            // Kentridge application parked at FrontEnd/MainMenu for the entire replay. Keep the
            // otherwise inert companion alive for one short discovery window; ordinary gameplay
            // still removes it after five seconds when no macro-evidence driver is present.
            if (FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>() == null)
            {
                if (!ShouldAwaitEvidence(Time.realtimeSinceStartup - _installedAt))
                    Destroy(gameObject);
                return;
            }

            _root ??= FindFirstObjectByType<KentridgeProductionCompositionRoot>();
            if (_root == null || !_root.IsComposed)
                return;

            ApplicationFlowSnapshot flow = _root.FlowSnapshot;
            if (flow.Lifecycle == ApplicationLifecycle.StartingSession ||
                flow.Lifecycle == ApplicationLifecycle.InGame)
            {
                Destroy(gameObject);
                return;
            }

            if (!ShouldRequestNewGame(flow, _requestIssued))
                return;

            ApplicationOperationResult result = _root.RequestNewGame();
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Macro evidence Application New Game rejected: " +
                    result.Failure + ": " + result.Detail);
            }

            _requestIssued = true;
            Debug.Log("MACROEVIDENCE application-new-game-requested");
        }

        internal static bool ShouldAwaitEvidence(float elapsedSeconds) =>
            elapsedSeconds < EvidenceDiscoveryTimeoutSeconds;

        internal static bool ShouldRequestNewGame(
            ApplicationFlowSnapshot flow,
            bool alreadyRequested)
        {
            return !alreadyRequested &&
                   flow.Lifecycle == ApplicationLifecycle.FrontEnd &&
                   flow.Screen == ApplicationScreen.MainMenu;
        }
    }
}
