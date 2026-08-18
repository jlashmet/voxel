using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.Features
{
    /// <summary>WB040 contract coverage for the shared detailed-house path.</summary>
    public sealed class HouseInvariantDeterminismTests
    {
        [Test]
        public void FarmhouseHasNavigableInteriorAndBoundedFacadeOpenings()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 7);

            Assert.IsTrue(config.Footprint.IsWellFormed);
            Assert.IsTrue(config.Walls.IsWellFormed);
            Assert.IsTrue(config.FrontDoors.IsWellFormed);
            Assert.IsTrue(config.RearDoors.IsWellFormed);
            Assert.IsTrue(config.FrontWindows.IsWellFormed);
            Assert.IsTrue(config.RearWindows.IsWellFormed);

            Assert.Greater(config.Width - 2 * config.WallThickness, 0);
            Assert.Greater(config.Depth - 2 * config.WallThickness, 0);
            Assert.LessOrEqual(
                config.FrontDoors.Opening.BottomOffset + config.FrontDoors.Opening.Height,
                config.Walls.Height);
            Assert.LessOrEqual(
                config.FrontWindows.Opening.BottomOffset + config.FrontWindows.Opening.Height,
                config.Walls.Height);
            Assert.GreaterOrEqual(config.Walls.Height,
                config.FloorCount * config.FloorHeight);
        }

        [Test]
        public void SameFarmhouseConfigCompilesAndEvaluatesDeterministically()
        {
            HouseConfig firstConfig = HousePresets.Farmhouse(3, 7);
            HouseConfig secondConfig = HousePresets.Farmhouse(3, 7);
            int[] firstProgram = HouseProgramCompiler.BuildProgram(in firstConfig, 0, 1);
            int[] secondProgram = HouseProgramCompiler.BuildProgram(in secondConfig, 0, 1);

            CollectionAssert.AreEqual(firstProgram, secondProgram);

            FeatureCatalogue catalogue = BuildCatalogue(in firstConfig, firstProgram, Allocator.Temp);
            var first = new NativeList<Primitive>(64, Allocator.Temp);
            var second = new NativeList<Primitive>(64, Allocator.Temp);
            var firstAnchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            var secondAnchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            try
            {
                var parameters = new ParameterSet();
                const uint worldSeed = 0x51A7E123u;
                const ulong instanceSeed = 0xD00DFEED12345678ul;

                Assert.AreEqual(EvaluationResult.Ok,
                    ShapeProgram.Evaluate(
                        in catalogue, 0, in parameters, int3.zero, 0,
                        worldSeed, instanceSeed, first, firstAnchors));
                Assert.AreEqual(EvaluationResult.Ok,
                    ShapeProgram.Evaluate(
                        in catalogue, 0, in parameters, int3.zero, 0,
                        worldSeed, instanceSeed, second, secondAnchors));

                Assert.AreEqual(first.Length, second.Length);
                Assert.AreEqual(firstAnchors.Length, secondAnchors.Length);
                for (int i = 0; i < first.Length; i++)
                {
                    Assert.AreEqual(first[i].Shape, second[i].Shape, $"primitive {i} shape");
                    Assert.AreEqual(first[i].Mode, second[i].Mode, $"primitive {i} mode");
                    Assert.AreEqual(first[i].Material, second[i].Material, $"primitive {i} material");
                    Assert.AreEqual(first[i].A, second[i].A, $"primitive {i} min");
                    Assert.AreEqual(first[i].B, second[i].B, $"primitive {i} max");
                }
            }
            finally
            {
                secondAnchors.Dispose();
                firstAnchors.Dispose();
                second.Dispose();
                first.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void FarmhousePrimitivesRemainInsideDeclaredFootprint()
        {
            HouseConfig config = HousePresets.Farmhouse(3, 7);
            int[] program = HouseProgramCompiler.BuildProgram(in config, 0, 1);
            FeatureCatalogue catalogue = BuildCatalogue(in config, program, Allocator.Temp);
            var primitives = new NativeList<Primitive>(64, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(4, Allocator.Temp);
            try
            {
                var parameters = new ParameterSet();
                Assert.AreEqual(EvaluationResult.Ok,
                    ShapeProgram.Evaluate(
                        in catalogue, 0, in parameters, int3.zero, 0,
                        123u, 456ul, primitives, anchors));
                Assert.Greater(primitives.Length, 0);

                int3 footprint = catalogue.Definitions[0].Footprint;
                for (int i = 0; i < primitives.Length; i++)
                {
                    primitives[i].Bounds(out int3 min, out int3 max);
                    Assert.GreaterOrEqual(min.x, 0, $"primitive {i} escapes -X");
                    Assert.GreaterOrEqual(min.y, 0, $"primitive {i} escapes -Y");
                    Assert.GreaterOrEqual(min.z, 0, $"primitive {i} escapes -Z");
                    Assert.Less(max.x, footprint.x, $"primitive {i} escapes +X");
                    Assert.Less(max.y, footprint.y, $"primitive {i} escapes +Y");
                    Assert.Less(max.z, footprint.z, $"primitive {i} escapes +Z");
                }
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                catalogue.Dispose();
            }
        }

        private static FeatureCatalogue BuildCatalogue(
            in HouseConfig config,
            int[] program,
            Allocator allocator)
        {
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 0,
                parameters: 0,
                anchors: 2,
                slots: 0,
                programLength: program.Length,
                materials: 0,
                explicitPlacements: 0,
                overrides: 0,
                allocator);

            for (int i = 0; i < program.Length; i++)
                catalogue.Program[i] = program[i];

            catalogue.Anchors[0] = new AnchorSpec
            {
                Name = "door",
                LocalPosition = new int3(config.Width / 2, config.FoundationDepth, 0),
                Facing = Facing.South,
            };
            catalogue.Anchors[1] = new AnchorSpec
            {
                Name = "hearth",
                LocalPosition = new int3(
                    config.Width / 2, config.FoundationDepth, config.Depth / 2),
                Facing = Facing.Up,
            };

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = "farmhouse-test",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.LowestGround,
                Footprint = new int3(config.Width, RequiredHeight(in config), config.Depth),
                MaxSlope = 3,
                Precedence = 100,
                AnchorOffset = 0,
                AnchorCount = 2,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaxPrimitives = 256,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            Assert.AreEqual(CatalogueLoadResult.Ok, result);
            return catalogue;
        }

        private static int RequiredHeight(in HouseConfig config)
        {
            int roofBase = config.FoundationDepth + config.Walls.Height;
            int roofHeight = config.Roof.Style == RoofStyle.Flat
                ? config.Roof.Thickness
                : ((config.Roof.RidgeAxis == RoofAxis.Z ? config.Width : config.Depth) / 2)
                  * config.Roof.PitchRise / config.Roof.PitchRun;
            int chimneyHeight = config.Chimney.Enabled
                ? roofBase + config.Chimney.Geometry.Height
                : 0;
            return math.max(roofBase + roofHeight, chimneyHeight) + 1;
        }
    }
}
