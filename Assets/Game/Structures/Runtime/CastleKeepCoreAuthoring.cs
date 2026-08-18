using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Game-owned authoring for the keep's primary massing, circulation, windows, and facade.
    /// Interior furnishing and attached wings are separate content passes so the castle vocabulary
    /// does not collapse back into one monolithic builder.
    /// </summary>
    public static class CastleKeepCoreAuthoring
    {
        public static int3 Minimum(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            return new int3(
                plan.Centre.x - plan.KeepHalfX,
                baseY,
                plan.Centre.z - plan.KeepHalfZ + 60);
        }

        public static int3 Size(in CastlePlan plan) =>
            new(plan.KeepHalfX * 2, plan.KeepHeight, plan.KeepHalfZ * 2);

        public static void AuthorShell(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            AuthorShell(
                authoring,
                in plan,
                in components.KeepFoundation,
                components.KeepFoundationTopOffset,
                in components.KeepWalls,
                in components.Palette);
        }

        public static void AuthorShell(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in StructureFootprintConfig foundation,
            int foundationTopOffset)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            CastleComponentConfig components = CastleComponentPresets.Compatibility(in plan, in palette);
            AuthorShell(
                authoring,
                in plan,
                in foundation,
                foundationTopOffset,
                in components.KeepWalls,
                in components.Palette);
        }

        public static void AuthorShell(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            in StructureFootprintConfig foundation,
            int foundationTopOffset,
            in StructureWallRunConfig wall,
            in StructureMaterialPalette palette)
        {
            Require(authoring);
            if (!foundation.IsWellFormed || foundation.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException("Castle keep foundation must be a well-formed slab.");
            if (foundationTopOffset < 0)
                throw new System.ArgumentOutOfRangeException(nameof(foundationTopOffset));
            if (!wall.IsWellFormed)
                throw new System.ArgumentException("Castle keep wall configuration is invalid.");

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 legacySize = Size(in plan);
            int3 size = new(legacySize.x, wall.Height, legacySize.z);
            int thickness = wall.Thickness;

            // The compatibility footprint is local to the keep minimum, matching the historical
            // six-voxel apron and four-voxel foundation cap exactly.
            StructureComponentAuthoring.AuthorSlabFoundation(
                authoring,
                new int3(min.x, baseY + foundationTopOffset, min.z),
                in foundation,
                in palette);

            authoring.HollowBox(
                min,
                size,
                thickness,
                palette.Resolve(wall.PrimaryMaterial),
                false,
                false);

            // HollowBox writes only the shell. Preserve the base floor and explicitly clear the
            // occupied volume before floors, partitions, furniture, and circulation are authored.
            authoring.FillBulk(
                new int3(min.x + thickness, baseY + 1, min.z + thickness),
                new int3(
                    size.x - 2 * thickness,
                    size.y - 1,
                    size.z - 2 * thickness),
                palette.Resolve(StructureMaterialRole.Opening));
        }

        public static void AuthorCornerTurrets(
            IStructureAuthoringSession authoring,
            in CastlePlan plan)
        {
            Require(authoring);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 size = Size(in plan);

            for (int i = 0; i < 4; i++)
            {
                int cx = min.x + (i % 2 == 0 ? 0 : size.x);
                int cz = min.z + (i < 2 ? 0 : size.z);
                CastleTowerAuthoring.AuthorTower(
                    authoring,
                    in plan,
                    new int3(cx, baseY, cz),
                    26,
                    plan.KeepHeight + 30,
                    true);
            }
        }

        public static void AuthorCirculation(
            IStructureAuthoringSession authoring,
            in CastlePlan plan)
        {
            Require(authoring);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 size = Size(in plan);

            int entranceX = plan.Centre.x;
            authoring.Arch(
                new int3(entranceX - 15, baseY + 1, min.z - 1),
                30,
                34,
                10,
                2,
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(entranceX - 15, baseY + 2, min.z + 9),
                new int3(4, 29, 3),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(entranceX + 11, baseY + 2, min.z + 9),
                new int3(4, 29, 3),
                GameMaterialIds.Wood);

            // Reassert a clear entrance aisle after furnishing so generated clutter can never
            // seal the principal doorway.
            authoring.Box(
                new int3(entranceX - 9, baseY + 1, min.z + 8),
                new int3(18, 24, size.z / 2 - 28),
                GameMaterialIds.Empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;

            authoring.Box(
                new int3(grandX, baseY + 1, grandZ),
                new int3(grandWidth, plan.FloorHeight + 18, grandSteps * grandRun),
                GameMaterialIds.Empty);
            authoring.Stairs(
                new int3(grandX, baseY + 1, grandZ),
                grandWidth,
                grandSteps,
                grandRise,
                grandRun,
                2,
                GameMaterialIds.Wood);

            authoring.Box(
                new int3(grandX - 3, baseY + 1, grandZ),
                new int3(3, 20, 3),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(grandX + grandWidth, baseY + 1, grandZ),
                new int3(3, 20, 3),
                GameMaterialIds.Wood);

            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            authoring.SpiralStair(
                stairX,
                baseY + 2,
                stairZ,
                stairRadius,
                plan.Floors * plan.FloorHeight,
                GameMaterialIds.Stone);
        }

        public static void AuthorWindows(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            Require(authoring);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 size = Size(in plan);

            for (int floor = 0; floor < plan.Floors; floor++)
            {
                int y = baseY + floor * plan.FloorHeight + 12;
                int height = floor == 1 ? plan.FloorHeight - 14 : plan.FloorHeight - 18;

                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - 8;
                    bool mainEntrance = floor == 0 && i == 1;
                    if (!mainEntrance)
                    {
                        authoring.Arch(
                            new int3(x, y, min.z),
                            16,
                            height,
                            9,
                            2,
                            GameMaterialIds.Empty);
                        authoring.Box(
                            new int3(x + 3, y + 4, min.z + 2),
                            new int3(10, height - 10, 2),
                            GameMaterialIds.LitWindow);
                        authoring.Box(
                            new int3(x + 7, y + 5, min.z + 1),
                            new int3(2, height - 12, 3),
                            GameMaterialIds.DarkStone);
                        authoring.Box(
                            new int3(x + 3, y + height / 2, min.z + 1),
                            new int3(10, 2, 3),
                            GameMaterialIds.DarkStone);
                    }

                    authoring.Arch(
                        new int3(x, y, min.z + size.z - 8),
                        16,
                        height,
                        9,
                        2,
                        GameMaterialIds.Empty);
                }
            }
        }

        public static void AuthorFacade(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            Require(authoring);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 size = Size(in plan);

            for (int floor = 1; floor < plan.Floors; floor++)
            {
                int courseY = baseY + floor * plan.FloorHeight - 3;
                authoring.Box(
                    new int3(min.x - 3, courseY, min.z - 3),
                    new int3(size.x + 6, 3, 4),
                    GameMaterialIds.DarkStone);
                authoring.Box(
                    new int3(min.x - 3, courseY, min.z + size.z - 1),
                    new int3(size.x + 6, 3, 4),
                    GameMaterialIds.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 52;
                authoring.Box(
                    new int3(bannerX - 7, baseY + plan.FloorHeight * 2 + 8, min.z - 3),
                    new int3(14, 54, 3),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(bannerX - 10, baseY + plan.FloorHeight * 2 + 59, min.z - 4),
                    new int3(20, 3, 4),
                    GameMaterialIds.Gold);
            }

            int2[] keepStains =
            {
                new(-74, 5), new(-35, 14), new(42, 8), new(76, 20),
            };
            for (int i = 0; i < keepStains.Length; i++)
            {
                int stainX = plan.Centre.x + keepStains[i].x;
                int stainHeight = 8 + (i * 6 % 15);
                authoring.Box(
                    new int3(stainX, baseY + keepStains[i].y, min.z - 2),
                    new int3(9 + (i & 1) * 6, stainHeight, 2),
                    GameMaterialIds.Moss);
                authoring.Box(
                    new int3(stainX + 3, baseY + 2, min.z - 2),
                    new int3(3, keepStains[i].y + 5, 2),
                    GameMaterialIds.Moss);
            }

            CastleKeepOrielAuthoring.Author(authoring, in plan, min, size, baseY);
        }

        public static void AuthorFloorSlabs(
            IStructureAuthoringSession authoring,
            in CastlePlan plan)
        {
            Require(authoring);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int3 min = Minimum(in plan);
            int3 size = Size(in plan);

            for (int floor = 1; floor < plan.Floors; floor++)
            {
                int y = baseY + floor * plan.FloorHeight;
                authoring.Box(
                    new int3(min.x + 8, y, min.z + 8),
                    new int3(size.x - 16, 3, size.z - 16),
                    GameMaterialIds.Wood);
            }
        }

        private static void Require(IStructureAuthoringSession authoring)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
        }
    }
}
