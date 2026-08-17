using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedOpeningAuthoring = VoxelEngine.Structures.Runtime.StructureOpeningAuthoring;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Cathedral-scale composition over ChurchAuthoring. The nested ChurchConfig remains the source
    /// of nave/choir/apse semantics; this layer adds only cathedral-specific massing and attachments.
    /// </summary>
    public static class CathedralAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Cathedral configuration is invalid.", nameof(config));

            AuthorFoundation(authoring, origin, in config);

            // The cathedral owns the overall slab. Suppress the nested church slab so the base
            // composition does not redundantly rewrite the same foundation region.
            ChurchConfig church = config.Church;
            church.Footprint.FoundationStyle = StructureFoundationStyle.None;
            church.Footprint.FoundationDepth = 0;
            ChurchAuthoring.Author(authoring, origin, in church);

            AuthorExtraAisles(authoring, origin, in config);
            AuthorTransept(authoring, origin, in config);
            AuthorSideChapels(authoring, origin, in config);
            AuthorRoseWindow(authoring, origin, in config);
            AuthorWestFrontTowers(authoring, origin, in config);
            AuthorCrossingTower(authoring, origin, in config);
            AuthorCrypt(authoring, origin, in config);
        }

        private static void AuthorFoundation(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (config.Footprint.FoundationStyle == StructureFoundationStyle.None) return;
            if (config.Footprint.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException(
                    "Cathedral authoring currently supports None or Slab foundations only.", nameof(config));

            StructureFootprintRect world = StructureCardinalTransform.Rect(
                in config.Footprint.Primary,
                config.Church.EntryFacing);
            authoring.Box(
                new int3(
                    origin.x + world.Min.x,
                    origin.y - config.Footprint.FoundationDepth,
                    origin.z + world.Min.y),
                new int3(world.Size.x, config.Footprint.FoundationDepth, world.Size.y),
                config.Church.Palette.Resolve(config.Footprint.FoundationMaterial));
        }

        private static void AuthorExtraAisles(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (config.ExtraAisleCountPerSide <= 0) return;

            ChurchConfig church = config.Church;
            int frontZ = church.Footprint.Primary.Min.y;
            int wall = church.WallThickness;
            int archCount = config.ExtraAisleArch.MaxCountForSpan(church.NaveLength);
            int windowCount = config.ExtraAisleWindow.MaxCountForSpan(church.NaveLength);
            Facing localWest = Facing.West;
            Facing localEast = Facing.East;

            StructureFootprintRect previousWestLocal = new StructureFootprintRect(
                new int2(-church.NaveWidth / 2 - church.AisleWidth, frontZ),
                new int2(church.AisleWidth, church.NaveLength));
            StructureFootprintRect previousEastLocal = new StructureFootprintRect(
                new int2(church.NaveWidth / 2, frontZ),
                new int2(church.AisleWidth, church.NaveLength));
            int previousWestHeight = church.AisleHeight;
            int previousEastHeight = church.AisleHeight;

            for (int level = 0; level < config.ExtraAisleCountPerSide; level++)
            {
                int baseHalfWidth = config.BaseAssemblyWidth / 2 + level * config.ExtraAisleWidth;
                var westLocal = new StructureFootprintRect(
                    new int2(-baseHalfWidth - config.ExtraAisleWidth, frontZ),
                    new int2(config.ExtraAisleWidth, church.NaveLength));
                var eastLocal = new StructureFootprintRect(
                    new int2(baseHalfWidth, frontZ),
                    new int2(config.ExtraAisleWidth, church.NaveLength));

                ResolveRect(in westLocal, origin, church.EntryFacing,
                    out int3 westMin, out int westWidth, out int westDepth);
                ResolveRect(in eastLocal, origin, church.EntryFacing,
                    out int3 eastMin, out int eastWidth, out int eastDepth);
                authoring.HollowBox(
                    westMin,
                    new int3(westWidth, config.ExtraAisleHeight, westDepth),
                    wall,
                    church.Palette.Resolve(church.NaveWalls.PrimaryMaterial),
                    false,
                    false);
                authoring.HollowBox(
                    eastMin,
                    new int3(eastWidth, config.ExtraAisleHeight, eastDepth),
                    wall,
                    church.Palette.Resolve(church.NaveWalls.PrimaryMaterial),
                    false,
                    false);

                ResolveRect(in previousWestLocal, origin, church.EntryFacing,
                    out int3 previousWestMin, out int previousWestWidth, out int previousWestDepth);
                ResolveRect(in previousEastLocal, origin, church.EntryFacing,
                    out int3 previousEastMin, out int previousEastWidth, out int previousEastDepth);
                Facing west = StructureCardinalTransform.FacingDirection(localWest, church.EntryFacing);
                Facing east = StructureCardinalTransform.FacingDirection(localEast, church.EntryFacing);

                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    previousWestMin,
                    previousWestWidth,
                    previousWestHeight,
                    previousWestDepth,
                    wall,
                    in config.ExtraAisleArch,
                    archCount,
                    west,
                    0,
                    config.ExtraAisleArch.Spacing,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    westMin,
                    westWidth,
                    config.ExtraAisleHeight,
                    westDepth,
                    wall,
                    in config.ExtraAisleArch,
                    archCount,
                    east,
                    0,
                    config.ExtraAisleArch.Spacing,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    previousEastMin,
                    previousEastWidth,
                    previousEastHeight,
                    previousEastDepth,
                    wall,
                    in config.ExtraAisleArch,
                    archCount,
                    east,
                    0,
                    config.ExtraAisleArch.Spacing,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    eastMin,
                    eastWidth,
                    config.ExtraAisleHeight,
                    eastDepth,
                    wall,
                    in config.ExtraAisleArch,
                    archCount,
                    west,
                    0,
                    config.ExtraAisleArch.Spacing,
                    in church.Palette);

                AuthorRoof(
                    authoring,
                    westMin,
                    westWidth,
                    westDepth,
                    origin.y + config.ExtraAisleHeight,
                    in config.ExtraAisleRoof,
                    church.EntryFacing,
                    in church.Palette);
                AuthorRoof(
                    authoring,
                    eastMin,
                    eastWidth,
                    eastDepth,
                    origin.y + config.ExtraAisleHeight,
                    in config.ExtraAisleRoof,
                    church.EntryFacing,
                    in church.Palette);

                if (level == config.ExtraAisleCountPerSide - 1)
                {
                    SharedOpeningAuthoring.AuthorRepeated(
                        authoring,
                        westMin,
                        westWidth,
                        config.ExtraAisleHeight,
                        westDepth,
                        wall,
                        in config.ExtraAisleWindow,
                        windowCount,
                        west,
                        0,
                        config.ExtraAisleWindow.Spacing,
                        in church.Palette);
                    SharedOpeningAuthoring.AuthorRepeated(
                        authoring,
                        eastMin,
                        eastWidth,
                        config.ExtraAisleHeight,
                        eastDepth,
                        wall,
                        in config.ExtraAisleWindow,
                        windowCount,
                        east,
                        0,
                        config.ExtraAisleWindow.Spacing,
                        in church.Palette);
                }

                previousWestLocal = westLocal;
                previousEastLocal = eastLocal;
                previousWestHeight = config.ExtraAisleHeight;
                previousEastHeight = config.ExtraAisleHeight;
            }
        }

        private static void AuthorTransept(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            ChurchConfig church = config.Church;
            int frontZ = church.Footprint.Primary.Min.y;
            int transeptMinZ = frontZ + config.TranseptCentreFromNaveFront - config.TranseptDepth / 2;
            var local = new StructureFootprintRect(
                new int2(-config.TranseptWidth / 2, transeptMinZ),
                new int2(config.TranseptWidth, config.TranseptDepth));
            ResolveRect(in local, origin, church.EntryFacing,
                out int3 min, out int width, out int depth);

            authoring.HollowBox(
                min,
                new int3(width, config.TranseptHeight, depth),
                church.WallThickness,
                church.Palette.Resolve(church.NaveWalls.PrimaryMaterial),
                false,
                false);

            // The crossing clearance removes only the central assembly slice. It clears the
            // inherited nave/aisle side walls and the transept north/south walls without punching
            // through the exterior ends of either transept arm.
            var crossingLocal = new StructureFootprintRect(
                new int2(
                    -config.NaveAssemblyWidth / 2 + church.WallThickness,
                    transeptMinZ),
                new int2(
                    config.NaveAssemblyWidth - church.WallThickness * 2,
                    config.TranseptDepth));
            ResolveRect(in crossingLocal, origin, church.EntryFacing,
                out int3 crossingMin, out int crossingWidth, out int crossingDepth);
            authoring.Box(
                crossingMin,
                new int3(crossingWidth, config.CrossingClearanceHeight, crossingDepth),
                church.Palette.Resolve(StructureMaterialRole.Opening));

            AuthorRoof(
                authoring,
                min,
                width,
                depth,
                origin.y + config.TranseptHeight,
                in config.TranseptRoof,
                church.EntryFacing,
                in church.Palette);
        }

        private static void AuthorSideChapels(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (!config.SideChapelsEnabled) return;

            ChurchConfig church = config.Church;
            int wall = church.WallThickness;
            int frontZ = church.Footprint.Primary.Min.y;
            int sanctuaryStartZ = frontZ + church.NaveLength;
            int sanctuaryCentreZ = sanctuaryStartZ + church.SanctuaryLength / 2;
            var sanctuaryLocal = new StructureFootprintRect(
                new int2(-church.SanctuaryWidth / 2, sanctuaryStartZ),
                new int2(church.SanctuaryWidth, church.SanctuaryLength));
            ResolveRect(in sanctuaryLocal, origin, church.EntryFacing,
                out int3 sanctuaryMin, out int sanctuaryWidth, out int sanctuaryDepth);

            int groupLength = config.SideChapelWidth +
                (config.SideChapelCountPerSide - 1) * config.SideChapelSpacing;
            int firstCentreZ = sanctuaryCentreZ - groupLength / 2 + config.SideChapelWidth / 2;
            Facing worldWest = StructureCardinalTransform.FacingDirection(Facing.West, church.EntryFacing);
            Facing worldEast = StructureCardinalTransform.FacingDirection(Facing.East, church.EntryFacing);

            for (int i = 0; i < config.SideChapelCountPerSide; i++)
            {
                int centreZ = firstCentreZ + i * config.SideChapelSpacing;
                int groupOffset = centreZ - sanctuaryCentreZ;
                var westLocal = new StructureFootprintRect(
                    new int2(
                        -church.SanctuaryWidth / 2 - config.SideChapelDepth,
                        centreZ - config.SideChapelWidth / 2),
                    new int2(config.SideChapelDepth, config.SideChapelWidth));
                var eastLocal = new StructureFootprintRect(
                    new int2(
                        church.SanctuaryWidth / 2,
                        centreZ - config.SideChapelWidth / 2),
                    new int2(config.SideChapelDepth, config.SideChapelWidth));
                ResolveRect(in westLocal, origin, church.EntryFacing,
                    out int3 westMin, out int westWidth, out int westDepth);
                ResolveRect(in eastLocal, origin, church.EntryFacing,
                    out int3 eastMin, out int eastWidth, out int eastDepth);

                authoring.HollowBox(
                    westMin,
                    new int3(westWidth, config.SideChapelHeight, westDepth),
                    wall,
                    church.Palette.Resolve(church.NaveWalls.PrimaryMaterial),
                    false,
                    false);
                authoring.HollowBox(
                    eastMin,
                    new int3(eastWidth, config.SideChapelHeight, eastDepth),
                    wall,
                    church.Palette.Resolve(church.NaveWalls.PrimaryMaterial),
                    false,
                    false);

                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    sanctuaryMin,
                    sanctuaryWidth,
                    church.SanctuaryHeight,
                    sanctuaryDepth,
                    wall,
                    in config.SideChapelArch,
                    1,
                    worldWest,
                    groupOffset,
                    0,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    westMin,
                    westWidth,
                    config.SideChapelHeight,
                    westDepth,
                    wall,
                    in config.SideChapelArch,
                    1,
                    worldEast,
                    0,
                    0,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    sanctuaryMin,
                    sanctuaryWidth,
                    church.SanctuaryHeight,
                    sanctuaryDepth,
                    wall,
                    in config.SideChapelArch,
                    1,
                    worldEast,
                    groupOffset,
                    0,
                    in church.Palette);
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    eastMin,
                    eastWidth,
                    config.SideChapelHeight,
                    eastDepth,
                    wall,
                    in config.SideChapelArch,
                    1,
                    worldWest,
                    0,
                    0,
                    in church.Palette);

                AuthorRoof(
                    authoring,
                    westMin,
                    westWidth,
                    westDepth,
                    origin.y + config.SideChapelHeight,
                    in config.SideChapelRoof,
                    church.EntryFacing,
                    in church.Palette);
                AuthorRoof(
                    authoring,
                    eastMin,
                    eastWidth,
                    eastDepth,
                    origin.y + config.SideChapelHeight,
                    in config.SideChapelRoof,
                    church.EntryFacing,
                    in church.Palette);
            }
        }

        private static void AuthorRoseWindow(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (!config.RoseWindowEnabled) return;
            ChurchConfig church = config.Church;
            int frontZ = church.Footprint.Primary.Min.y;
            var naveLocal = new StructureFootprintRect(
                new int2(-church.NaveWidth / 2, frontZ),
                new int2(church.NaveWidth, church.NaveLength));
            ResolveRect(in naveLocal, origin, church.EntryFacing,
                out int3 naveMin, out int naveWidth, out int naveDepth);
            SharedOpeningAuthoring.AuthorRepeated(
                authoring,
                naveMin,
                naveWidth,
                church.NaveWalls.Height,
                naveDepth,
                church.WallThickness,
                in config.RoseWindow,
                1,
                StructureCardinalTransform.FacingDirection(Facing.South, church.EntryFacing),
                0,
                0,
                in church.Palette);
        }

        private static void AuthorWestFrontTowers(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (!config.WestFrontTowersEnabled) return;
            AuthorFrontTower(authoring, origin, -config.WestTowerCentreOffset, in config);
            AuthorFrontTower(authoring, origin, config.WestTowerCentreOffset, in config);
        }

        private static void AuthorFrontTower(
            IStructureAuthoringSession authoring,
            int3 origin,
            int localCentreX,
            in CathedralConfig config)
        {
            ChurchConfig church = config.Church;
            TowerConfig tower = config.WestFrontTower;
            int frontZ = church.Footprint.Primary.Min.y;
            var local = new StructureFootprintRect(
                new int2(localCentreX - tower.Width / 2, frontZ),
                new int2(tower.Width, tower.Depth));
            ResolveRect(in local, origin, church.EntryFacing,
                out int3 min, out int width, out int depth);
            AuthorTowerShellAndOpenings(
                authoring,
                min,
                width,
                depth,
                origin.y,
                in tower,
                church.WallThickness,
                church.EntryFacing,
                in church.Palette);
            AuthorRoof(
                authoring,
                min,
                width,
                depth,
                origin.y + tower.Height,
                in tower.Roof,
                church.EntryFacing,
                in church.Palette);

            if (config.WestTowerSpiresEnabled)
            {
                int2 centre = StructureCardinalTransform.Point(
                    new int2(localCentreX, frontZ + tower.Depth / 2),
                    church.EntryFacing);
                authoring.Cone(
                    origin.x + centre.x,
                    origin.y + tower.Height + math.max(2, tower.Roof.PitchRise / 2),
                    origin.z + centre.y,
                    math.max(2, math.min(tower.Width, tower.Depth) / 2),
                    config.WestTowerSpireHeight,
                    church.Palette.Resolve(tower.Roof.MaterialRole));
            }
        }

        private static void AuthorCrossingTower(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (!config.CrossingTowerEnabled) return;
            ChurchConfig church = config.Church;
            TowerConfig tower = config.CrossingTower;
            int frontZ = church.Footprint.Primary.Min.y;
            int centreZ = frontZ + config.TranseptCentreFromNaveFront;
            var local = new StructureFootprintRect(
                new int2(-tower.Width / 2, centreZ - tower.Depth / 2),
                new int2(tower.Width, tower.Depth));
            ResolveRect(in local, origin, church.EntryFacing,
                out int3 footprintMin, out int width, out int depth);
            int baseY = origin.y + math.max(church.NaveWalls.Height, config.TranseptHeight);
            int3 towerMin = new int3(footprintMin.x, baseY, footprintMin.z);
            AuthorTowerShellAndOpenings(
                authoring,
                towerMin,
                width,
                depth,
                baseY,
                in tower,
                church.WallThickness,
                church.EntryFacing,
                in church.Palette);
            AuthorRoof(
                authoring,
                towerMin,
                width,
                depth,
                baseY + tower.Height,
                in tower.Roof,
                church.EntryFacing,
                in church.Palette);

            if (config.CrossingSpireEnabled)
            {
                int2 centre = StructureCardinalTransform.Point(
                    new int2(0, centreZ),
                    church.EntryFacing);
                authoring.Cone(
                    origin.x + centre.x,
                    baseY + tower.Height + math.max(2, tower.Roof.PitchRise / 2),
                    origin.z + centre.y,
                    math.max(2, math.min(tower.Width, tower.Depth) / 2),
                    config.CrossingSpireHeight,
                    church.Palette.Resolve(tower.Roof.MaterialRole));
            }
        }

        private static void AuthorTowerShellAndOpenings(
            IStructureAuthoringSession authoring,
            int3 min,
            int width,
            int depth,
            int baseY,
            in TowerConfig tower,
            int wallThickness,
            Facing entryFacing,
            in StructureMaterialPalette palette)
        {
            authoring.HollowBox(
                min,
                new int3(width, tower.Height, depth),
                wallThickness,
                palette.Resolve(tower.WallMaterialRole),
                false,
                false);
            if (!tower.OpeningsEnabled) return;

            Facing[] facades = { Facing.South, Facing.North, Facing.West, Facing.East };
            for (int i = 0; i < facades.Length; i++)
            {
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring,
                    min,
                    width,
                    tower.Height,
                    depth,
                    wallThickness,
                    in tower.Opening,
                    1,
                    StructureCardinalTransform.FacingDirection(facades[i], entryFacing),
                    0,
                    0,
                    in palette);
            }
        }

        private static void AuthorCrypt(
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralConfig config)
        {
            if (!config.CryptEnabled) return;
            ChurchConfig church = config.Church;
            int2 localCentre = new int2(
                config.CryptAnchor.LocalPosition.x,
                config.CryptAnchor.LocalPosition.z);
            int2 centre = StructureCardinalTransform.Point(localCentre, church.EntryFacing);
            int topY = origin.y - config.CryptTopOffset;
            int bottomY = topY - config.CryptHeight;
            int3 min = new int3(
                origin.x + centre.x - config.CryptWidth / 2,
                bottomY,
                origin.z + centre.y - config.CryptDepth / 2);
            int3 size = new int3(config.CryptWidth, config.CryptHeight, config.CryptDepth);

            authoring.Box(min, size, GameMaterialIds.Empty);
            authoring.HollowBox(
                min,
                size,
                church.WallThickness,
                church.Palette.Resolve(StructureMaterialRole.Underground),
                true,
                true);
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
                authoring,
                shellMin,
                width,
                depth,
                roofY,
                in roof,
                palette.Resolve(roof.MaterialRole));
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
