using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Deterministic authoring helper for structure-owned gameplay objects and their signal graph.</summary>
    public sealed class WorldObjectAuthoringSession
    {
        private readonly uint _worldSeed;
        private readonly uint _parentId;
        private readonly List<WorldObjectDescriptor> _objects = new List<WorldObjectDescriptor>();
        private readonly List<WorldObjectConnection> _connections = new List<WorldObjectConnection>();
        private readonly Dictionary<uint, WorldObjectId> _ids = new Dictionary<uint, WorldObjectId>();

        public WorldObjectAuthoringSession(uint worldSeed, uint parentId)
        {
            if (parentId == 0) throw new ArgumentOutOfRangeException(nameof(parentId));
            _worldSeed = worldSeed;
            _parentId = parentId;
        }

        public int ObjectCount => _objects.Count;
        public int ConnectionCount => _connections.Count;

        public WorldObjectId Place(uint localKey, WorldObjectKind kind, DecorationBounds bounds, int3 facing,
            uint variant = 0, WorldObjectStateFlags? defaultState = null, int parameter0 = int.MinValue,
            int parameter1 = int.MinValue, int parameter2 = int.MinValue, int parameter3 = int.MinValue)
        {
            if (localKey == 0 || _ids.ContainsKey(localKey)) throw new ArgumentException("World object local keys must be unique and non-zero.", nameof(localKey));
            WorldObjectPreset preset = WorldObjectContentCatalog.Get(kind);
            if (preset.Kind == WorldObjectKind.Unknown) throw new ArgumentException("Unknown world object kind.", nameof(kind));

            WorldObjectId id = WorldObjectIds.Create(_worldSeed, _parentId, localKey);
            var descriptor = new WorldObjectDescriptor
            {
                Id = id,
                Kind = kind,
                Capabilities = preset.Capabilities,
                Bounds = bounds,
                Facing = facing,
                Variant = variant,
                LocalKey = localKey,
                ParentId = _parentId,
                DefaultState = defaultState ?? preset.DefaultState,
                Parameter0 = parameter0 == int.MinValue ? preset.Parameter0 : parameter0,
                Parameter1 = parameter1 == int.MinValue ? preset.Parameter1 : parameter1,
                Parameter2 = parameter2 == int.MinValue ? preset.Parameter2 : parameter2,
                Parameter3 = parameter3 == int.MinValue ? preset.Parameter3 : parameter3,
            };
            if (!descriptor.IsWellFormed) throw new ArgumentException("Invalid world object placement.");
            _objects.Add(descriptor);
            _ids.Add(localKey, id);
            return id;
        }

        public void Connect(uint sourceKey, WorldObjectSignal signal, uint targetKey, WorldObjectAction action, int argument = 0)
        {
            if (!_ids.TryGetValue(sourceKey, out WorldObjectId source)) throw new ArgumentException("Unknown source key.", nameof(sourceKey));
            if (!_ids.TryGetValue(targetKey, out WorldObjectId target)) throw new ArgumentException("Unknown target key.", nameof(targetKey));
            var connection = new WorldObjectConnection
            {
                Source = source,
                Signal = signal,
                Target = target,
                Action = action,
                Argument = argument,
            };
            if (!connection.IsWellFormed) throw new ArgumentException("Invalid world object connection.");
            _connections.Add(connection);
        }

        public WorldObjectDescriptor[] BuildObjects() => _objects.ToArray();
        public WorldObjectConnection[] BuildConnections() => _connections.ToArray();

        public WorldObjectDescriptor[] BuildObjectsFrom(int startIndex)
        {
            if (startIndex < 0 || startIndex > _objects.Count)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            int count = _objects.Count - startIndex;
            var result = new WorldObjectDescriptor[count];
            for (int i = 0; i < count; i++) result[i] = _objects[startIndex + i];
            return result;
        }
    }
}
