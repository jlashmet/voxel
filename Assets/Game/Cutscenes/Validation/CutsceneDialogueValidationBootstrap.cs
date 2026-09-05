using System;
using Game.Cutscenes.Api;
using Game.Cutscenes.Presentation;
using Game.Cutscenes.Runtime;
using UnityEngine;

namespace Game.Cutscenes.Validation
{
    public sealed class CutsceneDialogueValidationBootstrap : MonoBehaviour
    {
        private TimedCutsceneDialogueRuntime _runtime;

        private void Start()
        {
            try
            {
                _runtime = new TimedCutsceneDialogueRuntime(
                    (speaker, cue) => cue.Value == "mountain-dragon-hello"
                        ? "Hello, I'm Mr. Dragon."
                        : cue.Value,
                    displayDurationMilliseconds: 8000);

                CutsceneDialogueOverlay overlay = gameObject.AddComponent<CutsceneDialogueOverlay>();
                overlay.Bind(_runtime);
                _runtime.Execute(
                    new CutsceneActorId("mountain-dragon"),
                    new CutsceneCueId("mountain-dragon-hello"));

                if (_runtime.ActiveDialogue != "Hello, I'm Mr. Dragon.")
                    throw new InvalidOperationException("Production dialogue runtime did not expose the authored line.");

                Debug.Log("CUTSCENE_VALIDATION dialogue-active=Hello, I'm Mr. Dragon.");
            }
            catch (Exception ex)
            {
                Debug.LogError("CUTSCENE_VALIDATION failure: " + ex);
                throw;
            }
        }

        private void Update()
        {
            if (_runtime == null) return;
            int elapsedMilliseconds = Mathf.Max(0, Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f));
            _runtime.Advance(elapsedMilliseconds);
        }
    }
}
