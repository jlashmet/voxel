using System;
using Game.Application.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    [DefaultExecutionOrder(-200)]
    internal sealed class KentridgeMacroWorldApplicationStartDriver : MonoBehaviour
    {
        private KentridgeProductionCompositionRoot _root;
        private bool _requestIssued;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("Kentridge Macro World Application Start")
            {
                hideFlags = HideFlags.DontSave
            };
            host.AddComponent<KentridgeMacroWorldApplicationStartDriver>();
        }

        private void Start()
        {
            if (FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>() != null)
            {
                return;
            }

            Destroy(gameObject);
        }

        private void Update()
        {
            if (FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>() == null)
            {
                Destroy(gameObject);
                return;
            }

            _root ??= FindFirstObjectByType<KentridgeProductionCompositionRoot>();
            if (_root == null || !_root.IsComposed)
            {
                return;
            }

            ApplicationFlowSnapshot flow = _root.FlowSnapshot;
            if (flow.Lifecycle == ApplicationLifecycle.StartingSession ||
                flow.Lifecycle == ApplicationLifecycle.InGame)
            {
                Destroy(gameObject);
                return;
            }

            if (!ShouldRequestNewGame(flow, _requestIssued))
            {
                return;
            }

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
