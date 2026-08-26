using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Paints continuous dark-stone pedestrian margins along Kentridge's authored streets. This is a
    /// public-realm layer, not another terrain grade: it follows whatever road/terrace surface is
    /// already present and gives the brown carriageway a legible urban edge. Named frontage paths
    /// remain one precedence higher so entrances visibly cross the sidewalk into each building.
    /// </summary>
    public static class KentridgeUrbanSidewalkCatalogue
    {
        public const int SidewalkWidthDm = 10;
        public const int RoadOverlapDm = 2;
        public const byte SidewalkPrecedence = 59;
        /// <summary>
        /// Vertical extent of a sidewalk's placement footprint, in voxels.
        ///
        /// Deliberately a fixed window rather than <c>TerrainSampler.MaxHeight + 32</c>, which is
        /// what it used to be. That coupling read as "search the whole world height", and it was
        /// harmless only while the world ceiling was 488 voxels. Raising the ceiling to 60000 for
        /// kilometre-scale mountains took this to 60032 and every sidewalk definition failed
        /// <c>FeatureDefinition.FootprintWithinBudget</c> (cap 1280), so the catalogue threw
        /// FootprintExceedsBudget and the showcase could not construct a world at all.
        ///
        /// A sidewalk is town furniture on the valley floor; it has no business declaring a
        /// footprint that spans six kilometres of altitude. 520 preserves the search window this
        /// catalogue actually had when it was written and authored against.
        /// </summary>
        private const int VerticalSearchVoxels = 520;
        private static int ProgramLengthPerStrip =>
            ShapeOps.InstructionLength(ShapeOp.EmitBox)
            + ShapeOps.InstructionLength(ShapeOp.End);

        private readonly struct Strip
        {
            public readonly string Id;
            public readonly int XDm;
            public readonly int ZDm;
            public readonly int WidthDm;
            public readonly int DepthDm;

            public Strip(string id, int xDm, int zDm, int widthDm, int depthDm)
            {
                Id = id;
                XDm = xDm;
                ZDm = zDm;
                WidthDm = widthDm;
                DepthDm = depthDm;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(seed);
            var strips = new List<Strip>(8);

            AddVerticalPair(
                strips,
                "main",
                KentridgeTownPlanner.MainSpineXDm,
                KentridgeTownPlanner.MainRoadWidthDm,
                skeleton.Get(KentridgeUrbanNodeId.CivicCrown).CentreDm.Y,
                skeleton.Get(KentridgeUrbanNodeId.SouthApproach).CentreDm.Y);

            AddHorizontalPair(
                strips,
                "market",
                KentridgeTownPlanner.MarketStreetZDm,
                KentridgeTownPlanner.SecondaryRoadWidthDm,
                FindStreet(plan, "market-street").Points[0].X,
                skeleton.Get(KentridgeUrbanNodeId.EastMarketJunction).CentreDm.X);

            AddHorizontalPair(
                strips,
                "residential",
                KentridgeTownPlanner.ResidentialStreetZDm,
                KentridgeTownPlanner.ResidentialRoadWidthDm,
                FindStreet(plan, "residential-street").Points[0].X,
                KentridgeTownPlanner.EastLaneXDm);

            AddVerticalPair(
                strips,
                "east-service",
                KentridgeTownPlanner.EastLaneXDm,
                KentridgeTownPlanner.ServiceRoadWidthDm,
                skeleton.Get(KentridgeUrbanNodeId.EastRidgeLanding).CentreDm.Y,
                980);

            int scale = settings.VoxelsPerDecimetre;
            // FoundationStone and Masonry both map to material 1 in the current host. Material 1 is
            // intentionally rendered as a terrain field in the smooth pipeline, so surface-paint
            // sidewalks must use the distinct dark-masonry channel to retain an architectural read.
            byte stone = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: strips.Count,
                rules: strips.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: strips.Count * ProgramLengthPerStrip,
                materials: 0,
                explicitPlacements: strips.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < strips.Count; i++)
            {
                Strip strip = strips[i];
                int width = strip.WidthDm * scale;
                int depth = strip.DepthDm * scale;
                int[] program = PaintProgram(width, depth, stone);
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-sidewalk-" + strip.Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(width, VerticalSearchVoxels, depth),
                    MaxSlope = 32,
                    Precedence = SidewalkPrecedence,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaxPrimitives = 1,
                };
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(strip.XDm * scale, 0, strip.ZDm * scale),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
                catalogue.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 32,
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };
                programOffset += program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge sidewalk catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static void AddVerticalPair(List<Strip> strips, string id, int centreXDm,
                                            int roadWidthDm, int z0, int z1)
        {
            int minZ = Math.Min(z0, z1);
            int depth = Math.Abs(z1 - z0);
            int halfRoad = roadWidthDm / 2;
            int stripWidth = SidewalkWidthDm + RoadOverlapDm;
            strips.Add(new Strip(
                id + "-west",
                centreXDm - halfRoad - SidewalkWidthDm,
                minZ,
                stripWidth,
                depth));
            strips.Add(new Strip(
                id + "-east",
                centreXDm + halfRoad - RoadOverlapDm,
                minZ,
                stripWidth,
                depth));
        }

        private static void AddHorizontalPair(List<Strip> strips, string id, int centreZDm,
                                              int roadWidthDm, int x0, int x1)
        {
            int minX = Math.Min(x0, x1);
            int width = Math.Abs(x1 - x0);
            int halfRoad = roadWidthDm / 2;
            int stripDepth = SidewalkWidthDm + RoadOverlapDm;
            strips.Add(new Strip(
                id + "-south",
                minX,
                centreZDm - halfRoad - SidewalkWidthDm,
                width,
                stripDepth));
            strips.Add(new Strip(
                id + "-north",
                minX,
                centreZDm + halfRoad - RoadOverlapDm,
                width,
                stripDepth));
        }

        private static PlannedStreet FindStreet(SettlementPlan plan, string id)
        {
            for (int i = 0; i < plan.Streets.Count; i++)
                if (plan.Streets[i].Id == id) return plan.Streets[i];
            throw new InvalidOperationException("Kentridge street missing for sidewalk: " + id);
        }

        private static int[] PaintProgram(int width, int depth, byte material)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox,
                0,
                0, 0, 0,
                width, VerticalSearchVoxels, depth,
                material,
                0, 0,
                (int)PrimitiveMode.PaintSurface,
                (int)ShapeOp.End,
                0,
            };
        }
    }
}
