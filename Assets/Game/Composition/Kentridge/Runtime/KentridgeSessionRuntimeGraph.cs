using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Composition.Kentridge.Api;
using Game.Composition.WorldBuilderWorldGen;
using Game.Cutscenes.Api;
using Game.Outcomes.Api;
using Game.Quests.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;

namespace Game.Composition.Kentridge.Runtime
{
    /// <summary>
    /// Kentridge composition adapter for the production SessionOrchestration graph. It reuses the
    /// existing campaign/world bootstrap and exposes only lifecycle hooks plus Kentridge-specific
    /// gameplay commands to the Kentridge composition root.
    /// </summary>
    public sealed class KentridgeSessionRuntimeGraphFactory : ISessionRuntimeGraphFactory
    {
        private readonly CampaignBlueprint _blueprint;
        private readonly KentridgeCampaignGenerationPlan _generation;
        private readonly KentridgeCampaignRealizationFacts _realizationFacts;
        private readonly IKentridgeCampaignActorHost _actors;
        private readonly ICutscenePresentation _presentation;
        private readonly IKentridgeCampaignSecretHost _secretHost;

        public KentridgeSessionRuntimeGraph Current { get; private set; }

        public KentridgeSessionRuntimeGraphFactory(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts realizationFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IKentridgeCampaignSecretHost secretHost = null)
        {
            _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            _generation = generation ?? throw new ArgumentNullException(nameof(generation));
            _realizationFacts = realizationFacts ?? throw new ArgumentNullException(nameof(realizationFacts));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _secretHost = secretHost;
        }

        public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (Current != null && !Current.IsDisposed)
                throw new SessionCompositionException(
                    GameSessionFailure.CompositionFailed,
                    "Kentridge already has a composed runtime graph. Shut it down before composing another run.");

            KentridgeCampaignSession session = KentridgeCampaignSessionBootstrap.CreateSession(
                _blueprint,
                _generation,
                _realizationFacts,
                _actors,
                _presentation,
                _secretHost);
            Current = new KentridgeSessionRuntimeGraph(session, OnDisposed);
            return Current;
        }

        private void OnDisposed(KentridgeSessionRuntimeGraph graph)
        {
            if (ReferenceEquals(Current, graph)) Current = null;
        }
    }

    public sealed class KentridgeSessionRuntimeGraph : ISessionRuntimeGraph
    {
        private sealed class CampaignUpdateStep : ISessionUpdateStep
        {
            private readonly KentridgeCampaignSession _session;

            public CampaignUpdateStep(KentridgeCampaignSession session) =>
                _session = session ?? throw new ArgumentNullException(nameof(session));

            public SessionUpdatePhase Phase => SessionUpdatePhase.ProgressionAndStory;
            public int Order => 0;
            public string SemanticId => "kentridge.campaign";
            public void Tick(int elapsedMilliseconds) => _session.Runtime.Tick(elapsedMilliseconds);
        }

        private readonly Action<KentridgeSessionRuntimeGraph> _disposed;
        private readonly IReadOnlyList<ISessionUpdateStep> _steps;
        private bool _commandsEnabled;

        public KentridgeCampaignSession Session { get; }
        public bool IsDisposed { get; private set; }
        public bool GameplayBindingsReady => !IsDisposed && Session != null;
        public IReadOnlyList<ISessionUpdateStep> UpdateSteps => _steps;
        public IGameOutcomeQuery OutcomeQuery => null;
        public int LastNewGameMatchedCount { get; private set; }

        internal KentridgeSessionRuntimeGraph(
            KentridgeCampaignSession session,
            Action<KentridgeSessionRuntimeGraph> disposed)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _disposed = disposed;
            _steps = Array.AsReadOnly<ISessionUpdateStep>(new ISessionUpdateStep[]
            {
                new CampaignUpdateStep(Session)
            });
        }

        public void InitializeNewGame()
        {
            ThrowIfDisposed();
            LastNewGameMatchedCount = Session.StartNewGame();
        }

        public void StartCommands()
        {
            ThrowIfDisposed();
            _commandsEnabled = true;
        }

        public void StopCommands()
        {
            _commandsEnabled = false;
        }

        public void SettleAuthoritativeState()
        {
            if (IsDisposed) return;
            Session.SynchronizeRewards();
        }

        public void DetachExternalAdapters()
        {
            // Actor/presentation/world resources are owned by the Kentridge composition root. The
            // graph only stops routing into them; their disposal remains after orchestration shutdown.
            _commandsEnabled = false;
        }

        public void InteractWithNpc(NpcRef npc)
        {
            RequireCommands();
            Session.Runtime.InteractWithNpc(npc);
        }

        public IReadOnlyList<QuestEvent> ObserveQuest(QuestObservation observation)
        {
            RequireCommands();
            return Session.ObserveQuest(observation);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            _commandsEnabled = false;
            IsDisposed = true;
            _disposed?.Invoke(this);
        }

        private void RequireCommands()
        {
            ThrowIfDisposed();
            if (!_commandsEnabled)
                throw new InvalidOperationException(
                    "Kentridge gameplay commands are unavailable until the composed session is Running.");
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(KentridgeSessionRuntimeGraph));
        }
    }
}
