using System.IO;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeStreetLampSupportPlayTests
    {
        [Test]
        public void CapturedEastMarketLampKeepsPlanarSupportUnderLantern()
        {
            const uint seed = 1592594996u;
            VoxelWorldGenSettings settings = CreateSettings();
            FeatureCatalogue street = KentridgeStreetDressingCatalogue.Build(seed, settings, Allocator.TempJob);
            FeatureCatalogue terrace = KentridgeDistrictTerraceCatalogue.Build(seed, settings, Allocator.TempJob);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                const int capturedX = 1530;
                const int capturedZ = 549;
                int lampDefinition = -1;
                ExplicitPlacement capturedPlacement = default;

                for (int ruleIndex = 0; ruleIndex < street.Rules.Length && lampDefinition < 0; ruleIndex++)
                {
                    PlacementRule rule = street.Rules[ruleIndex];
                    for (int p = 0; p < rule.ExplicitCount; p++)
                    {
                        ExplicitPlacement placement = street.ExplicitPlacements[rule.ExplicitOffset + p];
                        if (placement.Position.x == capturedX && placement.Position.z == capturedZ)
                        {
                            lampDefinition = rule.DefinitionId;
                            capturedPlacement = placement;
                            break;
                        }
                    }
                }

                Assert.That(lampDefinition, Is.GreaterThanOrEqualTo(0),
                    "Captured east-market lamp placement not found in production street dressing.");

                int macroSurfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                    capturedX, capturedZ, seed, settings.VoxelsPerDecimetre);
                int generatedGroundSurfaceY = FindGeneratedGroundSurfaceY(
                    terrace, capturedX, capturedZ, seed, primitives, anchors);

                Assert.That(generatedGroundSurfaceY, Is.GreaterThan(int.MinValue),
                    "No production terrace solid owns the captured lamp column.");
                Assert.AreNotEqual(macroSurfaceY, generatedGroundSurfaceY + 1,
                    "The capture must keep discriminating district ground from the macro elevation.");
                Assert.AreEqual(generatedGroundSurfaceY + 1, capturedPlacement.Position.y,
                    "Captured lamp origin must remain the first empty voxel above the working-yard terrace solid.");

                primitives.Clear();
                anchors.Clear();
                FeatureDefinition lamp = street.Definitions[lampDefinition];
                ParameterSet lampParameters = DefaultParameters(street, lamp);
                EvaluationResult result = ShapeProgram.Evaluate(
                    in street, lampDefinition, in lampParameters, capturedPlacement.Position,
                    capturedPlacement.Orientation, seed, 0UL, primitives, anchors);
                Assert.AreEqual(EvaluationResult.Ok, result,
                    "Production lamp shape program must evaluate successfully.");

                Primitive? foot = null;
                Primitive? pole = null;
                Primitive? lantern = null;
                byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
                byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
                byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);

                for (int i = 0; i < primitives.Length; i++)
                {
                    Primitive primitive = primitives[i];
                    if (primitive.Shape == PrimitiveShape.Cylinder &&
                        primitive.Material == stone && primitive.SurfaceStyle == SurfaceStyles.Planar)
                    {
                        foot = primitive;
                    }
                    else if (primitive.Shape == PrimitiveShape.Box &&
                             primitive.Material == dark && primitive.SurfaceStyle == SurfaceStyles.Planar)
                    {
                        pole = primitive;
                    }
                    else if (primitive.Shape == PrimitiveShape.Box && primitive.Material == warm)
                    {
                        lantern = primitive;
                    }
                }

                Assert.That(foot.HasValue, "Expected embedded Planar stone lamp foot.");
                Assert.That(pole.HasValue, "Expected Planar dark-masonry lamp support.");
                Assert.That(lantern.HasValue, "Expected warm-window lantern head.");

                foot.Value.Bounds(out int3 footMin, out int3 footMax);
                pole.Value.Bounds(out int3 poleMin, out int3 poleMax);
                lantern.Value.Bounds(out int3 lanternMin, out int3 lanternMax);

                Assert.AreEqual(generatedGroundSurfaceY, footMin.y,
                    "Lamp foot must overlap the terrace top occupied voxel so reconstruction cannot expose a seam.");
                Assert.AreEqual(capturedPlacement.Position.y + 3, footMax.y,
                    "Embedding the lamp foot must preserve its visible top plane.");
                Assert.LessOrEqual(poleMin.y, footMax.y + 1,
                    "Planar foot must meet the pole without a vertical support gap.");
                Assert.LessOrEqual(lanternMin.y, poleMax.y + 1,
                    "Planar pole must remain continuous under the lantern head.");

                ExportFreshShowcaseBakeForDiagnostic();
            }
            finally
            {
                if (anchors.IsCreated) anchors.Dispose();
                if (primitives.IsCreated) primitives.Dispose();
                terrace.Dispose();
                street.Dispose();
            }
        }

        private static void ExportFreshShowcaseBakeForDiagnostic()
        {
            string source = Path.Combine(Application.dataPath, "Resources/VoxelShowcase/ShowcaseWorld.bytes");
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/SingleTest"));
            Directory.CreateDirectory(artifactRoot);
            File.Copy(source, Path.Combine(artifactRoot, "ShowcaseWorld.bytes"), true);
        }

        private static int FindGeneratedGroundSurfaceY(
            in FeatureCatalogue catalogue,
            int queryX,
            int queryZ,
            uint terrainSeed,
            NativeList<Primitive> primitives,
            NativeList<ResolvedAnchor> anchors)
        {
            int surfaceY = int.MinValue;

            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                ParameterSet parameters = DefaultParameters(catalogue, definition);

                for (int p = 0; p < rule.ExplicitCount; p++)
                {
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset + p];
                    primitives.Clear();
                    anchors.Clear();

                    EvaluationResult result = ShapeProgram.Evaluate(
                        in catalogue, rule.DefinitionId, in parameters, placement.Position,
                        placement.Orientation, terrainSeed, 0UL, primitives, anchors);
                    Assert.AreEqual(EvaluationResult.Ok, result,
                        $"Terrace definition {rule.DefinitionId} must evaluate successfully.");

                    for (int i = 0; i < primitives.Length; i++)
                    {
                        Primitive primitive = primitives[i];
                        if (primitive.Mode != PrimitiveMode.Fill && primitive.Mode != PrimitiveMode.FillIfEmpty)
                            continue;

                        primitive.Bounds(out int3 min, out int3 max);
                        if (queryX < min.x || queryX > max.x || queryZ < min.z || queryZ > max.z)
                            continue;

                        surfaceY = math.max(surfaceY, max.y);
                    }
                }
            }

            return surfaceY;
        }

        private static ParameterSet DefaultParameters(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition)
        {
            var parameters = new ParameterSet();
            for (int i = 0; i < definition.ParameterCount; i++)
                parameters[i] = catalogue.Parameters[definition.ParameterOffset + i].Default;
            return parameters;
        }

        private static VoxelWorldGenSettings CreateSettings()
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
