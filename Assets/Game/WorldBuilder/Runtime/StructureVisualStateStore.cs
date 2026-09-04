using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Compact in-memory coarse semantic state. Intact is the implicit default; only exceptional
    /// states are retained, so this never becomes a shadow copy of distant voxel contents.
    /// </summary>
    public sealed class StructureVisualStateStore : IStructureVisualStateStore
    {
        private readonly Dictionary<ulong, StructureVisualState> _states = new();

        public StructureVisualState Get(ulong structureId)
        {
            return _states.TryGetValue(structureId, out StructureVisualState state)
                ? state
                : StructureVisualState.Intact;
        }

        public void Set(ulong structureId, StructureVisualState state)
        {
            if (!Enum.IsDefined(typeof(StructureVisualState), state))
                throw new ArgumentOutOfRangeException(nameof(state));

            if (state == StructureVisualState.Intact)
            {
                _states.Remove(structureId);
                return;
            }

            _states[structureId] = state;
        }

        public bool Remove(ulong structureId) => _states.Remove(structureId);

        public void Clear() => _states.Clear();
    }
}
