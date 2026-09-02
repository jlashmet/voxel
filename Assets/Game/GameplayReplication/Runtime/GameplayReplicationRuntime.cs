using System;
using System.Collections.Generic;
using Game.GameplayReplication.Api;

namespace Game.GameplayReplication.Runtime
{
    public sealed class GameplayPublicationBuilder
    {
        private readonly IGameplayProjectionSource[] _sources;
        private GameplayRevision _revision;

        public GameplayPublicationBuilder(IEnumerable<IGameplayProjectionSource> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var copy = new List<IGameplayProjectionSource>(sources);
            copy.Sort((left, right) => left.Descriptor.Id.CompareTo(right.Descriptor.Id));
            for (int i = 1; i < copy.Count; i++)
            {
                if (copy[i - 1].Descriptor.Id == copy[i].Descriptor.Id)
                    throw new ArgumentException("Projection source ids must be unique: " + copy[i].Descriptor.Id, nameof(sources));
            }
            _sources = copy.ToArray();
            _revision = new GameplayRevision(0);
        }

        public GameplayRevision CurrentRevision => _revision;
        public GameplayPublication PublishSnapshot() => Publish(GameplayPublicationKind.Snapshot);
        public GameplayPublication PublishDelta() => Publish(GameplayPublicationKind.Delta);

        private GameplayPublication Publish(GameplayPublicationKind kind)
        {
            var states = new GameplayProjectionState[_sources.Length];
            for (int i = 0; i < _sources.Length; i++)
            {
                GameplayProjectionState state = _sources[i].Capture();
                if (state == null) throw new InvalidOperationException("Projection source returned null: " + _sources[i].Descriptor.Id);
                if (state.Descriptor.Id != _sources[i].Descriptor.Id || state.Descriptor.SchemaVersion != _sources[i].Descriptor.SchemaVersion)
                    throw new InvalidOperationException("Projection source returned a state with a descriptor that does not match its registration: " + _sources[i].Descriptor.Id);
                states[i] = state;
            }

            _revision = _revision.Next();
            return new GameplayPublication(_revision, kind, states);
        }
    }

    public sealed class GameplayReplicationReadState : IGameplayReplicationReadState, IGameplayPublicationSink
    {
        private readonly Dictionary<GameplayProjectionId, GameplayProjectionDescriptor> _knownDescriptors;
        private readonly Dictionary<GameplayProjectionId, GameplayProjectionState> _states;
        private GameplayRevision _revision;
        private GameplaySynchronizationState _synchronizationState;

        public GameplayReplicationReadState(IEnumerable<GameplayProjectionDescriptor> descriptors)
        {
            if (descriptors == null) throw new ArgumentNullException(nameof(descriptors));
            _knownDescriptors = new Dictionary<GameplayProjectionId, GameplayProjectionDescriptor>();
            foreach (GameplayProjectionDescriptor descriptor in descriptors)
            {
                if (descriptor == null) throw new ArgumentException("Descriptors cannot contain null.", nameof(descriptors));
                if (_knownDescriptors.ContainsKey(descriptor.Id))
                    throw new ArgumentException("Projection descriptor ids must be unique: " + descriptor.Id, nameof(descriptors));
                _knownDescriptors.Add(descriptor.Id, descriptor);
            }
            _states = new Dictionary<GameplayProjectionId, GameplayProjectionState>();
            _revision = new GameplayRevision(0);
            _synchronizationState = GameplaySynchronizationState.Empty;
        }

        public GameplayRevision Revision => _revision;
        public GameplaySynchronizationState SynchronizationState => _synchronizationState;
        public bool GameplayReady
        {
            get
            {
                if (_synchronizationState != GameplaySynchronizationState.Synchronized) return false;
                foreach (GameplayProjectionDescriptor descriptor in _knownDescriptors.Values)
                {
                    if (!descriptor.RequiredForGameplayReady) continue;
                    if (!_states.TryGetValue(descriptor.Id, out GameplayProjectionState state)) return false;
                    if (state.Descriptor.SchemaVersion != descriptor.SchemaVersion) return false;
                }
                return true;
            }
        }

        public bool TryGetProjection(GameplayProjectionId id, out GameplayProjectionState state) => _states.TryGetValue(id, out state);

        public GameplayApplyResult Apply(GameplayPublication publication)
        {
            if (publication == null) throw new ArgumentNullException(nameof(publication));
            if (publication.Revision.CompareTo(_revision) <= 0)
                return GameplayApplyResult.DuplicateOrStale;

            if (!AreCompatible(publication))
            {
                _synchronizationState = GameplaySynchronizationState.RepairRequired;
                return GameplayApplyResult.IncompatibleProjection;
            }

            if (publication.Kind == GameplayPublicationKind.Delta)
            {
                if (_synchronizationState != GameplaySynchronizationState.Synchronized || publication.Revision != _revision.Next())
                {
                    _synchronizationState = GameplaySynchronizationState.RepairRequired;
                    return GameplayApplyResult.GapDetected;
                }

                foreach (GameplayProjectionState state in publication.Projections)
                    _states[state.Descriptor.Id] = state;
            }
            else
            {
                _states.Clear();
                foreach (GameplayProjectionState state in publication.Projections)
                    _states[state.Descriptor.Id] = state;
            }

            _revision = publication.Revision;
            _synchronizationState = GameplaySynchronizationState.Synchronized;
            return GameplayApplyResult.Applied;
        }

        private bool AreCompatible(GameplayPublication publication)
        {
            foreach (GameplayProjectionState state in publication.Projections)
            {
                if (_knownDescriptors.TryGetValue(state.Descriptor.Id, out GameplayProjectionDescriptor expected) && expected.SchemaVersion != state.Descriptor.SchemaVersion)
                    return false;
            }
            return true;
        }
    }
}
