using System;
using System.Collections.Generic;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct WorldObjectStateDelta
    {
        public WorldObjectId Id;
        public WorldObjectStateFlags State;
        public int RuntimeValue0;
        public int RuntimeValue1;

        public bool IsWellFormed => Id.Value != 0;
    }

    public struct WorldObjectResolvedState
    {
        public WorldObjectDescriptor Descriptor;
        public WorldObjectStateFlags State;
        public int RuntimeValue0;
        public int RuntimeValue1;

        public bool IsDestroyed => (State & WorldObjectStateFlags.Destroyed) != 0;
        public bool IsOpen => (State & WorldObjectStateFlags.Open) != 0;
        public bool IsLocked => (State & WorldObjectStateFlags.Locked) != 0;
        public bool IsPowered => (State & WorldObjectStateFlags.Powered) != 0;
    }

    public sealed class WorldObjectStateStore
    {
        private readonly Dictionary<WorldObjectId, WorldObjectStateDelta> _deltas = new Dictionary<WorldObjectId, WorldObjectStateDelta>();

        public int Count => _deltas.Count;

        public bool TryGet(WorldObjectId id, out WorldObjectStateDelta delta) => _deltas.TryGetValue(id, out delta);

        public void Set(in WorldObjectStateDelta delta)
        {
            if (!delta.IsWellFormed) throw new ArgumentException("Invalid world object delta.", nameof(delta));
            _deltas[delta.Id] = delta;
        }

        public void Remove(WorldObjectId id) => _deltas.Remove(id);
        public void Clear() => _deltas.Clear();

        public WorldObjectStateDelta[] Snapshot()
        {
            var result = new WorldObjectStateDelta[_deltas.Count];
            int i = 0;
            foreach (var pair in _deltas) result[i++] = pair.Value;
            return result;
        }
    }

    public static class WorldObjectStateResolver
    {
        public static WorldObjectResolvedState Resolve(in WorldObjectDescriptor descriptor, WorldObjectStateStore store)
        {
            var resolved = new WorldObjectResolvedState
            {
                Descriptor = descriptor,
                State = descriptor.DefaultState,
            };

            if (store != null && store.TryGet(descriptor.Id, out WorldObjectStateDelta delta))
            {
                resolved.State = delta.State;
                resolved.RuntimeValue0 = delta.RuntimeValue0;
                resolved.RuntimeValue1 = delta.RuntimeValue1;
            }
            return resolved;
        }
    }

    public static class WorldObjectActions
    {
        public static bool TryApply(in WorldObjectResolvedState current, WorldObjectAction action, int argument,
            out WorldObjectStateDelta delta, out WorldObjectSignal emitted)
        {
            delta = new WorldObjectStateDelta
            {
                Id = current.Descriptor.Id,
                State = current.State,
                RuntimeValue0 = current.RuntimeValue0,
                RuntimeValue1 = current.RuntimeValue1,
            };
            emitted = WorldObjectSignal.None;

            if ((current.State & WorldObjectStateFlags.Destroyed) != 0) return false;

            switch (action)
            {
                case WorldObjectAction.Open:
                    if ((current.State & (WorldObjectStateFlags.Locked | WorldObjectStateFlags.Disabled)) != 0) return false;
                    delta.State |= WorldObjectStateFlags.Open;
                    emitted = WorldObjectSignal.Opened;
                    return true;
                case WorldObjectAction.Close:
                    delta.State &= ~WorldObjectStateFlags.Open;
                    emitted = WorldObjectSignal.Closed;
                    return true;
                case WorldObjectAction.Lock:
                    if ((current.Descriptor.Capabilities & WorldObjectCapabilities.Lockable) == 0) return false;
                    delta.State |= WorldObjectStateFlags.Locked;
                    return true;
                case WorldObjectAction.Unlock:
                    delta.State &= ~WorldObjectStateFlags.Locked;
                    return true;
                case WorldObjectAction.Activate:
                    delta.State |= WorldObjectStateFlags.Active;
                    emitted = WorldObjectSignal.Activated;
                    return true;
                case WorldObjectAction.Deactivate:
                    delta.State &= ~WorldObjectStateFlags.Active;
                    emitted = WorldObjectSignal.Deactivated;
                    return true;
                case WorldObjectAction.Toggle:
                    delta.State ^= WorldObjectStateFlags.Active;
                    emitted = WorldObjectSignal.Toggled;
                    return true;
                case WorldObjectAction.Enable:
                    delta.State &= ~WorldObjectStateFlags.Disabled;
                    return true;
                case WorldObjectAction.Disable:
                    delta.State |= WorldObjectStateFlags.Disabled;
                    return true;
                case WorldObjectAction.Trigger:
                    delta.State |= WorldObjectStateFlags.Triggered;
                    emitted = WorldObjectSignal.Activated;
                    return true;
                case WorldObjectAction.Reset:
                    delta.State &= ~(WorldObjectStateFlags.Triggered | WorldObjectStateFlags.Active);
                    return true;
                case WorldObjectAction.PowerOn:
                    delta.State |= WorldObjectStateFlags.Powered;
                    emitted = WorldObjectSignal.Powered;
                    return true;
                case WorldObjectAction.PowerOff:
                    delta.State &= ~WorldObjectStateFlags.Powered;
                    emitted = WorldObjectSignal.Unpowered;
                    return true;
                case WorldObjectAction.Reveal:
                    delta.State &= ~WorldObjectStateFlags.Hidden;
                    return true;
                case WorldObjectAction.Hide:
                    delta.State |= WorldObjectStateFlags.Hidden;
                    return true;
                case WorldObjectAction.MoveToStop:
                    delta.RuntimeValue0 = argument;
                    delta.State |= WorldObjectStateFlags.Moving;
                    return true;
                default:
                    return false;
            }
        }
    }

    public sealed class WorldObjectSignalGraph
    {
        private readonly WorldObjectConnection[] _connections;

        public WorldObjectSignalGraph(WorldObjectConnection[] connections)
        {
            _connections = connections ?? Array.Empty<WorldObjectConnection>();
        }

        public int Route(WorldObjectId source, WorldObjectSignal signal, List<WorldObjectConnection> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            int count = 0;
            for (int i = 0; i < _connections.Length; i++)
            {
                if (_connections[i].Source == source && _connections[i].Signal == signal)
                {
                    output.Add(_connections[i]);
                    count++;
                }
            }
            return count;
        }
    }
}
