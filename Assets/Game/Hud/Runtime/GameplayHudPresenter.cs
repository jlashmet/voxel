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

        private GUIStyle _overline;
        private GUIStyle _label;
        private GUIStyle _strong;
        private GUIStyle _value;
        private GUIStyle _key;
        private GUIStyle _promptAction;
        private GUIStyle _promptCapability;
        private GUIStyle _toast;

        private Texture2D _panelTexture;
        private Texture2D _panelStrongTexture;
        private Texture2D _trackTexture;
        private Texture2D _healthTexture;
        private Texture2D _accentTexture;
        private Texture2D _warningTexture;
        private Texture2D _dangerTexture;
        private Texture2D _keyTexture;

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

        private void OnDestroy()
        {
            DestroyTexture(_panelTexture);
            DestroyTexture(_panelStrongTexture);
            DestroyTexture(_trackTexture);
            DestroyTexture(_healthTexture);
            DestroyTexture(_accentTexture);
            DestroyTexture(_warningTexture);
            DestroyTexture(_dangerTexture);
            DestroyTexture(_keyTexture);
        }

        private void DrawStatus(HudSnapshot hud)
        {
            float height = hud.Vitality.Visible ? 96f : 64f;
            Rect panel = new Rect(24f, 24f, 306f, height);
            DrawPanel(panel, ReadinessAccent(hud.Readiness));

            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, 132f, 16f), "SESSION", _overline);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 28f, panel.width - 36f, 24f), ReadinessText(hud.Readiness), _strong);
            if (!hud.Vitality.Visible) return;

            GUI.Label(new Rect(panel.x + 18f, panel.y + 58f, 70f, 16f), "VITALITY", _overline);
            float fraction = hud.Vitality.Maximum <= 0 ? 0f : Mathf.Clamp01((float)hud.Vitality.Current / hud.Vitality.Maximum);
            Rect track = new Rect(panel.x + 88f, panel.y + 62f, 142f, 10f);
            GUI.DrawTexture(track, _trackTexture, ScaleMode.StretchToFill);
            Rect fill = new Rect(track.x + 2f, track.y + 2f, Mathf.Max(0f, (track.width - 4f) * fraction), track.height - 4f);
            if (fill.width > 0f)
                GUI.DrawTexture(fill, hud.Vitality.Defeated ? _dangerTexture : _healthTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(panel.x + 238f, panel.y + 54f, 50f, 26f), hud.Vitality.Current + "/" + hud.Vitality.Maximum, _value);
        }

        private void DrawEncounter(HudSnapshot hud)
        {
            if (!hud.Encounter.Visible) return;
            string state = hud.Encounter.CombatRequired ? "COMBAT" : "ENCOUNTER";
            string detail = FriendlySemantic(hud.Encounter.SemanticKind) + "  ·  " + hud.Encounter.Lifecycle.ToUpperInvariant();
            float detailWidth = _label.CalcSize(new GUIContent(detail)).x;
            float width = Mathf.Clamp(detailWidth + 112f, 280f, Mathf.Max(280f, Screen.width - 48f));
            Rect panel = new Rect((Screen.width - width) * 0.5f, 24f, width, 54f);
            GUI.DrawTexture(panel, _panelStrongTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), hud.Encounter.CombatRequired ? _dangerTexture : _warningTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 9f, 82f, 16f), state, _overline);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 25f, panel.width - 36f, 22f), detail, _strong);
        }

        private void DrawProgression(HudSnapshot hud)
        {
            if (!hud.TrackedProgression.Visible) return;
            Rect panel = new Rect(Screen.width - 330f, 24f, 306f, 82f);
            DrawPanel(panel, _accentTexture);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, 16f), "TRACKED OBJECTIVE", _overline);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 30f, panel.width - 36f, 24f), hud.TrackedProgression.Label, _strong);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 55f, panel.width - 36f, 18f), hud.TrackedProgression.ProgressText, _label);
        }

        private void DrawInteraction(HudSnapshot hud)
        {
            if (!hud.Interaction.Visible) return;

            string action = hud.Interaction.ActionText;
            string capability = hud.Interaction.CapabilityText;
            float actionWidth = _promptAction.CalcSize(new GUIContent(action)).x;
            float capabilityWidth = string.IsNullOrWhiteSpace(capability) ? 0f : _promptCapability.CalcSize(new GUIContent(capability)).x;
            float contentWidth = Mathf.Max(actionWidth, capabilityWidth);
            float width = Mathf.Clamp(82f + contentWidth, 230f, Mathf.Max(230f, Screen.width - 48f));
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - 92f, width, 58f);
            GUI.DrawTexture(panel, _panelStrongTexture, ScaleMode.StretchToFill);

            Rect key = new Rect(panel.x + 12f, panel.y + 11f, 38f, 36f);
            GUI.DrawTexture(key, _keyTexture, ScaleMode.StretchToFill);
            GUI.Label(key, hud.Interaction.BindingLabel, _key);

            float textX = key.xMax + 14f;
            GUI.Label(new Rect(textX, panel.y + 9f, panel.xMax - textX - 12f, 24f), action, _promptAction);
            if (!string.IsNullOrWhiteSpace(capability))
                GUI.Label(new Rect(textX, panel.y + 31f, panel.xMax - textX - 12f, 18f), capability.ToUpperInvariant(), _promptCapability);
        }

        private void DrawTransient(HudSnapshot hud)
        {
            if (hud.TransientEvents.Count == 0) return;
            string text = hud.TransientEvents[hud.TransientEvents.Count - 1].Text;
            Rect panel = new Rect(Screen.width - 330f, Screen.height - 92f, 306f, 58f);
            GUI.DrawTexture(panel, _panelTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), _accentTexture, ScaleMode.StretchToFill);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 8f, panel.width - 34f, panel.height - 16f), text, _toast);
        }

        private void DrawPanel(Rect panel, Texture2D accent)
        {
            GUI.DrawTexture(panel, _panelTexture, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(panel.x, panel.y, 4f, panel.height), accent, ScaleMode.StretchToFill);
        }

        private void EnsureStyles()
        {
            if (_panelTexture != null) return;

            _panelTexture = MakeTexture(new Color(0.025f, 0.035f, 0.055f, 0.88f));
            _panelStrongTexture = MakeTexture(new Color(0.02f, 0.028f, 0.045f, 0.94f));
            _trackTexture = MakeTexture(new Color(0.08f, 0.10f, 0.14f, 0.95f));
            _healthTexture = MakeTexture(new Color(0.19f, 0.82f, 0.59f, 1f));
            _accentTexture = MakeTexture(new Color(0.22f, 0.72f, 0.94f, 1f));
            _warningTexture = MakeTexture(new Color(0.96f, 0.67f, 0.21f, 1f));
            _dangerTexture = MakeTexture(new Color(0.95f, 0.28f, 0.32f, 1f));
            _keyTexture = MakeTexture(new Color(0.12f, 0.18f, 0.25f, 0.98f));

            _overline = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.68f, 0.78f, 1f) }
            };
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.72f, 0.79f, 0.84f, 1f) }
            };
            _strong = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _value = new GUIStyle(_strong) { alignment = TextAnchor.MiddleRight, fontSize = 13 };
            _key = new GUIStyle(_strong) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            _promptAction = new GUIStyle(_strong) { fontSize = 16 };
            _promptCapability = new GUIStyle(_overline) { fontSize = 10 };
            _toast = new GUIStyle(_label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) Destroy(texture);
        }

        private Texture2D ReadinessAccent(HudReadinessState state)
        {
            switch (state)
            {
                case HudReadinessState.GameplayReady: return _healthTexture;
                case HudReadinessState.Reconnecting: return _dangerTexture;
                case HudReadinessState.Resynchronizing: return _warningTexture;
                default: return _accentTexture;
            }
        }

        private static string FriendlySemantic(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ACTIVE";
            return value.Replace('-', ' ').Replace('_', ' ').ToUpperInvariant();
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
