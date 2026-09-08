using System;
using Game.Application.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    [DefaultExecutionOrder(-200)]
    internal sealed class KentridgeMacroWorldApplicationStartDriver : MonoBehaviour
    {
        private const float EvidenceDiscoveryTimeoutSeconds = 5f;
        private const float EvidenceProbeIntervalSeconds = 0.25f;

        private KentridgeProductionCompositionRoot _root;
        private bool _requestIssued;
        private float _installedAt;
        private float _nextEvidenceProbeAt;
        private bool _validationEvidenceFound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Kentridge Macro World Application Start")
            {
                hideFlags = HideFlags.DontSave
            };
            host.AddComponent<KentridgeMacroWorldApplicationStartDriver>();
        }

        private void Awake()
        {
            _installedAt = Time.realtimeSinceStartup;
            _nextEvidenceProbeAt = _installedAt;
        }

        private void Update()
        {
            // SceneIssue validation-profile helpers intentionally live on HideFlags.DontSave
            // objects. Unity's ordinary FindFirstObjectByType discovery omits those objects, which
            // made the previous companion report no evidence even though EvidenceDriver.OnEnable
            // had already run. Resources.FindObjectsOfTypeAll is the validation-safe discovery path;
            // throttle it so ordinary gameplay pays only a handful of probes during the bounded
            // five-second discovery window and then removes this otherwise inert companion.
            float now = Time.realtimeSinceStartup;
            if (!_validationEvidenceFound && now >= _nextEvidenceProbeAt)
            {
                _validationEvidenceFound = HasActiveValidationEvidence();
                _nextEvidenceProbeAt = now + EvidenceProbeIntervalSeconds;
            }

            if (!_validationEvidenceFound)
            {
                if (!ShouldAwaitEvidence(now - _installedAt))
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

        internal static bool HasActiveValidationEvidence()
        {
            KentridgeMacroWorldEvidenceDriver[] drivers =
                Resources.FindObjectsOfTypeAll<KentridgeMacroWorldEvidenceDriver>();
            for (var i = 0; i < drivers.Length; i++)
                if (drivers[i] != null && drivers[i].isActiveAndEnabled)
                    return true;
            return false;
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
