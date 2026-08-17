using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Shared rectangular-shell facade opening and frame emission.</summary>
    public static class StructureOpeningAuthoring
    {
        public static void AuthorRepeated(
            IStructureAuthoringSession authoring,
            int3 shellMin,
            int width,
            int height,
            int depth,
            int wallThickness,
            in OpeningConfig opening,
            int count,
            Facing facade,
            int groupOffset,
            int spacing,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!opening.IsWellFormed) throw new System.ArgumentException("Opening is invalid.", nameof(opening));
            if (count <= 0) throw new System.ArgumentOutOfRangeException(nameof(count));
            if (wallThickness <= 0 || width <= wallThickness * 2 || depth <= wallThickness * 2)
                throw new System.ArgumentOutOfRangeException(nameof(wallThickness));
            if (opening.BottomOffset < 0 || opening.BottomOffset + opening.Height >= height)
                throw new System.ArgumentException("Opening does not fit shell height.", nameof(opening));

            int span = facade == Facing.North || facade == Facing.South ? width : depth;
            int centreSpacing = count <= 1 ? 0 : spacing;
            int groupWidth = opening.Width + (count - 1) * centreSpacing;
            int firstCentre = span / 2 + groupOffset - groupWidth / 2 + opening.Width / 2;
            for (int i = 0; i < count; i++)
                AuthorOne(authoring, shellMin, width, depth, wallThickness,
                    firstCentre + i * centreSpacing, facade, in opening, in palette);
        }

        private static void AuthorOne(
            IStructureAuthoringSession authoring,
            int3 shellMin,
            int width,
            int depth,
            int wall,
            int facadeCentre,
            Facing facade,
            in OpeningConfig opening,
            in StructureMaterialPalette palette)
        {
            int y = shellMin.y + opening.BottomOffset;
            int3 carveMin;
            int3 carveSize;
            switch (facade)
            {
                case Facing.South:
                    carveMin = new int3(shellMin.x + facadeCentre - opening.Width / 2, y, shellMin.z - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.North:
                    carveMin = new int3(shellMin.x + facadeCentre - opening.Width / 2, y,
                        shellMin.z + depth - wall - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.West:
                    carveMin = new int3(shellMin.x - 1, y,
                        shellMin.z + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                case Facing.East:
                    carveMin = new int3(shellMin.x + width - wall - 1, y,
                        shellMin.z + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(facade));
            }

            authoring.Box(carveMin, carveSize, palette.Resolve(opening.FillMaterialRole));
            AuthorFrame(authoring, shellMin, width, depth, facadeCentre, facade, in opening, in palette);
        }

        private static void AuthorFrame(
            IStructureAuthoringSession authoring,
            int3 shellMin,
            int width,
            int depth,
            int facadeCentre,
            Facing facade,
            in OpeningConfig opening,
            in StructureMaterialPalette palette)
        {
            if (opening.FrameThickness <= 0 && opening.LintelThickness <= 0) return;
            byte material = palette.Resolve(opening.FrameMaterialRole);
            int y = shellMin.y + opening.BottomOffset;
            int side = math.max(1, opening.FrameThickness);
            int top = math.max(side, opening.LintelThickness);
            int half = opening.Width / 2;

            if (facade == Facing.North || facade == Facing.South)
            {
                int z = facade == Facing.South ? shellMin.z - 1 : shellMin.z + depth;
                int left = shellMin.x + facadeCentre - half - side;
                int right = shellMin.x + facadeCentre - half + opening.Width;
                authoring.Box(new int3(left, y, z), new int3(side, opening.Height + top, 1), material);
                authoring.Box(new int3(right, y, z), new int3(side, opening.Height + top, 1), material);
                authoring.Box(new int3(left, y + opening.Height, z),
                    new int3(opening.Width + side * 2, top, 1), material);
            }
            else
            {
                int x = facade == Facing.West ? shellMin.x - 1 : shellMin.x + width;
                int left = shellMin.z + facadeCentre - half - side;
                int right = shellMin.z + facadeCentre - half + opening.Width;
                authoring.Box(new int3(x, y, left), new int3(1, opening.Height + top, side), material);
                authoring.Box(new int3(x, y, right), new int3(1, opening.Height + top, side), material);
                authoring.Box(new int3(x, y + opening.Height, left),
                    new int3(1, top, opening.Width + side * 2), material);
            }
        }
    }
}
