using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Shared bounded column/colonnade emission.</summary>
    public static class StructureColumnAuthoring
    {
        public static void AuthorColumn(
            IStructureAuthoringSession authoring,
            int3 baseCentre,
            in ColumnConfig config,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed) throw new System.ArgumentException("Column configuration is invalid.", nameof(config));

            int half = config.Width / 2;
            if (config.BaseHeight > 0)
                Emit(authoring, new int3(baseCentre.x, baseCentre.y, baseCentre.z),
                    config.Width + 2, config.BaseHeight, in config, palette.Resolve(config.BaseMaterialRole));

            int shaftY = baseCentre.y + config.BaseHeight;
            int shaftHeight = config.Height - config.BaseHeight - config.CapitalHeight;
            Emit(authoring, new int3(baseCentre.x, shaftY, baseCentre.z),
                config.Width, shaftHeight, in config, palette.Resolve(config.ShaftMaterialRole));

            if (config.CapitalHeight > 0)
                Emit(authoring, new int3(baseCentre.x, shaftY + shaftHeight, baseCentre.z),
                    config.Width + 2, config.CapitalHeight, in config, palette.Resolve(config.CapitalMaterialRole));
        }

        public static void AuthorRow(
            IStructureAuthoringSession authoring,
            int3 firstCentre,
            int2 stepDirection,
            int count,
            in ColumnConfig config,
            in StructureMaterialPalette palette)
        {
            if (count <= 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (math.abs(stepDirection.x) + math.abs(stepDirection.y) != 1)
                throw new System.ArgumentException("Column row direction must be cardinal.", nameof(stepDirection));

            for (int i = 0; i < count; i++)
            {
                int3 centre = new int3(
                    firstCentre.x + stepDirection.x * config.Spacing * i,
                    firstCentre.y,
                    firstCentre.z + stepDirection.y * config.Spacing * i);
                AuthorColumn(authoring, centre, in config, in palette);
            }
        }

        private static void Emit(
            IStructureAuthoringSession authoring,
            int3 centre,
            int width,
            int height,
            in ColumnConfig config,
            byte material)
        {
            int half = width / 2;
            if (config.Shape == StructureColumnShape.Round)
                authoring.Cylinder(centre.x, centre.y, centre.z, math.max(1, half), height, material);
            else
                authoring.Box(
                    new int3(centre.x - half, centre.y, centre.z - half),
                    new int3(width, height, width),
                    material);
        }
    }
}
