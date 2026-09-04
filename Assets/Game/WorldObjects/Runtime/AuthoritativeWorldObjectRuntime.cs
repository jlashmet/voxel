using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Characters.Api;
using Game.WorldObjects.Api;

namespace Game.WorldObjects.Runtime
{
    public sealed class WorldObjectRegistry : IWorldObjectRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<WorldObjectId, IWorldObjectBehavior> _objects =
            new Dictionary<WorldObjectId, IWorldObjectBehavior>();

        public bool TryRegister(IWorldObjectBehavior behavior)
        {
            if (behavior == null || !behavior.Id.IsValid) return false;
            lock (_gate)
            {
                if (_objects.ContainsKey(behavior.Id)) return false;
                _objects.Add(behavior.Id, behavior);
                return true;
            }
        }

        public bool TryGet(WorldObjectId objectId, out IWorldObjectBehavior behavior)
        {
            lock (_gate) return _objects.TryGetValue(objectId, out behavior);
        }

        public IReadOnlyList<IWorldObjectBehavior> GetAt(CharacterVector3 position)
        {
            lock (_gate)
            {
                var matches = new List<IWorldObjectBehavior>();
                foreach (var value in _objects.Values)
                    if (value.Position == position)
                        matches.Add(value);
                matches.Sort((left, right) => left.Id.CompareTo(right.Id));
                return matches.ToArray();
            }
        }

        public IReadOnlyList<WorldObjectStateSnapshot> CaptureState()
        {
            lock (_gate)
            {
                var ordered = new List<IWorldObjectBehavior>(_objects.Values);
                ordered.Sort((left, right) => left.Id.CompareTo(right.Id));
                var snapshots = new WorldObjectStateSnapshot[ordered.Count];
                for (var i = 0; i < ordered.Count; i++) snapshots[i] = ordered[i].CaptureState();
                return snapshots;
            }
        }

