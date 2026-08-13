using System;

namespace VoxelEngine.Core.Storage
{
    [Flags]
    public enum MaterialAdjacencyEffect : byte
    {
        None = 0,
        Supported = 1 << 0,
        React = 1 << 1,
        Spread = 1 << 2,
        RejectPlacement = 1 << 3,
    }

    /// <summary>
    /// Symmetric simulation-level material adjacency rules. Rendering joins and feature sockets
    /// deliberately use separate catalogues.
    /// </summary>
    public unsafe struct MaterialAdjacencyCatalogue
    {
        public const int MaxMaterials = 32;
        private fixed byte _effects[MaxMaterials * MaxMaterials];

        public void Set(byte materialA, byte materialB, MaterialAdjacencyEffect effect)
        {
            if (materialA >= MaxMaterials || materialB >= MaxMaterials) return;
            _effects[materialA * MaxMaterials + materialB] = (byte)effect;
            _effects[materialB * MaxMaterials + materialA] = (byte)effect;
        }

        public MaterialAdjacencyEffect Get(byte materialA, byte materialB)
        {
            if (materialA >= MaxMaterials || materialB >= MaxMaterials)
                return MaterialAdjacencyEffect.RejectPlacement;
            return (MaterialAdjacencyEffect)_effects[materialA * MaxMaterials + materialB];
        }
    }
}
