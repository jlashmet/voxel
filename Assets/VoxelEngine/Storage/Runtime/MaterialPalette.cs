using System.Runtime.CompilerServices;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
{
    /// <summary>
    /// Material palette with physical destruction classes and independent simulation properties.
    ///
    /// FR-005 requires at least two classes of distinct destruction behaviour — e.g., stone
    /// that crumbles into debris vs wood that splinters. The palette maps material indices to
    /// their properties: hardness, destructibility, debris type, surface defaults, and independent
    /// simulation traits such as flammability.
    ///
    /// DestructionClass determines the physical response when a voxel is destroyed. Flammability
    /// remains a separate property so a material can, for example, splinter and also catch fire.
    /// </summary>
    public unsafe struct MaterialPalette : IMaterialAuthoringCatalogue,
                                           IMaterialPlacementCatalogue,
                                           IMaterialPresentationCatalogue,
                                           IMaterialSimulationCatalogue
    {
        /// <summary>Number of occupied palette slots, including any gaps below the highest registered ID.</summary>
        public int Count => _count;
        public uint Version { get; private set; }

        private byte _count;

        // Primitive parallel fixed buffers keep MaterialPalette blittable and usable inside Burst
        // jobs while allowing independent properties to evolve without exposing a storage layout.
        private fixed byte _hardness[MaxMaterials];
        private fixed byte _destructionClass[MaxMaterials];
        private fixed byte _flammable[MaxMaterials];
        private fixed ushort _defaultSurfaceStyle[MaxMaterials];
        private fixed uint _allowedCoatings[MaxMaterials];
        private fixed ushort _placementSurfaceStyle[MaxMaterials];
        private fixed byte _placementCoating[MaxMaterials];
        private fixed byte _registered[MaxMaterials];

        public bool IsCreated => _count > 0;

        /// <summary>Clears every authored slot. Intended for composition-time catalogue replacement.</summary>
        public void Clear() => this = default;

        /// <summary>Register a material with its destruction class and properties.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Register(byte index, byte hardness, DestructionClass destructionClass)
        {
            Register(index, hardness, destructionClass, SurfaceStyles.Smooth, uint.MaxValue);
        }

        public void Register(byte index, byte hardness, DestructionClass destructionClass,
                             ushort defaultSurfaceStyle, uint allowedCoatings,
                             ushort placementSurfaceStyle = SurfaceStyles.MaterialDefault,
                             byte placementCoating = Coatings.None)
        {
            if ((uint)index >= (uint)MaxMaterials)
                return; // Silently ignore — palette entries beyond capacity are undefined.

            _hardness[index] = hardness;
            _destructionClass[index] = (byte)destructionClass;
            // Preserve the old authoring shorthand while making the property independent. Existing
            // Splinter materials are organic wood/cloth and therefore default to flammable too.
            _flammable[index] = destructionClass == DestructionClass.Flammable
                             || destructionClass == DestructionClass.Splinter ? (byte)1 : (byte)0;
            _defaultSurfaceStyle[index] = defaultSurfaceStyle;
            _allowedCoatings[index] = allowedCoatings;
            _placementSurfaceStyle[index] = placementSurfaceStyle;
            _placementCoating[index] = placementCoating;
            _registered[index] = 1;
            Version++;
            if (index + 1 > _count) _count = (byte)(index + 1);
        }

        /// <summary>Registers the generic definition supplied by a game/content composition root.</summary>
        public void Register(in MaterialDefinition definition)
        {
            Register(definition.MaterialId, definition.Hardness, definition.DestructionClass,
                     definition.DefaultSurfaceStyle, definition.AllowedCoatings,
                     definition.PlacementSurfaceStyle, definition.PlacementCoating);
            SetFlammable(definition.MaterialId, definition.Flammable);
        }

        /// <summary>Overrides whether an already registered material participates in fire.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlammable(byte materialIndex, bool flammable = true)
        {
            if (!IsRegistered(materialIndex)) return;
            byte value = flammable ? (byte)1 : (byte)0;
            if (_flammable[materialIndex] == value) return;
            _flammable[materialIndex] = value;
            Version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFlammable(byte materialIndex) =>
            IsRegistered(materialIndex) && _flammable[materialIndex] != 0;

        public ushort GetDefaultSurfaceStyle(byte materialIndex) =>
            IsRegistered(materialIndex)
                ? _defaultSurfaceStyle[materialIndex] : SurfaceStyles.Smooth;

        public ushort GetPlacementSurfaceStyle(byte materialIndex) =>
            IsRegistered(materialIndex)
                ? _placementSurfaceStyle[materialIndex] : SurfaceStyles.MaterialDefault;

        public byte GetPlacementCoating(byte materialIndex) =>
            IsRegistered(materialIndex) ? _placementCoating[materialIndex] : Coatings.None;

        public bool IsRegistered(byte materialIndex) =>
            materialIndex < _count && _registered[materialIndex] != 0;

        public bool AllowsCoating(byte materialIndex, byte coatingId) =>
            IsRegistered(materialIndex) && coatingId < 32
            && (_allowedCoatings[materialIndex] & (1u << coatingId)) != 0;

        public MaterialPaletteView PresentationView => MaterialPaletteView.Capture(in this);
        public MaterialSimulationView SimulationView => MaterialSimulationView.Capture(in this);

        public static implicit operator MaterialPaletteView(MaterialPalette source) =>
            MaterialPaletteView.Capture(in source);

        /// <summary>Look up the destruction class for a registered material index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DestructionClass GetDestructionClass(byte materialIndex)
        {
            if (!IsRegistered(materialIndex))
                return DestructionClass.None; // Unknown or gap slots are inert.

            return (DestructionClass)_destructionClass[materialIndex];
        }

        /// <summary>Look up the hardness for a registered material index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetHardness(byte materialIndex)
        {
            if (!IsRegistered(materialIndex))
                return 0; // Unknown or gap slots have no authored hardness.

            return _hardness[materialIndex];
        }

        /// <summary>True when this material can be destroyed (not indestructible bedrock).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDestructible(byte materialIndex) => GetDestructionClass(materialIndex) != DestructionClass.None;

        /// <summary>Maximum palette entries. Sufficient for any session — materials don't change mid-game.</summary>
        private const int MaxMaterials = 32;
    }
}
