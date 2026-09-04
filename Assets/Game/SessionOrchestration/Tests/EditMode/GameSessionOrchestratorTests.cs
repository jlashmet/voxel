using System;
using System.Collections.Generic;
using Game.Outcomes.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using NUnit.Framework;

namespace Game.SessionOrchestration.Tests
{
    public sealed class GameSessionOrchestratorTests
    {
        private static GameSessionIdentity Identity(string session = "fixture-session") =>
            new GameSessionIdentity("fixture-campaign", "fixture-world", session, "fixture-config");

        [Test]
        public void NewRun_ComposesReadyRunningAndUsesDeterministicUpdateOrder()
        {
            var trace = new List<string>();
            var graph = new FixtureGraph(trace, true,
                new FixtureStep(SessionUpdatePhase.Combat, 0, "combat", trace),
                new FixtureStep(SessionUpdatePhase.Interaction, 5, "interaction-b", trace),
                new FixtureStep(SessionUpdatePhase.Interaction, 1, "interaction-a", trace),
                new FixtureStep(SessionUpdatePhase.ProgressionAndStory, 0, "story", trace));
            var factory = new FixtureFactory(graph);
            var runtime = new GameSessionOrchestrator(factory);

            GameSessionOperationResult prepared = runtime.Prepare(GameSessionStartRequest.NewGame(Identity()));
            Assert.That(prepared.Succeeded, Is.True);
            Assert.That(runtime.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Ready));
            Assert.That(runtime.Snapshot.GameplayReady, Is.True);
            Assert.That(factory.ComposeCount, Is.EqualTo(1));

            Assert.That(runtime.EnterRunning().Succeeded, Is.True);
            Assert.That(graph.InitializeNewGameCount, Is.EqualTo(1));
            Assert.That(graph.CommandsStarted, Is.True);
            Assert.That(runtime.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Running));

