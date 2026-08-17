using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;

namespace Game.Structures.Runtime
{
    /// <summary>Deterministic shed composition over shared architectural component configs.</summary>
    public static class ShedAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ShedConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Shed configuration is invalid.", nameof(config));

            int minX = origin.x + config.Footprint.Primary.Min.x;
            int minZ = origin.z + config.Footprint.Primary.Min.y;
            int baseY = origin.y;

            AuthorFoundation(authoring, minX, baseY, minZ, in config);
            authoring.HollowBox(
                new int3(minX, baseY, minZ),
                new int3(config.Width, config.Height, config.Depth),
                config.WallThickness,
                config.Palette.Resolve(config.Walls.PrimaryMaterial),
                false,
                false);

            AuthorOpenings(authoring, minX, baseY, minZ, in config.Door,
                config.DoorCount, config.DoorFacade, config.DoorGroupOffset,
                config.DoorSpacing, in config);
            if (config.WindowsEnabled)
                AuthorOpenings(authoring, minX, baseY, minZ, in config.Window,
                    config.WindowCount, config.WindowFacade, config.WindowGroupOffset,
                    config.WindowSpacing, in config);

            SharedRoofAuthoring.Author(
                authoring,
                new int3(minX, baseY, minZ),
                config.Width,
                config.Depth,
                baseY + config.Height,
                in config.Roof,
                config.Palette.Resolve(config.Roof.MaterialRole));
        }

        private static void AuthorFoundation(IStructureAuthoringSession authoring,
            int minX, int baseY, int minZ, in ShedConfig config)
        {
            if (config.Footprint.FoundationStyle == StructureFoundationStyle.None) return;
            if (config.Footprint.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException(
                    "Shed authoring currently supports None or Slab foundations only.", nameof(config));

            authoring.Box(
                new int3(minX, baseY - config.Footprint.FoundationDepth, minZ),
                new int3(config.Width, config.Footprint.FoundationDepth, config.Depth),
                config.Palette.Resolve(config.Footprint.FoundationMaterial));
        }

        private static void AuthorOpenings(IStructureAuthoringSession authoring,
            int minX, int baseY, int minZ, in OpeningConfig opening, int count,
            Facing facade, int groupOffset, int spacing, in ShedConfig config)
        {
            int span = facade == Facing.North || facade == Facing.South ? config.Width : config.Depth;
            int centreSpacing = count <= 1 ? 0 : spacing;
            int groupWidth = opening.Width + (count - 1) * centreSpacing;
            int firstCentre = span / 2 + groupOffset - groupWidth / 2 + opening.Width / 2;
            for (int i = 0; i < count; i++)
                AuthorOpening(authoring, minX, baseY, minZ,
                    firstCentre + i * centreSpacing, facade, in opening, in config);
        }

        private static void AuthorOpening(IStructureAuthoringSession authoring,
            int minX, int baseY, int minZ, int facadeCentre, Facing facade,
            in OpeningConfig opening, in ShedConfig config)
        {
            int wall = config.WallThickness;
            int y = baseY + opening.BottomOffset;
            int3 carveMin;
            int3 carveSize;
            switch (facade)
            {
                case Facing.South:
                    carveMin = new int3(minX + facadeCentre - opening.Width / 2, y, minZ - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.North:
                    carveMin = new int3(minX + facadeCentre - opening.Width / 2, y,
                        minZ + config.Depth - wall - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.West:
                    carveMin = new int3(minX - 1, y,
                        minZ + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                case Facing.East:
                    carveMin = new int3(minX + config.Width - wall - 1, y,
                        minZ + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(facade));
            }

            authoring.Box(carveMin, carveSize, config.Palette.Resolve(opening.FillMaterialRole));
            AuthorFrame(authoring, minX, baseY, minZ, facadeCentre, facade, in opening, in config);
        }

        private static void AuthorFrame(IStructureAuthoringSession authoring,
            int minX, int baseY, int minZ, int facadeCentre, Facing facade,
            in OpeningConfig opening, in ShedConfig config)
        {
            if (opening.FrameThickness <= 0 && opening.LintelThickness <= 0) return;
            byte material = config.Palette.Resolve(opening.FrameMaterialRole);
            int y = baseY + opening.BottomOffset;
            int side = math.max(1, opening.FrameThickness);
            int top = math.max(side, opening.LintelThickness);
            int half = opening.Width / 2;

            if (facade == Facing.North || facade == Facing.South)
            {
                int z = facade == Facing.South ? minZ - 1 : minZ + config.Depth;
                int left = minX + facadeCentre - half - side;
                int right = minX + facadeCentre - half + opening.Width;
                authoring.Box(new int3(left, y, z),
                    new int3(side, opening.Height + top, 1), material);
                authoring.Box(new int3(right, y, z),
                    new int3(side, opening.Height + top, 1), material);
                authoring.Box(new int3(left, y + opening.Height, z),
                    new int3(opening.Width + side * 2, top, 1), material);
            }
            else
            {
                int x = facade == Facing.West ? minX - 1 : minX + config.Width;
                int left = minZ + facadeCentre - half - side;
                int right = minZ + facadeCentre - half + opening.Width;
                authoring.Box(new int3(x, y, left),
                    new int3(1, opening.Height + top, side), material);
                authoring.Box(new int3(x, y, right),
                    new int3(1, opening.Height + top, side), material);
                authoring.Box(new int3(x, y + opening.Height, left),
                    new int3(1, top, opening.Width + side * 2), material);
            }
        }
    }
}
