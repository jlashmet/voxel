using System.Runtime.CompilerServices;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Material palette with destruction behaviour classes.
    ///
    /// FR-005 requires at least two classes of distinct destruction behaviour — e.g., stone
    /// that crumbles into debris vs wood that splinters and catches fire. The palette maps
    /// material indices to their properties: hardness, destructibility, debris type, and
    /// what happens when a voxel of that material is targeted by a destruction event.
    ///
    /// Each material has a DestructionClass that determines:
    ///   - What fraction of targeted voxels actually change (hard materials resist partial destruction)
    ///   - The type of debris generated on destruction (none, particles, physics bodies)
    ///   - Whether the material spreads fire/chemical reactions to adjacent bricks
    /// </summary>
    public unsafe struct MaterialPalette : IMaterialAuthoringCatalogue,
                                           IMaterialPresentationCatalogue,
                                           IMaterialSimulationCatalogue
    {
        /// <summary>Number of registered materials in the palette.</summary>
        public int Count => _count;
        public uint Version { get; private set; }

        private byte _count;

        // Both entry fields are single bytes, so the palette is two parallel fixed buffers
        // rather than a buffer of structs — C# fixed buffers admit only primitive element
        // types, and this keeps MaterialPalette blittable and usable inside Burst jobs.
        private fixed byte _hardness[MaxMaterials];
        private fixed byte _destructionClass[MaxMaterials];
        private fixed ushort _defaultSurfaceStyle[MaxMaterials];
        private fixed uint _allowedCoatings[MaxMaterials];
        private fixed byte _registered[MaxMaterials];

        public bool IsCreated => _count > 0;

        /// <summary>Register a material with its destruction class and properties.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(byte index, byte hardness, DestructionClass destructionClass)
        {
            Register(index, hardness, destructionClass, SurfaceStyles.Smooth, uint.MaxValue);
        }

        public void Register(byte index, byte hardness, DestructionClass destructionClass,
                             ushort defaultSurfaceStyle, uint allowedCoatings)
        {
            if ((uint)index >= (uint)MaxMaterials)
                return; // Silently ignore — palette entries beyond capacity are undefined.

            _hardness[index] = hardness;
            _destructionClass[index] = (byte)destructionClass;
            _defaultSurfaceStyle[index] = defaultSurfaceStyle;
            _allowedCoatings[index] = allowedCoatings;
            _registered[index] = 1;
            Version++;
            if (index + 1 > _count) _count = (byte)(index + 1);
        }

        public ushort GetDefaultSurfaceStyle(byte materialIndex) =>
            IsRegistered(materialIndex)
                ? _defaultSurfaceStyle[materialIndex] : SurfaceStyles.Smooth;

        public bool IsRegistered(byte materialIndex) =>
            materialIndex < _count && _registered[materialIndex] != 0;

        public bool AllowsCoating(byte materialIndex, byte coatingId) =>
            IsRegistered(materialIndex) && coatingId < 32
            && (_allowedCoatings[materialIndex] & (1u << coatingId)) != 0;

        public MaterialPaletteView PresentationView => MaterialPaletteView.Capture(in this);
        public MaterialSimulationView SimulationView => MaterialSimulationView.Capture(in this);

        public static implicit operator MaterialPaletteView(MaterialPalette source) =>
            MaterialPaletteView.Capture(in source);

        /// <summary>Look up the destruction class for a given material index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DestructionClass GetDestructionClass(byte materialIndex)
        {
            if ((uint)materialIndex >= (uint)_count)
                return DestructionClass.None; // Out-of-palette materials are treated as inert.

            return (DestructionClass)_destructionClass[materialIndex];
        }

        /// <summary>Look up the hardness for a given material index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetHardness(byte materialIndex)
        {
            if ((uint)materialIndex >= (uint)_count)
                return 0; // Unknown materials resist no destruction.

            return _hardness[materialIndex];
        }

        /// <summary>True when this material can be destroyed (not indestructible bedrock).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDestructible(byte materialIndex) => GetDestructionClass(materialIndex) != DestructionClass.None;

        /// <summary>Maximum palette entries. Sufficient for any session — materials don't change mid-game.</summary>
        private const int MaxMaterials = 32;
    }
}
