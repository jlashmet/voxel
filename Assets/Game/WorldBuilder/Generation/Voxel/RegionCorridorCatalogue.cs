using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// The country between two settlements: the road that joins them, the river that crosses it,
    /// and the bridge that carries the road over the river.
    ///
    /// These three are one catalogue because they are one decision. A road placed without knowing
    /// where the river is has to be patched afterwards; a bridge authored separately from both ends
    /// up spanning the wrong water at the wrong height. Generating them together means the crossing
    /// is chosen once and the road, the gap in it, and the span that fills the gap all agree.
    ///
    /// Every altitude is sampled from the terrain rather than assumed. The river bed is cut below
    /// the lowest ground it passes through, the water sits below the banks, and the deck clears the
    /// highest bank — so the crossing is correct on whatever terrain the seed produces instead of
    /// on the terrain that happened to exist when it was written.
    /// </summary>
    /// <summary>
    /// Where the crossing is and the altitudes it resolved to. Exposed so the arrangement can be
    /// checked without inspecting emitted voxels: whether a bridge clears its river is a property
    /// of these four numbers, and reading them is a far more direct test than looking for water
    /// under a deck in a built world.
    /// </summary>
    public readonly struct RegionCorridorPlan
    {
        public readonly int RoadXDm;
        public readonly int CrossingZDm;
        public readonly int RiverBedDm;
        public readonly int WaterTopDm;
        public readonly int DeckUnderDm;
        public readonly int LowestBankDm;
        public readonly int HighestBankDm;

        public RegionCorridorPlan(
            int roadXDm, int crossingZDm, int riverBedDm, int waterTopDm,
            int deckUnderDm, int lowestBankDm, int highestBankDm)
        {
            RoadXDm = roadXDm;
            CrossingZDm = crossingZDm;
            RiverBedDm = riverBedDm;
            WaterTopDm = waterTopDm;
            DeckUnderDm = deckUnderDm;
            LowestBankDm = lowestBankDm;
            HighestBankDm = highestBankDm;
        }
    }

    public static class RegionCorridorCatalogue
    {
        /// <summary>Half-width of the carriageway, in decimetres.</summary>
        private const int RoadHalfWidthDm = 30;

        /// <summary>Length of one emitted road tile along the route.</summary>
        private const int RoadTileDm = 80;

        /// <summary>
        /// Vertical band each road tile paints through, in decimetres.
        ///
        /// <see cref="PrimitiveMode.PaintSurface"/> repaints the top of whatever solid column it
        /// covers, so this is a mask over the ground rather than a slab laid on it: it only has to
        /// contain the terrain surface across one tile. Four metres tolerates roughly two metres of
        /// fall across an eight-metre tile, which is far more than a route between two towns has.
        /// </summary>
        private const int RoadBandDm = 40;

        /// <summary>Half-width of the river channel. A crossing this wide needs a real bridge.</summary>
        private const int RiverHalfWidthDm = 60;

        /// <summary>How far the river runs either side of the road before leaving the corridor.</summary>
        // Emitted as tiles, because a feature footprint is capped at 1280 voxels and a river
        // long enough to cross the corridor is far wider than that.
        private const int RiverReachDm = 800;
        private const int RiverTileDm = 200;

        /// <summary>Depth of the cut below the lowest bank, and how much of it holds water.</summary>
        private const int ChannelDepthDm = 24;
        private const int WaterDepthDm = 16;

        /// <summary>Clearance from the highest bank to the underside of the deck.</summary>
        private const int DeckClearanceDm = 26;
        private const int DeckThicknessDm = 8;
        private const int ParapetHeightDm = 12;

        /// <summary>How far the deck runs onto each bank beyond the channel edge.</summary>
        private const int BridgeAbutmentDm = 80;

        /// <summary>Along-channel depth of one pier.</summary>
        private const int PierDepthDm = 40;

        /// <summary>
        /// How far above the highest bank the channel is cut.
        ///
        /// The carve has to reach daylight along the whole reach, not just at the lowest point:
        /// cutting to a fixed height above the <em>lowest</em> bank leaves the channel roofed over
        /// wherever the ground rises, and a river under a lid is a cave.
        /// </summary>
        private const int ChannelCrestDm = 20;

        /// <summary>
        /// Chooses the crossing and resolves every altitude from the terrain, without emitting
        /// anything. <see cref="Build"/> is this plus the geometry.
        /// </summary>
        public static RegionCorridorPlan Plan(
            uint seed, VoxelWorldGenSettings settings, Int2 fromDm, Int2 toDm)
        {
            int scale = settings.VoxelsPerDecimetre;

            // The route is the straight line between the two town centres. Both towns are planned
            // around the same north-south axis, so this is a road rather than a diagonal scar.
            if (fromDm.X != toDm.X)
                throw new NotSupportedException(
                    "RegionCorridorCatalogue currently joins settlements that share an axis; " +
                    "'" + fromDm.X + "' and '" + toDm.X + "' differ.");

            int roadXDm = fromDm.X;
            int southZDm = Math.Min(fromDm.Y, toDm.Y);
            int northZDm = Math.Max(fromDm.Y, toDm.Y);

            // Leave each town's own streets alone: the corridor starts outside them.
            int startZDm = southZDm + TownClearanceDm;
            int endZDm = northZDm - TownClearanceDm;
            if (endZDm - startZDm < RiverHalfWidthDm * 4)
                throw new InvalidOperationException(
                    "Settlements are too close together for a river crossing between them.");

            int crossingZDm = (startZDm + endZDm) / 2;

            SampleCrossing(seed, roadXDm, crossingZDm, scale,
                           out int lowestBankDm, out int highestBankDm);

            int bedDm = lowestBankDm - ChannelDepthDm;
            int waterTopDm = bedDm + WaterDepthDm;
            int deckUnderDm = highestBankDm + DeckClearanceDm;

            return new RegionCorridorPlan(
                roadXDm, crossingZDm, bedDm, waterTopDm, deckUnderDm,
                lowestBankDm, highestBankDm);
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Int2 fromDm,
            Int2 toDm,
            Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            RegionCorridorPlan plan = Plan(seed, settings, fromDm, toDm);

            int roadXDm = plan.RoadXDm;
            int crossingZDm = plan.CrossingZDm;
            int bedDm = plan.RiverBedDm;
            int waterTopDm = plan.WaterTopDm;
            int deckUnderDm = plan.DeckUnderDm;

            int southZDm = Math.Min(fromDm.Y, toDm.Y);
            int northZDm = Math.Max(fromDm.Y, toDm.Y);
            int startZDm = southZDm + TownClearanceDm;
            int endZDm = northZDm - TownClearanceDm;

            var road = new List<int>();
            for (int z = startZDm; z < endZDm; z += RoadTileDm)
            {
                // The road stops at the water and resumes on the far bank; the span covers the gap.
                if (z + RoadTileDm > crossingZDm - RiverHalfWidthDm
                    && z < crossingZDm + RiverHalfWidthDm)
                    continue;
                road.Add(z);
            }

            if (road.Count == 0)
                throw new InvalidOperationException("Corridor produced no road tiles.");

            return Emit(seed, settings, scale, roadXDm, crossingZDm, road,
                        bedDm, waterTopDm, deckUnderDm, plan.HighestBankDm, allocator);
        }

        /// <summary>How far outside a town centre the open road begins.</summary>
        private const int TownClearanceDm = 700;

        /// <summary>
        /// Reads the ground either side of the crossing. The bed is cut relative to the lowest
        /// bank so the channel is never left hanging above the ground it passes through, and the
        /// deck is raised relative to the highest so it clears both banks rather than only the one
        /// it was measured from.
        /// </summary>
        private static void SampleCrossing(
            uint seed, int roadXDm, int crossingZDm, int scale,
            out int lowestBankDm, out int highestBankDm)
        {
            int lowest = int.MaxValue;
            int highest = int.MinValue;

            for (int offset = -RiverReachDm; offset <= RiverReachDm; offset += 100)
            {
                int xDm = roadXDm + offset;
                for (int zOffset = -RiverHalfWidthDm; zOffset <= RiverHalfWidthDm; zOffset += 50)
                {
                    int height = TerrainQuery.HeightAt(
                        xDm * scale, (crossingZDm + zOffset) * scale, seed) / scale;
                    if (height < lowest) lowest = height;
                    if (height > highest) highest = height;
                }
            }

            lowestBankDm = lowest;
            highestBankDm = highest;
        }

        private static FeatureCatalogue Emit(
            uint seed,
            VoxelWorldGenSettings settings, int scale,
            int roadXDm, int crossingZDm, List<int> roadTiles,
            int bedDm, int waterTopDm, int deckUnderDm, int highestBankDm,
            Allocator allocator)
        {
            byte roadMaterial = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte water = settings.Materials.Resolve(MaterialRole.Water);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte masonry = settings.Materials.Resolve(MaterialRole.Masonry);

            // Every program below is authored from zero, and the altitude it belongs at travels in
            // its placement instead. That split is not a style choice: rasterisation validates each
            // emitted primitive against [placement, placement + footprint), so a program that names
            // an absolute altitude describes geometry hundreds of decimetres outside a footprint
            // anchored at y=0 and is discarded whole. This catalogue used to do exactly that, and
            // the river and the bridge silently never appeared while their plan still passed every
            // arithmetic check made of it.
            int channelTopDm = highestBankDm + ChannelCrestDm;
            int[] roadProgram = RoadProgram(scale, roadMaterial);
            int[] riverProgram = RiverProgram(scale, water, bedDm, waterTopDm, channelTopDm);
            int[] bridgeProgram = BridgeProgram(scale, stone, masonry, deckUnderDm, bedDm);

            const int RoadId = 0, RiverId = 1, BridgeId = 2;
            int programLength = roadProgram.Length + riverProgram.Length + bridgeProgram.Length;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 3,
                rules: 3,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: roadTiles.Count + (RiverReachDm * 2 / RiverTileDm) + 1,
                overrides: 0,
                allocator);

            try
            {
                int programOffset = 0;
                int placementOffset = 0;

                // -- road ---------------------------------------------------------
                CopyProgram(ref catalogue, programOffset, roadProgram);
                catalogue.Definitions[RoadId] = Definition(
                    "corridor-road", BasePlaneRule.FixedAltitude, 0,
                    new int3(RoadHalfWidthDm * 2 * scale, RoadBandDm * scale, RoadTileDm * scale),
                    programOffset, roadProgram.Length, precedence: 8);

                for (int i = 0; i < roadTiles.Count; i++)
                {
                    // Each tile is placed at the ground it actually covers. A single altitude for
                    // the whole route would bury the road in every rise and leave it hanging over
                    // every dip, and the band is a surface mask that has to contain the terrain to
                    // paint it at all.
                    int tileGroundDm = MidGroundDm(
                        seed, scale,
                        roadXDm - RoadHalfWidthDm, roadTiles[i],
                        RoadHalfWidthDm * 2, RoadTileDm);

                    catalogue.ExplicitPlacements[placementOffset + i] = new ExplicitPlacement
                    {
                        Position = new int3(
                            (roadXDm - RoadHalfWidthDm) * scale,
                            (tileGroundDm - RoadBandDm / 2) * scale,
                            roadTiles[i] * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                }

                catalogue.Rules[RoadId] = ExplicitRule(RoadId, placementOffset, roadTiles.Count);
                placementOffset += roadTiles.Count;
                programOffset += roadProgram.Length;

                // -- river --------------------------------------------------------
                CopyProgram(ref catalogue, programOffset, riverProgram);
                catalogue.Definitions[RiverId] = Definition(
                    "corridor-river", BasePlaneRule.FixedAltitude, 0,
                    new int3(RiverTileDm * scale,
                             (channelTopDm - bedDm) * scale,
                             RiverHalfWidthDm * 2 * scale),
                    programOffset, riverProgram.Length, precedence: 9);

                int riverTiles = 0;
                for (int x = roadXDm - RiverReachDm; x < roadXDm + RiverReachDm; x += RiverTileDm)
                {
                    // One channel altitude for the whole reach, because water finds a level. The
                    // carve reaching above the highest bank is what keeps that single altitude from
                    // burying the channel where the ground rises.
                    catalogue.ExplicitPlacements[placementOffset + riverTiles] = new ExplicitPlacement
                    {
                        Position = new int3(
                            x * scale,
                            bedDm * scale,
                            (crossingZDm - RiverHalfWidthDm) * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                    riverTiles++;
                }

                catalogue.Rules[RiverId] = ExplicitRule(RiverId, placementOffset, riverTiles);
                placementOffset += riverTiles;
                programOffset += riverProgram.Length;

                // -- bridge -------------------------------------------------------
                CopyProgram(ref catalogue, programOffset, bridgeProgram);
                catalogue.Definitions[BridgeId] = Definition(
                    "corridor-bridge", BasePlaneRule.FixedAltitude, 0,
                    new int3((RoadHalfWidthDm * 2 + 20) * scale,
                             (deckUnderDm - bedDm + DeckThicknessDm + ParapetHeightDm) * scale,
                             (RiverHalfWidthDm * 2 + BridgeAbutmentDm * 2) * scale),
                    programOffset, bridgeProgram.Length, precedence: 10);

                // Anchored on the river bed, because that is where its piers start. Everything the
                // bridge program measures — pier height, deck, parapets — is a height above the
                // bed, so the bed is the one altitude the whole structure is authored from.
                catalogue.ExplicitPlacements[placementOffset] = new ExplicitPlacement
                {
                    Position = new int3(
                        (roadXDm - RoadHalfWidthDm - 10) * scale,
                        bedDm * scale,
                        (crossingZDm - RiverHalfWidthDm - BridgeAbutmentDm) * scale),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
                catalogue.Rules[BridgeId] = ExplicitRule(BridgeId, placementOffset, 1);

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException(
                        "Region corridor catalogue failed validation: " + result);

                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static int[] RoadProgram(int scale, byte roadMaterial)
        {
            var b = new CorridorProgram();
            b.PaintSurface(0, 0, 0,
                           RoadHalfWidthDm * 2 * scale, RoadBandDm * scale, RoadTileDm * scale,
                           roadMaterial);
            return b.Finish();
        }

        /// <summary>
        /// The channel, authored upward from the bed at local zero. The placement puts the bed at
        /// its resolved altitude.
        /// </summary>
        private static int[] RiverProgram(
            int scale, byte water, int bedDm, int waterTopDm, int channelTopDm)
        {
            var b = new CorridorProgram();

            // Cut the channel clear of the highest bank so the carve removes the hillside the
            // river runs through, not merely the topsoil.
            b.Carve(0, 0, 0,
                    RiverTileDm * scale, (channelTopDm - bedDm) * scale,
                    RiverHalfWidthDm * 2 * scale);

            // Then fill the bottom of the cut with water, leaving banks above the surface.
            b.Fill(0, 0, 0,
                   RiverTileDm * scale, (waterTopDm - bedDm) * scale, RiverHalfWidthDm * 2 * scale,
                   water);
            return b.Finish();
        }

        /// <summary>
        /// The span, authored upward from the river bed at local zero. Local z runs from the near
        /// abutment, so the channel occupies
        /// [<see cref="BridgeAbutmentDm"/>, BridgeAbutmentDm + 2 × <see cref="RiverHalfWidthDm"/>].
        /// </summary>
        private static int[] BridgeProgram(
            int scale, byte stone, byte masonry, int deckUnderDm, int bedDm)
        {
            var b = new CorridorProgram();
            int spanZ = (RiverHalfWidthDm * 2 + BridgeAbutmentDm * 2) * scale;
            int deckW = (RoadHalfWidthDm * 2 + 20) * scale;

            // Two piers rising from the river bed carry the span. Without them a deck this long
            // reads as a plank floating over the water. They stand just inside each channel edge,
            // which is both where the load is and the only place a pier is visible from the bank.
            int pierW = 24 * scale;
            int pierH = (deckUnderDm - bedDm) * scale;
            int nearPierZ = BridgeAbutmentDm * scale;
            int farPierZ = (BridgeAbutmentDm + RiverHalfWidthDm * 2 - PierDepthDm) * scale;
            b.Fill((deckW - pierW) / 2, 0, nearPierZ,
                   pierW, pierH, PierDepthDm * scale, stone);
            b.Fill((deckW - pierW) / 2, 0, farPierZ,
                   pierW, pierH, PierDepthDm * scale, stone);

            // The deck itself, carried across the whole gap and onto both banks.
            int deckY = (deckUnderDm - bedDm) * scale;
            b.Fill(0, deckY, 0, deckW, DeckThicknessDm * scale, spanZ, stone);

            // Parapets, so it reads as a bridge rather than a causeway.
            int parapet = 6 * scale;
            int parapetY = deckY + DeckThicknessDm * scale;
            b.Fill(0, parapetY, 0, parapet, ParapetHeightDm * scale, spanZ, masonry);
            b.Fill(deckW - parapet, parapetY, 0, parapet, ParapetHeightDm * scale, spanZ, masonry);
            return b.Finish();
        }

        /// <summary>
        /// Midpoint between the lowest and highest ground under a footprint, in decimetres. The
        /// midpoint rather than either extreme, because the road band is centred on the value and
        /// has to reach both ends of the fall across a tile.
        /// </summary>
        private static int MidGroundDm(
            uint seed, int scale, int fromXDm, int fromZDm, int widthDm, int depthDm)
        {
            int lowest = int.MaxValue;
            int highest = int.MinValue;

            for (int dz = 0; dz <= depthDm; dz += depthDm / 2)
            for (int dx = 0; dx <= widthDm; dx += widthDm / 2)
            {
                int height = TerrainQuery.HeightAt(
                    (fromXDm + dx) * scale, (fromZDm + dz) * scale, seed) / scale;
                if (height < lowest) lowest = height;
                if (height > highest) highest = height;
            }

            return (lowest + highest) / 2;
        }

        private static FeatureDefinition Definition(
            string name, BasePlaneRule basePlane, int fixedAltitude,
            int3 footprint, int programOffset, int programLength, byte precedence)
        {
            return new FeatureDefinition
            {
                Name = new FixedString64Bytes(name),
                Kind = FeatureKind.Landform,
                BasePlane = basePlane,
                FixedAltitude = fixedAltitude,
                Footprint = footprint,
                MaxSlope = 64,
                Precedence = precedence,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = programOffset,
                ProgramLength = programLength,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 8,
            };
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset, int count) =>
            new PlacementRule
            {
                DefinitionId = definitionId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 64,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }

        private sealed class CorridorProgram
        {
            private readonly List<int> _code = new List<int>();

            public void PaintSurface(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.PaintSurface);

            public void Fill(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.Fill);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   0, 0, 0, (int)PrimitiveMode.Carve);

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
