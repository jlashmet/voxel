using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Deterministic shed composition over shared architectural component configs.</summary>
    public static class ShedAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int3 origin,
            in ShedConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Shed configuration is invalid.", nameof(config));

            int minX = origin.x + config.Footprint.Primary.Min.x;
            int minZ = origin.z + config.Footprint.Primary.Min.y;
            int baseY = origin.y;
            int width = config.Width;
            int depth = config.Depth;
            int height = config.Height;

            AuthorFoundation(authoring, minX, baseY, minZ, width, depth, in config);

            authoring.HollowBox(
                new int3(minX, baseY, minZ),
                new int3(width, height, depth),
                config.WallThickness,
                config.Palette.Resolve(config.Walls.PrimaryMaterial),
                false,
                false);

            AuthorOpenings(
                authoring,
                minX,
                baseY,
                minZ,
                in config.Door,
                config.DoorCount,
                config.DoorFacade,
                config.DoorGroupOffset,
                config.DoorSpacing,
                in config);

            if (config.WindowsEnabled)
            {
                AuthorOpenings(
                    authoring,
                    minX,
                    baseY,
                    minZ,
                    in config.Window,
                    config.WindowCount,
                    config.WindowFacade,
                    config.WindowGroupOffset,
                    config.WindowSpacing,
                    in config);
            }

            AuthorRoof(authoring, minX, baseY + height, minZ, in config);
        }

        private static void AuthorFoundation(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int width,
            int depth,
            in ShedConfig config)
        {
            if (config.Footprint.FoundationStyle == StructureFoundationStyle.None)
                return;

            if (config.Footprint.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException(
                    "Shed authoring currently supports None or Slab foundations only.", nameof(config));

            authoring.Box(
                new int3(minX, baseY - config.Footprint.FoundationDepth, minZ),
                new int3(width, config.Footprint.FoundationDepth, depth),
                config.Palette.Resolve(config.Footprint.FoundationMaterial));
        }

        private static void AuthorOpenings(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            in OpeningConfig opening,
            int count,
            Facing facade,
            int groupOffset,
            int spacing,
            in ShedConfig config)
        {
            int span = facade == Facing.North || facade == Facing.South
                ? config.Width
                : config.Depth;
            int centreSpacing = count <= 1 ? 0 : spacing;
            int groupWidth = opening.Width + (count - 1) * centreSpacing;
            int firstCentre = span / 2 + groupOffset - groupWidth / 2 + opening.Width / 2;

            for (int i = 0; i < count; i++)
            {
                int centre = firstCentre + i * centreSpacing;
                AuthorOpening(
                    authoring,
                    minX,
                    baseY,
                    minZ,
                    centre,
                    facade,
                    in opening,
                    in config);
            }
        }

        private static void AuthorOpening(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int facadeCentre,
            Facing facade,
            in OpeningConfig opening,
            in ShedConfig config)
        {
            int wall = config.WallThickness;
            int y = baseY + opening.BottomOffset;
            byte fill = config.Palette.Resolve(opening.FillMaterialRole);
            int3 carveMin;
            int3 carveSize;

            switch (facade)
            {
                case Facing.South:
                    carveMin = new int3(
                        minX + facadeCentre - opening.Width / 2,
                        y,
                        minZ - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.North:
                    carveMin = new int3(
                        minX + facadeCentre - opening.Width / 2,
                        y,
                        minZ + config.Depth - wall - 1);
                    carveSize = new int3(opening.Width, opening.Height, wall + 2);
                    break;
                case Facing.West:
                    carveMin = new int3(
                        minX - 1,
                        y,
                        minZ + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                case Facing.East:
                    carveMin = new int3(
                        minX + config.Width - wall - 1,
                        y,
                        minZ + facadeCentre - opening.Width / 2);
                    carveSize = new int3(wall + 2, opening.Height, opening.Width);
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(facade));
            }

            authoring.Box(carveMin, carveSize, fill);
            AuthorFrame(
                authoring,
                minX,
                baseY,
                minZ,
                facadeCentre,
                facade,
                in opening,
                in config);
        }

        private static void AuthorFrame(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int facadeCentre,
            Facing facade,
            in OpeningConfig opening,
            in ShedConfig config)
        {
            int frame = opening.FrameThickness;
            int lintel = opening.LintelThickness;
            if (frame <= 0 && lintel <= 0) return;

            byte material = config.Palette.Resolve(opening.FrameMaterialRole);
            int y = baseY + opening.BottomOffset;
            int sideThickness = math.max(1, frame);
            int topThickness = math.max(sideThickness, lintel);
            int halfWidth = opening.Width / 2;

            if (facade == Facing.North || facade == Facing.South)
            {
                int z = facade == Facing.South ? minZ - 1 : minZ + config.Depth;
                int leftX = minX + facadeCentre - halfWidth - sideThickness;
                int rightX = minX + facadeCentre - halfWidth + opening.Width;
                authoring.Box(
                    new int3(leftX, y, z),
                    new int3(sideThickness, opening.Height + topThickness, 1),
                    material);
                authoring.Box(
                    new int3(rightX, y, z),
                    new int3(sideThickness, opening.Height + topThickness, 1),
                    material);
                authoring.Box(
                    new int3(leftX, y + opening.Height, z),
                    new int3(opening.Width + sideThickness * 2, topThickness, 1),
                    material);
            }
            else
            {
                int x = facade == Facing.West ? minX - 1 : minX + config.Width;
                int leftZ = minZ + facadeCentre - halfWidth - sideThickness;
                int rightZ = minZ + facadeCentre - halfWidth + opening.Width;
                authoring.Box(
                    new int3(x, y, leftZ),
                    new int3(1, opening.Height + topThickness, sideThickness),
                    material);
                authoring.Box(
                    new int3(x, y, rightZ),
                    new int3(1, opening.Height + topThickness, sideThickness),
                    material);
                authoring.Box(
                    new int3(x, y + opening.Height, leftZ),
                    new int3(1, topThickness, opening.Width + sideThickness * 2),
                    material);
            }
        }

        private static void AuthorRoof(
            IStructureAuthoringSession authoring,
            int minX,
            int roofY,
            int minZ,
            in ShedConfig config)
        {
            int eave = config.Roof.EaveOverhang;
            int roofWidth = config.Width + eave * 2;
            int roofDepth = config.Depth + eave * 2;
            int roofMinX = minX - eave;
            int roofMinZ = minZ - eave;
            byte material = config.Palette.Resolve(config.Roof.MaterialRole);

            switch (config.Roof.Style)
            {
                case RoofStyle.Flat:
                    authoring.Box(
                        new int3(roofMinX, roofY, roofMinZ),
                        new int3(roofWidth, config.Roof.Thickness, roofDepth),
                        material);
                    break;

                case RoofStyle.Gable:
                {
                    int slopeSpan = config.Roof.RidgeAxis == RoofAxis.X
                        ? roofDepth
                        : roofWidth;
                    int halfSpan = math.max(1, slopeSpan / 2);
                    int roofHeight = math.max(
                        config.Roof.Thickness,
                        (halfSpan * config.Roof.PitchRise + config.Roof.PitchRun - 1) /
                        config.Roof.PitchRun);
                    authoring.Gable(
                        new int3(roofMinX, roofY, roofMinZ),
                        new int3(roofWidth, roofHeight, roofDepth),
                        config.Roof.RidgeAxis == RoofAxis.X,
                        material);
                    break;
                }

                case RoofStyle.Shed:
                    AuthorShedRoof(
                        authoring,
                        roofMinX,
                        roofY,
                        roofMinZ,
                        roofWidth,
                        roofDepth,
                        in config.Roof,
                        material);
                    break;

                default:
                    throw new System.ArgumentException(
                        $"Unsupported shed roof style {config.Roof.Style}.", nameof(config));
            }
        }

        private static void AuthorShedRoof(
            IStructureAuthoringSession authoring,
            int minX,
            int baseY,
            int minZ,
            int width,
            int depth,
            in RoofConfig roof,
            byte material)
        {
            if (roof.RidgeAxis == RoofAxis.X)
            {
                for (int z = 0; z < depth; z++)
                {
                    int rise = z * roof.PitchRise / roof.PitchRun;
                    authoring.Box(
                        new int3(minX, baseY + rise, minZ + z),
                        new int3(width, roof.Thickness, 1),
                        material);
                }
            }
            else
            {
                for (int x = 0; x < width; x++)
                {
                    int rise = x * roof.PitchRise / roof.PitchRun;
                    authoring.Box(
                        new int3(minX + x, baseY + rise, minZ),
                        new int3(1, roof.Thickness, depth),
                        material);
                }
            }
        }
    }
}
