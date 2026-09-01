using Game.WorldBuilder.Voxel;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarWorldPrimitiveBakeSeamTests
    {
        private const uint Seed = 0x46415237u;

        [Test]
        public void CanonicalShapePrograms_DeriveFarBakeInputsForTownAndMountain_WithoutRegionResidency()
        {
            FeatureCatalogue townCatalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed, BuildKentridgeSettings(), Allocator.Temp);
            FeatureCatalogue mountainCatalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                BuildMountainSpec(),
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 2,
                allocator: Allocator.Temp);

            try
            {
                int townDefinitionId = FindDefinitionStartingWith(
                    in townCatalogue, "kentridge-foundation-skirt-");
                int mountainDefinitionId = FindDefinition(
                    in mountainCatalogue, WorldBuilderMountainLandmarkCatalogue.LandformDefinitionName);

                Assert.Zero(townCatalogue.Definitions[townDefinitionId].ParameterCount,
                    "The structural discriminator should not require an authoring-only parameter adapter.");
                Assert.Zero(mountainCatalogue.Definitions[mountainDefinitionId].ParameterCount,
                    "The natural-landmark discriminator should not require an authoring-only parameter adapter.");

                CoarseMassing town = EvaluateMassing(
                    in townCatalogue,
                    townDefinitionId,
                    new int3(-640, 64, 960),
                    instanceSeed: 0x544F574Eu,
                    out int townPrimitiveCount);
                CoarseMassing mountain = EvaluateMassing(
                    in mountainCatalogue,
                    mountainDefinitionId,
                    mountainCatalogue.ExplicitPlacements[0].Position,
                    instanceSeed: 0x4D4F554E5441494Eul,
                    out int mountainPrimitiveCount);

                Assert.Greater(townPrimitiveCount, 0);
                Assert.Greater(mountainPrimitiveCount, 0);
                Assert.That(town.ShapeMask & ShapeBit(PrimitiveShape.Box), Is.Not.Zero,
                    "Kentridge structural geometry should survive as generic box massing.");
                Assert.That(mountain.ShapeMask & ShapeBit(PrimitiveShape.Frustum), Is.Not.Zero,
                    "The mountain landform should survive as generic curved massing.");
                Assert.That(mountain.ShapeMask & ShapeBit(PrimitiveShape.Ramp), Is.Not.Zero,
                    "The mountain ascent should survive as generic ramp massing.");
                AssertPositiveBounds(town);
                AssertPositiveBounds(mountain);

                CoarseMassing townRepeat = EvaluateMassing(
                    in townCatalogue,
                    townDefinitionId,
                    new int3(-640, 64, 960),
                    instanceSeed: 0x544F574Eu,
                    out int townRepeatCount);
                CoarseMassing mountainRepeat = EvaluateMassing(
                    in mountainCatalogue,
                    mountainDefinitionId,
                    mountainCatalogue.ExplicitPlacements[0].Position,
                    instanceSeed: 0x4D4F554E5441494Eul,
                    out int mountainRepeatCount);

                Assert.AreEqual(townPrimitiveCount, townRepeatCount);
                Assert.AreEqual(mountainPrimitiveCount, mountainRepeatCount);
                AssertMassingEqual(town, townRepeat);
                AssertMassingEqual(mountain, mountainRepeat);
            }
            finally
            {
                mountainCatalogue.Dispose();
                townCatalogue.Dispose();
            }
        }

        private static CoarseMassing EvaluateMassing(
            in FeatureCatalogue catalogue,
            int definitionId,
            int3 origin,
            ulong instanceSeed,
            out int primitiveCount)
        {
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);
            try
            {
                ParameterSet parameters = default;
                EvaluationResult result = ShapeProgram.Evaluate(
                    in catalogue,
                    definitionId,
                    in parameters,
                    origin,
                    orientation: 0,
                    terrainSeed: Seed,
                    instanceSeed: instanceSeed,
                    primitives: primitives,
                    anchors: anchors);

                Assert.AreEqual(EvaluationResult.Ok, result);
                Assert.Greater(primitives.Length, 0,
                    "A normal generated feature must expose primitive massing before voxel rasterization.");

                primitiveCount = primitives.Length;
                primitives[0].Bounds(out int3 min, out int3 max);
                ulong shapeMask = 0;

                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    primitive.Bounds(out int3 primitiveMin, out int3 primitiveMax);
                    min = math.min(min, primitiveMin);
                    max = math.max(max, primitiveMax);
                    shapeMask |= ShapeBit(primitive.Shape);
                }

                return new CoarseMassing(min, max, shapeMask);
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
            }
        }

        private static int FindDefinition(in FeatureCatalogue catalogue, string name)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                if (catalogue.Definitions[i].Name.ToString() == name)
                    return i;
            }

            Assert.Fail($"Expected production feature definition '{name}'.");
            return -1;
        }

        private static int FindDefinitionStartingWith(in FeatureCatalogue catalogue, string prefix)
        {
            for (int i = 0; i < catalogue.Definitions.Length; i++)
            {
                if (catalogue.Definitions[i].Name.ToString().StartsWith(prefix))
                    return i;
            }

            Assert.Fail($"Expected a production feature definition beginning with '{prefix}'.");
            return -1;
        }

        private static MountainLandmarkSpec BuildMountainSpec() => new MountainLandmarkSpec(
            origin: new int3(2048, 180, 4096),
            footprintEdge: 256,
            mountainRadius: 96,
            mountainHeight: 80,
            summitRadius: 32,
            pathWidth: 12,
            pathRun: 80,
            pathRise: 12,
            switchbackCount: 5,
            placeholderSize: 16);

        private static VoxelWorldGenSettings BuildKentridgeSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static ulong ShapeBit(PrimitiveShape shape) => 1ul << (int)shape;

        private static void AssertPositiveBounds(CoarseMassing massing)
        {
            Assert.Greater(massing.Max.x, massing.Min.x);
            Assert.Greater(massing.Max.y, massing.Min.y);
            Assert.Greater(massing.Max.z, massing.Min.z);
        }

        private static void AssertMassingEqual(CoarseMassing expected, CoarseMassing actual)
        {
            Assert.AreEqual(expected.Min, actual.Min);
            Assert.AreEqual(expected.Max, actual.Max);
            Assert.AreEqual(expected.ShapeMask, actual.ShapeMask);
        }

        private readonly struct CoarseMassing
        {
            public int3 Min { get; }
            public int3 Max { get; }
            public ulong ShapeMask { get; }

            public CoarseMassing(int3 min, int3 max, ulong shapeMask)
            {
                Min = min;
                Max = max;
                ShapeMask = shapeMask;
            }
        }
    }
}
