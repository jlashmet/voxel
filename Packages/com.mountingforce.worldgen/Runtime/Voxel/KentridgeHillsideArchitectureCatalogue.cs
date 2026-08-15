using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Secondary built fabric that makes Kentridge read as a city embedded in its hillside rather
    /// than seventeen isolated role buildings sitting on terrain shelves.
    ///
    /// These pieces are deliberately Infrastructure: they are real hard-surface architecture, but
    /// gameplay does not bind quests or NPC identity to them. Narrow terrace dwellings grow out of
    /// steep shelf edges, roofed retaining galleries inhabit the longest exposed faces, and a high
    /// civic bridge crosses above the main ascent where the climbing road meets the summit district.
    /// </summary>
    public static class KentridgeHillsideArchitectureCatalogue
    {
        private const int DefinitionCount = 3;
        private const int TerraceHouseDefinition = 0;
        private const int CivicBridgeDefinition = 1;
        private const int RetainingGalleryDefinition = 2;
        private const int TerraceHouseCount = 7;
        private const int RetainingGalleryCount = 5;
        private const int EmbeddedBelowShelfDm = 56;
        private const int GalleryBelowShelfDm = 44;

        private readonly struct HouseSeed
        {
            public readonly int XDm;
            public readonly int ZDm;
            public readonly int ShelfXDm;
            public readonly int ShelfZDm;

            public HouseSeed(int xDm, int zDm, int shelfXDm, int shelfZDm)
            {
                XDm = xDm;
                ZDm = zDm;
                ShelfXDm = shelfXDm;
                ShelfZDm = shelfZDm;
            }
        }

        private readonly struct GallerySeed
        {
            public readonly int XDm;
            public readonly int ZDm;
            public readonly int ShelfXDm;
            public readonly int ShelfZDm;

            public GallerySeed(int xDm, int zDm, int shelfXDm, int shelfZDm)
            {
                XDm = xDm;
                ZDm = zDm;
                ShelfXDm = shelfXDm;
                ShelfZDm = shelfZDm;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            int s = settings.VoxelsPerDecimetre;
            int[] houseProgram = TerraceHouseProgram(settings);
            int[] bridgeProgram = CivicBridgeProgram(settings);
            int[] galleryProgram = RetainingGalleryProgram(settings);
            int bridgePlacement = TerraceHouseCount;
            int galleryPlacement = bridgePlacement + 1;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: houseProgram.Length + bridgeProgram.Length + galleryProgram.Length,
                materials: 0,
                explicitPlacements: TerraceHouseCount + 1 + RetainingGalleryCount,
                overrides: 0,
                allocator);

            CopyProgram(ref catalogue, 0, houseProgram);
            CopyProgram(ref catalogue, houseProgram.Length, bridgeProgram);
            CopyProgram(ref catalogue, houseProgram.Length + bridgeProgram.Length, galleryProgram);

            catalogue.Definitions[TerraceHouseDefinition] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-infrastructure-terrace-dwelling"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(64 * s, 112 * s, 58 * s),
                MaxSlope = 32,
                Precedence = 90,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = 0,
                ProgramLength = houseProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 56,
            };

            catalogue.Definitions[CivicBridgeDefinition] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-infrastructure-civic-bridge"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(100 * s, 70 * s, 30 * s),
                MaxSlope = 32,
                Precedence = 95,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = houseProgram.Length,
                ProgramLength = bridgeProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 8,
            };

            catalogue.Definitions[RetainingGalleryDefinition] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-infrastructure-retaining-gallery"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(104 * s, 72 * s, 32 * s),
                MaxSlope = 32,
                Precedence = 92,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = houseProgram.Length + bridgeProgram.Length,
                ProgramLength = galleryProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 40,
            };

            HouseSeed[] houses =
            {
                // A lower row beneath the market shops. Their roofs rise above the retaining line,
                // creating the first real cascade of overlapping urban mass in the south approach.
                new HouseSeed(720, 676, 770, 520),
                new HouseSeed(860, 676, 910, 520),
                new HouseSeed(1000, 676, 1050, 520),

                // The inn's western undercroft/annex occupies the next shelf transition.
                new HouseSeed(870, 426, 950, 340),

                // Two summit-edge dwellings sit below the church/mayor shelf on either side of the
                // main road, and one lower annex grows out beneath the Radcliffe estate.
                new HouseSeed(930, 226, 1000, 150),
                new HouseSeed(1280, 218, 1300, 150),
                new HouseSeed(1600, 382, 1650, 250),
            };

            for (int i = 0; i < houses.Length; i++)
            {
                HouseSeed house = houses[i];
                int shelfSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                    house.ShelfXDm, house.ShelfZDm, seed, s);
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(
                        house.XDm * s,
                        shelfSurface - EmbeddedBelowShelfDm * s,
                        house.ZDm * s),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
            }

            // At z=260 the authored main road is ~4.4 m below the civic summit. A 4.6 m bridge
            // clearance therefore puts the deck almost exactly on the church/mayor shelf, producing
            // a true over/under connection instead of a decorative bridge floating at arbitrary Y.
            int bridgeSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                KentridgeTownPlanner.MainSpineXDm, 260, seed, s);
            catalogue.ExplicitPlacements[bridgePlacement] = new ExplicitPlacement
            {
                Position = new int3(1120 * s, bridgeSurface, 246 * s),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            GallerySeed[] galleries =
            {
                // Roofed frontage on both sides of the upper shelf turns its remaining brown bank
                // into inhabited urban fabric without closing the main ascent.
                new GallerySeed(1000, 426, 1052, 340),
                new GallerySeed(1240, 426, 1290, 340),

                // The western civic face frames the stair/bridge approach while leaving a broad
                // opening around the centreline for the player's view toward the summit.
                new GallerySeed(1010, 226, 1060, 150),

                // Radcliffe's ridge is the largest visible plinth from the south-east overview.
                // Two gallery modules split that face into roofed, lit frontage around the annex.
                new GallerySeed(1490, 382, 1545, 250),
                new GallerySeed(1680, 382, 1735, 250),
            };

            for (int i = 0; i < galleries.Length; i++)
            {
                GallerySeed gallery = galleries[i];
                int shelfSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                    gallery.ShelfXDm, gallery.ShelfZDm, seed, s);
                catalogue.ExplicitPlacements[galleryPlacement + i] = new ExplicitPlacement
                {
                    Position = new int3(
                        gallery.XDm * s,
                        shelfSurface - GalleryBelowShelfDm * s,
                        gallery.ZDm * s),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
            }

            catalogue.Rules[TerraceHouseDefinition] = ExplicitRule(
                TerraceHouseDefinition, 0, TerraceHouseCount);
            catalogue.Rules[CivicBridgeDefinition] = ExplicitRule(
                CivicBridgeDefinition, bridgePlacement, 1);
            catalogue.Rules[RetainingGalleryDefinition] = ExplicitRule(
                RetainingGalleryDefinition, galleryPlacement, RetainingGalleryCount);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge hillside architecture catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset, int count)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 32,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static int[] TerraceHouseProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte foundation = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte wall = settings.Materials.Resolve(MaterialRole.Masonry);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte glass = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.RoofTile);
            var b = new ProgramBuilder();

            int x0 = 4 * s;
            int z0 = 4 * s;
            int width = 56 * s;
            int depth = 48 * s;
            int foundationH = 6 * s;
            int wallH = 68 * s;
            int thickness = 4 * s;
            int floorH = 34 * s;

            b.Box(x0, 0, z0, width, foundationH, depth, foundation);
            b.Box(x0, foundationH, z0, width, wallH, depth, wall);
            b.Carve(x0 + thickness, foundationH, z0 + thickness,
                    width - thickness * 2, wallH, depth - thickness * 2);

            int doorW = 12 * s;
            int doorX = x0 + width / 2 - doorW / 2;
            b.Carve(doorX, foundationH, z0, doorW, 24 * s, thickness + s);

            for (int storey = 0; storey < 2; storey++)
            {
                int y = foundationH + storey * floorH + 18 * s;
                AddWindowZ(b, x0 + 9 * s, y, z0, 10 * s, 12 * s, thickness + s, glass);
                AddWindowZ(b, x0 + width - 19 * s, y, z0,
                           10 * s, 12 * s, thickness + s, glass);
                AddWindowZ(b, x0 + 9 * s, y, z0 + depth - thickness - s,
                           10 * s, 12 * s, thickness + s, glass);
                AddWindowZ(b, x0 + width - 19 * s, y, z0 + depth - thickness - s,
                           10 * s, 12 * s, thickness + s, glass);
            }

            int sideY0 = foundationH + 18 * s;
            int sideY1 = foundationH + floorH + 18 * s;
            AddWindowX(b, x0, sideY0, z0 + 18 * s,
                       thickness + s, 12 * s, 10 * s, glass);
            AddWindowX(b, x0 + width - thickness - s, sideY0, z0 + 18 * s,
                       thickness + s, 12 * s, 10 * s, glass);
            AddWindowX(b, x0, sideY1, z0 + 18 * s,
                       thickness + s, 12 * s, 10 * s, glass);
            AddWindowX(b, x0 + width - thickness - s, sideY1, z0 + 18 * s,
                       thickness + s, 12 * s, 10 * s, glass);

            int beam = 3 * s;
            b.Box(x0, foundationH, z0 - s, beam, wallH, 3 * s, timber);
            b.Box(x0 + width - beam, foundationH, z0 - s, beam, wallH, 3 * s, timber);
            b.Box(x0, foundationH + floorH - 2 * s, z0 - s,
                  width, 4 * s, 3 * s, timber);
            b.Box(x0, foundationH + wallH - 3 * s, z0 - s,
                  width, 3 * s, 3 * s, timber);

            b.Prism(x0 - 4 * s, foundationH + wallH, z0 - 4 * s,
                    width + 8 * s, 26 * s, depth + 8 * s,
                    PrismProfile.Gable, roof);
            return b.Finish();
        }

        private static int[] CivicBridgeProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            // Piers sit outside the 56 dm carriageway, leaving a generous central opening.
            b.Box(0, 0, 4 * s, 12 * s, 48 * s, 22 * s, stone);
            b.Box(88 * s, 0, 4 * s, 12 * s, 48 * s, 22 * s, stone);
            b.Box(0, 46 * s, 0, 100 * s, 6 * s, 30 * s, stone);

            // Deep parapets make the crossing read as a real elevated street from overview angles.
            b.Box(0, 52 * s, 0, 100 * s, 8 * s, 4 * s, dark);
            b.Box(0, 52 * s, 26 * s, 100 * s, 8 * s, 4 * s, dark);
            b.Box(0, 44 * s, 4 * s, 12 * s, 6 * s, 22 * s, dark);
            b.Box(88 * s, 44 * s, 4 * s, 12 * s, 6 * s, 22 * s, dark);
            return b.Finish();
        }

        private static int[] RetainingGalleryProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte glass = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.RoofTile);
            var b = new ProgramBuilder();

            int x0 = 4 * s;
            int z0 = 4 * s;
            int width = 96 * s;
            int depth = 24 * s;
            int foundationH = 4 * s;
            int wallH = 40 * s;
            int wallTop = foundationH + wallH;

            b.Box(x0, 0, z0, width, foundationH, depth, dark);
            b.Box(x0, foundationH, z0, width, wallH, depth, stone);

            // Three deep lit recesses turn a retaining face into occupied frontage. The windows sit
            // at the rear of each reveal so the stone piers still have visible thickness.
            for (int bay = 0; bay < 3; bay++)
            {
                int openingX = x0 + (10 + bay * 29) * s;
                b.Carve(openingX, 10 * s, z0,
                        18 * s, 24 * s, 12 * s);
                b.Box(openingX + 2 * s, 12 * s, z0 + 10 * s,
                      14 * s, 20 * s, 2 * s, glass);
            }

            // A projecting pier/cornice rhythm is readable even when the window recesses are in
            // shadow, and the roof lets this layer join the town's cascading red-roof silhouette.
            for (int pier = 0; pier <= 3; pier++)
            {
                int x = x0 + Math.Min(91, pier * 29) * s;
                b.Box(x, foundationH, z0 - 2 * s,
                      5 * s, wallH, 6 * s, dark);
            }

            b.Box(x0, wallTop - 4 * s, z0 - 2 * s,
                  width, 4 * s, depth + 4 * s, dark);
            b.Prism(0, wallTop, 0,
                    104 * s, 18 * s, 32 * s,
                    PrismProfile.Gable, roof);
            return b.Finish();
        }

        private static void AddWindowZ(ProgramBuilder b, int x, int y, int z,
                                       int width, int height, int depth, byte material)
        {
            b.Carve(x, y, z, width, height, depth);
            b.Box(x, y, z, width, height, depth, material);
        }

        private static void AddWindowX(ProgramBuilder b, int x, int y, int z,
                                       int depth, int height, int width, byte material)
        {
            b.Carve(x, y, z, depth, height, width);
            b.Box(x, y, z, depth, height, width, material);
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);
            }

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Prism(int x, int y, int z, int sx, int sy, int sz,
                              PrismProfile profile, byte material) =>
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                   (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0);
                _code.AddRange(operands);
            }
        }
    }
}
