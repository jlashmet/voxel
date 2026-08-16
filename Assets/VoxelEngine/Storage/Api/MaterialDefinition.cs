namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Engine-facing, semantic-free description of one material slot.
    /// Games/content own the identity and meaning of MaterialId; Storage only consumes the
    /// physical/simulation and authoring properties needed to build its fixed runtime palette.
    /// </summary>
    public readonly struct MaterialDefinition
    {
        public readonly byte MaterialId;
        public readonly byte Hardness;
        public readonly DestructionClass DestructionClass;
        public readonly ushort DefaultSurfaceStyle;
        public readonly uint AllowedCoatings;
        public readonly bool Flammable;

        /// <summary>
        /// Surface style used when an authoring tool creates new cells of this material. This is
        /// intentionally distinct from the material's general presentation default.
        /// </summary>
        public readonly ushort PlacementSurfaceStyle;

        /// <summary>
        /// Optional coating applied when authoring this material onto an existing solid. Zero means
        /// ordinary replacement. This is a generic placement property; semantic material identity
        /// remains application-owned.
        /// </summary>
        public readonly byte PlacementCoating;

        public MaterialDefinition(byte materialId, byte hardness, DestructionClass destructionClass,
                                  ushort defaultSurfaceStyle, uint allowedCoatings, bool flammable,
                                  ushort placementSurfaceStyle = SurfaceStyles.MaterialDefault,
                                  byte placementCoating = Coatings.None)
        {
            MaterialId = materialId;
            Hardness = hardness;
            DestructionClass = destructionClass;
            DefaultSurfaceStyle = defaultSurfaceStyle;
            AllowedCoatings = allowedCoatings;
            Flammable = flammable;
            PlacementSurfaceStyle = placementSurfaceStyle;
            PlacementCoating = placementCoating;
        }
    }
}
