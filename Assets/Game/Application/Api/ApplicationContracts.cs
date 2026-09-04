using System;
using System.Collections.Generic;
using Game.Input.Api;

namespace Game.Application.Api
{
    public enum ApplicationLifecycle : byte
    {
        Boot = 0,
        FrontEnd = 1,
        StartingSession = 2,
        InGame = 3,
        ReturningToFrontEnd = 4,
        Exiting = 5
    }

    public enum ApplicationScreen : byte
    {
        Boot = 0,
        MainMenu = 1,
        Continue = 2,
        Multiplayer = 3,
        Party = 4,
        Settings = 5,
        Loading = 6,
        Gameplay = 7,
        InGameMenu = 8,
        Outcome = 9,
        Error = 10
    }

    public enum ApplicationIntentKind : byte
    {
        NewGame = 0,
        Continue = 1,
        Host = 2,
        Join = 3,
        StartParty = 4,
        LeaveGame = 5,
        QuitApplication = 6
    }

    public enum ApplicationFailure : byte
    {
        None = 0,
        InvalidState = 1,
        Busy = 2,
        SaveUnavailable = 3,
        SessionFormationFailed = 4,
        PartyCommandRejected = 5,
        SessionPrepareFailed = 6,
        SessionStartFailed = 7,
        SessionUpdateFailed = 8,
        TeardownFailed = 9,
        InvalidPreferences = 10
    }

    public readonly struct ApplicationSessionDescriptor
    {
        public string CampaignId { get; }
        public string WorldId { get; }
        public string SessionId { get; }
        public string ConfigurationId { get; }

        public ApplicationSessionDescriptor(string campaignId, string worldId, string sessionId, string configurationId)
        {
            CampaignId = Require(campaignId, nameof(campaignId));
            WorldId = Require(worldId, nameof(worldId));
            SessionId = Require(sessionId, nameof(sessionId));
            ConfigurationId = Require(configurationId, nameof(configurationId));
        }

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Semantic application session id is required.", name);
            return value.Trim();
        }
    }

    public readonly struct ApplicationOperationResult
    {
        public bool Succeeded => Failure == ApplicationFailure.None;
        public ApplicationFailure Failure { get; }
        public string Detail { get; }

        private ApplicationOperationResult(ApplicationFailure failure, string detail)
        {
            Failure = failure;
            Detail = detail ?? string.Empty;
        }

        public static ApplicationOperationResult Success() => new ApplicationOperationResult(ApplicationFailure.None, string.Empty);
        public static ApplicationOperationResult Reject(ApplicationFailure failure, string detail)
        {
            if (failure == ApplicationFailure.None) throw new ArgumentException("Rejected result requires a failure.", nameof(failure));
            return new ApplicationOperationResult(failure, detail);
        }
    }

    public readonly struct ApplicationFlowSnapshot
    {
        public ApplicationLifecycle Lifecycle { get; }
        public ApplicationScreen Screen { get; }
        public bool GameplayReady { get; }
        public ApplicationFailure LastFailure { get; }
        public string Detail { get; }

        public ApplicationFlowSnapshot(
            ApplicationLifecycle lifecycle,
            ApplicationScreen screen,
            bool gameplayReady,
            ApplicationFailure lastFailure,
            string detail)
        {
            Lifecycle = lifecycle;
            Screen = screen;
            GameplayReady = gameplayReady;
            LastFailure = lastFailure;
            Detail = detail ?? string.Empty;
        }
    }

    public sealed class UserPreferences
    {
        private readonly InputBindingOverride[] _bindings;

        public float MasterVolume { get; }
        public float UiScale { get; }
        public IReadOnlyList<InputBindingOverride> BindingOverrides => _bindings;

        public UserPreferences(float masterVolume, float uiScale, IReadOnlyList<InputBindingOverride> bindingOverrides = null)
        {
            if (masterVolume < 0f || masterVolume > 1f) throw new ArgumentOutOfRangeException(nameof(masterVolume));
            if (uiScale < 0.75f || uiScale > 1.5f) throw new ArgumentOutOfRangeException(nameof(uiScale));
            MasterVolume = masterVolume;
            UiScale = uiScale;
            if (bindingOverrides == null)
            {
                _bindings = Array.Empty<InputBindingOverride>();
                return;
            }
            _bindings = new InputBindingOverride[bindingOverrides.Count];
            for (int i = 0; i < bindingOverrides.Count; i++) _bindings[i] = bindingOverrides[i];
        }

        public static UserPreferences Default => new UserPreferences(1f, 1f);
    }

    public interface IUserPreferencesStore
    {
        bool TryLoad(out UserPreferences preferences);
        void Save(UserPreferences preferences);
    }

    public interface IAudioPreferencesSink
    {
        void Apply(UserPreferences preferences);
    }

    public interface IApplicationExitPort
    {
        void RequestExit();
    }
}
