using System;
using System.Collections.Generic;
using Game.Outcomes.Api;
using Game.SessionOrchestration.Api;

namespace Game.SessionOrchestration.Runtime
{
    public enum SessionUpdatePhase
    {
        CommandIntake = 100,
        Interaction = 200,
        ProgressionAndStory = 300,
        Encounter = 400,
        Combat = 500,
        Replication = 600,
        Presentation = 700
    }

    public interface ISessionUpdateStep
    {
        SessionUpdatePhase Phase { get; }
        int Order { get; }
        string SemanticId { get; }
        void Tick(int elapsedMilliseconds);
    }

    public interface ISessionRuntimeGraph : IDisposable
    {
        bool GameplayBindingsReady { get; }
        IReadOnlyList<ISessionUpdateStep> UpdateSteps { get; }
        IGameOutcomeQuery OutcomeQuery { get; }
        void InitializeNewGame();
        void StartCommands();
        void StopCommands();
        void SettleAuthoritativeState();
        void DetachExternalAdapters();
    }

    public interface ISessionRuntimeGraphFactory
    {
        ISessionRuntimeGraph Compose(GameSessionIdentity identity);
    }

    public interface ISessionPersistenceBridge
    {
        void Restore(GameSessionIdentity identity, string restoreSourceId, ISessionRuntimeGraph graph);
        void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph);
    }

    public sealed class SessionCompositionException : Exception
    {
        public GameSessionFailure Failure { get; }

        public SessionCompositionException(GameSessionFailure failure, string message)
            : base(message)
        {
            if (failure == GameSessionFailure.None)
                throw new ArgumentException("Composition failure must be semantic.", nameof(failure));
            Failure = failure;
        }

        public SessionCompositionException(GameSessionFailure failure, string message, Exception innerException)
            : base(message, innerException)
        {
            if (failure == GameSessionFailure.None)
                throw new ArgumentException("Composition failure must be semantic.", nameof(failure));
            Failure = failure;
        }
    }

    /// <summary>
    /// Owns only lifecycle, readiness, deterministic update ordering, persistence hand-off, outcome
    /// observation and teardown. Domain rules remain in the composed subsystem graph.
    /// </summary>
    public sealed class GameSessionOrchestrator : IGameSessionControl
    {
        private sealed class StepComparer : IComparer<ISessionUpdateStep>
        {
            public int Compare(ISessionUpdateStep left, ISessionUpdateStep right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return -1;
                if (right == null) return 1;
                int phase = left.Phase.CompareTo(right.Phase);
                if (phase != 0) return phase;
                int order = left.Order.CompareTo(right.Order);
                if (order != 0) return order;
                return StringComparer.Ordinal.Compare(left.SemanticId, right.SemanticId);
            }
        }

        private static readonly IComparer<ISessionUpdateStep> UpdateStepComparer = new StepComparer();

        private readonly ISessionRuntimeGraphFactory _factory;
        private readonly ISessionPersistenceBridge _persistence;
        private readonly List<ISessionUpdateStep> _orderedSteps = new List<ISessionUpdateStep>();

        private ISessionRuntimeGraph _graph;
        private GameSessionStartRequest _startRequest;
        private ComposedSessionHandle _handle;
        private GameSessionLifecycle _lifecycle = GameSessionLifecycle.Uninitialized;
        private GameSessionFailure _failure;
        private string _diagnostic = string.Empty;
        private int _generation;

        public GameSessionOrchestrator(
            ISessionRuntimeGraphFactory factory,
            ISessionPersistenceBridge persistence = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _persistence = persistence;
        }

        public GameSessionSnapshot Snapshot => new GameSessionSnapshot(
            _lifecycle,
            (_lifecycle == GameSessionLifecycle.Ready
             || _lifecycle == GameSessionLifecycle.Running
             || _lifecycle == GameSessionLifecycle.Resolved)
            && _graph != null
            && _graph.GameplayBindingsReady,
            _handle,
            _failure,
            _diagnostic);

        public GameSessionOperationResult Prepare(GameSessionStartRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (_lifecycle != GameSessionLifecycle.Uninitialized
                && _lifecycle != GameSessionLifecycle.Stopped
                && _lifecycle != GameSessionLifecycle.Failed)
                return RejectInvalid("A session can only be composed from Uninitialized, Stopped, or Failed.");
            if (_graph != null)
                return RejectInvalid("A previous runtime graph still exists and must be shut down first.");

            _failure = GameSessionFailure.None;
            _diagnostic = string.Empty;
            _handle = null;
            _startRequest = request;
            _orderedSteps.Clear();
            _lifecycle = GameSessionLifecycle.Composing;

            try
            {
                _graph = _factory.Compose(request.Identity);
                if (_graph == null)
                    throw new SessionCompositionException(
                        GameSessionFailure.MissingDependency,
                        "Composition returned no runtime graph.");

                IReadOnlyList<ISessionUpdateStep> steps = _graph.UpdateSteps;
                if (steps == null)
                    throw new SessionCompositionException(
                        GameSessionFailure.CompositionFailed,
                        "Runtime graph returned no deterministic update-step collection.");
                for (int i = 0; i < steps.Count; i++)
                {
                    ISessionUpdateStep step = steps[i];
                    if (step == null || string.IsNullOrWhiteSpace(step.SemanticId))
                        throw new SessionCompositionException(
                            GameSessionFailure.CompositionFailed,
                            "Runtime graph contains an invalid update step at index " + i + ".");
                    _orderedSteps.Add(step);
                }
                _orderedSteps.Sort(UpdateStepComparer);
                EnsureUniqueStepOrder(_orderedSteps);

                if (request.Kind == GameSessionStartKind.Resume)
                {
                    if (_persistence == null)
                        throw new SessionCompositionException(
                            GameSessionFailure.MissingDependency,
                            "Resume requires a persistence bridge.");
                    try
                    {
                        _persistence.Restore(request.Identity, request.RestoreSourceId, _graph);
                    }
                    catch (SessionCompositionException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new SessionCompositionException(
                            GameSessionFailure.RestoreFailed,
                            "Persistence restore failed.",
                            exception);
                    }
                }

                if (!_graph.GameplayBindingsReady)
                    throw new SessionCompositionException(
                        GameSessionFailure.BindingsNotReady,
                        "Required world/session/replication/player bindings are not ready.");

                _generation++;
                _handle = new ComposedSessionHandle(request.Identity, _generation);
                _lifecycle = GameSessionLifecycle.Ready;
                return GameSessionOperationResult.Success(_handle);
            }
            catch (SessionCompositionException exception)
            {
                return FailComposition(exception.Failure, exception.Message);
            }
            catch (Exception exception)
            {
                return FailComposition(GameSessionFailure.CompositionFailed, exception.Message);
            }
        }

        public GameSessionOperationResult EnterRunning()
        {
            if (_lifecycle != GameSessionLifecycle.Ready || _graph == null || _startRequest == null)
                return RejectInvalid("Only a Ready composed session may enter Running.");

            try
            {
                // Resume intentionally does not replay new-game initialization. The persistence bridge
                // restores current authority state through the same graph composed above.
                if (_startRequest.Kind == GameSessionStartKind.NewGame)
                    _graph.InitializeNewGame();
                _graph.StartCommands();
                _lifecycle = GameSessionLifecycle.Running;
                return GameSessionOperationResult.Success(_handle);
            }
            catch (Exception exception)
            {
                _lifecycle = GameSessionLifecycle.Failed;
                _failure = GameSessionFailure.StartupFailed;
                _diagnostic = exception.Message;
                return GameSessionOperationResult.Reject(_failure, _diagnostic);
            }
        }

        public GameSessionOperationResult Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (_lifecycle != GameSessionLifecycle.Running || _graph == null)
                return RejectInvalid("Only a Running session may update.");

            try
            {
                for (int i = 0; i < _orderedSteps.Count; i++)
                    _orderedSteps[i].Tick(elapsedMilliseconds);

                IGameOutcomeQuery outcomeQuery = _graph.OutcomeQuery;
                if (outcomeQuery != null
                    && outcomeQuery.Snapshot().Lifecycle == GameOutcomeLifecycle.Resolved)
                {
                    _graph.StopCommands();
                    _lifecycle = GameSessionLifecycle.Resolved;
                }
                return GameSessionOperationResult.Success(_handle);
            }
            catch (Exception exception)
            {
                _lifecycle = GameSessionLifecycle.Failed;
                _failure = GameSessionFailure.StartupFailed;
                _diagnostic = "Runtime update failed: " + exception.Message;
                return GameSessionOperationResult.Reject(_failure, _diagnostic);
            }
        }

        public GameSessionOperationResult Capture()
        {
            if (_lifecycle != GameSessionLifecycle.Ready
                && _lifecycle != GameSessionLifecycle.Running
                && _lifecycle != GameSessionLifecycle.Resolved)
                return RejectInvalid("Only a composed Ready, Running, or Resolved session can be captured.");
            if (_graph == null || _handle == null)
                return RejectInvalid("No composed graph is available to capture.");
            if (_persistence == null)
                return GameSessionOperationResult.Reject(
                    GameSessionFailure.CaptureUnavailable,
                    "No persistence bridge is configured.");

            try
            {
                _persistence.Capture(_handle.Identity, _graph);
                return GameSessionOperationResult.Success(_handle);
            }
            catch (Exception exception)
            {
                return GameSessionOperationResult.Reject(
                    GameSessionFailure.CaptureFailed,
                    "Persistence capture failed: " + exception.Message);
            }
        }

        public GameSessionOperationResult Shutdown()
        {
            if (_lifecycle == GameSessionLifecycle.Composing || _lifecycle == GameSessionLifecycle.ShuttingDown)
                return RejectInvalid("Shutdown cannot re-enter partial composition or teardown.");
            if (_graph == null)
            {
                if (_lifecycle == GameSessionLifecycle.Uninitialized
                    || _lifecycle == GameSessionLifecycle.Stopped)
                    return GameSessionOperationResult.Success(_handle);
                _lifecycle = GameSessionLifecycle.Stopped;
                return GameSessionOperationResult.Success(_handle);
            }

            _lifecycle = GameSessionLifecycle.ShuttingDown;
            Exception firstFailure = null;
            TryTeardown(() => _graph.StopCommands(), ref firstFailure);
            TryTeardown(() => _graph.SettleAuthoritativeState(), ref firstFailure);
            TryTeardown(() => _graph.DetachExternalAdapters(), ref firstFailure);
            TryTeardown(() => _graph.Dispose(), ref firstFailure);
            _graph = null;
            _orderedSteps.Clear();
            _startRequest = null;

            if (firstFailure != null)
            {
                _lifecycle = GameSessionLifecycle.Failed;
                _failure = GameSessionFailure.ShutdownFailed;
                _diagnostic = firstFailure.Message;
                return GameSessionOperationResult.Reject(_failure, _diagnostic);
            }

            _lifecycle = GameSessionLifecycle.Stopped;
            _failure = GameSessionFailure.None;
            _diagnostic = string.Empty;
            return GameSessionOperationResult.Success(_handle);
        }

        private GameSessionOperationResult FailComposition(GameSessionFailure failure, string diagnostic)
        {
            if (_graph != null)
            {
                try { _graph.Dispose(); }
                catch { }
            }
            _graph = null;
            _orderedSteps.Clear();
            _startRequest = null;
            _handle = null;
            _lifecycle = GameSessionLifecycle.Failed;
            _failure = failure;
            _diagnostic = diagnostic ?? string.Empty;
            return GameSessionOperationResult.Reject(_failure, _diagnostic);
        }

        private GameSessionOperationResult RejectInvalid(string diagnostic) =>
            GameSessionOperationResult.Reject(GameSessionFailure.InvalidState, diagnostic);

        private static void EnsureUniqueStepOrder(IReadOnlyList<ISessionUpdateStep> steps)
        {
            for (int i = 1; i < steps.Count; i++)
            {
                ISessionUpdateStep previous = steps[i - 1];
                ISessionUpdateStep current = steps[i];
                if (previous.Phase == current.Phase
                    && previous.Order == current.Order
                    && string.Equals(previous.SemanticId, current.SemanticId, StringComparison.Ordinal))
                    throw new SessionCompositionException(
                        GameSessionFailure.CompositionFailed,
                        "Duplicate deterministic update step '" + current.SemanticId + "'.");
            }
        }

        private static void TryTeardown(Action action, ref Exception firstFailure)
        {
            try { action(); }
            catch (Exception exception)
            {
                if (firstFailure == null) firstFailure = exception;
            }
        }
    }
}
