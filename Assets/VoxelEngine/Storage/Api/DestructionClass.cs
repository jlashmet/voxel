namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Stable logical destruction behaviour for an authoritative material. This is simulation
    /// vocabulary, not physical storage layout, so cross-system authoring may use it without
    /// depending on Storage.Runtime.
    /// </summary>
    public enum DestructionClass : byte
    {
        /// <summary>Indestructible — bedrock, terrain boundaries, protected zones.</summary>
        None = 0,

        /// <summary>Crumbles into static geometry debris (falling blocks that settle).</summary>
        Crumble = 1,

        /// <summary>Splinters into physics-driven debris bodies (splintering wood, etc.).</summary>
        Splinter = 2,

        /// <summary>Explodes into particles and dust — no physics debris.</summary>
        Powder = 3,

        /// <summary>Liquid — spreads to adjacent logical voxel blocks on destruction.</summary>
        Spreading = 4,

        /// <summary>
        /// Legacy shorthand for content authored before flammability became an independent
        /// material property. New content may keep its physical destruction class and opt into
        /// fire simulation separately.
        /// </summary>
        Flammable = 5,
    }
}
