using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

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
            NativeList<ShapeCommand> shapes = new NativeList<ShapeCommand>(Allocator.Temp);
            NativeList<ShapeCommand> terrainShapes = new NativeList<ShapeCommand>(Allocator.Temp);

            try
            {
                int lampDefinition = -1;
                int lampPlacement = -1;
                for (int d = 0; d < street.Definitions.Length; d++)
                {
                    FeatureDefinition definition = street.Definitions[d];
                    for (int p = 0; p < definition.PlacementCount; p++)
                    {
                        int index = definition.PlacementStart + p;
                        ExplicitPlacement placement = street.ExplicitPlacements[index];
                        if (placement.Position.x == 1530 && placement.Position.z == 549)
                        {
                            lampDefinition = d;
                            lampPlacement = index;
                            break;
                        }
                    }
                    if (lampPlacement >= 0)
                        break;
                }

                Assert.That(lampDefinition, Is.GreaterThanOrEqualTo(0), "Captured east-market lamp definition not found.");
                Assert.That(lampPlacement, Is.GreaterThanOrEqualTo(0), "Captured east-market lamp placement not found.");

                ExplicitPlacement capturedPlacement = street.ExplicitPlacements[lampPlacement];
                FeatureDefinition capturedDefinition = street.Definitions[lampDefinition];
                Assert.That(capturedDefinition.ProgramIndex, Is.GreaterThanOrEqualTo(0));

                int macroSurfaceY = KentridgeVerticalProfile.SurfaceYAtDm(1530, 549, seed, settings.VoxelsPerDecimetre);
                int generatedGroundSurfaceY = int.MinValue;
                int3 terraceQuery = new int3(1530, 0, 549);
                for (int d = 0; d < terrace.Definitions.Length; d++)
                {
                    FeatureDefinition definition = terrace.Definitions[d];
                    for (int p = 0; p < definition.PlacementCount; p++)
                    {
                        ExplicitPlacement placement = terrace.ExplicitPlacements[definition.PlacementStart + p];
                        VoxelBounds bounds = definition.LocalBounds.Translate(placement.Position);
                        if (terraceQuery.x < bounds.Min.x || terraceQuery.x > bounds.Max.x ||
                            terraceQuery.z < bounds.Min.z || terraceQuery.z > bounds.Max.z)
                            continue;

                        terrainShapes.Clear();
                        ShapeProgramEvaluator.Evaluate(terrace.Programs[definition.ProgramIndex], terrainShapes);
                        for (int s = 0; s < terrainShapes.Length; s++)
                        {
                            ShapeCommand command = terrainShapes[s];
                            VoxelBounds commandBounds = command.Bounds.Translate(placement.Position);
                            if (terraceQuery.x < commandBounds.Min.x || terraceQuery.x > commandBounds.Max.x ||
                                terraceQuery.z < commandBounds.Min.z || terraceQuery.z > commandBounds.Max.z)
                                continue;
                            generatedGroundSurfaceY = math.max(generatedGroundSurfaceY, commandBounds.Max.y);
                        }
                    }
                }

                Assert.That(generatedGroundSurfaceY, Is.GreaterThan(int.MinValue), "No production terrace solid owns the captured lamp column.");
                Assert.AreNotEqual(macroSurfaceY, generatedGroundSurfaceY + 1,
                    "The capture must keep discriminating district ground from the macro elevation.");
                Assert.AreEqual(generatedGroundSurfaceY + 1, capturedPlacement.Position.y,
                    "Captured lamp origin must remain the first empty voxel above the working-yard terrace solid.");

                shapes.Clear();
                ShapeProgramEvaluator.Evaluate(street.Programs[capturedDefinition.ProgramIndex], shapes);
                ShapeCommand? foot = null;
                ShapeCommand? pole = null;
                ShapeCommand? lantern = null;
                for (int i = 0; i < shapes.Length; i++)
                {
                    ShapeCommand command = shapes[i];
                    command.Bounds(out int3 min, out int3 max);
                    if (command.SurfaceStyle != SurfaceStyles.Planar)
                        continue;
                    if (command.Material == settings.Materials.Stone && min.y < 0 && max.y == 3)
                        foot = command;
                    else if (command.Material == settings.Materials.DarkStone && min.y == 4 && max.y >= 30)
                        pole = command;
                    else if (command.Material == settings.Materials.Glow && min.y >= 30)
                        lantern = command;
                }

                Assert.That(foot.HasValue, "Expected embedded Planar stone lamp foot.");
                Assert.That(pole.HasValue, "Expected Planar dark-stone lamp support.");
                Assert.That(lantern.HasValue, "Expected Planar glow lantern head.");

                foot.Value.Bounds(out int3 footMin, out int3 footMax);
                pole.Value.Bounds(out int3 poleMin, out int3 poleMax);
                lantern.Value.Bounds(out int3 lanternMin, out int3 lanternMax);
                Assert.AreEqual(generatedGroundSurfaceY - 1, capturedPlacement.Position.y + footMin.y,
                    "Lamp foot must overlap the terrace top by one voxel so Smooth reconstruction cannot expose a seam.");
                Assert.AreEqual(capturedPlacement.Position.y + 3, capturedPlacement.Position.y + footMax.y,
                    "Embedding the foot must preserve its original top plane.");
                Assert.LessOrEqual(footMax.y + 1, poleMin.y,
                    "Foot must meet the pole without moving upper lamp geometry.");
                Assert.LessOrEqual(poleMax.y, lanternMin.y,
                    "Planar support must remain continuous under the lantern.");
            }
            finally
            {
                if (terrainShapes.IsCreated) terrainShapes.Dispose();
                if (shapes.IsCreated) shapes.Dispose();
                terrace.Dispose();
                street.Dispose();
            }
        }

        private static VoxelWorldGenSettings CreateSettings()
        {
            VoxelMaterialIds materials = new VoxelMaterialIds(
                air: 0, dirt: 1, grass: 2, stone: 3, wood: 4, leaf: 5, darkStone: 6,
                roofTile: 8, slate: 7, cloth: 9, moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }
    }
}