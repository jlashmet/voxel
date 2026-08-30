using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Reusable streamed physical realization for WaterBody regions. The macro planner owns where
    /// water is allowed and how routes respond to it; this backend turns that intent into a carved
    /// basin plus the engine's existing non-solid water presentation material. Definitions remain
    /// bounded and explicit so ordinary WorldBuilder region clipping/LOD owns residency.
    /// </summary>
    public static class TopDownWorldWaterBodyVoxelCatalogue
    {
        public const string DefinitionPrefix = "macro-water-basin-";
        public const int MinimumDepthDm = 24;
        public const int MaximumDepthDm = 60;
        public const int WaterSurfaceThicknessDm = 2;
        private const int BankInsetDm = 12;
        private const int ShoreInsetDm = 6;
        private const int HeadroomDm = 2;
        private const int MaxAltitudeVoxels = 4096;

        public static FeatureCatalogue Build(
            TopDownWorldPhysicalPlan plan,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            int scale = settings.VoxelsPerDecimetre;
            int waterCount = 0;
            for (var i = 0; i < plan.Regions.Count; i++)
                if (plan.Regions[i].Spec.Kind == TopDownWorldRegionKind.WaterBody) waterCount++;
            if (waterCount == 0) return default;

            const int ProgramIntsPerDefinition = 28;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: waterCount,
                rules: waterCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: waterCount * ProgramIntsPerDefinition,
                materials: 0,
                explicitPlacements: waterCount,
                overrides: 0,
                allocator);

            try
            {
                byte water = settings.Materials.Resolve(MaterialRole.Water);
                int definitionId = 0;
                int programOffset = 0;
                for (var i = 0; i < plan.Regions.Count; i++)
                {
                    TopDownWorldRegionPlan region = plan.Regions[i];
                    if (region.Spec.Kind != TopDownWorldRegionKind.WaterBody) continue;

                    int width = region.HalfExtentXDm * 2 * scale;
                    int depth = region.HalfExtentZDm * 2 * scale;
                    int basinDepth = DepthVoxels(region, scale);
                    int headroom = HeadroomDm * scale;
                    int waterSurfaceThickness = WaterSurfaceThicknessDm * scale;
                    int height = basinDepth + headroom;
                    if (width > FeatureBudget.MaxFootprintVoxels
                        || depth > FeatureBudget.MaxFootprintVoxels
                        || height > FeatureBudget.MaxFootprintVoxels)
                        throw new InvalidOperationException(
                            "Macro water body '" + region.Spec.Id + "' exceeds the feature footprint budget.");
                    if (waterSurfaceThickness > headroom)
                        throw new InvalidOperationException(
                            "Macro water surface thickness must fit inside the carved basin headroom.");

                    int bankInset = Math.Max(BankInsetDm * scale, Math.Min(width, depth) / 16);
                    int shoreInset = ShoreInsetDm * scale;
                    int carveWidth = width - bankInset * 2;
                    int carveDepth = depth - bankInset * 2;
                    int waterInset = bankInset + shoreInset;
                    int waterWidth = width - waterInset * 2;
                    int waterDepth = depth - waterInset * 2;
                    if (carveWidth <= 0 || carveDepth <= 0 || waterWidth <= 0 || waterDepth <= 0)
                        throw new InvalidOperationException(
                            "Macro water body '" + region.Spec.Id + "' is too small for basin/shore insets.");

                    int carveRadius = Math.Max(8 * scale, Math.Min(carveWidth, carveDepth) / 8);
                    int waterRadius = Math.Max(6 * scale, Math.Min(waterWidth, waterDepth) / 10);
                    WriteProgram(
                        ref catalogue,
                        programOffset,
                        bankInset,
                        waterInset,
                        carveWidth,
                        carveDepth,
                        waterWidth,
                        waterDepth,
                        basinDepth,
                        headroom,
                        waterSurfaceThickness,
                        carveRadius,
                        waterRadius,
                        water);

                    catalogue.Definitions[definitionId] = new FeatureDefinition
                    {
                        Name = new FixedString64Bytes(DefinitionPrefix + region.Spec.Id),
                        Kind = FeatureKind.Landform,
                        BasePlane = BasePlaneRule.FixedAltitude,
                        FixedAltitude = 0,
                        Footprint = new int3(width, height, depth),
                        MaxSlope = 32,
                        Precedence = 6,
                        ParameterOffset = 0,
                        ParameterCount = 0,
                        AnchorOffset = 0,
                        AnchorCount = 0,
                        SlotOffset = 0,
                        SlotCount = 0,
                        ProgramOffset = programOffset,
                        ProgramLength = ProgramIntsPerDefinition,
                        MaterialOffset = 0,
                        MaterialCount = 0,
                        MaxPrimitives = 2
                    };

                    int ground = TerrainSampler.HeightAt(
                        region.CentreDm.X * scale,
                        region.CentreDm.Y * scale,
                        seed);
                    catalogue.ExplicitPlacements[definitionId] = new ExplicitPlacement
                    {
                        Position = new int3(
                            (region.CentreDm.X - region.HalfExtentXDm) * scale,
                            ground - basinDepth,
                            (region.CentreDm.Y - region.HalfExtentZDm) * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0
                    };
                    catalogue.Rules[definitionId] = new PlacementRule
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
                        ExplicitOffset = definitionId,
                        ExplicitCount = 1
                    };

                    definitionId++;
                    programOffset += ProgramIntsPerDefinition;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException("Macro water-body catalogue failed validation: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        public static int DepthVoxels(TopDownWorldRegionPlan region, int scale)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale));
            int authoredDepth = Math.Abs(region.ElevationDeltaDm);
            int clamped = Math.Max(MinimumDepthDm, Math.Min(MaximumDepthDm, authoredDepth));
            return clamped * scale;
        }

        private static void WriteProgram(
            ref FeatureCatalogue catalogue,
            int offset,
            int bankInset,
            int waterInset,
            int carveWidth,
            int carveDepth,
            int waterWidth,
            int waterDepth,
            int basinDepth,
            int headroom,
            int waterSurfaceThickness,
            int carveRadius,
            int waterRadius,
            byte water)
        {
            int[] program =
            {
                (int)ShapeOp.EmitRoundedBox, 0,
                bankInset, 0, bankInset,
                carveWidth, basinDepth + headroom, carveDepth,
                carveRadius, 0, 0, 0, (int)PrimitiveMode.Carve,
                (int)ShapeOp.EmitRoundedBox, 0,
                waterInset, basinDepth, waterInset,
                waterWidth, waterSurfaceThickness, waterDepth,
                waterRadius, water, 0, 0, (int)PrimitiveMode.Fill,
                (int)ShapeOp.End, 0
            };
            for (var i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }
    }
}
