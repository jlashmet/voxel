using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Shared repeated buttress and stepped flying-buttress emission.</summary>
    public static class StructureButtressAuthoring
    {
        public static void AuthorRepeated(
            IStructureAuthoringSession authoring,
            int3 wallMin,
            int wallLength,
            int wallHeight,
            Facing facade,
            in ButtressConfig config,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed) throw new System.ArgumentException("Buttress configuration is invalid.", nameof(config));
            if (!StructureCardinalTransform.IsCardinal(facade)) throw new System.ArgumentOutOfRangeException(nameof(facade));

            int count = config.MaxCountForSpan(wallLength);
            if (count <= 0) return;
            int first = config.StartMargin + config.Width / 2;
            for (int i = 0; i < count; i++)
            {
                int centre = first + i * config.Spacing;
                AuthorOne(authoring, wallMin, wallLength, wallHeight, centre, facade, in config, in palette);
            }
        }

        private static void AuthorOne(
            IStructureAuthoringSession authoring,
            int3 wallMin,
            int wallLength,
            int wallHeight,
            int centre,
            Facing facade,
            in ButtressConfig config,
            in StructureMaterialPalette palette)
        {
            int height = math.min(config.Height, wallHeight);
            int topWidth = math.max(1, config.Width * (100 - config.TaperPercent) / 100);
            byte material = palette.Resolve(config.MaterialRole);

            int3 baseMin;
            if (facade == Facing.West)
                baseMin = new int3(wallMin.x - config.Depth, wallMin.y, wallMin.z + centre - config.Width / 2);
            else if (facade == Facing.East)
                baseMin = new int3(wallMin.x, wallMin.y, wallMin.z + centre - config.Width / 2);
            else if (facade == Facing.South)
                baseMin = new int3(wallMin.x + centre - config.Width / 2, wallMin.y, wallMin.z - config.Depth);
            else
                baseMin = new int3(wallMin.x + centre - config.Width / 2, wallMin.y, wallMin.z);

            // Two stacked integer boxes approximate taper without introducing sloped primitive state.
            int lowerHeight = math.max(1, height * 2 / 3);
            AuthorPierBox(authoring, baseMin, facade, config.Width, config.Depth, lowerHeight, material);
            if (height > lowerHeight)
            {
                int inset = (config.Width - topWidth) / 2;
                int3 upperMin = baseMin;
                upperMin.y += lowerHeight;
                if (facade == Facing.West || facade == Facing.East) upperMin.z += inset;
                else upperMin.x += inset;
                AuthorPierBox(authoring, upperMin, facade, topWidth, config.Depth,
                    height - lowerHeight, material);
            }

            if (config.FlyingEnabled)
                AuthorFlying(authoring, baseMin, facade, in config, palette.Resolve(config.FlyingMaterialRole));
        }

        private static void AuthorFlying(
            IStructureAuthoringSession authoring,
            int3 pierMin,
            Facing facade,
            in ButtressConfig config,
            byte material)
        {
            int steps = math.max(1, config.FlyingSpan);
            for (int s = 0; s < steps; s++)
            {
                int y = pierMin.y + config.FlyingSupportHeight + s * config.FlyingRise / steps;
                int3 min = pierMin;
                min.y = y;

                if (facade == Facing.West)
                    min.x = pierMin.x + config.Depth + s;
                else if (facade == Facing.East)
                    min.x = pierMin.x - s - 1;
                else if (facade == Facing.South)
                    min.z = pierMin.z + config.Depth + s;
                else
                    min.z = pierMin.z - s - 1;

                int3 size = (facade == Facing.West || facade == Facing.East)
                    ? new int3(1, config.FlyingThickness, config.Width)
                    : new int3(config.Width, config.FlyingThickness, 1);
                authoring.Box(min, size, material);
            }
        }

        private static void AuthorPierBox(
            IStructureAuthoringSession authoring,
            int3 min,
            Facing facade,
            int width,
            int depth,
            int height,
            byte material)
        {
            int3 size = (facade == Facing.West || facade == Facing.East)
                ? new int3(depth, height, width)
                : new int3(width, height, depth);
            authoring.Box(min, size, material);
        }
    }
}
