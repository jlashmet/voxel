using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeStreetLampSupportPlayTests
    {
        private const uint ShowcaseSeed = 1592594996u;
        private const int CapturedLampXDm = 1530;
        private const int CapturedLampZDm = 549;

        [Test]
        public void CapturedEastMarketLampKeepsPlanarSupportUnderLantern()
        {
            FeatureCatalogue catalogue = KentridgeStreetDressingCatalogue.Build(
                ShowcaseSeed, BuildSettings(), Allocator.Temp);
            var primitives = new NativeList<Primitive>(8, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(1, Allocator.Temp);

            try
            {
                PlacementRule lampRule = catalogue.Rules[0];
                FeatureDefinition lampDefinition = catalogue.Definitions[lampRule.DefinitionId];
                ExplicitPlacement placement = default;
                bool found = false;

                for (int i = 0; i < lampRule.ExplicitCount; i++)
                {
                    ExplicitPlacement candidate =
                        catalogue.ExplicitPlacements[lampRule.ExplicitOffset + i];
                    if (candidate.Position.x == CapturedLampXDm
                        && candidate.Position.z == CapturedLampZDm)
                    {
                        placement = candidate;
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found,
                    "The exact east-market lamp visible from SceneIssue 20260826-132505 must remain authored.");

                ParameterSet parameters = FeatureGeneration.ResolveParameters(
                    in catalogue, in lampDefinition, in placement, lampRule.DefinitionId,
                    placement.Position, ShowcaseSeed);
                ulong instanceSeed = FeatureGeneration.InstanceSeed(
                    ShowcaseSeed, lampRule.DefinitionId, placement.Position);

                EvaluationResult result = ShapeProgram.Evaluate(
                    in catalogue, lampRule.DefinitionId, in parameters,
                    placement.Position, placement.Orientation,
                    ShowcaseSeed, instanceSeed, primitives, anchors);

                Assert.AreEqual(EvaluationResult.Ok, result);
                Assert.AreEqual(4, primitives.Length,
                    "The production lamp should remain base, pole, lantern, and roof.");

                Primitive support = default;
                Primitive lantern = default;
                bool foundSupport = false;
                bool foundLantern = false;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Shape == PrimitiveShape.Box && primitive.Material == 6)
                    {
                        support = primitive;
                        foundSupport = true;
                    }
                    else if (primitive.Shape == PrimitiveShape.Box && primitive.Material == 15)
                    {
                        lantern = primitive;
                        foundLantern = true;
                    }
                }

                Assert.IsTrue(foundSupport,
                    "The captured lamp must retain its dark-stone support primitive.");
                Assert.IsTrue(foundLantern,
                    "The captured lamp must retain the warm lantern head that exposed the floating-support defect.");
                Assert.AreEqual(SurfaceStyles.Planar, support.SurfaceStyle,
                    "A 3x3-voxel lamp pole must not inherit dark stone's Smooth reconstruction.");

                support.Bounds(out _, out var supportMax);
                lantern.Bounds(out var lanternMin, out _);
                Assert.GreaterOrEqual(supportMax.y, lanternMin.y,
                    "The exact support must physically overlap the lantern instead of leaving a vertical gap.");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                catalogue.Dispose();
            }
        }

        private static VoxelWorldGenSettings BuildSettings()
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
