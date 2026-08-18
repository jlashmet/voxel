using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Game-owned curtain-wall vocabulary. Geometry is authored only through
    /// <see cref="IStructureAuthoringSession"/>; material choices are game content.
    /// </summary>
    public static class CastleCurtainAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            Author(authoring, in plan, in components);
        }

        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleComponentConfig components)
        {
            if (!components.IsWellFormed)
                throw new System.ArgumentException("Castle component configuration is invalid.", nameof(components));

            CastleCurtainConfig curtain = CastleCurtainPresets.Compatibility(in components);
            Author(authoring, in plan, in curtain);
        }

        /// <summary>
        /// Authors the canonical configurable curtain surface. Rectangle and bounded orthogonal
        /// polygon layouts use the same shared wall-run/battlement path; segmentation only divides
        /// a span into bounded runs and never selects a different geometry implementation.
        /// </summary>
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCurtainConfig curtain)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!curtain.IsWellFormed)
                throw new System.ArgumentException("Castle curtain configuration is invalid.", nameof(curtain));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            switch (curtain.Layout)
            {
                case CastleCurtainLayoutKind.Rectangular:
                    AuthorRectangle(authoring, in plan, in curtain, baseY);
                    CurtainFacadeDetails(authoring, in plan, in curtain, baseY);
                    break;

                case CastleCurtainLayoutKind.Polygon:
                    AuthorPolygon(authoring, in plan, in curtain, baseY);
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(curtain.Layout));
            }
        }

        /// <summary>Compatibility overload retained for callers still supplying the shared pieces.</summary>
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in StructureWallRunConfig wallX,
            in StructureWallRunConfig wallZ,
            in BattlementConfig battlements)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            Author(authoring, in plan, in wallX, in wallZ, in battlements, in palette);
        }

        /// <summary>Compatibility overload retained while callers migrate to CastleCurtainConfig.</summary>
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in StructureWallRunConfig wallX,
            in StructureWallRunConfig wallZ,
            in BattlementConfig battlements,
            in StructureMaterialPalette palette)
        {
            if (!wallX.IsWellFormed || !wallZ.IsWellFormed || !battlements.IsWellFormed)
                throw new System.ArgumentException("Castle curtain configuration is invalid.");

            StructureWallRunConfig wall = wallX;
            int longest = math.max(wallX.Length, wallZ.Length);
            var curtain = new CastleCurtainConfig
            {
                Layout = CastleCurtainLayoutKind.Rectangular,
                RectangularHalfExtents = new int2(wallX.Length / 2, wallZ.Length / 2),
                Wall = wall,
                MaximumSegmentLength = longest,
                Battlements = battlements,
                Palette = palette,
            };
            Author(authoring, in plan, in curtain);
        }

        private static void AuthorRectangle(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCurtainConfig curtain,
            int baseY)
        {
            int hx = curtain.RectangularHalfExtents.x;
            int hz = curtain.RectangularHalfExtents.y;
            int thickness = curtain.Thickness;
            StructureWallRunConfig wallX = curtain.RectangularWallX();
            StructureWallRunConfig wallZ = curtain.RectangularWallZ();

            AuthorSegmentedWall(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new int3(1, 0, 0), true, in wallX, in curtain);
            AuthorSegmentedWall(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z + hz - thickness),
                new int3(1, 0, 0), true, in wallX, in curtain);
            AuthorSegmentedWall(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new int3(0, 0, 1), false, in wallZ, in curtain);
            AuthorSegmentedWall(authoring,
                new int3(plan.Centre.x + hx - thickness, baseY, plan.Centre.z - hz),
                new int3(0, 0, 1), false, in wallZ, in curtain);
        }

        private static void AuthorPolygon(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCurtainConfig curtain,
            int baseY)
        {
            for (int i = 0; i < curtain.PolygonVertices.Length; i++)
            {
                int2 a = curtain.PolygonVertices[i];
                int2 b = curtain.PolygonVertices[(i + 1) % curtain.PolygonVertices.Length];
                int dx = b.x - a.x;
                int dz = b.y - a.y;
                bool alongX = dz == 0;
                int spanLength = alongX ? math.abs(dx) : math.abs(dz);

                // Shared wall runs use positive-size bulk fills. Normalize negative polygon edges
                // to their minimum endpoint rather than introducing a castle-only rasterizer.
                int2 localStart = alongX
                    ? new int2(math.min(a.x, b.x), a.y)
                    : new int2(a.x, math.min(a.y, b.y));
                int3 direction = alongX ? new int3(1, 0, 0) : new int3(0, 0, 1);
                int3 start = new(
                    plan.Centre.x + localStart.x,
                    baseY,
                    plan.Centre.z + localStart.y);

                StructureWallRunConfig wall = curtain.WallForSpan(spanLength);
                AuthorSegmentedWall(authoring, start, direction, alongX, in wall, in curtain);
            }
        }

        private static void AuthorSegmentedWall(
            IStructureAuthoringSession authoring,
            int3 start,
            int3 direction,
            bool alongX,
            in StructureWallRunConfig wall,
            in CastleCurtainConfig curtain)
        {
            int offset = 0;
            while (offset < wall.Length)
            {
                int segmentLength = math.min(curtain.MaximumSegmentLength, wall.Length - offset);
                StructureWallRunConfig segment = wall;
                segment.Length = segmentLength;

                // Insets are derived from CornerBehavior, so the split carries the run's corner
                // trimming only at the two ends that are still the whole run's ends. Interior joints
                // between segments must not inset, or the curtain gains a gap at every seam.
                bool trimStart = offset == 0 && wall.StartInset != 0;
                bool trimEnd = offset + segmentLength == wall.Length && wall.EndInset != 0;
                segment.CornerBehavior =
                    trimStart && trimEnd ? StructureWallCornerBehavior.TrimBoth :
                    trimStart ? StructureWallCornerBehavior.TrimStart :
                    trimEnd ? StructureWallCornerBehavior.TrimEnd :
                    StructureWallCornerBehavior.Overlap;

                WallRun(
                    authoring,
                    start + direction * offset,
                    direction,
                    in segment,
                    in curtain.Battlements,
                    in curtain.Palette,
                    alongX);
                offset += segmentLength;
            }
        }

        private static void CurtainFacadeDetails(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in CastleCurtainConfig curtain,
            int baseY)
        {
            int hx = curtain.RectangularHalfExtents.x;
            int hz = curtain.RectangularHalfExtents.y;
            int thickness = curtain.Thickness;
            int gateZ = plan.Centre.z - hz;
            int wallTop = baseY + curtain.Height;

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = plan.Centre.x + side * (112 + bay * 58);
                if (math.abs(x - plan.Centre.x) >= hx - plan.TowerRadius) continue;

                authoring.Box(new int3(x - 7, baseY, gateZ - 12),
                    new int3(14, 58, 14), GameMaterialIds.DarkStone);
                authoring.Box(new int3(x - 5, baseY + 50, gateZ - 9),
                    new int3(10, curtain.Height - 44, 11), GameMaterialIds.Stone);
                authoring.Box(new int3(x - 9, baseY + 52, gateZ - 14),
                    new int3(18, 5, 16), GameMaterialIds.Stone);

                int panelX = x + side * 26 - 10;
                authoring.Arch(new int3(panelX, baseY + 28, gateZ - 2),
                    20, 38, 3, 2, GameMaterialIds.DarkStone);
                authoring.Arch(new int3(panelX + 6, baseY + 39, gateZ - 3),
                    8, 21, 4, 2, GameMaterialIds.Empty);
            }

            for (int x = plan.Centre.x - hx + plan.TowerRadius;
                 x <= plan.Centre.x + hx - plan.TowerRadius; x += 24)
            {
                if (math.abs(x - plan.Centre.x) < 82) continue;
                authoring.Box(new int3(x - 5, wallTop - 8, gateZ - 10),
                    new int3(10, 12, 12), GameMaterialIds.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int x = plan.Centre.x + side * 132;
                authoring.Cylinder(x, wallTop + 1, gateZ + thickness / 2,
                    14, 28, GameMaterialIds.Stone);
                authoring.Cylinder(x, wallTop + 25, gateZ + thickness / 2,
                    17, 5, GameMaterialIds.DarkStone, 10);
                authoring.Cone(x, wallTop + 29, gateZ + thickness / 2,
                    16, 32, GameMaterialIds.Slate);
                authoring.Box(new int3(x, wallTop + 60, gateZ + thickness / 2),
                    new int3(2, 15, 2), GameMaterialIds.Gold);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                const int width = 58;
                int galleryX = plan.Centre.x + side * (hx * 3 / 5) - width / 2;
                int galleryY = wallTop - 34;
                int galleryZ = gateZ - 17;

                authoring.Box(new int3(galleryX, galleryY, galleryZ),
                    new int3(width, 4, 20), GameMaterialIds.Wood);
                for (int post = 4; post < width - 2; post += 16)
                {
                    authoring.Box(new int3(galleryX + post, galleryY + 4, galleryZ + 2),
                        new int3(3, 21, 3), GameMaterialIds.Wood);
                    authoring.Box(new int3(galleryX + post, galleryY - 10, galleryZ + 13),
                        new int3(4, 12, 4), GameMaterialIds.DarkStone);
                }
                authoring.Box(new int3(galleryX + 2, galleryY + 13, galleryZ),
                    new int3(width - 4, 3, 3), GameMaterialIds.Wood);
                authoring.Box(new int3(galleryX - 3, galleryY + 24, galleryZ - 2),
                    new int3(width + 6, 3, 24), GameMaterialIds.Tile);
                authoring.Box(new int3(galleryX + 3, galleryY + 7, galleryZ + 3),
                    new int3(4, 7, 4), GameMaterialIds.LitWindow);
                authoring.Box(new int3(galleryX + width - 7, galleryY + 7, galleryZ + 3),
                    new int3(4, 7, 4), GameMaterialIds.LitWindow);
            }

            for (int side = -1; side <= 1; side += 2)
            for (int z = plan.Centre.z - hz + 76; z < plan.Centre.z + hz - 54; z += 82)
            {
                int x = plan.Centre.x + side * hx;
                int outerX = x + side * 2;
                authoring.Box(new int3(outerX + (side < 0 ? -10 : 0), baseY, z - 6),
                    new int3(10, 62, 12), GameMaterialIds.DarkStone);
                authoring.Box(new int3(outerX + (side < 0 ? -7 : 0), baseY + 54, z - 5),
                    new int3(7, curtain.Height - 48, 10), GameMaterialIds.Stone);
            }

            int2[] frontWeathering =
            {
                new(-190, 15), new(-146, 7), new(-94, 24),
                new(103, 10), new(158, 27), new(205, 13),
            };
            for (int i = 0; i < frontWeathering.Length; i++)
            {
                int patchX = plan.Centre.x + frontWeathering[i].x;
                int patchY = baseY + frontWeathering[i].y;
                int width = 13 + (i * 7 % 17);
                int height = 5 + (i * 5 % 13);
                if (math.abs(patchX - plan.Centre.x) > hx - plan.TowerRadius - width) continue;
                authoring.Box(new int3(patchX, patchY, gateZ - 2),
                    new int3(width, height, 2), GameMaterialIds.Moss);
                if ((i & 1) == 0)
                    authoring.Box(new int3(patchX + width / 3, baseY + 2, gateZ - 2),
                        new int3(5, frontWeathering[i].y + 4, 2), GameMaterialIds.Moss);
            }
        }

        private static void WallRun(
            IStructureAuthoringSession authoring,
            int3 start,
            int3 dir,
            in StructureWallRunConfig wall,
            in BattlementConfig battlements,
            in StructureMaterialPalette palette,
            bool alongX)
        {
            int length = wall.UsableLength;
            int thickness = wall.Thickness;
            int height = wall.Height;
            int3 usableStart = start + dir * wall.StartInset;

            StructureComponentAuthoring.AuthorWallRun(
                authoring, start, dir, alongX, in wall, in palette);

            if (wall.RepetitionSpacing > 0 && wall.RepetitionOffset < length)
            {
                var slit = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 1,
                    Height = 28,
                    BottomOffset = 40,
                    Spacing = wall.RepetitionSpacing,
                    StartMargin = wall.RepetitionOffset,
                    EndMargin = 0,
                    FillMaterialRole = StructureMaterialRole.Opening,
                };
                StructureComponentAuthoring.AuthorRepeatedOpenings(
                    authoring,
                    usableStart,
                    dir,
                    alongX,
                    length,
                    thickness,
                    in slit,
                    in palette);
            }

            int3 walkMin = usableStart + new int3(0, height, 0);
            int3 walkSize = alongX
                ? new int3(length, 1, thickness)
                : new int3(thickness, 1, length);
            authoring.FillBulk(walkMin, walkSize, palette.Resolve(StructureMaterialRole.PrimaryWall));

            StructureComponentAuthoring.AuthorBattlements(
                authoring,
                usableStart + new int3(0, height + 1, 0),
                dir,
                alongX,
                length,
                in battlements,
                in palette);

            if (length > 400)
            {
                for (int i = 120; i < length - 120; i += 200)
                {
                    int3 at = usableStart + dir * i;
                    int3 bannerSize = alongX
                        ? new int3(1, 46, 14)
                        : new int3(14, 46, 1);
                    authoring.FillBulk(new int3(at.x, start.y + height - 60, at.z),
                        bannerSize, palette.Resolve(StructureMaterialRole.Detail));
                }
            }
        }
    }
}
