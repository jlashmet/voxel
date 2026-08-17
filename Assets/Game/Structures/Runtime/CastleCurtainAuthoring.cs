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

            Author(
                authoring,
                in plan,
                in components.CurtainWallX,
                in components.CurtainWallZ,
                in components.CurtainBattlements,
                in components.Palette);
        }

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

        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in StructureWallRunConfig wallX,
            in StructureWallRunConfig wallZ,
            in BattlementConfig battlements,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!wallX.IsWellFormed || !wallZ.IsWellFormed || !battlements.IsWellFormed)
                throw new System.ArgumentException("Castle curtain configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = wallX.Length / 2;
            int hz = wallZ.Length / 2;
            int thickness = wallX.Thickness;

            WallRun(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new int3(1, 0, 0), in wallX, in battlements, in palette, true);
            WallRun(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z + hz - thickness),
                new int3(1, 0, 0), in wallX, in battlements, in palette, true);
            WallRun(authoring,
                new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz),
                new int3(0, 0, 1), in wallZ, in battlements, in palette, false);
            WallRun(authoring,
                new int3(plan.Centre.x + hx - thickness, baseY, plan.Centre.z - hz),
                new int3(0, 0, 1), in wallZ, in battlements, in palette, false);

            CurtainFacadeDetails(authoring, in plan, baseY);
        }

        private static void CurtainFacadeDetails(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int baseY)
        {
            int hx = plan.BaileyHalfX;
            int hz = plan.BaileyHalfZ;
            int gateZ = plan.Centre.z - hz;
            int wallTop = baseY + plan.WallHeight;

            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 3; bay++)
            {
                int x = plan.Centre.x + side * (112 + bay * 58);
                if (math.abs(x - plan.Centre.x) >= hx - plan.TowerRadius) continue;

                authoring.Box(new int3(x - 7, baseY, gateZ - 12),
                    new int3(14, 58, 14), GameMaterialIds.DarkStone);
                authoring.Box(new int3(x - 5, baseY + 50, gateZ - 9),
                    new int3(10, plan.WallHeight - 44, 11), GameMaterialIds.Stone);
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
                authoring.Cylinder(x, wallTop + 1, gateZ + plan.WallThickness / 2,
                    14, 28, GameMaterialIds.Stone);
                authoring.Cylinder(x, wallTop + 25, gateZ + plan.WallThickness / 2,
                    17, 5, GameMaterialIds.DarkStone, 10);
                authoring.Cone(x, wallTop + 29, gateZ + plan.WallThickness / 2,
                    16, 32, GameMaterialIds.Slate);
                authoring.Box(new int3(x, wallTop + 60, gateZ + plan.WallThickness / 2),
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
                    new int3(7, plan.WallHeight - 48, 10), GameMaterialIds.Stone);
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

            if (wall.RepetitionSpacing > 0)
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
