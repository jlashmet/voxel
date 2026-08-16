using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned physical/simulation projection for one semantic material id.
    /// Storage types describe the engine representation; this game owns the chosen values.
    /// Rendering-specific texture and shader data deliberately live outside this definition.
    /// </summary>
    public readonly struct GameMaterialDefinition
    {
        public readonly byte Id;
        public readonly string Name;
        public readonly byte Hardness;
        public readonly DestructionClass DestructionClass;
        public readonly byte DefaultSurfaceStyle;
        public readonly uint AllowedCoatings;
        public readonly bool Flammable;

        public GameMaterialDefinition(
            byte id,
            string name,
            byte hardness,
            DestructionClass destructionClass,
            byte defaultSurfaceStyle,
            uint allowedCoatings,
            bool flammable = false)
        {
            Id = id;
            Name = name;
            Hardness = hardness;
            DestructionClass = destructionClass;
            DefaultSurfaceStyle = defaultSurfaceStyle;
            AllowedCoatings = allowedCoatings;
            Flammable = flammable;
        }
    }
}
