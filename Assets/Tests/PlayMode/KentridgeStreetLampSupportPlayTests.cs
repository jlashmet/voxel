using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

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
            VoxelWorldGenSettings settings = BuildSettings();
            FeatureCatalogue catalogue = KentridgeStreetDressingCatalogue.Build(
                ShowcaseSeed, settings, Allocator.Temp);
            FeatureCatalogue districtCatalogue = KentridgeDistrictTerraceCatalogue.Build(
                ShowcaseSeed, settings, Allocator.Temp);
            var primitives = new NativeList<Primitive>(48, Allocator.Temp);
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

                PlacementRule districtRule = default;
                FeatureDefinition districtDefinition = default;
                bool foundWorkingYard = false;
                for (int i = 0; i < districtCatalogue.Rules.Length; i++)
                {
                    PlacementRule candidateRule = districtCatalogue.Rules[i];
                    FeatureDefinition candidateDefinition =
                        districtCatalogue.Definitions[candidateRule.DefinitionId];
                    if (candidateDefinition.Name.ToString()
                        != "kentridge-district-terrace-working-yard")
                        continue;

                    districtRule = candidateRule;
                    districtDefinition = candidateDefinition;
                    foundWorkingYard = true;
                    break;
                }

                Assert.IsTrue(foundWorkingYard,
                    "The working-yard district terrace that owns the captured sidewalk must remain authored.");
                Assert.AreEqual(1, districtRule.ExplicitCount);

                ExplicitPlacement districtPlacement =
                    districtCatalogue.ExplicitPlacements[districtRule.ExplicitOffset];
                ParameterSet districtParameters = FeatureGeneration.ResolveParameters(
                    in districtCatalogue, in districtDefinition, in districtPlacement,
                    districtRule.DefinitionId, districtPlacement.Position, ShowcaseSeed);
                ulong districtInstanceSeed = FeatureGeneration.InstanceSeed(
                    ShowcaseSeed, districtRule.DefinitionId, districtPlacement.Position);

                EvaluationResult districtResult = ShapeProgram.Evaluate(
                    in districtCatalogue, districtRule.DefinitionId, in districtParameters,
                    districtPlacement.Position, districtPlacement.Orientation,
                    ShowcaseSeed, districtInstanceSeed, primitives, anchors);
                Assert.AreEqual(EvaluationResult.Ok, districtResult);

                int worldX = CapturedLampXDm * settings.VoxelsPerDecimetre;
                int worldZ = CapturedLampZDm * settings.VoxelsPerDecimetre;
                int generatedGroundSurfaceY = int.MinValue;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Mode != PrimitiveMode.Fill
                        || primitive.Shape != PrimitiveShape.Box)
                        continue;

                    primitive.Bounds(out var min, out var max);
                    if (worldX < min.x || worldX > max.x
                        || worldZ < min.z || worldZ > max.z)
                        continue;

                    int candidateSurfaceY = max.y + 1;
                    if (candidateSurfaceY > generatedGroundSurfaceY)
                        generatedGroundSurfaceY = candidateSurfaceY;
                }

                Assert.AreNotEqual(int.MinValue, generatedGroundSurfaceY,
                    "The production working-yard terrace must generate solid support under the captured lamp column.");
                Assert.AreEqual(generatedGroundSurfaceY, placement.Position.y,
                    "The captured lamp origin must remain the first empty voxel above its generated district shoulder.");
                Assert.AreNotEqual(
                    KentridgeVerticalProfile.SurfaceYAtDm(
                        CapturedLampXDm, CapturedLampZDm, ShowcaseSeed,
                        settings.VoxelsPerDecimetre),
                    placement.Position.y,
                    "This regression must exercise the captured shoulder/macro mismatch rather than a flat macro column.");

                primitives.Clear();
                anchors.Clear();

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

                Primitive foot = default;
                Primitive support = default;
                Primitive lantern = default;
                bool foundFoot = false;
                bool foundSupport = false;
                bool foundLantern = false;
                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Shape == PrimitiveShape.Cylinder && primitive.Material == 1)
                    {
                        foot = primitive;
                        foundFoot = true;
                    }
                    else if (primitive.Shape == PrimitiveShape.Box && primitive.Material == 6)
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

                Assert.IsTrue(foundFoot,
                    "The captured lamp must retain its foundation-stone ground-contact cylinder.");
                Assert.IsTrue(foundSupport,
                    "The captured lamp must retain its dark-stone support primitive.");
                Assert.IsTrue(foundLantern,
                    "The captured lamp must retain the warm lantern head that exposed the floating-support defect.");
                Assert.AreEqual(SurfaceStyles.Planar, foot.SurfaceStyle,
                    "The visible lamp foot must reconstruct exactly instead of inheriting stone smoothing.");
                Assert.AreEqual(SurfaceStyles.Planar, support.SurfaceStyle,
                    "A 3x3-voxel lamp pole must not inherit dark stone's Smooth reconstruction.");

                foot.Bounds(out var footMin, out var footMax);
                Assert.AreEqual(generatedGroundSurfaceY - 1, footMin.y,
                    "The exact lamp foot must overlap the terrace's top occupied voxel so Smooth ground reconstruction cannot open a visible seam.");
                Assert.AreEqual(
                    placement.Position.y + 4 * settings.VoxelsPerDecimetre - 1,
                    footMax.y,
                    "Embedding the foot must preserve its original top plane and therefore leave all upper lamp geometry unchanged.");

                support.Bounds(out _, out var supportMax);
                lantern.Bounds(out var lanternMin, out _);
                Assert.GreaterOrEqual(supportMax.y, lanternMin.y,
                    "The exact support must physically overlap the lantern instead of leaving a vertical gap.");
            }
            finally
            {
                anchors.Dispose();
                primitives.Dispose();
                districtCatalogue.Dispose();
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
