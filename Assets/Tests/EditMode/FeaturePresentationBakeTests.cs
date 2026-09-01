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
    public sealed class FeaturePresentationBakeTests
    {
        private const uint Seed = 0x46415237u;

        [Test]
        public void GenericBaker_BakesUnrelatedStructureAndLandform_FromNormalCataloguePlacements()
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
                FindFirstExplicitDefinition(
                    in townCatalogue,
                    FeatureKind.Structure,
                    out int townDefinitionId,
                    out ExplicitPlacement townPlacement);
                int mountainDefinitionId = FindDefinition(
                    in mountainCatalogue,
                    WorldBuilderMountainLandmarkCatalogue.LandformDefinitionName);
                ExplicitPlacement mountainPlacement = FindExplicitPlacement(
                    in mountainCatalogue, mountainDefinitionId);

                IFeaturePresentationBaker baker = new FeaturePresentationBaker();

                Assert.IsTrue(baker.TryBake(
                    in townCatalogue, Seed, townDefinitionId, in townPlacement,
                    out FeaturePresentationBake town));
                Assert.IsTrue(baker.TryBake(
                    in mountainCatalogue, Seed, mountainDefinitionId, in mountainPlacement,
                    out FeaturePresentationBake mountain));

                Assert.Greater(town.PrimitiveCount, 0);
                Assert.Greater(mountain.PrimitiveCount, 0);
                Assert.AreNotEqual(town.SourceId, mountain.SourceId);
                Assert.AreNotEqual(0ul, town.Revision);
                Assert.AreNotEqual(0ul, mountain.Revision);
                Assert.AreEqual(FeatureKind.Structure, town.Kind);
                Assert.AreEqual(FeatureKind.Landform, mountain.Kind);
                AssertPositiveBounds(town);
                AssertPositiveBounds(mountain);
                AssertBakeContainsShape(town, PrimitiveShape.Box);
                AssertBakeContainsShape(mountain, PrimitiveShape.Frustum);

                Assert.IsTrue(baker.TryBake(
                    in townCatalogue, Seed, townDefinitionId, in townPlacement,
                    out FeaturePresentationBake townRepeat));
                Assert.IsTrue(baker.TryBake(
                    in mountainCatalogue, Seed, mountainDefinitionId, in mountainPlacement,
                    out FeaturePresentationBake mountainRepeat));

                AssertBakeEqual(town, townRepeat);
                AssertBakeEqual(mountain, mountainRepeat);
            }
            finally
            {
                mountainCatalogue.Dispose();
                townCatalogue.Dispose();
            }
        }

        private static void FindFirstExplicitDefinition(
            in FeatureCatalogue catalogue,
            FeatureKind kind,
            out int definitionId,
            out ExplicitPlacement placement)
        {
            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if (rule.ExplicitCount <= 0
                    || (uint)rule.DefinitionId >= (uint)catalogue.Definitions.Length
                    || catalogue.Definitions[rule.DefinitionId].Kind != kind)
                    continue;

                definitionId = rule.DefinitionId;
                placement = catalogue.ExplicitPlacements[rule.ExplicitOffset];
                return;
            }

            Assert.Fail($"Expected an explicit production placement for feature kind {kind}.");
            definitionId = -1;
            placement = default;
        }

        private static ExplicitPlacement FindExplicitPlacement(
            in FeatureCatalogue catalogue,
            int definitionId)
        {
            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if (rule.DefinitionId == definitionId && rule.ExplicitCount > 0)
                    return catalogue.ExplicitPlacements[rule.ExplicitOffset];
            }

            Assert.Fail($"Expected an explicit production placement for definition {definitionId}.");
            return default;
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

        private static void AssertPositiveBounds(FeaturePresentationBake bake)
        {
            Assert.Greater(bake.BoundsMax.x, bake.BoundsMin.x);
            Assert.Greater(bake.BoundsMax.y, bake.BoundsMin.y);
            Assert.Greater(bake.BoundsMax.z, bake.BoundsMin.z);
        }

        private static void AssertBakeContainsShape(
            FeaturePresentationBake bake,
            PrimitiveShape expectedShape)
        {
            for (int i = 0; i < bake.PrimitiveCount; i++)
            {
                if (bake.GetPrimitive(i).Shape == expectedShape)
                    return;
            }

            Assert.Fail($"Expected baked primitive shape {expectedShape}.");
        }

        private static void AssertBakeEqual(
            FeaturePresentationBake expected,
            FeaturePresentationBake actual)
        {
            Assert.AreEqual(expected.SourceId, actual.SourceId);
            Assert.AreEqual(expected.Revision, actual.Revision);
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.Position, actual.Position);
            Assert.AreEqual(expected.Orientation, actual.Orientation);
            Assert.AreEqual(expected.BoundsMin, actual.BoundsMin);
            Assert.AreEqual(expected.BoundsMax, actual.BoundsMax);
            Assert.AreEqual(expected.PrimitiveCount, actual.PrimitiveCount);

            for (int i = 0; i < expected.PrimitiveCount; i++)
                AssertPrimitiveEqual(expected.GetPrimitive(i), actual.GetPrimitive(i));
        }

        private static void AssertPrimitiveEqual(Primitive expected, Primitive actual)
        {
            Assert.AreEqual(expected.Shape, actual.Shape);
            Assert.AreEqual(expected.Mode, actual.Mode);
            Assert.AreEqual(expected.Material, actual.Material);
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle);
            Assert.AreEqual(expected.Coating, actual.Coating);
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags);
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail);
            Assert.AreEqual(expected.Axis, actual.Axis);
            Assert.AreEqual(expected.Direction, actual.Direction);
            Assert.AreEqual(expected.Profile, actual.Profile);
            Assert.AreEqual(expected.Order, actual.Order);
            Assert.AreEqual(expected.A, actual.A);
            Assert.AreEqual(expected.B, actual.B);
            Assert.AreEqual(expected.Radius, actual.Radius);
            Assert.AreEqual(expected.InnerRadius, actual.InnerRadius);
            Assert.AreEqual(expected.C, actual.C);
            Assert.AreEqual(expected.D, actual.D);
            Assert.AreEqual(expected.StartDirection, actual.StartDirection);
            Assert.AreEqual(expected.EndDirection, actual.EndDirection);
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
    }
}
