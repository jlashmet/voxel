using System;
using Game.Hud.Api;
using Game.Input.Api;
using UnityEngine;

namespace Game.Hud.Runtime
{
    [AddComponentMenu("Game/HUD/Gameplay HUD Presenter")]
    public sealed class GameplayHudPresenter : MonoBehaviour
    {
        private IHudSnapshotProvider _provider;
        private LocalPlayerId _localPlayer;
        private GUIStyle _panel;
        private GUIStyle _label;
        private GUIStyle _strong;
        private GUIStyle _prompt;

        public void Configure(IHudSnapshotProvider provider, LocalPlayerId localPlayer)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _localPlayer = localPlayer;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || _provider == null) return;
            EnsureStyles();
            HudSnapshot hud = _provider.Project(_localPlayer);
            DrawStatus(hud);
            DrawEncounter(hud);
            DrawProgression(hud);
            DrawInteraction(hud);
            DrawTransient(hud);
        }

        private void DrawStatus(HudSnapshot hud)
        {
            Rect panel = new Rect(24f, 24f, 300f, hud.Vitality.Visible ? 92f : 58f);
            GUI.Box(panel, GUIContent.none, _panel);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, 24f), ReadinessText(hud.Readiness), _strong);
            if (!hud.Vitality.Visible) return;

            GUI.Label(new Rect(panel.x + 16f, panel.y + 44f, 70f, 22f), "VITALITY", _label);
            float fraction = hud.Vitality.Maximum <= 0 ? 0f : Mathf.Clamp01((float)hud.Vitality.Current / hud.Vitality.Maximum);
            Rect track = new Rect(panel.x + 92f, panel.y + 48f, 140f, 14f);
            GUI.Box(track, GUIContent.none);
            GUI.Box(new Rect(track.x + 2f, track.y + 2f, Mathf.Max(0f, (track.width - 4f) * fraction), track.height - 4f), GUIContent.none);
            GUI.Label(new Rect(panel.x + 238f, panel.y + 42f, 50f, 24f), hud.Vitality.Current + "/" + hud.Vitality.Maximum, _label);
        }

        private void DrawEncounter(HudSnapshot hud)
        {
            if (!hud.Encounter.Visible) return;
            string state = hud.Encounter.CombatRequired ? "COMBAT" : "ENCOUNTER";
            string text = state + "  ·  " + hud.Encounter.SemanticKind + "  ·  " + hud.Encounter.Lifecycle;
            Vector2 size = _strong.CalcSize(new GUIContent(text));
            float width = Mathf.Min(Screen.width - 48f, size.x + 36f);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, 24f, width, 42f), text, _panel);
        }

        private void DrawProgression(HudSnapshot hud)
        {
            if (!hud.TrackedProgression.Visible) return;
            Rect panel = new Rect(Screen.width - 324f, 24f, 300f, 72f);
            GUI.Box(panel, GUIContent.none, _panel);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 24f), hud.TrackedProgression.Label, _strong);
            GUI.Label(new Rect(panel.x + 16f, panel.y + 38f, panel.width - 32f, 22f), hud.TrackedProgression.ProgressText, _label);
        }

        private void DrawInteraction(HudSnapshot hud)
        {
            if (!hud.Interaction.Visible) return;
            string text = "[" + hud.Interaction.BindingLabel + "]  " + hud.Interaction.ActionText;
            if (!string.IsNullOrWhiteSpace(hud.Interaction.CapabilityText)) text += "  ·  " + hud.Interaction.CapabilityText;
            Vector2 size = _prompt.CalcSize(new GUIContent(text));
            float width = Mathf.Min(Screen.width - 48f, size.x + 42f);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - 82f, width, 48f), text, _prompt);
        }

        private void DrawTransient(HudSnapshot hud)
        {
            if (hud.TransientEvents.Count == 0) return;
            string text = hud.TransientEvents[hud.TransientEvents.Count - 1].Text;
            GUI.Box(new Rect(Screen.width - 324f, Screen.height - 82f, 300f, 48f), text, _panel);
        }

        private void EnsureStyles()
        {
            if (_panel != null) return;
            _panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 10) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _strong = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _prompt = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(18, 18, 10, 10) };
        }

        private static string ReadinessText(HudReadinessState state)
        {
            switch (state)
            {
                case HudReadinessState.Reconnecting: return "RECONNECTING";
                case HudReadinessState.Resynchronizing: return "RESYNCHRONIZING";
                case HudReadinessState.GameplayReady: return "GAMEPLAY READY";
                default: return "WAITING FOR SESSION";
            }
        }
    }
}
