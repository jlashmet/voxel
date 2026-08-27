using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeHouseVerticalCirculationPlayModeTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void ProductionGeneratedHousesCoordinateStairsOpeningsAndUpperGuards()
        {
            VoxelWorldGenSettings settings = BuildSettings();
            SettlementPlan plan = SettlementVoxelPlan.Resolve(Seed, in settings);
            FeatureCatalogue catalogue = KentridgeSharedStructureVoxelCatalogue.Build(
                Seed, settings, Allocator.Temp);
            var primitives = new NativeList<Primitive>(Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(Allocator.Temp);

            try
            {
                int multiStoreyCount = 0;
                int singleStoreyCount = 0;

                for (int roleId = 0; roleId < catalogue.Definitions.Length; roleId++)
                {
                    BuildingPlot plot = FindRole(plan, roleId);
                    StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                    StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, Seed);
                    if (!form.IsGenerated)
                        continue;

                    primitives.Clear();
                    anchors.Clear();
                    FeatureDefinition definition = catalogue.Definitions[roleId];
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[roleId];
                    ParameterSet parameters = default;
                    EvaluationResult evaluation = ShapeProgram.Evaluate(
                        in catalogue,
                        roleId,
                        in parameters,
                        placement.Position,
                        placement.Orientation,
                        Seed,
                        FeatureGeneration.InstanceSeed(Seed, roleId, placement.Position),
                        primitives,
                        anchors);

                    Assert.AreEqual(EvaluationResult.Ok, evaluation,
                        ((KentridgeRole)roleId) + " production program must evaluate successfully.");
                    Assert.LessOrEqual(primitives.Length, definition.MaxPrimitives,
                        ((KentridgeRole)roleId) + " vertical circulation must stay within MaxPrimitives.");

                    int scale = settings.VoxelsPerDecimetre;
                    int foundationY = placement.Position.y + plan.Theme.FoundationHeightDm * scale;
                    int floorHeight = plan.Theme.FloorHeightDm * scale;
                    int slabThickness = math.max(1, 3 * scale);
                    int slabMinY = foundationY + floorHeight - slabThickness;
                    int upperFloorY = foundationY + floorHeight;
                    int slabCarves = CollectSlabOpeningBounds(
                        primitives,
                        slabMinY,
                        slabThickness,
                        out int3 openingMin,
                        out int3 openingMax);

                    if (form.Storeys <= 1)
                    {
                        singleStoreyCount++;
                        Assert.AreEqual(0, slabCarves,
                            ((KentridgeRole)roleId) +
                            " is single-storey and must not receive a synthetic circulation opening.");
                        continue;
                    }

                    multiStoreyCount++;
                    Assert.GreaterOrEqual(slabCarves, 2,
                        ((KentridgeRole)roleId) +
                        " must carve the return flights/landing through the intermediate slab.");

                    var stairTopLevels = new HashSet<int>();
                    int upperGuards = 0;
                    for (int i = 0; i < primitives.Length; i++)
                    {
                        Primitive primitive = primitives[i];
                        if (primitive.Shape != PrimitiveShape.Box || primitive.Mode != PrimitiveMode.Fill)
                            continue;

                        primitive.Bounds(out int3 min, out int3 max);
                        if (!HorizontalIntersects(min, max, openingMin, openingMax))
                            continue;

                        if (min.y == foundationY && max.y < upperFloorY)
                            stairTopLevels.Add(max.y);

                        int height = max.y - min.y + 1;
                        int sizeX = max.x - min.x + 1;
                        int sizeZ = max.z - min.z + 1;
                        if (min.y == upperFloorY
                            && height == 9 * scale
                            && (sizeX == scale || sizeZ == scale))
                        {
                            upperGuards++;
                        }
                    }

                    Assert.GreaterOrEqual(stairTopLevels.Count, 5,
                        ((KentridgeRole)roleId) +
                        " must contain a real rising stair sequence beneath the derived opening.");
                    Assert.GreaterOrEqual(upperGuards, 3,
                        ((KentridgeRole)roleId) +
                        " must guard the upper opening while leaving the return-flight egress open.");
                }

                Assert.Greater(multiStoreyCount, 0,
                    "The production Kentridge seed must exercise generated multi-storey houses.");
                Assert.Greater(singleStoreyCount, 0,
                    "The production Kentridge seed must retain a generated single-storey negative case.");
            }
            finally
            {
                if (anchors.IsCreated) anchors.Dispose();
                if (primitives.IsCreated) primitives.Dispose();
                catalogue.Dispose();
            }
        }

        private static int CollectSlabOpeningBounds(
            NativeList<Primitive> primitives,
            int slabMinY,
            int slabThickness,
            out int3 openingMin,
            out int3 openingMax)
        {
            openingMin = new int3(int.MaxValue, int.MaxValue, int.MaxValue);
            openingMax = new int3(int.MinValue, int.MinValue, int.MinValue);
            int count = 0;

            for (int i = 0; i < primitives.Length; i++)
            {
                Primitive primitive = primitives[i];
                if (primitive.Shape != PrimitiveShape.Box || primitive.Mode != PrimitiveMode.Carve)
                    continue;

                primitive.Bounds(out int3 min, out int3 max);
                if (min.y != slabMinY || max.y != slabMinY + slabThickness - 1)
                    continue;

                openingMin = math.min(openingMin, min);
                openingMax = math.max(openingMax, max);
                count++;
            }

            return count;
        }

        private static bool HorizontalIntersects(
            int3 minA,
            int3 maxA,
            int3 minB,
            int3 maxB) =>
            minA.x <= maxB.x && maxA.x >= minB.x
            && minA.z <= maxB.z && maxA.z >= minB.z;

        private static BuildingPlot FindRole(SettlementPlan plan, int roleId)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
                if (plan.Plots[i].RoleId == roleId)
                    return plan.Plots[i];

            Assert.Fail("Kentridge settlement is missing stable role id " + roleId + ".");
            return default;
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
