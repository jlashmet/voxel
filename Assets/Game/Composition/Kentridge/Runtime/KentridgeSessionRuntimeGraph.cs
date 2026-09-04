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
    /// Composition extension point for production systems that are Unity-bound to the playable slice
    /// (for example the forest encounter/input/combat presentation). The extension is created from
    /// inside the session graph factory, so it participates in the same readiness/update/teardown path
    /// without forcing Unity dependencies into this engine-neutral assembly.
    /// </summary>
    public interface IKentridgeSessionRuntimeExtensionFactory
    {
        IKentridgeSessionRuntimeExtension Compose(
            GameSessionIdentity identity,
            IKentridgeCampaignActorHost actors);
    }

    public interface IKentridgeSessionRuntimeExtension : IDisposable
    {
        bool GameplayBindingsReady { get; }
        IReadOnlyList<ISessionUpdateStep> UpdateSteps { get; }
        void StartCommands();
        void StopCommands();
        void SettleAuthoritativeState();
        void DetachExternalAdapters();
    }

    /// <summary>
    /// Kentridge composition adapter for the production SessionOrchestration graph. It reuses the
    /// existing campaign/world bootstrap and composes optional Unity-bound gameplay extensions from a
    /// factory supplied by the playable composition root.
    /// </summary>
    public sealed class KentridgeSessionRuntimeGraphFactory : ISessionRuntimeGraphFactory
    {
        private readonly CampaignBlueprint _blueprint;
        private readonly KentridgeCampaignGenerationPlan _generation;
        private readonly KentridgeCampaignRealizationFacts _realizationFacts;
        private readonly IKentridgeCampaignActorHost _actors;
        private readonly ICutscenePresentation _presentation;
        private readonly IKentridgeCampaignSecretHost _secretHost;
        private readonly IKentridgeSessionRuntimeExtensionFactory _extensionFactory;

        public KentridgeSessionRuntimeGraph Current { get; private set; }

        public KentridgeSessionRuntimeGraphFactory(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts realizationFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IKentridgeCampaignSecretHost secretHost = null,
            IKentridgeSessionRuntimeExtensionFactory extensionFactory = null)
        {
            _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            _generation = generation ?? throw new ArgumentNullException(nameof(generation));
            _realizationFacts = realizationFacts ?? throw new ArgumentNullException(nameof(realizationFacts));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _secretHost = secretHost;
            _extensionFactory = extensionFactory;
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
            IKentridgeSessionRuntimeExtension extension = null;
            try
            {
                extension = _extensionFactory?.Compose(identity, _actors);
                Current = new KentridgeSessionRuntimeGraph(session, extension, OnDisposed);
                return Current;
            }
            catch
            {
                extension?.Dispose();
                throw;
            }
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
        private readonly IKentridgeSessionRuntimeExtension _extension;
        private readonly IReadOnlyList<ISessionUpdateStep> _steps;
        private bool _commandsEnabled;

        public KentridgeCampaignSession Session { get; }
        public bool IsDisposed { get; private set; }
        public bool GameplayBindingsReady =>
            !IsDisposed
            && Session != null
            && (_extension == null || _extension.GameplayBindingsReady);
        public IReadOnlyList<ISessionUpdateStep> UpdateSteps => _steps;
        public IGameOutcomeQuery OutcomeQuery => null;
        public int LastNewGameMatchedCount { get; private set; }

        internal KentridgeSessionRuntimeGraph(
            KentridgeCampaignSession session,
            IKentridgeSessionRuntimeExtension extension,
            Action<KentridgeSessionRuntimeGraph> disposed)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            _extension = extension;
            _disposed = disposed;

            var steps = new List<ISessionUpdateStep> { new CampaignUpdateStep(Session) };
            IReadOnlyList<ISessionUpdateStep> extensionSteps = extension?.UpdateSteps;
            if (extensionSteps != null)
            {
                for (int i = 0; i < extensionSteps.Count; i++)
                {
                    ISessionUpdateStep step = extensionSteps[i]
                        ?? throw new SessionCompositionException(
                            GameSessionFailure.CompositionFailed,
                            "Kentridge extension contains a null session update step.");
                    steps.Add(step);
                }
            }
            _steps = steps.AsReadOnly();
        }

        public void InitializeNewGame()
        {
            ThrowIfDisposed();
            LastNewGameMatchedCount = Session.StartNewGame();
        }

        public void StartCommands()
        {
            ThrowIfDisposed();
            _extension?.StartCommands();
            _commandsEnabled = true;
        }

        public void StopCommands()
        {
            _commandsEnabled = false;
            _extension?.StopCommands();
        }

        public void SettleAuthoritativeState()
        {
            if (IsDisposed) return;
            Session.SynchronizeRewards();
            _extension?.SettleAuthoritativeState();
        }

        public void DetachExternalAdapters()
        {
            _commandsEnabled = false;
            _extension?.DetachExternalAdapters();
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
            try
            {
                _extension?.Dispose();
            }
            finally
            {
                IsDisposed = true;
                _disposed?.Invoke(this);
            }
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
