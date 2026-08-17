using System;
using System.Collections.Generic;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Executes interactions, persistent state changes and signal propagation for one generated world-object scene.
    /// Descriptors remain deterministic baseline data; only state deltas are retained at runtime/save time.
    /// </summary>
    public sealed class WorldObjectSceneRuntime
    {
        private const int MaxPropagationSteps = 256;

        private readonly Dictionary<WorldObjectId, WorldObjectDescriptor> _objects;
        private readonly WorldObjectSignalGraph _graph;
        private readonly WorldObjectStateStore _state;
        private readonly List<WorldObjectConnection> _routed = new List<WorldObjectConnection>(16);
        private readonly Queue<PendingSignal> _signals = new Queue<PendingSignal>(16);

        public WorldObjectSceneRuntime(WorldObjectDescriptor[] objects, WorldObjectConnection[] connections,
            WorldObjectStateStore state = null)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            _objects = new Dictionary<WorldObjectId, WorldObjectDescriptor>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                if (!objects[i].IsWellFormed)
                    throw new ArgumentException("Scene contains an invalid world-object descriptor.", nameof(objects));
                if (_objects.ContainsKey(objects[i].Id))
                    throw new ArgumentException("Scene contains duplicate world-object ids.", nameof(objects));
                _objects.Add(objects[i].Id, objects[i]);
            }
            _graph = new WorldObjectSignalGraph(connections);
            _state = state ?? new WorldObjectStateStore();
        }

        public int ObjectCount => _objects.Count;
        public WorldObjectStateStore StateStore => _state;

        public bool TryResolve(WorldObjectId id, out WorldObjectResolvedState resolved)
        {
            if (!_objects.TryGetValue(id, out WorldObjectDescriptor descriptor))
            {
                resolved = default;
                return false;
            }
            resolved = WorldObjectStateResolver.Resolve(in descriptor, _state);
            return true;
        }

        public bool TryInteract(WorldObjectId id, WorldObjectInteraction interaction,
            out WorldObjectInteractionResult result)
        {
            result = default;
            if (!TryResolve(id, out WorldObjectResolvedState current)) return false;
            if (!WorldObjectBehavior.TryInteract(in current, interaction, out result)) return false;
            if (result.Changed)
            {
                PrimeTimedReset(in current.Descriptor, ref result.Delta);
                _state.Set(in result.Delta);
            }
            if (result.Signal != WorldObjectSignal.None)
                Propagate(id, result.Signal);
            return true;
        }

        public bool TryApply(WorldObjectId id, WorldObjectAction action, int argument = 0)
        {
            if (!TryResolve(id, out WorldObjectResolvedState current)) return false;
            if (!WorldObjectActions.TryApply(in current, action, argument,
                    out WorldObjectStateDelta delta, out WorldObjectSignal emitted))
                return false;
            PrimeTimedReset(in current.Descriptor, ref delta);
            _state.Set(in delta);
            if (emitted != WorldObjectSignal.None)
                Propagate(id, emitted);
            return true;
        }

        /// <summary>
        /// Advances deterministic coarse runtime timers. A triggered object with Parameter0 > 0 uses that value
        /// as its reset delay in ticks. The timer is stored in the sparse state delta and therefore survives
        /// streaming/save boundaries without persisting frame-level animation state.
        /// </summary>
        public int Tick(int ticks = 1)
        {
            if (ticks <= 0) return 0;
            int changed = 0;
            foreach (var pair in _objects)
            {
                WorldObjectDescriptor descriptor = pair.Value;
                if (descriptor.Parameter0 <= 0) continue;
                WorldObjectResolvedState current = WorldObjectStateResolver.Resolve(in descriptor, _state);
                if ((current.State & WorldObjectStateFlags.Triggered) == 0) continue;

                int remaining = current.RuntimeValue1 > 0 ? current.RuntimeValue1 : descriptor.Parameter0;
                remaining -= ticks;
                var delta = new WorldObjectStateDelta
                {
                    Id = descriptor.Id,
                    State = current.State,
                    RuntimeValue0 = current.RuntimeValue0,
                    RuntimeValue1 = Math.Max(0, remaining),
                };
                if (remaining <= 0)
                {
                    delta.State &= ~(WorldObjectStateFlags.Triggered | WorldObjectStateFlags.Active);
                    delta.RuntimeValue1 = 0;
                }
                _state.Set(in delta);
                changed++;
            }
            return changed;
        }

        public WorldObjectStateDelta[] SnapshotState() => _state.Snapshot();

        private static void PrimeTimedReset(in WorldObjectDescriptor descriptor, ref WorldObjectStateDelta delta)
        {
            if (descriptor.Parameter0 > 0 &&
                (delta.State & WorldObjectStateFlags.Triggered) != 0 && delta.RuntimeValue1 <= 0)
                delta.RuntimeValue1 = descriptor.Parameter0;
        }

        private void Propagate(WorldObjectId source, WorldObjectSignal signal)
        {
            _signals.Clear();
            _signals.Enqueue(new PendingSignal(source, signal));
            int steps = 0;

            while (_signals.Count > 0 && steps++ < MaxPropagationSteps)
            {
                PendingSignal next = _signals.Dequeue();
                _routed.Clear();
                _graph.Route(next.Source, next.Signal, _routed);
                for (int i = 0; i < _routed.Count; i++)
                {
                    WorldObjectConnection connection = _routed[i];
                    if (!TryResolve(connection.Target, out WorldObjectResolvedState target)) continue;
                    if (!WorldObjectActions.TryApply(in target, connection.Action, connection.Argument,
                            out WorldObjectStateDelta delta, out WorldObjectSignal emitted))
                        continue;
                    PrimeTimedReset(in target.Descriptor, ref delta);
                    _state.Set(in delta);
                    if (emitted != WorldObjectSignal.None)
                        _signals.Enqueue(new PendingSignal(connection.Target, emitted));
                }
            }
        }

        private readonly struct PendingSignal
        {
            public readonly WorldObjectId Source;
            public readonly WorldObjectSignal Signal;
            public PendingSignal(WorldObjectId source, WorldObjectSignal signal)
            {
                Source = source;
                Signal = signal;
            }
        }
    }
}
