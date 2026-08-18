using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Composition.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Builds the showcase-owned detailed farmhouse as an ordinary immutable feature catalogue.
    /// The house is compiled through the same shared HouseConfig/HouseProgramCompiler path as other
    /// authored houses and then composed with the Kentridge catalogue by the application root.
    /// </summary>
    internal static class ShowcaseDetailedHouseCatalogue
    {
        // East of the service lane and just north of Market Street. Keeping the authored origin off
        // the roadbed makes the example visible near town without overlapping stable Kentridge roles.
        private const int PlacementX = 1540;
        private const int PlacementZ = 560;

        public static FeatureCatalogue Build(
            uint seed,
            in ShowcaseMaterialSet materials,
            Allocator allocator)
        {
            HouseConfig config = ShowcaseHouseComposition.DetailedFarmhouse(in materials);
            int[] program = HouseProgramCompiler.BuildProgram(in config, 0, 1);

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 2,
                slots: 0,
                programLength: program.Length,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            for (int i = 0; i < program.Length; i++)
                catalogue.Program[i] = program[i];

            int wallBaseY = config.FoundationDepth;
            int doorY = wallBaseY + config.FrontDoors.Opening.BottomOffset;
            catalogue.Anchors[0] = new AnchorSpec
            {
                Name = "door",
                LocalPosition = new int3(config.Width / 2, doorY, 0),
                Facing = Facing.South,
                SnapToGround = false,
            };
            catalogue.Anchors[1] = new AnchorSpec
            {
                Name = "hearth",
                LocalPosition = new int3(config.Width / 2, wallBaseY, config.Depth / 2),
                Facing = Facing.Up,
                SnapToGround = false,
            };

            int footprintHeight = RequiredHeight(in config);
            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = "showcase-detailed-farmhouse",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.LowestGround,
                Footprint = new int3(config.Width, footprintHeight, config.Depth),
                MaxSlope = 3,
                Precedence = 110,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 2,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 256,
            };

            int surfaceY = LowestGround(seed, PlacementX, PlacementZ, config.Width, config.Depth);
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(PlacementX, surfaceY - config.FoundationDepth, PlacementZ),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 3,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result == CatalogueLoadResult.Ok)
                return catalogue;

            catalogue.Dispose();
            throw new InvalidOperationException(
                "Detailed showcase farmhouse catalogue failed validation: " + result);
        }

        private static int RequiredHeight(in HouseConfig config)
        {
            int roofBase = config.FoundationDepth + config.Walls.Height;
            int roofHeight;
            if (config.Roof.Style == RoofStyle.Flat)
            {
                roofHeight = config.Roof.Thickness;
            }
            else
            {
                int span = config.Roof.RidgeAxis == RoofAxis.Z ? config.Width : config.Depth;
                roofHeight = (span / 2) * config.Roof.PitchRise / config.Roof.PitchRun;
            }

            int maxY = roofBase + roofHeight;
            if (config.Chimney.Enabled)
                maxY = math.max(maxY, roofBase + config.Chimney.Geometry.Height);

            return maxY + 1;
        }

        private static int LowestGround(
            uint seed,
            int originX,
            int originZ,
            int width,
            int depth)
        {
            const int samplesPerAxis = 5;
            int lowest = int.MaxValue;
            for (int iz = 0; iz < samplesPerAxis; iz++)
            for (int ix = 0; ix < samplesPerAxis; ix++)
            {
                int x = originX + (width - 1) * ix / (samplesPerAxis - 1);
                int z = originZ + (depth - 1) * iz / (samplesPerAxis - 1);
                int sample = TerrainQuery.HeightAt(x, z, seed);
                if (sample < lowest) lowest = sample;
            }

            return lowest;
        }
    }
}
