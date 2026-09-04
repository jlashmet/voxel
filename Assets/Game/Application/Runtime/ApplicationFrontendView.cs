using System;
using Game.Application.Api;
using Game.Sessions.Api;
using UnityEngine;

namespace Game.Application.Runtime
{
    public sealed class ApplicationFrontendConfiguration
    {
        public ApplicationSessionDescriptor NewGame { get; }
        public string ContinueSaveId { get; }
        public HostSessionRequest Host { get; }
        public JoinSessionRequest Join { get; }

        public ApplicationFrontendConfiguration(
            ApplicationSessionDescriptor newGame,
            string continueSaveId,
            HostSessionRequest host,
            JoinSessionRequest join)
        {
            NewGame = newGame;
            ContinueSaveId = continueSaveId ?? string.Empty;
            Host = host;
            Join = join;
        }
    }

    /// <summary>
    /// Thin production frontend. It renders local navigation and sends semantic intents to
    /// ApplicationFlowCoordinator only; it never loads scenes, pauses Time.timeScale or touches transport.
    /// </summary>
    public sealed class ApplicationFrontendView : MonoBehaviour
    {
        private ApplicationFlowCoordinator _flow;
        private ApplicationFrontendConfiguration _configuration;
        private string _feedback = string.Empty;
        private float _masterVolume = 1f;
        private float _uiScale = 1f;

        public bool IsBound => _flow != null && _configuration != null;

        public void Bind(ApplicationFlowCoordinator flow, ApplicationFrontendConfiguration configuration)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private void Update()
        {
            if (_flow == null) return;
            ApplicationLifecycle lifecycle = _flow.Snapshot.Lifecycle;
            if (lifecycle == ApplicationLifecycle.StartingSession || lifecycle == ApplicationLifecycle.InGame)
                _flow.Update(Mathf.Max(0, Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f)));
        }

        private void OnGUI()
        {
            if (!IsBound) return;
            ApplicationFlowSnapshot snapshot = _flow.Snapshot;
            GUILayout.BeginArea(new Rect(28f, 28f, 420f, 650f), GUI.skin.box);
            GUILayout.Label("APPLICATION FRONTEND");
            GUILayout.Label("Lifecycle: " + snapshot.Lifecycle);
            GUILayout.Label("Screen: " + snapshot.Screen);
            if (snapshot.LastFailure != ApplicationFailure.None)
                GUILayout.Label("Error: " + snapshot.LastFailure + " — " + snapshot.Detail);
            if (!string.IsNullOrWhiteSpace(_feedback)) GUILayout.Label(_feedback);
            GUILayout.Space(8f);

            if (snapshot.Lifecycle == ApplicationLifecycle.FrontEnd)
                DrawFrontEnd(snapshot);
            else if (snapshot.Lifecycle == ApplicationLifecycle.StartingSession)
                GUILayout.Label("Preparing authoritative gameplay state…");
            else if (snapshot.Lifecycle == ApplicationLifecycle.InGame)
                DrawInGame(snapshot);
            else if (snapshot.Lifecycle == ApplicationLifecycle.Exiting)
                GUILayout.Label("Exiting…");

            GUILayout.EndArea();
        }

        private void DrawFrontEnd(ApplicationFlowSnapshot snapshot)
        {
            switch (snapshot.Screen)
            {
                case ApplicationScreen.MainMenu:
                    if (GUILayout.Button("New Game")) Report(_flow.RequestNewGame(_configuration.NewGame));
                    if (GUILayout.Button("Continue")) Report(_flow.RequestContinue(_configuration.ContinueSaveId));
                    if (GUILayout.Button("Multiplayer")) Report(_flow.OpenScreen(ApplicationScreen.Multiplayer));
                    if (GUILayout.Button("Settings")) Report(_flow.OpenScreen(ApplicationScreen.Settings));
                    if (GUILayout.Button("Quit")) Report(_flow.RequestQuitApplication());
                    break;
                case ApplicationScreen.Multiplayer:
                    if (GUILayout.Button("Host")) Report(_flow.RequestHost(_configuration.Host));
                    if (GUILayout.Button("Join")) Report(_flow.RequestJoin(_configuration.Join));
                    if (GUILayout.Button("Back")) Report(_flow.CloseScreen());
                    break;
                case ApplicationScreen.Party:
                    GUILayout.Label("Party state is sourced from SessionPresentation.");
                    if (_flow.TryCapturePartyScreen(out var party))
                        GUILayout.Label("Session: " + party.SessionId + "   Members: " + party.Members.Count + "/" + party.Capacity);
                    if (GUILayout.Button("Start")) Report(_flow.RequestPartyStart());
                    if (GUILayout.Button("Leave")) Report(_flow.RequestLeaveGame());
                    break;
                case ApplicationScreen.Settings:
                    _masterVolume = GUILayout.HorizontalSlider(_masterVolume, 0f, 1f);
                    GUILayout.Label("Master volume " + _masterVolume.ToString("0.00"));
                    _uiScale = GUILayout.HorizontalSlider(_uiScale, 0.75f, 1.5f);
                    GUILayout.Label("UI scale " + _uiScale.ToString("0.00"));
                    if (GUILayout.Button("Apply")) Report(_flow.ApplyPreferences(new UserPreferences(_masterVolume, _uiScale)));
                    if (GUILayout.Button("Back")) Report(_flow.CloseScreen());
                    break;
                case ApplicationScreen.Error:
                    GUILayout.Label("Startup failed. Return to the main menu using the owning composition flow.");
                    break;
            }
        }

        private void DrawInGame(ApplicationFlowSnapshot snapshot)
        {
            if (snapshot.Screen == ApplicationScreen.Outcome)
            {
                GUILayout.Label("Outcome: " + snapshot.Detail);
                if (GUILayout.Button("Return to Frontend")) Report(_flow.ReturnFromOutcome());
                return;
            }
            if (snapshot.Screen == ApplicationScreen.InGameMenu || snapshot.Screen == ApplicationScreen.Settings)
            {
                if (snapshot.Screen == ApplicationScreen.Settings)
                {
                    _masterVolume = GUILayout.HorizontalSlider(_masterVolume, 0f, 1f);
                    GUILayout.Label("Master volume " + _masterVolume.ToString("0.00"));
                    if (GUILayout.Button("Apply")) Report(_flow.ApplyPreferences(new UserPreferences(_masterVolume, _uiScale)));
                }
                if (GUILayout.Button("Back")) Report(_flow.CloseScreen());
                if (GUILayout.Button("Leave Game")) Report(_flow.RequestLeaveGame());
                return;
            }
            GUILayout.Label("Gameplay active — simulation remains authoritative and unpaused.");
            if (GUILayout.Button("Menu")) Report(_flow.OpenScreen(ApplicationScreen.InGameMenu));
        }

        private void Report(ApplicationOperationResult result)
        {
            _feedback = result.Succeeded ? string.Empty : result.Failure + ": " + result.Detail;
        }
    }
}
