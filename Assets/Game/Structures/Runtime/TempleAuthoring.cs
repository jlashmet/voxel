using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Temple composition over shared platform/steps, opening, roof, and column contracts.</summary>
    public static class TempleAuthoring
    {
        public static void Author(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 origin,
            in TempleConfig config)
        {
            if (components == null) throw new System.ArgumentNullException(nameof(components));
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

            AuthorApproach(components, authoring, origin, in config);
            int sanctuaryBaseY = origin.y + config.PlatformHeight;
            AuthorSanctuary(components, authoring, origin, sanctuaryBaseY, in config);
            if (config.CourtyardEnabled)
                AuthorCourtyard(components, authoring, origin, sanctuaryBaseY, in config);
            if (config.ColonnadeEnabled)
                AuthorColonnade(components, authoring, origin, sanctuaryBaseY, in config);
        }

        private static void AuthorApproach(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 origin,
            in TempleConfig config)
        {
            int localBottomZ = config.Footprint.Primary.Min.y - config.ApproachStairs.TotalRun;
            int2 localBottom = new int2(0, localBottomZ);
            int2 worldBottom = StructureCardinalTransform.Point(localBottom, config.EntryFacing);
            var request = new StructureStairAuthoringRequest
            {
                BottomCentre = new int3(origin.x + worldBottom.x, origin.y, origin.z + worldBottom.y),
                AscentDirection = StructureCardinalTransform.FacingDirection(Facing.North, config.EntryFacing),
                Stair = config.ApproachStairs,
                Palette = config.Palette,
            };
            components.AuthorStair(authoring, in request);
        }

        private static void AuthorSanctuary(
            IStructureComponentAuthoring components,
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

            var opening = new StructureOpeningAuthoringRequest
            {
                ShellMin = min,
                Width = world.Size.x,
                Height = config.SanctuaryHeight,
                Depth = world.Size.y,
                WallThickness = config.WallThickness,
                Opening = config.SanctuaryDoor,
                Count = 1,
                Facade = StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing),
                Palette = config.Palette,
            };
            components.AuthorOpenings(authoring, in opening);

            RoofConfig roof = config.SanctuaryRoof;
            roof.RidgeAxis = StructureCardinalTransform.Axis(roof.RidgeAxis, config.EntryFacing);
            var roofRequest = new StructureRoofAuthoringRequest
            {
                FootprintMin = min,
                Width = world.Size.x,
                Depth = world.Size.y,
                BaseY = baseY + config.SanctuaryHeight,
                Roof = roof,
                Material = config.Palette.Resolve(roof.MaterialRole),
            };
            components.AuthorRoof(authoring, in roofRequest);
        }

        private static void AuthorCourtyard(
            IStructureComponentAuthoring components,
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
            var opening = new StructureOpeningAuthoringRequest
            {
                ShellMin = min,
                Width = world.Size.x,
                Height = config.CourtyardWallHeight,
                Depth = world.Size.y,
                WallThickness = config.WallThickness,
                Opening = config.CourtyardGate,
                Count = 1,
                Facade = StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing),
                Palette = config.Palette,
            };
            components.AuthorOpenings(authoring, in opening);
        }

        private static void AuthorColonnade(
            IStructureComponentAuthoring components,
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
                AuthorColumn(components, authoring, origin, baseY, new int2(x, minZ), in config);
                AuthorColumn(components, authoring, origin, baseY, new int2(x, maxZ), in config);
            }
            for (int i = 1; i < countZ - 1; i++)
            {
                int z = minZ + i * config.Columns.Spacing;
                AuthorColumn(components, authoring, origin, baseY, new int2(minX, z), in config);
                AuthorColumn(components, authoring, origin, baseY, new int2(maxX, z), in config);
            }
        }

        private static void AuthorColumn(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 origin,
            int baseY,
            int2 local,
            in TempleConfig config)
        {
            int2 world = StructureCardinalTransform.Point(local, config.EntryFacing);
            var request = new StructureColumnAuthoringRequest
            {
                BaseCentre = new int3(origin.x + world.x, baseY, origin.z + world.y),
                Column = config.Columns,
                Palette = config.Palette,
            };
            components.AuthorColumn(authoring, in request);
        }
    }
}
