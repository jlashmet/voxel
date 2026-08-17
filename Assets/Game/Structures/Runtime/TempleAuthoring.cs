using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedColumnAuthoring = VoxelEngine.Structures.Runtime.StructureColumnAuthoring;
using SharedOpeningAuthoring = VoxelEngine.Structures.Runtime.StructureOpeningAuthoring;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;
using SharedStairAuthoring = VoxelEngine.Structures.Runtime.StructureStairAuthoring;

namespace Game.Structures.Runtime
{
    /// <summary>Temple composition over shared platform/steps, opening, roof, and column contracts.</summary>
    public static class TempleAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in TempleConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Temple configuration is invalid.", nameof(config));

            StructureFootprintRect platformWorld = StructureCardinalTransform.Rect(
                in config.Footprint.Primary, config.EntryFacing);
            int3 platformMin = new int3(
                origin.x + platformWorld.Min.x,
                origin.y,
                origin.z + platformWorld.Min.y);
            authoring.Box(
                platformMin,
                new int3(platformWorld.Size.x, config.PlatformHeight, platformWorld.Size.y),
                config.Palette.Resolve(config.Footprint.FoundationMaterial));

            AuthorApproach(authoring, origin, in config);
            int sanctuaryBaseY = origin.y + config.PlatformHeight;
            AuthorSanctuary(authoring, origin, sanctuaryBaseY, in config);
            if (config.CourtyardEnabled)
                AuthorCourtyard(authoring, origin, sanctuaryBaseY, in config);
            if (config.ColonnadeEnabled)
                AuthorColonnade(authoring, origin, sanctuaryBaseY, in config);
        }

        private static void AuthorApproach(
            IStructureAuthoringSession authoring,
            int3 origin,
            in TempleConfig config)
        {
            int localBottomZ = config.Footprint.Primary.Min.y - config.ApproachStairs.TotalRun;
            int2 localBottom = new int2(0, localBottomZ);
            int2 worldBottom = StructureCardinalTransform.Point(localBottom, config.EntryFacing);
            Facing ascent = StructureCardinalTransform.FacingDirection(Facing.North, config.EntryFacing);
            SharedStairAuthoring.Author(
                authoring,
                new int3(origin.x + worldBottom.x, origin.y, origin.z + worldBottom.y),
                ascent,
                in config.ApproachStairs,
                in config.Palette);
        }

        private static void AuthorSanctuary(
            IStructureAuthoringSession authoring,
            int3 origin,
            int baseY,
            in TempleConfig config)
        {
            var local = new StructureFootprintRect(
                new int2(-config.SanctuaryWidth / 2, -config.SanctuaryDepth / 2),
                new int2(config.SanctuaryWidth, config.SanctuaryDepth));
            StructureFootprintRect world = StructureCardinalTransform.Rect(in local, config.EntryFacing);
            int3 min = new int3(origin.x + world.Min.x, baseY, origin.z + world.Min.y);
            authoring.HollowBox(
                min,
                new int3(world.Size.x, config.SanctuaryHeight, world.Size.y),
                config.WallThickness,
                config.Palette.Resolve(StructureMaterialRole.PrimaryWall),
                true,
                false);

            SharedOpeningAuthoring.AuthorRepeated(
                authoring,
                min,
                world.Size.x,
                config.SanctuaryHeight,
                world.Size.y,
                config.WallThickness,
                in config.SanctuaryDoor,
                1,
                StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing),
                0,
                0,
                in config.Palette);

            RoofConfig roof = config.SanctuaryRoof;
            roof.RidgeAxis = StructureCardinalTransform.Axis(roof.RidgeAxis, config.EntryFacing);
            SharedRoofAuthoring.Author(
                authoring,
                min,
                world.Size.x,
                world.Size.y,
                baseY + config.SanctuaryHeight,
                in roof,
                config.Palette.Resolve(roof.MaterialRole));
        }

        private static void AuthorCourtyard(
            IStructureAuthoringSession authoring,
            int3 origin,
            int baseY,
            in TempleConfig config)
        {
            var local = new StructureFootprintRect(
                new int2(-config.CourtyardWidth / 2, -config.CourtyardDepth / 2),
                new int2(config.CourtyardWidth, config.CourtyardDepth));
            StructureFootprintRect world = StructureCardinalTransform.Rect(in local, config.EntryFacing);
            int3 min = new int3(origin.x + world.Min.x, baseY, origin.z + world.Min.y);
            authoring.HollowBox(
                min,
                new int3(world.Size.x, config.CourtyardWallHeight, world.Size.y),
                config.WallThickness,
                config.Palette.Resolve(StructureMaterialRole.SecondaryWall),
                false,
                false);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring,
                min,
                world.Size.x,
                config.CourtyardWallHeight,
                world.Size.y,
                config.WallThickness,
                in config.CourtyardGate,
                1,
                StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing),
                0,
                0,
                in config.Palette);
        }

        private static void AuthorColonnade(
            IStructureAuthoringSession authoring,
            int3 origin,
            int baseY,
            in TempleConfig config)
        {
            int minX = -config.PlatformWidth / 2 + config.ColumnInset;
            int maxX = config.PlatformWidth / 2 - config.ColumnInset;
            int minZ = -config.PlatformDepth / 2 + config.ColumnInset;
            int maxZ = config.PlatformDepth / 2 - config.ColumnInset;
            int countX = config.Columns.MaxCountForSpan(maxX - minX + config.Columns.Width, 0);
            int countZ = config.Columns.MaxCountForSpan(maxZ - minZ + config.Columns.Width, 0);

            for (int i = 0; i < countX; i++)
            {
                int x = minX + i * config.Columns.Spacing;
                AuthorColumn(authoring, origin, baseY, new int2(x, minZ), in config);
                AuthorColumn(authoring, origin, baseY, new int2(x, maxZ), in config);
            }
            for (int i = 1; i < countZ - 1; i++)
            {
                int z = minZ + i * config.Columns.Spacing;
                AuthorColumn(authoring, origin, baseY, new int2(minX, z), in config);
                AuthorColumn(authoring, origin, baseY, new int2(maxX, z), in config);
            }
        }

        private static void AuthorColumn(
            IStructureAuthoringSession authoring,
            int3 origin,
            int baseY,
            int2 local,
            in TempleConfig config)
        {
            int2 world = StructureCardinalTransform.Point(local, config.EntryFacing);
            SharedColumnAuthoring.AuthorColumn(
                authoring,
                new int3(origin.x + world.x, baseY, origin.z + world.y),
                in config.Columns,
                in config.Palette);
        }
    }
}
