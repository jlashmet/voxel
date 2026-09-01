using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Streaming-compatible voxel realization of a planned macro world. Large-scale geography is
    /// still expressed as bounded feature definitions and explicit placements, so ordinary region
    /// generation/LOD clips it instead of eagerly constructing remote scene objects.
    /// </summary>
    public static class TopDownWorldPhysicalVoxelCatalogue
    {
        private const int VerticalBandDm = 80;
        private const int PassCarveHeightDm = 180;
        private const int MaxAltitudeVoxels = 4096;
        private const int BuildingFoundationDm = 8;
        private const int BuildingRoofDm = 24;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingWallThicknessDm = 4;
        private const int BuildingTerrainSamplesPerAxis = 5;
        private const int StreetSpanDm = 500;

        private sealed class DefinitionBuild
        {
            public string Name;
            public FeatureKind Kind;
            public int Width;
            public int Height;
            public int Depth;
            public int Precedence;
            public int[] Program;
            public readonly List<int3> Placements = new List<int3>();
        }

        public static TopDownWorldPhysicalPlan Plan(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalIntentSpec intent,
            Int2 rootCentreDm,
            int cellSizeDm,
            VoxelWorldGenSettings settings) =>
            TopDownWorldPhysicalPlanner.Plan(
                layout,
                intent,
                rootCentreDm,
                cellSizeDm,
                settings.VoxelsPerDecimetre);

        public static FeatureCatalogue Build(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalIntentSpec intent,
            Int2 rootCentreDm,
            int cellSizeDm,
            VoxelWorldGenSettings settings,
            Allocator allocator,
            bool includeWaterBodies = true)
        {
            TopDownWorldPhysicalPlan plan = Plan(layout, intent, rootCentreDm, cellSizeDm, settings);
            int scale = settings.VoxelsPerDecimetre;
            int verticalBand = VerticalBandDm * scale;
            byte road = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte foundation = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte roof = settings.Materials.Resolve(MaterialRole.RoofTile);
            byte water = settings.Materials.Resolve(MaterialRole.Water);
            byte moss = settings.Materials.Resolve(MaterialRole.Moss);
            byte masonry = settings.Materials.Resolve(MaterialRole.Masonry);

            var definitions = new List<DefinitionBuild>();

            for (var i = 0; i < plan.Regions.Count; i++)
            {
                TopDownWorldRegionPlan region = plan.Regions[i];
                if (!includeWaterBodies && region.Spec.Kind == TopDownWorldRegionKind.WaterBody)
                    continue;

                int width = region.HalfExtentXDm * 2 * scale;
                int depth = region.HalfExtentZDm * 2 * scale;
                int ground = TerrainSampler.HeightAt(
                    region.CentreDm.X * scale,
                    region.CentreDm.Y * scale,
                    layout.Seed);
                DefinitionBuild build = RegionDefinition(region, width, depth, verticalBand, scale, water, moss, timber, masonry);
                ValidateFootprint(build);
                int placementY = region.Spec.Kind == TopDownWorldRegionKind.MountainRidge
                    || region.Spec.Kind == TopDownWorldRegionKind.ValleyPass
                    ? ground
                    : ground - verticalBand / 2;
                build.Placements.Add(new int3(
                    (region.CentreDm.X - region.HalfExtentXDm) * scale,
                    placementY,
                    (region.CentreDm.Y - region.HalfExtentZDm) * scale));
                definitions.Add(build);
            }

            for (var i = 0; i < plan.Routes.Count; i++)
            {
                TopDownWorldPhysicalRoutePlan routePlan = plan.Routes[i];
                int width = routePlan.Route.CorridorWidthDm * scale;
                var build = new DefinitionBuild
                {
                    Name = "macro-road-" + i,
                    Kind = FeatureKind.Landform,
                    Width = width,
                    Height = verticalBand,
                    Depth = width,
                    Precedence = 9,
                    Program = PaintProgram(width, verticalBand, width, road)
                };
                for (var p = 0; p < routePlan.Tiles.Count; p++)
                {
                    Int2 centre = routePlan.Tiles[p];
                    int ground = TerrainSampler.HeightAt(centre.X * scale, centre.Y * scale, layout.Seed);
                    build.Placements.Add(new int3(
                        (centre.X - routePlan.Route.CorridorWidthDm / 2) * scale,
                        ground - verticalBand / 2,
                        (centre.Y - routePlan.Route.CorridorWidthDm / 2) * scale));
                }
                definitions.Add(build);
            }

            for (var i = 0; i < plan.Settlements.Count; i++)
            {
                TopDownWorldSettlementPlan settlement = plan.Settlements[i];
                if (settlement.RealizationKind == TopDownWorldSettlementRealizationKind.ExistingRichGeneration)
                    continue;

                int streetWidth = TopDownWorldPhysicalPlanner.GenericSettlementStreetHalfWidthDm * 2 * scale;
                int streetSpan = StreetSpanDm * scale;
                var street = new DefinitionBuild
                {
                    Name = "macro-town-streets-" + settlement.Node.Id,
                    Kind = FeatureKind.Landform,
                    Width = streetSpan,
                    Height = verticalBand,
                    Depth = streetSpan,
                    Precedence = 8,
                    Program = CrossPaintProgram(streetSpan, streetWidth, verticalBand, road)
                };
                int townGround = TerrainSampler.HeightAt(
                    settlement.CentreDm.X * scale,
                    settlement.CentreDm.Y * scale,
                    layout.Seed);
                street.Placements.Add(new int3(
                    (settlement.CentreDm.X - StreetSpanDm / 2) * scale,
                    townGround - verticalBand / 2,
                    (settlement.CentreDm.Y - StreetSpanDm / 2) * scale));
                ValidateFootprint(street);
                definitions.Add(street);

                for (var b = 0; b < settlement.Buildings.Count; b++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[b];
                    int width = building.HalfExtentXDm * 2 * scale;
                    int depth = building.HalfExtentZDm * 2 * scale;
                    int height = building.HeightDm * scale;
                    SampleBuildingTerrainRelief(
                        building,
                        scale,
                        layout.Seed,
                        out int minimumGround,
                        out int maximumGround);
                    int terrainRelief = maximumGround - minimumGround;
                    var structure = new DefinitionBuild
                    {
                        Name = "macro-town-building-" + settlement.Node.Id + "-" + b,
                        Kind = FeatureKind.Structure,
                        Width = width + BuildingFoundationInsetDm * 2 * scale,
                        Height = terrainRelief + (building.HeightDm + BuildingRoofDm) * scale,
                        Depth = depth + BuildingFoundationInsetDm * 2 * scale,
                        Precedence = 10,
                        Program = BuildingProgram(
                            width,
                            depth,
                            height,
                            terrainRelief,
                            scale,
                            foundation,
                            timber,
                            roof)
                    };
                    structure.Placements.Add(new int3(
                        (building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm) * scale,
                        minimumGround,
                        (building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm) * scale));
                    ValidateFootprint(structure);
                    definitions.Add(structure);
                }
            }

            int programLength = 0;
            int placementCount = 0;
            for (var i = 0; i < definitions.Count; i++)
            {
                programLength += definitions[i].Program.Length;
                placementCount += definitions[i].Placements.Count;
            }
            if (definitions.Count > FeatureBudget.MaxDefinitions)
                throw new InvalidOperationException("Macro physical catalogue exceeds feature definition budget.");

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitions.Count,
                rules: definitions.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placementCount,
                overrides: 0,
                allocator);

            try
            {
                int programOffset = 0;
                int placementOffset = 0;
                for (var i = 0; i < definitions.Count; i++)
                {
                    DefinitionBuild source = definitions[i];
                    CopyProgram(ref catalogue, programOffset, source.Program);
                    catalogue.Definitions[i] = Definition(
                        source.Name,
                        source.Kind,
                        source.Width,
                        source.Height,
                        source.Depth,
                        programOffset,
                        source.Program.Length,
                        source.Precedence,
                        CountEmitInstructions(source.Program));
                    int first = placementOffset;
                    for (var p = 0; p < source.Placements.Count; p++)
                    {
                        catalogue.ExplicitPlacements[placementOffset++] = new ExplicitPlacement
                        {
                            Position = source.Placements[p],
                            Orientation = 0,
                            OverrideOffset = 0,
                            OverrideCount = 0
                        };
                    }
                    catalogue.Rules[i] = ExplicitRule(i, first, source.Placements.Count);
                    programOffset += source.Program.Length;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException("Macro physical voxel catalogue failed validation: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static DefinitionBuild RegionDefinition(
            TopDownWorldRegionPlan region,
            int width,
            int depth,
            int verticalBand,
            int scale,
            byte water,
            byte moss,
            byte timber,
            byte masonry)
        {
            switch (region.Spec.Kind)
            {
                case TopDownWorldRegionKind.WaterBody:
                    return new DefinitionBuild
                    {
                        Name = "macro-region-water-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = verticalBand,
                        Depth = depth,
                        Precedence = 5,
                        Program = PaintProgram(width, verticalBand, depth, water)
                    };
                case TopDownWorldRegionKind.MountainRidge:
                    int height = Math.Max(60, region.ElevationDeltaDm) * scale;
                    int radius = Math.Min(width, depth) / 2;
                    return new DefinitionBuild
                    {
                        Name = "macro-region-ridge-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = height,
                        Depth = depth,
                        Precedence = 5,
                        Program = RidgeProgram(width, depth, height, radius, masonry)
                    };
                case TopDownWorldRegionKind.ValleyPass:
                    int carveHeight = PassCarveHeightDm * scale;
                    return new DefinitionBuild
                    {
                        Name = "macro-region-pass-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = carveHeight,
                        Depth = depth,
                        Precedence = 7,
                        Program = CarveProgram(width, carveHeight, depth)
                    };
                case TopDownWorldRegionKind.ForestWoodland:
                    return new DefinitionBuild
                    {
                        Name = "macro-region-woodland-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = verticalBand,
                        Depth = depth,
                        Precedence = 3,
                        Program = PaintProgram(width, verticalBand, depth, timber)
                    };
                case TopDownWorldRegionKind.PlainsMeadow:
                    return new DefinitionBuild
                    {
                        Name = "macro-region-meadow-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = verticalBand,
                        Depth = depth,
                        Precedence = 3,
                        Program = PaintProgram(width, verticalBand, depth, moss)
                    };
                default:
                    return new DefinitionBuild
                    {
                        Name = "macro-region-country-" + region.Spec.Id,
                        Kind = FeatureKind.Landform,
                        Width = width,
                        Height = verticalBand,
                        Depth = depth,
                        Precedence = 2,
                        Program = PaintProgram(width, verticalBand, depth, masonry)
                    };
            }
        }

        private static int[] RidgeProgram(int width, int depth, int height, int radius, byte material)
        {
            // The frustum produces a readable ridge mass while staying one bounded streaming
            // feature. A designated ValleyPass feature at higher precedence carves the crossing.
            int centreX = width / 2;
            int centreZ = depth / 2;
            return new[]
            {
                (int)ShapeOp.EmitFrustum, 0,
                centreX, 0, centreZ,
                height, radius, Math.Max(24, radius / 3), 1,
                material, 0, 0, (int)PrimitiveMode.FillIfEmpty,
                (int)ShapeOp.End, 0
            };
        }

        private static int[] BuildingProgram(
            int width,
            int depth,
            int height,
            int terrainRelief,
            int scale,
            byte foundation,
            byte timber,
            byte roof)
        {
            int inset = BuildingFoundationInsetDm * scale;
            int normalFoundationHeight = BuildingFoundationDm * scale;
            int foundationTop = terrainRelief + normalFoundationHeight;
            int wallHeight = Math.Max(scale, height - normalFoundationHeight);
            int wallThickness = Math.Max(scale, BuildingWallThicknessDm * scale);
            int foundationWidth = width + inset * 2;
            int foundationDepth = depth + inset * 2;
            int foundationThickness = Math.Max(wallThickness, inset + wallThickness);
            int foundationInnerDepth = Math.Max(scale, foundationDepth - foundationThickness * 2);
            int innerDepth = Math.Max(scale, depth - wallThickness * 2);
            int roofBase = terrainRelief + height;
            int roofHeight = BuildingRoofDm * scale;
            return new[]
            {
                // A generic blockout only needs a grounded perimeter plinth. The outer foundation
                // still spans the sampled terrain relief and supports every exterior timber wall,
                // while the intentionally unused interior no longer pays solid-slab raster cost.
                (int)ShapeOp.EmitBox, 0,
                0, 0, 0,
                foundationWidth, foundationTop, foundationThickness,
                foundation, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                0, 0, foundationDepth - foundationThickness,
                foundationWidth, foundationTop, foundationThickness,
                foundation, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                0, 0, foundationThickness,
                foundationThickness, foundationTop, foundationInnerDepth,
                foundation, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                foundationWidth - foundationThickness, 0, foundationThickness,
                foundationThickness, foundationTop, foundationInnerDepth,
                foundation, 0, 0, (int)PrimitiveMode.Fill,

                // Readable generic blockouts only need an exterior shell. Four bounded wall boxes
                // preserve the authored footprint, height, collision silhouette, and roof support
                // without publishing the former solid interior volume into every covered region.
                (int)ShapeOp.EmitBox, 0,
                inset, foundationTop, inset,
                width, wallHeight, wallThickness,
                timber, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                inset, foundationTop, inset + depth - wallThickness,
                width, wallHeight, wallThickness,
                timber, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                inset, foundationTop, inset + wallThickness,
                wallThickness, wallHeight, innerDepth,
                timber, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.EmitBox, 0,
                inset + width - wallThickness, foundationTop, inset + wallThickness,
                wallThickness, wallHeight, innerDepth,
                timber, 0, 0, (int)PrimitiveMode.Fill,

                (int)ShapeOp.EmitPrism, 0,
                0, roofBase, 0,
                foundationWidth, roofHeight, foundationDepth,
                (int)PrismProfile.Gable,
                roof, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.End, 0
            };
        }

        private static int[] CrossPaintProgram(int span, int width, int height, byte material)
        {
            int offset = (span - width) / 2;
            return new[]
            {
                (int)ShapeOp.EmitBox, 0,
                0, 0, offset,
                span, height, width,
                material, 0, 0, (int)PrimitiveMode.PaintSurface,
                (int)ShapeOp.EmitBox, 0,
                offset, 0, 0,
                width, height, span,
                material, 0, 0, (int)PrimitiveMode.PaintSurface,
                (int)ShapeOp.End, 0
            };
        }

        private static int[] PaintProgram(int width, int height, int depth, byte material)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox, 0,
                0, 0, 0,
                width, height, depth,
                material, 0, 0, (int)PrimitiveMode.PaintSurface,
                (int)ShapeOp.End, 0
            };
        }

        private static int[] CarveProgram(int width, int height, int depth)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox, 0,
                0, 0, 0,
                width, height, depth,
                0, 0, 0, (int)PrimitiveMode.Carve,
                (int)ShapeOp.End, 0
            };
        }

        private static void SampleBuildingTerrainRelief(
            TopDownWorldBuildingBlockoutPlan building,
            int scale,
            uint seed,
            out int minimumGround,
            out int maximumGround)
        {
            int leftDm = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
            int rightDm = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
            int backDm = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
            int frontDm = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
            minimumGround = int.MaxValue;
            maximumGround = int.MinValue;

            for (var x = 0; x < BuildingTerrainSamplesPerAxis; x++)
            {
                int xDm = leftDm + (rightDm - leftDm) * x / (BuildingTerrainSamplesPerAxis - 1);
                for (var z = 0; z < BuildingTerrainSamplesPerAxis; z++)
                {
                    int zDm = backDm + (frontDm - backDm) * z / (BuildingTerrainSamplesPerAxis - 1);
                    int ground = TerrainSampler.HeightAt(xDm * scale, zDm * scale, seed);
                    minimumGround = Math.Min(minimumGround, ground);
                    maximumGround = Math.Max(maximumGround, ground);
                }
            }
        }

        private static void ValidateFootprint(DefinitionBuild build)
        {
            if (build.Width > FeatureBudget.MaxFootprintVoxels
                || build.Height > FeatureBudget.MaxFootprintVoxels
                || build.Depth > FeatureBudget.MaxFootprintVoxels)
                throw new InvalidOperationException(
                    "Macro physical feature '" + build.Name + "' exceeds the 128 m footprint budget.");
        }

        private static FeatureDefinition Definition(
            string name,
            FeatureKind kind,
            int width,
            int height,
            int depth,
            int programOffset,
            int programLength,
            int precedence,
            int maxPrimitives)
        {
            return new FeatureDefinition
            {
                Name = new FixedString64Bytes(name),
                Kind = kind,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(width, height, depth),
                MaxSlope = 32,
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
                MaxPrimitives = maxPrimitives
            };
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
                MaxAltitude = MaxAltitudeVoxels,
                MaxSlope = 32,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count
            };
        }

        private static int CountEmitInstructions(int[] program)
        {
            var count = 0;
            for (var i = 0; i < program.Length;)
            {
                ShapeOp op = (ShapeOp)program[i];
                if (ShapeOps.IsEmit(op)) count++;
                int length = ShapeOps.InstructionLength(op);
                if (length < 0) throw new InvalidOperationException("Macro physical program contains invalid opcode.");
                i += length;
                if (op == ShapeOp.End) break;
            }
            return count;
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (var i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }
    }
}
