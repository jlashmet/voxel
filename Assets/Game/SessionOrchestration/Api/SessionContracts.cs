using System;

namespace Game.SessionOrchestration.Api
{
    public enum GameSessionLifecycle
    {
        Uninitialized = 0,
        Composing = 1,
        Ready = 2,
        Running = 3,
        Resolved = 4,
        ShuttingDown = 5,
        Stopped = 6,
        Failed = 7
    }

    public enum GameSessionStartKind
    {
        NewGame = 0,
        Resume = 1
    }

    public enum GameSessionFailure
    {
        None = 0,
        InvalidState = 1,
        MissingDependency = 2,
        CompositionFailed = 3,
        RestoreFailed = 4,
        BindingsNotReady = 5,
        StartupFailed = 6,
        CaptureUnavailable = 7,
        CaptureFailed = 8,
        ShutdownFailed = 9
    }

    public sealed class GameSessionIdentity : IEquatable<GameSessionIdentity>
    {
        public string CampaignId { get; }
        public string WorldId { get; }
        public string SessionId { get; }
        public string ConfigurationId { get; }

        public GameSessionIdentity(
            string campaignId,
            string worldId,
            string sessionId,
            string configurationId)
        {
            CampaignId = RequireId(campaignId, nameof(campaignId));
            WorldId = RequireId(worldId, nameof(worldId));
            SessionId = RequireId(sessionId, nameof(sessionId));
            ConfigurationId = RequireId(configurationId, nameof(configurationId));
        }

        public bool Equals(GameSessionIdentity other)
        {
            return other != null
                && string.Equals(CampaignId, other.CampaignId, StringComparison.Ordinal)
                && string.Equals(WorldId, other.WorldId, StringComparison.Ordinal)
                && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal)
                && string.Equals(ConfigurationId, other.ConfigurationId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => Equals(obj as GameSessionIdentity);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(CampaignId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(WorldId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(SessionId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ConfigurationId);
                return hash;
            }
        }

        public override string ToString() =>
            CampaignId + "/" + WorldId + "/" + SessionId + "@" + ConfigurationId;

        private static string RequireId(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Semantic session ids must be non-empty.", paramName);
            return value;
        }
    }

    public sealed class GameSessionStartRequest
    {
        public GameSessionIdentity Identity { get; }
        public GameSessionStartKind Kind { get; }
        public string RestoreSourceId { get; }

        private GameSessionStartRequest(
            GameSessionIdentity identity,
            GameSessionStartKind kind,
            string restoreSourceId)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Kind = kind;
            RestoreSourceId = restoreSourceId ?? string.Empty;
            if (kind == GameSessionStartKind.Resume && string.IsNullOrWhiteSpace(RestoreSourceId))
                throw new ArgumentException("Resume requires a semantic restore source id.", nameof(restoreSourceId));
            if (kind == GameSessionStartKind.NewGame && RestoreSourceId.Length != 0)
                throw new ArgumentException("New game cannot specify a restore source.", nameof(restoreSourceId));
        }

        public static GameSessionStartRequest NewGame(GameSessionIdentity identity) =>
            new GameSessionStartRequest(identity, GameSessionStartKind.NewGame, string.Empty);

        public static GameSessionStartRequest Resume(GameSessionIdentity identity, string restoreSourceId) =>
            new GameSessionStartRequest(identity, GameSessionStartKind.Resume, restoreSourceId);
    }

    public sealed class ComposedSessionHandle
    {
        public GameSessionIdentity Identity { get; }
        public int Generation { get; }

        public ComposedSessionHandle(GameSessionIdentity identity, int generation)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Generation = generation;
        }
    }

    public readonly struct GameSessionSnapshot
    {
        public GameSessionLifecycle Lifecycle { get; }
        public bool GameplayReady { get; }
        public ComposedSessionHandle Handle { get; }
        public GameSessionFailure Failure { get; }
        public string Diagnostic { get; }

        public GameSessionSnapshot(
            GameSessionLifecycle lifecycle,
            bool gameplayReady,
            ComposedSessionHandle handle,
            GameSessionFailure failure,
            string diagnostic)
        {
            Lifecycle = lifecycle;
            GameplayReady = gameplayReady;
            Handle = handle;
            Failure = failure;
            Diagnostic = diagnostic ?? string.Empty;
        }
    }

    public readonly struct GameSessionOperationResult
    {
        public bool Succeeded { get; }
        public GameSessionFailure Failure { get; }
        public string Diagnostic { get; }
        public ComposedSessionHandle Handle { get; }

        private GameSessionOperationResult(
            bool succeeded,
            GameSessionFailure failure,
            string diagnostic,
            ComposedSessionHandle handle)
        {
            Succeeded = succeeded;
            Failure = failure;
            Diagnostic = diagnostic ?? string.Empty;
            Handle = handle;
        }

        public static GameSessionOperationResult Success(ComposedSessionHandle handle = null) =>
            new GameSessionOperationResult(true, GameSessionFailure.None, string.Empty, handle);

        public static GameSessionOperationResult Reject(GameSessionFailure failure, string diagnostic) =>
            new GameSessionOperationResult(false, failure, diagnostic, null);
    }

    public interface IGameSessionControl
    {
        GameSessionSnapshot Snapshot { get; }
        GameSessionOperationResult Prepare(GameSessionStartRequest request);
        GameSessionOperationResult EnterRunning();
        GameSessionOperationResult Tick(int elapsedMilliseconds);
        GameSessionOperationResult Capture();
        GameSessionOperationResult Shutdown();
    }
}
