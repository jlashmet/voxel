using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedOpeningAuthoring = VoxelEngine.Structures.Runtime.StructureOpeningAuthoring;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministic church composition. The authored plan is local-South and rotated cardinally
    /// through StructureCardinalTransform before all shell/opening/roof writes.
    /// </summary>
    public static class ChurchAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int3 origin,
            in ChurchConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Church configuration is invalid.", nameof(config));

            int baseY = origin.y;
            int frontZ = config.Footprint.Primary.Min.y;
            byte wallMaterial = config.Palette.Resolve(config.NaveWalls.PrimaryMaterial);

            AuthorFoundation(authoring, origin, in config);

            var naveLocal = new StructureFootprintRect(
                new int2(-config.NaveWidth / 2, frontZ),
                new int2(config.NaveWidth, config.NaveLength));
            ResolveRect(in naveLocal, origin, config.EntryFacing,
                out int3 naveMin, out int naveWidth, out int naveDepth);
            authoring.HollowBox(
                naveMin,
                new int3(naveWidth, config.NaveWalls.Height, naveDepth),
                config.WallThickness,
                wallMaterial,
                false,
                false);

            StructureFootprintRect westAisleLocal = default;
            StructureFootprintRect eastAisleLocal = default;
            int3 westAisleMin = default;
            int3 eastAisleMin = default;
            int westAisleWidth = 0, westAisleDepth = 0;
            int eastAisleWidth = 0, eastAisleDepth = 0;
            if (config.AislesEnabled)
            {
                westAisleLocal = new StructureFootprintRect(
                    new int2(-config.NaveWidth / 2 - config.AisleWidth, frontZ),
                    new int2(config.AisleWidth, config.NaveLength));
                eastAisleLocal = new StructureFootprintRect(
                    new int2(config.NaveWidth / 2, frontZ),
                    new int2(config.AisleWidth, config.NaveLength));
                ResolveRect(in westAisleLocal, origin, config.EntryFacing,
                    out westAisleMin, out westAisleWidth, out westAisleDepth);
                ResolveRect(in eastAisleLocal, origin, config.EntryFacing,
                    out eastAisleMin, out eastAisleWidth, out eastAisleDepth);
                authoring.HollowBox(
                    westAisleMin,
                    new int3(westAisleWidth, config.AisleHeight, westAisleDepth),
                    config.WallThickness,
                    wallMaterial,
                    false,
                    false);
                authoring.HollowBox(
                    eastAisleMin,
                    new int3(eastAisleWidth, config.AisleHeight, eastAisleDepth),
                    config.WallThickness,
                    wallMaterial,
                    false,
                    false);
            }

            var sanctuaryLocal = new StructureFootprintRect(
                new int2(-config.SanctuaryWidth / 2, frontZ + config.NaveLength),
                new int2(config.SanctuaryWidth, config.SanctuaryLength));
            ResolveRect(in sanctuaryLocal, origin, config.EntryFacing,
                out int3 sanctuaryMin, out int sanctuaryWidth, out int sanctuaryDepth);
            authoring.HollowBox(
                sanctuaryMin,
                new int3(sanctuaryWidth, config.SanctuaryHeight, sanctuaryDepth),
                config.WallThickness,
                wallMaterial,
                false,
                false);

            AuthorInternalConnections(
                authoring,
                naveMin,
                naveWidth,
                naveDepth,
                westAisleMin,
                westAisleWidth,
                westAisleDepth,
                eastAisleMin,
                eastAisleWidth,
                eastAisleDepth,
                sanctuaryMin,
                sanctuaryWidth,
                sanctuaryDepth,
                in config);

            AuthorApse(authoring, origin, frontZ, in config);
            AuthorFacadeOpenings(
                authoring,
                naveMin,
                naveWidth,
                naveDepth,
                westAisleMin,
                westAisleWidth,
                westAisleDepth,
                eastAisleMin,
                eastAisleWidth,
                eastAisleDepth,
                in config);
            AuthorBellTower(authoring, origin, frontZ, naveMin, naveWidth, naveDepth, in config);

            AuthorRoof(authoring, naveMin, naveWidth, naveDepth,
                baseY + config.NaveWalls.Height, in config.NaveRoof,
                config.EntryFacing, in config.Palette);
            if (config.AislesEnabled)
            {
                AuthorRoof(authoring, westAisleMin, westAisleWidth, westAisleDepth,
                    baseY + config.AisleHeight, in config.AisleRoof,
                    config.EntryFacing, in config.Palette);
                AuthorRoof(authoring, eastAisleMin, eastAisleWidth, eastAisleDepth,
                    baseY + config.AisleHeight, in config.AisleRoof,
                    config.EntryFacing, in config.Palette);
            }
            AuthorRoof(authoring, sanctuaryMin, sanctuaryWidth, sanctuaryDepth,
                baseY + config.SanctuaryHeight, in config.SanctuaryRoof,
                config.EntryFacing, in config.Palette);
        }

        private static void AuthorFoundation(
            IStructureAuthoringSession authoring,
            int3 origin,
            in ChurchConfig config)
        {
            if (config.Footprint.FoundationStyle == StructureFoundationStyle.None) return;
            if (config.Footprint.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException(
                    "Church authoring currently supports None or Slab foundations only.", nameof(config));

            StructureFootprintRect world = StructureCardinalTransform.Rect(
                in config.Footprint.Primary,
                config.EntryFacing);
            authoring.Box(
                new int3(origin.x + world.Min.x,
                    origin.y - config.Footprint.FoundationDepth,
                    origin.z + world.Min.y),
                new int3(world.Size.x, config.Footprint.FoundationDepth, world.Size.y),
                config.Palette.Resolve(config.Footprint.FoundationMaterial));
        }

        private static void AuthorInternalConnections(
            IStructureAuthoringSession authoring,
            int3 naveMin,
            int naveWidth,
            int naveDepth,
            int3 westAisleMin,
            int westAisleWidth,
            int westAisleDepth,
            int3 eastAisleMin,
            int eastAisleWidth,
            int eastAisleDepth,
            int3 sanctuaryMin,
            int sanctuaryWidth,
            int sanctuaryDepth,
            in ChurchConfig config)
        {
            Facing naveNorth = StructureCardinalTransform.FacingDirection(Facing.North, config.EntryFacing);
            Facing sanctuarySouth = StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                config.WallThickness, in config.SanctuaryArch, 1,
                naveNorth, 0, 0, in config.Palette);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, sanctuaryMin, sanctuaryWidth, config.SanctuaryHeight, sanctuaryDepth,
                config.WallThickness, in config.SanctuaryArch, 1,
                sanctuarySouth, 0, 0, in config.Palette);

            if (!config.AislesEnabled) return;
            int count = config.AisleArch.MaxCountForSpan(config.NaveLength);
            Facing west = StructureCardinalTransform.FacingDirection(Facing.West, config.EntryFacing);
            Facing east = StructureCardinalTransform.FacingDirection(Facing.East, config.EntryFacing);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                config.WallThickness, in config.AisleArch, count,
                west, 0, config.AisleArch.Spacing, in config.Palette);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, westAisleMin, westAisleWidth, config.AisleHeight, westAisleDepth,
                config.WallThickness, in config.AisleArch, count,
                east, 0, config.AisleArch.Spacing, in config.Palette);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                config.WallThickness, in config.AisleArch, count,
                east, 0, config.AisleArch.Spacing, in config.Palette);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, eastAisleMin, eastAisleWidth, config.AisleHeight, eastAisleDepth,
                config.WallThickness, in config.AisleArch, count,
                west, 0, config.AisleArch.Spacing, in config.Palette);
        }

        private static void AuthorFacadeOpenings(
            IStructureAuthoringSession authoring,
            int3 naveMin,
            int naveWidth,
            int naveDepth,
            int3 westAisleMin,
            int westAisleWidth,
            int westAisleDepth,
            int3 eastAisleMin,
            int eastAisleWidth,
            int eastAisleDepth,
            in ChurchConfig config)
        {
            Facing south = StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing);
            Facing west = StructureCardinalTransform.FacingDirection(Facing.West, config.EntryFacing);
            Facing east = StructureCardinalTransform.FacingDirection(Facing.East, config.EntryFacing);

            if (config.BellTowerPlacement != ChurchBellTowerPlacement.FrontCentre)
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                    config.WallThickness, in config.MainPortal, 1,
                    south, 0, 0, in config.Palette);

            int windowCount = config.Window.MaxCountForSpan(config.NaveLength);
            if (config.AislesEnabled)
            {
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, westAisleMin, westAisleWidth, config.AisleHeight, westAisleDepth,
                    config.WallThickness, in config.Window, windowCount,
                    west, 0, config.Window.Spacing, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, eastAisleMin, eastAisleWidth, config.AisleHeight, eastAisleDepth,
                    config.WallThickness, in config.Window, windowCount,
                    east, 0, config.Window.Spacing, in config.Palette);
            }
            else
            {
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                    config.WallThickness, in config.Window, windowCount,
                    west, 0, config.Window.Spacing, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                    config.WallThickness, in config.Window, windowCount,
                    east, 0, config.Window.Spacing, in config.Palette);
            }

            if (config.SideDoorsEnabled)
            {
                int3 westShell = config.AislesEnabled ? westAisleMin : naveMin;
                int westWidth = config.AislesEnabled ? westAisleWidth : naveWidth;
                int westDepth = config.AislesEnabled ? westAisleDepth : naveDepth;
                int westHeight = config.AislesEnabled ? config.AisleHeight : config.NaveWalls.Height;
                int3 eastShell = config.AislesEnabled ? eastAisleMin : naveMin;
                int eastWidth = config.AislesEnabled ? eastAisleWidth : naveWidth;
                int eastDepth = config.AislesEnabled ? eastAisleDepth : naveDepth;
                int eastHeight = config.AislesEnabled ? config.AisleHeight : config.NaveWalls.Height;
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, westShell, westWidth, westHeight, westDepth,
                    config.WallThickness, in config.SideDoor, 1,
                    west, 0, 0, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, eastShell, eastWidth, eastHeight, eastDepth,
                    config.WallThickness, in config.SideDoor, 1,
                    east, 0, 0, in config.Palette);
            }

            if (config.ClerestoryEnabled)
            {
                int count = config.ClerestoryWindow.MaxCountForSpan(config.NaveLength);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                    config.WallThickness, in config.ClerestoryWindow, count,
                    west, 0, config.ClerestoryWindow.Spacing, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, naveMin, naveWidth, config.NaveWalls.Height, naveDepth,
                    config.WallThickness, in config.ClerestoryWindow, count,
                    east, 0, config.ClerestoryWindow.Spacing, in config.Palette);
            }
        }

        private static void AuthorApse(
            IStructureAuthoringSession authoring,
            int3 origin,
            int frontZ,
            in ChurchConfig config)
        {
            if (!config.ApseEnabled) return;
            int localCentreZ = frontZ + config.NaveLength + config.SanctuaryLength;
            int2 rotatedCentre = StructureCardinalTransform.Point(
                new int2(0, localCentreZ), config.EntryFacing);
            int cx = origin.x + rotatedCentre.x;
            int cz = origin.z + rotatedCentre.y;
            byte wall = config.Palette.Resolve(config.NaveWalls.PrimaryMaterial);
            authoring.Cylinder(
                cx, origin.y, cz,
                config.ApseRadius,
                config.ApseHeight,
                wall,
                config.ApseRadius - config.WallThickness);

            // Remove the structure-facing half of the ring so the sanctuary and apse form one
            // navigable volume while retaining a true rounded exterior on the far half.
            var localSouthHalf = new StructureFootprintRect(
                new int2(-config.ApseRadius, localCentreZ - config.ApseRadius),
                new int2(config.ApseRadius * 2, config.ApseRadius + 1));
            ResolveRect(in localSouthHalf, origin, config.EntryFacing,
                out int3 carveMin, out int carveWidth, out int carveDepth);
            authoring.Box(
                carveMin,
                new int3(carveWidth, config.ApseHeight, carveDepth),
                config.Palette.Resolve(StructureMaterialRole.Opening));

            authoring.Cone(
                cx,
                origin.y + config.ApseHeight,
                cz,
                config.ApseRadius + config.SanctuaryRoof.EaveOverhang,
                config.ApseRoofHeight,
                config.Palette.Resolve(config.SanctuaryRoof.MaterialRole));
        }

        private static void AuthorBellTower(
            IStructureAuthoringSession authoring,
            int3 origin,
            int frontZ,
            int3 naveMin,
            int naveWidth,
            int naveDepth,
            in ChurchConfig config)
        {
            if (config.BellTowerPlacement == ChurchBellTowerPlacement.None) return;

            int towerX;
            switch (config.BellTowerPlacement)
            {
                case ChurchBellTowerPlacement.FrontCentre:
                    towerX = -config.BellTower.Width / 2;
                    break;
                case ChurchBellTowerPlacement.FrontLeft:
                    towerX = -config.NaveWidth / 2;
                    break;
                case ChurchBellTowerPlacement.FrontRight:
                    towerX = config.NaveWidth / 2 - config.BellTower.Width;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(config.BellTowerPlacement));
            }

            var local = new StructureFootprintRect(
                new int2(towerX, frontZ),
                new int2(config.BellTower.Width, config.BellTower.Depth));
            ResolveRect(in local, origin, config.EntryFacing,
                out int3 towerMin, out int towerWidth, out int towerDepth);
            authoring.HollowBox(
                towerMin,
                new int3(towerWidth, config.BellTower.Height, towerDepth),
                config.WallThickness,
                config.Palette.Resolve(config.BellTower.WallMaterialRole),
                false,
                false);

            Facing north = StructureCardinalTransform.FacingDirection(Facing.North, config.EntryFacing);
            Facing south = StructureCardinalTransform.FacingDirection(Facing.South, config.EntryFacing);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                config.WallThickness, in config.MainPortal, 1,
                north, 0, 0, in config.Palette);
            if (config.BellTowerPlacement == ChurchBellTowerPlacement.FrontCentre)
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                    config.WallThickness, in config.MainPortal, 1,
                    south, 0, 0, in config.Palette);

            if (config.BellTower.OpeningsEnabled)
            {
                Facing west = StructureCardinalTransform.FacingDirection(Facing.West, config.EntryFacing);
                Facing east = StructureCardinalTransform.FacingDirection(Facing.East, config.EntryFacing);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                    config.WallThickness, in config.BellTower.Opening, 1,
                    south, 0, 0, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                    config.WallThickness, in config.BellTower.Opening, 1,
                    north, 0, 0, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                    config.WallThickness, in config.BellTower.Opening, 1,
                    west, 0, 0, in config.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, towerMin, towerWidth, config.BellTower.Height, towerDepth,
                    config.WallThickness, in config.BellTower.Opening, 1,
                    east, 0, 0, in config.Palette);
            }

            AuthorRoof(authoring, towerMin, towerWidth, towerDepth,
                origin.y + config.BellTower.Height, in config.BellTower.Roof,
                config.EntryFacing, in config.Palette);

            if (config.SpireEnabled)
            {
                int2 localCentre = new int2(
                    towerX + config.BellTower.Width / 2,
                    frontZ + config.BellTower.Depth / 2);
                int2 centre = StructureCardinalTransform.Point(localCentre, config.EntryFacing);
                authoring.Cone(
                    origin.x + centre.x,
                    origin.y + config.BellTower.Height + math.max(2, config.BellTower.Roof.PitchRise / 2),
                    origin.z + centre.y,
                    math.max(2, math.min(config.BellTower.Width, config.BellTower.Depth) / 2),
                    config.SpireHeight,
                    config.Palette.Resolve(config.BellTower.Roof.MaterialRole));
            }
        }

        private static void AuthorRoof(
            IStructureAuthoringSession authoring,
            int3 shellMin,
            int width,
            int depth,
            int roofY,
            in RoofConfig localRoof,
            Facing entryFacing,
            in StructureMaterialPalette palette)
        {
            RoofConfig roof = localRoof;
            roof.RidgeAxis = StructureCardinalTransform.Axis(localRoof.RidgeAxis, entryFacing);
            SharedRoofAuthoring.Author(
                authoring, shellMin, width, depth, roofY,
                in roof, palette.Resolve(roof.MaterialRole));
        }

        private static void ResolveRect(
            in StructureFootprintRect local,
            int3 origin,
            Facing facing,
            out int3 min,
            out int width,
            out int depth)
        {
            StructureFootprintRect rotated = StructureCardinalTransform.Rect(in local, facing);
            min = new int3(origin.x + rotated.Min.x, origin.y, origin.z + rotated.Min.y);
            width = rotated.Size.x;
            depth = rotated.Size.y;
        }
    }
}
