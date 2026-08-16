namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Engine-facing, semantic-free description of one material slot.
    /// Games/content own the identity and meaning of MaterialId; Storage only consumes the
    /// physical/simulation projection needed to build its fixed runtime palette.
    /// </summary>
    public readonly struct MaterialDefinition
    {
        public readonly byte MaterialId;
        public readonly byte Hardness;
        public readonly DestructionClass DestructionClass;
        public readonly ushort DefaultSurfaceStyle;
        public readonly uint AllowedCoatings;
        public readonly bool Flammable;

        public MaterialDefinition(byte materialId, byte hardness, DestructionClass destructionClass,
                                  ushort defaultSurfaceStyle, uint allowedCoatings, bool flammable)
        {
            MaterialId = materialId;
            Hardness = hardness;
            DestructionClass = destructionClass;
            DefaultSurfaceStyle = defaultSurfaceStyle;
            AllowedCoatings = allowedCoatings;
            Flammable = flammable;
        }
    }
}
