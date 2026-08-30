using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class UndergroundCavernRuinProductionAcceptanceTests
    {
        private const uint Seed = 0x5EED1234u;
        private const float FixedStep = 1f / 60f;
        private const float ArrivalRadiusMetres = 0.75f;
        private const int MaxStepsPerWaypoint = 900;

        [Test]
        public void ProductionShowcaseWorldAuthorsTraversalCavernWithinBudgets()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            world.GenerateUndergroundCavernRuinsBlocking();
            AssertRoundedDestinationCirculationPlan();
            AssertIrregularRouteNaturalizationPlan();

            Assert.That(world.HasUndergroundCavernRuins, Is.True);
            Assert.That(world.UndergroundCavernRockMaterialId, Is.EqualTo(GameMaterialIds.DarkStone),
                "Natural cave rock must use the dark smooth triplanar material; bright build Stone failed the built-player cavern presentation gate.");
            Assert.That(world.UndergroundCavernTraversalDistance, Is.GreaterThanOrEqualTo(2400));
            Assert.That(world.UndergroundCavernMouthOpeningCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(world.UndergroundCavernDirectionChangeCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(world.UndergroundCavernNaturalizationNodeCount, Is.GreaterThanOrEqualTo(150));
            Assert.That(world.UndergroundCavernNaturalizationVoxelsWritten, Is.InRange(1L, 15_000_000L));
            Assert.That(world.UndergroundCavernStatueCount, Is.EqualTo(2));
            Assert.That(world.UndergroundCavernStalactiteCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(world.UndergroundCavernGeologicalCategoryCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(world.UndergroundCavernIrregularLobeCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(world.UndergroundCavernArchitectureDetailCount, Is.GreaterThanOrEqualTo(12));
            Assert.That(world.UndergroundCavernStatueDetailCount, Is.GreaterThanOrEqualTo(20));
            Assert.That(world.UndergroundCavernAdditionalFormationCount, Is.GreaterThanOrEqualTo(6));
            Assert.That(world.UndergroundCavernVisualFinishVoxelsWritten, Is.InRange(1L, 20_000_000L));
            Assert.That(world.UndergroundCavernRouteLightCount, Is.EqualTo(6));
            Assert.That(world.UndergroundCavernLocalLightCount, Is.InRange(7, 8));
            Assert.That(world.UndergroundCavernPreloadedRegionCount, Is.InRange(1, 128));
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.InRange(1L, 55_000_000L));

            float3 delta = world.UndergroundCavernCentreMetres - world.UndergroundCavernEntranceMetres;
            Assert.That(math.length(new float2(delta.x, delta.z)), Is.GreaterThan(250f));
            Assert.That(delta.y, Is.LessThan(-70f));

            float ruinDistance = HorizontalDistance((Vector3)world.UndergroundCavernCentreMetres, (Vector3)world.UndergroundCavernRuinCentreMetres);
            float approachDistance = HorizontalDistance((Vector3)world.UndergroundCavernRuinApproachMetres, (Vector3)world.UndergroundCavernRuinCentreMetres);
            Assert.That(ruinDistance, Is.GreaterThanOrEqualTo(10f));
            Assert.That(approachDistance, Is.GreaterThanOrEqualTo(6f));
            Assert.That(approachDistance, Is.LessThan(ruinDistance));

            float3[] route = world.UndergroundCavernTraversalWaypointsMetres;
            Assert.That(math.distance(route[route.Length - 1], world.UndergroundCavernRuinApproachMetres), Is.LessThan(0.01f));
            int motorSteps = WalkProductionRoute(world);

            TestContext.WriteLine(
                $"cavern writes={world.UndergroundCavernVoxelsWritten}; naturalizationWrites={world.UndergroundCavernNaturalizationVoxelsWritten}; " +
                $"naturalizationNodes={world.UndergroundCavernNaturalizationNodeCount}; visualFinishWrites={world.UndergroundCavernVisualFinishVoxelsWritten}; " +
                $"preloadRegions={world.UndergroundCavernPreloadedRegionCount}; traversal={world.UndergroundCavernTraversalDistance}; " +
                $"routeWaypoints={world.UndergroundCavernTraversalWaypointsMetres.Length}; motorSteps={motorSteps}; " +
                $"routeLights={world.UndergroundCavernRouteLightCount}; totalLights={world.UndergroundCavernLocalLightCount}; " +
                $"mouthLobes={world.UndergroundCavernMouthOpeningCount}; directionChanges={world.UndergroundCavernDirectionChangeCount}; " +
                $"cavernLobes={world.UndergroundCavernIrregularLobeCount}; architectureDetails={world.UndergroundCavernArchitectureDetailCount}; " +
                $"statueDetails={world.UndergroundCavernStatueDetailCount}; largeFormations={world.UndergroundCavernAdditionalFormationCount}; " +
                $"statues={world.UndergroundCavernStatueCount}; stalactites={world.UndergroundCavernStalactiteCount}; " +
                $"geologyCategories={world.UndergroundCavernGeologicalCategoryCount}; caveRockMaterial={world.UndergroundCavernRockMaterialId}; depthDeltaMetres={delta.y:F1}; " +
                $"ruinDistanceMetres={ruinDistance:F1}; approachDistanceMetres={approachDistance:F1}");

            int lights = world.UndergroundCavernLocalLightCount;
            long writes = world.UndergroundCavernVoxelsWritten;
            float3 centre = world.UndergroundCavernCentreMetres;
            world.GenerateUndergroundCavernRuinsBlocking();
            Assert.That(world.UndergroundCavernLocalLightCount, Is.EqualTo(lights));
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.EqualTo(writes));
            Assert.That(math.all(world.UndergroundCavernCentreMetres == centre), Is.True);
        }

        [Test]
        public void DestinationCirculationPlanUsesOverlappingRoundedNodes()
        {
            AssertRoundedDestinationCirculationPlan();
        }

        [Test]
        public void RouteNaturalizationPlanUsesDeterministicNonPeriodicLobes()
        {
            AssertIrregularRouteNaturalizationPlan();
        }

        private static void AssertRoundedDestinationCirculationPlan()
        {
            var cavern = new DecorationBounds { Min = new int3(-160, -700, -150), MaxExclusive = new int3(161, -520, 151) };
            var ruin = new DecorationBounds { Min = new int3(96, -700, -58), MaxExclusive = new int3(172, -638, 59) };
            UndergroundCavernCirculationPlan plan = UndergroundCavernCirculationProtection.ResolvePlan(in cavern, in ruin, Facing.East, 20, 32);
            UndergroundCavernCirculationPlan repeated = UndergroundCavernCirculationProtection.ResolvePlan(in cavern, in ruin, Facing.East, 20, 32);

            Assert.That(plan.IsWellFormed, Is.True);
            Assert.That(plan.Radius, Is.GreaterThan(10));
            Assert.That(plan.Spacing, Is.LessThan(plan.Radius));
            Assert.That(plan.NodeCount, Is.GreaterThanOrEqualTo(20));
            Assert.That(plan.Start.x, Is.LessThan(cavern.Min.x));
            Assert.That(plan.End.x, Is.GreaterThan(ruin.Min.x));
            Assert.That(plan.Start.z, Is.EqualTo(plan.End.z));
            Assert.That(4 * plan.Radius * plan.Radius - plan.Spacing * plan.Spacing, Is.GreaterThanOrEqualTo(20 * 20));
            Assert.That(repeated.Start, Is.EqualTo(plan.Start));
            Assert.That(repeated.End, Is.EqualTo(plan.End));
            Assert.That(repeated.NodeCount, Is.EqualTo(plan.NodeCount));
            Assert.That(repeated.Radius, Is.EqualTo(plan.Radius));
            Assert.That(repeated.Spacing, Is.EqualTo(plan.Spacing));
        }

        private static void AssertIrregularRouteNaturalizationPlan()
        {
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x564F584341564552ul,
                Seed,
                new int3(-3600, 180, -378),
                Facing.East,
                28,
                32,
                12);
            CaveConfig cave = CaveConfig.Default;
            cave.TunnelWidth = 28;
            cave.TunnelHeight = 32;
            cave.SegmentLength = 56;
            cave.MainSegmentCount = 58;
            cave.TurnChancePercent = 0;
            cave.VerticalChancePercent = 0;
            cave.MaxVerticalStepPerSegment = 0;
            cave.SurfaceDescentSegments = 52;
            cave.SurfaceDescentPerSegment = 18;
            cave.MinimumSurfaceCover = 18;
            cave.BranchChancePercent = 0;
            cave.MaxBranches = 0;
            cave.MaxBranchDepth = 0;
            cave.ChamberChancePercent = 12;
            cave.MinChamberRadius = 18;
            cave.MaxChamberRadius = 30;
            cave.MinChamberHeight = 34;
            cave.MaxChamberHeight = 48;
            cave.FloorRoughness = 2;
            cave.CeilingRoughness = 4;
            cave.WallRoughness = 3;
            cave.BoundsHalfExtents = new int3(3400, 1120, 320);
            cave.MinVerticalOffset = -1000;
            cave.MaxVerticalOffset = 24;
            UndergroundCavernTraversalProfile profile = UndergroundCavernTraversalProfile.LongDescent;

            UndergroundCavernNaturalizationNode[] first =
                UndergroundCavernRouteNaturalization.ResolvePlan(in request, in cave, in profile);
            UndergroundCavernNaturalizationNode[] repeated =
                UndergroundCavernRouteNaturalization.ResolvePlan(in request, in cave, in profile);

            Assert.That(first.Length, Is.GreaterThanOrEqualTo(150));
            Assert.That(repeated.Length, Is.EqualTo(first.Length));
            var spacingSeen = new bool[65];
            var ceilingSeen = new bool[65];
            int sideSwitches = 0;
            int previousSide = 0;

            for (int i = 0; i < first.Length; i++)
            {
                UndergroundCavernNaturalizationNode node = first[i];
                UndergroundCavernNaturalizationNode again = repeated[i];
                Assert.That(node.IsWellFormed, Is.True);
                Assert.That(node.PrimaryRadius, Is.GreaterThanOrEqualTo(cave.TunnelWidth / 2 + 2));
                Assert.That(node.StepToNext, Is.LessThanOrEqualTo(node.PrimaryRadius * 2));
                Assert.That(node.Distance, Is.EqualTo(again.Distance));
                Assert.That(node.StepToNext, Is.EqualTo(again.StepToNext));
                Assert.That(node.PrimaryRadius, Is.EqualTo(again.PrimaryRadius));
                Assert.That(node.PrimaryHeight, Is.EqualTo(again.PrimaryHeight));
                Assert.That(node.DominantSide, Is.EqualTo(again.DominantSide));
                Assert.That(node.SideOffset, Is.EqualTo(again.SideOffset));
                Assert.That(node.SideBaseOffset, Is.EqualTo(again.SideBaseOffset));
                Assert.That(node.SideRadius, Is.EqualTo(again.SideRadius));
                Assert.That(node.SideHeight, Is.EqualTo(again.SideHeight));
                Assert.That(node.UpperOffset, Is.EqualTo(again.UpperOffset));
                Assert.That(node.UpperBaseOffset, Is.EqualTo(again.UpperBaseOffset));
                Assert.That(node.UpperRadius, Is.EqualTo(again.UpperRadius));
                Assert.That(node.UpperHeight, Is.EqualTo(again.UpperHeight));

                spacingSeen[math.clamp(node.StepToNext, 0, spacingSeen.Length - 1)] = true;
                ceilingSeen[math.clamp(
                    node.UpperBaseOffset + node.UpperHeight - cave.TunnelHeight,
                    0,
                    ceilingSeen.Length - 1)] = true;
                if (previousSide != 0 && previousSide != node.DominantSide)
                    sideSwitches++;
                previousSide = node.DominantSide;
            }

            Assert.That(CountTrue(spacingSeen), Is.GreaterThanOrEqualTo(3));
            Assert.That(CountTrue(ceilingSeen), Is.GreaterThanOrEqualTo(4));
            Assert.That(sideSwitches, Is.GreaterThanOrEqualTo(4));
        }

        private static int CountTrue(bool[] values)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
                if (values[i]) count++;
            return count;
        }

        private static int WalkProductionRoute(ShowcaseWorld world)
        {
            float3[] route = world.UndergroundCavernTraversalWaypointsMetres;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Length, Is.GreaterThanOrEqualTo(25));
            var motor = new CharacterMotor { WalkSpeed = 5.5f };
            motor.SnapToGround(world, (Vector3)route[0]);

            int totalSteps = 0;
            for (int waypoint = 1; waypoint < route.Length; waypoint++)
            {
                Vector3 target = (Vector3)route[waypoint];
                int steps = 0;
                float distance = HorizontalDistance(motor.Position, target);
                while (distance > ArrivalRadiusMetres && steps < MaxStepsPerWaypoint)
                {
                    Vector3 wish = target - motor.Position;
                    wish.y = 0f;
                    if (wish.sqrMagnitude > 1e-6f) wish.Normalize();
                    motor.Step(world, wish, true, false, FixedStep);
                    steps++;
                    totalSteps++;
                    distance = HorizontalDistance(motor.Position, target);
                }
                Assert.That(distance, Is.LessThanOrEqualTo(ArrivalRadiusMetres),
                    $"Normal CharacterMotor traversal stalled before cavern waypoint {waypoint}/{route.Length - 1} at {motor.Position}, target {target}, remaining {distance:F2} m.");
            }
            Assert.That(motor.Position.y, Is.LessThan(((Vector3)route[0]).y - 60f));
            return totalSteps;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