        public WorldInteractionResult RestoreState(IReadOnlyList<WorldObjectStateSnapshot> snapshots)
        {
            if (snapshots == null) return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            lock (_gate)
            {
                var targets = new IWorldObjectBehavior[snapshots.Count];
                var seen = new HashSet<WorldObjectId>();
                for (var i = 0; i < snapshots.Count; i++)
                {
                    var snapshot = snapshots[i];
                    IWorldObjectBehavior behavior;
                    if (!snapshot.ObjectId.IsValid || !seen.Add(snapshot.ObjectId) ||
                        !_objects.TryGetValue(snapshot.ObjectId, out behavior))
                        return WorldInteractionResult.Reject(WorldInteractionFailure.UnknownObject);
                    if (behavior.Kind != snapshot.Kind)
                        return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
                    targets[i] = behavior;
                }

                var originals = new WorldObjectStateSnapshot[targets.Length];
                for (var i = 0; i < targets.Length; i++) originals[i] = targets[i].CaptureState();
                for (var i = 0; i < targets.Length; i++)
                {
                    var result = targets[i].RestoreState(snapshots[i]);
                    if (result.Succeeded) continue;
                    for (var rollback = 0; rollback < i; rollback++)
                        targets[rollback].RestoreState(originals[rollback]);
                    return result;
                }
                return WorldInteractionResult.Success();
            }
        }
    }

    public sealed class InteractionClickedProcessor : IWorldInteractionValidator
    {
        private readonly ICharacterQuery _characters;
        private readonly IWorldObjectRegistry _objects;
        private readonly IWorldInteractionFactSink _facts;
        private ulong _sequence;

        public InteractionClickedProcessor(
            ICharacterQuery characters,
            IWorldObjectRegistry objects,
            IWorldInteractionFactSink facts = null)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
            _facts = facts ?? NullWorldInteractionFactSink.Instance;
        }

        public WorldInteractionResult Process(ulong senderSteamId)
        {
            CharacterId actorId;
            var binding = new CharacterBinding("steam", senderSteamId.ToString(CultureInfo.InvariantCulture));
            if (!_characters.TryResolve(binding, out actorId))
                return WorldInteractionResult.Reject(WorldInteractionFailure.UnknownActor);

            CharacterSnapshot actor;
            if (!_characters.TryGet(actorId, out actor))
                return WorldInteractionResult.Reject(WorldInteractionFailure.UnknownActor);

            var candidates = _objects.GetAt(actor.Kinematics.Position);
            if (candidates.Count == 0)
                return WorldInteractionResult.Reject(WorldInteractionFailure.NoTarget);

            // Selection is deterministic and singular: only the lowest WorldObjectId candidate is attempted.
            var selected = candidates[0];
            var validation = Validate(actorId, selected.Id);
            if (!validation.Succeeded) return validation;

            var result = selected.Interact(new WorldInteractionContext(actorId));
            if (!result.Succeeded) return result;

            var state = selected.CaptureState();
            _sequence++;
            _facts.Publish(new WorldInteractionFact(
                _sequence,
                actorId,
                selected.Id,
                selected.Kind,
                state.StateCode,
                state.Revision));
            return result;
        }

        public WorldInteractionResult Validate(CharacterId actorId, WorldObjectId objectId)
        {
            CharacterSnapshot actor;
            if (!_characters.TryGet(actorId, out actor))
                return WorldInteractionResult.Reject(WorldInteractionFailure.UnknownActor);

            IWorldObjectBehavior behavior;
            if (!_objects.TryGet(objectId, out behavior))
                return WorldInteractionResult.Reject(WorldInteractionFailure.UnknownObject);

            if (behavior.Position != actor.Kinematics.Position)
                return WorldInteractionResult.Reject(WorldInteractionFailure.OutOfRange);
            return WorldInteractionResult.Success();
        }
    }

    public sealed class ItemPickupObject : IWorldObjectBehavior
    {
        private readonly IWorldItemPickupTransfer _transfer;
        private bool _enabled = true;
        private ulong _revision;

        public WorldObjectId Id { get; }
        public WorldObjectKind Kind => WorldObjectKind.ItemPickup;
        public CharacterVector3 Position { get; }
        public WorldItemPayload Payload { get; }
        public bool Enabled => _enabled;

        public ItemPickupObject(
            WorldObjectId id,
            CharacterVector3 position,
            WorldItemPayload payload,
            IWorldItemPickupTransfer transfer)
        {
            if (!id.IsValid) throw new ArgumentException("World object id is required.", nameof(id));
            Id = id;
            Position = position;
            Payload = payload;
            _transfer = transfer ?? throw new ArgumentNullException(nameof(transfer));
        }

        public WorldInteractionResult Interact(WorldInteractionContext context)
        {
            if (!_enabled) return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            if (!Payload.IsValid) return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidPayload);
            var result = _transfer.TryTransfer(context.ActorId, Id, Payload);
            if (!result.Succeeded) return result;
            _enabled = false;
            _revision++;
            return WorldInteractionResult.Success();
        }

        public WorldObjectStateSnapshot CaptureState() =>
            new WorldObjectStateSnapshot(Id, Kind, _enabled, _enabled ? 0 : 1, _revision);

        public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot)
        {
            if (snapshot.ObjectId != Id || snapshot.Kind != Kind ||
                (snapshot.StateCode != 0 && snapshot.StateCode != 1) ||
                snapshot.Enabled != (snapshot.StateCode == 0))
                return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            _enabled = snapshot.Enabled;
            _revision = snapshot.Revision;
            return WorldInteractionResult.Success();
        }
    }

    public sealed class DoorToggleObject : IWorldObjectBehavior
    {
        private ulong _revision;
        public WorldObjectId Id { get; }
        public WorldObjectKind Kind => WorldObjectKind.DoorToggle;
        public CharacterVector3 Position { get; }
        public bool IsOpen { get; private set; }

        public DoorToggleObject(WorldObjectId id, CharacterVector3 position, bool isOpen = false)
        {
            if (!id.IsValid) throw new ArgumentException("World object id is required.", nameof(id));
            Id = id;
            Position = position;
            IsOpen = isOpen;
        }

        public WorldInteractionResult Interact(WorldInteractionContext context)
        {
            IsOpen = !IsOpen;
            _revision++;
            return WorldInteractionResult.Success();
        }

        public WorldObjectStateSnapshot CaptureState() =>
            new WorldObjectStateSnapshot(Id, Kind, true, IsOpen ? 1 : 0, _revision);

        public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot)
        {
            if (snapshot.ObjectId != Id || snapshot.Kind != Kind || !snapshot.Enabled ||
                (snapshot.StateCode != 0 && snapshot.StateCode != 1))
                return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            IsOpen = snapshot.StateCode == 1;
            _revision = snapshot.Revision;
            return WorldInteractionResult.Success();
        }
    }

    public enum NestedSubsceneActiveState : byte
    {
        Inactive = 0,
        Active = 1
    }

    public sealed class NestedSubsceneToggleObject : IWorldObjectBehavior
    {
        private ulong _revision;
        public WorldObjectId Id { get; }
        public WorldObjectKind Kind => WorldObjectKind.NestedSubsceneToggle;
        public CharacterVector3 Position { get; }
        public string NestedSceneId { get; }
        public NestedSubsceneActiveState ActiveState { get; private set; }

        public NestedSubsceneToggleObject(
            WorldObjectId id,
            CharacterVector3 position,
            string nestedSceneId,
            NestedSubsceneActiveState activeState = NestedSubsceneActiveState.Inactive)
        {
            if (!id.IsValid) throw new ArgumentException("World object id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(nestedSceneId))
                throw new ArgumentException("Nested scene id is required.", nameof(nestedSceneId));
            Id = id;
            Position = position;
            NestedSceneId = nestedSceneId.Trim();
            ActiveState = activeState;
        }

        public WorldInteractionResult Interact(WorldInteractionContext context)
        {
            if (ActiveState != NestedSubsceneActiveState.Inactive && ActiveState != NestedSubsceneActiveState.Active)
                return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            ActiveState = ActiveState == NestedSubsceneActiveState.Active
                ? NestedSubsceneActiveState.Inactive
                : NestedSubsceneActiveState.Active;
            _revision++;
            return WorldInteractionResult.Success();
        }

        public WorldObjectStateSnapshot CaptureState() =>
            new WorldObjectStateSnapshot(Id, Kind, true, (int)ActiveState, _revision);

        public WorldInteractionResult RestoreState(WorldObjectStateSnapshot snapshot)
        {
            if (snapshot.ObjectId != Id || snapshot.Kind != Kind || !snapshot.Enabled ||
                (snapshot.StateCode != (int)NestedSubsceneActiveState.Inactive &&
                 snapshot.StateCode != (int)NestedSubsceneActiveState.Active))
                return WorldInteractionResult.Reject(WorldInteractionFailure.InvalidState);
            ActiveState = (NestedSubsceneActiveState)snapshot.StateCode;
            _revision = snapshot.Revision;
            return WorldInteractionResult.Success();
        }
    }

    public sealed class CompositeWorldInteractionFactSink : IWorldInteractionFactSink
    {
        private readonly IWorldInteractionFactSink[] _sinks;

        public CompositeWorldInteractionFactSink(params IWorldInteractionFactSink[] sinks)
        {
            _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            for (var i = 0; i < _sinks.Length; i++)
                if (_sinks[i] == null) throw new ArgumentException("Fact sink cannot be null.", nameof(sinks));
        }

        public void Publish(WorldInteractionFact fact)
        {
            for (var i = 0; i < _sinks.Length; i++) _sinks[i].Publish(fact);
        }
    }

    public sealed class NullWorldInteractionFactSink : IWorldInteractionFactSink
    {
        public static readonly NullWorldInteractionFactSink Instance = new NullWorldInteractionFactSink();
        private NullWorldInteractionFactSink() { }
        public void Publish(WorldInteractionFact fact) { }
    }
}
