using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Lightweight CPU-owned coarse state that survives voxel-region unload/reload for the lifetime
    /// of the world session. Authoritative gameplay/world events call Set; renderers only read it.
    /// </summary>
    public sealed class StructureVisualStateStore : IStructureVisualStateSource
    {
        private readonly Dictionary<ulong, StructureVisualState> _states =
            new Dictionary<ulong, StructureVisualState>();
        private ulong _revision;

        public ulong Revision => _revision;

        public StructureVisualState Get(ulong structureKey) =>
            _states.TryGetValue(structureKey, out StructureVisualState state)
                ? state
                : StructureVisualState.Intact;

        public bool Set(ulong structureKey, StructureVisualState state)
        {
            StructureVisualState current = Get(structureKey);
            if (current == state) return false;

            if (state == StructureVisualState.Intact)
                _states.Remove(structureKey);
            else
                _states[structureKey] = state;

            unchecked { _revision++; }
            return true;
        }

        public bool Remove(ulong structureKey) => Set(structureKey, StructureVisualState.Removed);
        public bool Restore(ulong structureKey) => Set(structureKey, StructureVisualState.Intact);
        public void Clear()
        {
            if (_states.Count == 0) return;
            _states.Clear();
            unchecked { _revision++; }
        }
    }
}
