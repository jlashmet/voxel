using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Bounded curtain-layout composition over the reusable straight-wall and battlement emitters.
    /// Compatibility rectangles retain the existing detailed castle facade; custom rectangles and
    /// rectilinear polygons intentionally author only the configurable perimeter vocabulary.
    /// </summary>
    public static class CastleCurtainLayoutAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCurtainConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Castle curtain configuration is invalid.", nameof(config));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            if (config.Layout == CastleCurtainLayoutKind.Rectangular)
            {
                StructureWallRunConfig wallX = config.RectangularWallX();
                StructureWallRunConfig wallZ = config.RectangularWallZ();
                int longest = math.max(wallX.Length, wallZ.Length);

                // The compatibility preset deliberately uses one segment per historical wall.
                // Delegate that exact case so decorative facade details and legacy write ordering
                // remain unchanged while the richer config becomes the public customization seam.
                if (config.MaximumSegmentLength >= longest)
                {
                    CastleCurtainAuthoring.Author(
                        authoring,
                        in plan,
                        in wallX,
                        in wallZ,
                        in config.Battlements,
                        in config.Palette);
                    return;
                }

                int hx = config.RectangularHalfExtents.x;
                int hz = config.RectangularHalfExtents.y;
                AuthorEdge(authoring, in config, plan.Centre, baseY,
                    new int2(-hx, -hz), new int2(hx, -hz));
                AuthorEdge(authoring, in config, plan.Centre, baseY,
                    new int2(-hx, hz), new int2(hx, hz));
                AuthorEdge(authoring, in config, plan.Centre, baseY,
                    new int2(-hx, -hz), new int2(-hx, hz));
                AuthorEdge(authoring, in config, plan.Centre, baseY,
                    new int2(hx, -hz), new int2(hx, hz));
                return;
            }

            for (int i = 0; i < config.PolygonVertices.Length; i++)
            {
                int2 a = config.PolygonVertices[i];
                int2 b = config.PolygonVertices[(i + 1) % config.PolygonVertices.Length];
                AuthorEdge(authoring, in config, plan.Centre, baseY, a, b);
            }
        }

        /// <summary>Pure helper used by tests/tooling to budget deterministic perimeter chunks.</summary>
        public static int SegmentCount(in CastleCurtainConfig config)
        {
            if (!config.IsWellFormed) return 0;

            int count = 0;
            if (config.Layout == CastleCurtainLayoutKind.Rectangular)
            {
                int x = config.RectangularHalfExtents.x * 2;
                int z = config.RectangularHalfExtents.y * 2;
                return 2 * Chunks(x, config.MaximumSegmentLength)
                     + 2 * Chunks(z, config.MaximumSegmentLength);
            }

            for (int i = 0; i < config.PolygonVertices.Length; i++)
            {
                int2 a = config.PolygonVertices[i];
                int2 b = config.PolygonVertices[(i + 1) % config.PolygonVertices.Length];
                int length = math.abs(b.x - a.x) + math.abs(b.y - a.y);
                count += Chunks(length, config.MaximumSegmentLength);
            }
            return count;
        }

        private static int Chunks(int length, int maximumSegmentLength) =>
            (length + maximumSegmentLength - 1) / maximumSegmentLength;

        private static void AuthorEdge(
            IStructureAuthoringSession authoring,
            in CastleCurtainConfig config,
            int3 centre,
            int baseY,
            int2 a,
            int2 b)
        {
            bool alongX = a.y == b.y;
            int length = alongX ? math.abs(b.x - a.x) : math.abs(b.y - a.y);
            int2 low = alongX
                ? new int2(math.min(a.x, b.x), a.y)
                : new int2(a.x, math.min(a.y, b.y));
            int3 direction = alongX ? new int3(1, 0, 0) : new int3(0, 0, 1);
            int3 edgeStart = new int3(centre.x + low.x, baseY, centre.z + low.y);

            int authored = 0;
            while (authored < length)
            {
                int chunkLength = math.min(config.MaximumSegmentLength, length - authored);
                StructureWallRunConfig wall = config.WallForSpan(chunkLength);

                // Chunk boundaries are an authoring/budget concern, not a visible gap. Only the
                // perimeter vertices need corner composition, so intermediate chunks overlap fully.
                wall.CornerBehavior = StructureWallCornerBehavior.Overlap;
                int3 chunkStart = edgeStart + direction * authored;
                AuthorWallChunk(
                    authoring,
                    chunkStart,
                    direction,
                    alongX,
                    in wall,
                    in config.Battlements,
                    in config.Palette);
                authored += chunkLength;
            }
        }

        private static void AuthorWallChunk(
            IStructureAuthoringSession authoring,
            int3 start,
            int3 direction,
            bool alongX,
            in StructureWallRunConfig wall,
            in BattlementConfig battlements,
            in StructureMaterialPalette palette)
        {
            StructureComponentAuthoring.AuthorWallRun(
                authoring, start, direction, alongX, in wall, in palette);

            int usableLength = wall.UsableLength;
            int3 usableStart = start + direction * wall.StartInset;
            if (wall.RepetitionSpacing > 0 && wall.RepetitionOffset < usableLength)
            {
                var slit = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 1,
                    Height = math.max(1, math.min(28, wall.Height - 1)),
                    BottomOffset = math.min(40, math.max(0, wall.Height - 29)),
                    Spacing = wall.RepetitionSpacing,
                    StartMargin = wall.RepetitionOffset,
                    EndMargin = 0,
                    FillMaterialRole = StructureMaterialRole.Opening,
                };
                StructureComponentAuthoring.AuthorRepeatedOpenings(
                    authoring,
                    usableStart,
                    direction,
                    alongX,
                    usableLength,
                    wall.Thickness,
                    in slit,
                    in palette);
            }

            int3 walkSize = alongX
                ? new int3(usableLength, 1, wall.Thickness)
                : new int3(wall.Thickness, 1, usableLength);
            authoring.FillBulk(
                usableStart + new int3(0, wall.Height, 0),
                walkSize,
                palette.Resolve(StructureMaterialRole.PrimaryWall));

            StructureComponentAuthoring.AuthorBattlements(
                authoring,
                usableStart + new int3(0, wall.Height + 1, 0),
                direction,
                alongX,
                usableLength,
                in battlements,
                in palette);
        }
    }
}