            Assert.That(runtime.Tick(16).Succeeded, Is.True);
            CollectionAssert.AreEqual(
                new[] { "interaction-a:16", "interaction-b:16", "story:16", "combat:16" },
                trace);
        }

        [Test]
        public void Resume_UsesSameFactoryPathRestoresBeforeRunningAndDoesNotReplayNewGame()
        {
            var graph = new FixtureGraph(new List<string>(), true);
            var factory = new FixtureFactory(graph);
            var persistence = new FixturePersistence();
            var runtime = new GameSessionOrchestrator(factory, persistence);
            GameSessionIdentity identity = Identity("resume-session");

            GameSessionOperationResult prepared = runtime.Prepare(
                GameSessionStartRequest.Resume(identity, "save-slot-7"));

            Assert.That(prepared.Succeeded, Is.True);
            Assert.That(factory.ComposeCount, Is.EqualTo(1));
            Assert.That(factory.LastIdentity, Is.SameAs(identity));
            Assert.That(persistence.RestoreCount, Is.EqualTo(1));
            Assert.That(persistence.LastRestoreSource, Is.EqualTo("save-slot-7"));
            Assert.That(graph.InitializeNewGameCount, Is.EqualTo(0));

            Assert.That(runtime.EnterRunning().Succeeded, Is.True);
            Assert.That(graph.InitializeNewGameCount, Is.EqualTo(0),
                "Resume must restore current authority state without replaying new-game one-shots.");
            Assert.That(graph.CommandsStarted, Is.True);
        }

        [Test]
        public void ResumeWithoutPersistence_FailsSemanticallyAndDisposesPartialGraph()
        {
            var graph = new FixtureGraph(new List<string>(), true);
            var runtime = new GameSessionOrchestrator(new FixtureFactory(graph));

            GameSessionOperationResult result = runtime.Prepare(
                GameSessionStartRequest.Resume(Identity(), "save-slot-1"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(GameSessionFailure.MissingDependency));
            Assert.That(runtime.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Failed));
            Assert.That(graph.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingGameplayBindingsAndPrematureCommandsFailDeterministically()
        {
            var notReady = new FixtureGraph(new List<string>(), false);
            var runtime = new GameSessionOrchestrator(new FixtureFactory(notReady));

            Assert.That(runtime.EnterRunning().Failure, Is.EqualTo(GameSessionFailure.InvalidState));
            Assert.That(runtime.Tick(1).Failure, Is.EqualTo(GameSessionFailure.InvalidState));

            GameSessionOperationResult result = runtime.Prepare(GameSessionStartRequest.NewGame(Identity()));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(GameSessionFailure.BindingsNotReady));
            Assert.That(notReady.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ResolvedOutcomeStopsCommandsWithoutDecidingOutcome()
        {
            var outcome = new FixtureOutcomeQuery();
            var graph = new FixtureGraph(new List<string>(), true) { Outcome = outcome };
            var runtime = new GameSessionOrchestrator(new FixtureFactory(graph));
            Assert.That(runtime.Prepare(GameSessionStartRequest.NewGame(Identity())).Succeeded, Is.True);
            Assert.That(runtime.EnterRunning().Succeeded, Is.True);

            outcome.Resolve(GameOutcomeDisposition.Success, "fixture:won");
            Assert.That(runtime.Tick(0).Succeeded, Is.True);

            Assert.That(runtime.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Resolved));
            Assert.That(graph.StopCommandsCount, Is.EqualTo(1));
            Assert.That(outcome.Snapshot().Outcome, Is.EqualTo(new OutcomeRef("fixture:won")));
            Assert.That(runtime.Tick(0).Failure, Is.EqualTo(GameSessionFailure.InvalidState));
        }

        [Test]
        public void CaptureDelegatesToPersistenceWithoutSerializingInOrchestrator()
        {
            var graph = new FixtureGraph(new List<string>(), true);
            var persistence = new FixturePersistence();
            var runtime = new GameSessionOrchestrator(new FixtureFactory(graph), persistence);
            Assert.That(runtime.Prepare(GameSessionStartRequest.NewGame(Identity())).Succeeded, Is.True);
            Assert.That(runtime.EnterRunning().Succeeded, Is.True);

            Assert.That(runtime.Capture().Succeeded, Is.True);
            Assert.That(persistence.CaptureCount, Is.EqualTo(1));
            Assert.That(persistence.LastGraph, Is.SameAs(graph));
        }

        [Test]
        public void Shutdown_IsOrderedDisposesOnceAndAllowsCleanRecreate()
        {
            var trace = new List<string>();
            var first = new FixtureGraph(trace, true);
            var second = new FixtureGraph(trace, true);
            var factory = new SequenceFactory(first, second);
            var runtime = new GameSessionOrchestrator(factory);

            GameSessionOperationResult firstPrepare = runtime.Prepare(
                GameSessionStartRequest.NewGame(Identity("first")));
            Assert.That(firstPrepare.Succeeded, Is.True);
            Assert.That(firstPrepare.Handle.Generation, Is.EqualTo(1));
            Assert.That(runtime.EnterRunning().Succeeded, Is.True);
            trace.Clear();

            Assert.That(runtime.Shutdown().Succeeded, Is.True);
            CollectionAssert.AreEqual(new[] { "stop", "settle", "detach", "dispose" }, trace);
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.Lifecycle, Is.EqualTo(GameSessionLifecycle.Stopped));
            Assert.That(runtime.Shutdown().Succeeded, Is.True);
            Assert.That(first.DisposeCount, Is.EqualTo(1));

            GameSessionOperationResult secondPrepare = runtime.Prepare(
                GameSessionStartRequest.NewGame(Identity("second")));
            Assert.That(secondPrepare.Succeeded, Is.True);
            Assert.That(secondPrepare.Handle.Generation, Is.EqualTo(2));
            Assert.That(factory.ComposeCount, Is.EqualTo(2));
        }

        private sealed class FixtureStep : ISessionUpdateStep
        {
            private readonly List<string> _trace;
            public SessionUpdatePhase Phase { get; }
            public int Order { get; }
            public string SemanticId { get; }

            public FixtureStep(SessionUpdatePhase phase, int order, string semanticId, List<string> trace)
            {
                Phase = phase;
                Order = order;
                SemanticId = semanticId;
                _trace = trace;
            }

            public void Tick(int elapsedMilliseconds) =>
                _trace.Add(SemanticId + ":" + elapsedMilliseconds);
        }

        private sealed class FixtureGraph : ISessionRuntimeGraph
        {
            private readonly List<string> _trace;
            private readonly IReadOnlyList<ISessionUpdateStep> _steps;
            public bool GameplayBindingsReady { get; }
            public IGameOutcomeQuery OutcomeQuery => Outcome;
            public FixtureOutcomeQuery Outcome { get; set; }
            public int InitializeNewGameCount { get; private set; }
            public bool CommandsStarted { get; private set; }
            public int StopCommandsCount { get; private set; }
            public int DisposeCount { get; private set; }
            public IReadOnlyList<ISessionUpdateStep> UpdateSteps => _steps;

            public FixtureGraph(List<string> trace, bool ready, params ISessionUpdateStep[] steps)
            {
                _trace = trace;
                GameplayBindingsReady = ready;
                _steps = Array.AsReadOnly(steps ?? new ISessionUpdateStep[0]);
            }

            public void InitializeNewGame() { InitializeNewGameCount++; }
            public void StartCommands() { CommandsStarted = true; }
            public void StopCommands() { CommandsStarted = false; StopCommandsCount++; _trace.Add("stop"); }
            public void SettleAuthoritativeState() { _trace.Add("settle"); }
            public void DetachExternalAdapters() { _trace.Add("detach"); }
            public void Dispose() { DisposeCount++; _trace.Add("dispose"); }
        }

        private sealed class FixtureFactory : ISessionRuntimeGraphFactory
        {
            private readonly ISessionRuntimeGraph _graph;
            public int ComposeCount { get; private set; }
            public GameSessionIdentity LastIdentity { get; private set; }

            public FixtureFactory(ISessionRuntimeGraph graph) { _graph = graph; }

            public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
            {
                ComposeCount++;
                LastIdentity = identity;
                return _graph;
            }
        }

        private sealed class SequenceFactory : ISessionRuntimeGraphFactory
        {
            private readonly Queue<ISessionRuntimeGraph> _graphs;
            public int ComposeCount { get; private set; }

            public SequenceFactory(params ISessionRuntimeGraph[] graphs) =>
                _graphs = new Queue<ISessionRuntimeGraph>(graphs);

            public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
            {
                ComposeCount++;
                return _graphs.Dequeue();
            }
        }

        private sealed class FixturePersistence : ISessionPersistenceBridge
        {
            public int RestoreCount { get; private set; }
            public int CaptureCount { get; private set; }
            public string LastRestoreSource { get; private set; }
            public ISessionRuntimeGraph LastGraph { get; private set; }

            public void Restore(GameSessionIdentity identity, string restoreSourceId, ISessionRuntimeGraph graph)
            {
                RestoreCount++;
                LastRestoreSource = restoreSourceId;
                LastGraph = graph;
            }

            public void Capture(GameSessionIdentity identity, ISessionRuntimeGraph graph)
            {
                CaptureCount++;
                LastGraph = graph;
            }
        }

        private sealed class FixtureOutcomeQuery : IGameOutcomeQuery
        {
            private GameOutcomeSnapshot _snapshot = new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Running,
                GameOutcomeDisposition.None,
                default(OutcomeRef),
                0);

            public GameOutcomeSnapshot Snapshot() => _snapshot;

            public void Resolve(GameOutcomeDisposition disposition, string outcome)
            {
                _snapshot = new GameOutcomeSnapshot(
                    GameOutcomeLifecycle.Resolved,
                    disposition,
                    new OutcomeRef(outcome),
                    _snapshot.Revision + 1);
            }
        }
    }
}
