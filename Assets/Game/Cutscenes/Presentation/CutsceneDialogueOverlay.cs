using System;
using Game.Cutscenes.Api;
using UnityEngine;

namespace Game.Cutscenes.Presentation
{
    /// <summary>
    /// Reusable client-local presentation for the currently active cutscene dialogue line.
    /// Gameplay and scene composition provide only an <see cref="IActiveCutsceneDialogue"/> source;
    /// layout and rendering stay inside the cutscene presentation module.
    /// </summary>
    public sealed class CutsceneDialogueOverlay : MonoBehaviour
    {
        private IActiveCutsceneDialogue _source;

        public void Bind(IActiveCutsceneDialogue source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        private void OnGUI()
        {
            string dialogue = _source?.ActiveDialogue;
            if (string.IsNullOrEmpty(dialogue)) return;

            const float width = 520f;
            const float height = 72f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - height - 36f,
                width,
                height);
            GUI.Box(rect, dialogue);
        }
    }
}
