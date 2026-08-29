using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

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

            Assert.That(world.HasUndergroundCavernRuins, Is.True);
            Assert.That(world.UndergroundCavernTraversalDistance, Is.GreaterThanOrEqualTo(2400),
                "The destination must remain a prolonged traversal from the surface mouth.");
            Assert.That(world.UndergroundCavernMouthOpeningCount, Is.GreaterThanOrEqualTo(4),
                "The production path must author a multi-lobed natural mouth, not only the rectangular core entrance.");
            Assert.That(world.UndergroundCavernDirectionChangeCount, Is.GreaterThanOrEqualTo(4),
                "The production descent must force multiple lateral direction changes.");
            Assert.That(world.UndergroundCavernStatueCount, Is.EqualTo(2));
            Assert.That(world.UndergroundCavernStalactiteCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(world.UndergroundCavernGeologicalCategoryCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(world.UndergroundCavernRouteLightCount, Is.EqualTo(6),
                "Supported local lights must recur along the prolonged descent rather than appearing only at doglegs.");
            Assert.That(world.UndergroundCavernLocalLightCount, Is.InRange(7, 8),
                "Six route lights plus one or two destination lights must remain inside the existing eight-light ceiling.");
            Assert.That(world.UndergroundCavernPreloadedRegionCount, Is.InRange(1, 128),
                "The deep route/cavern preload must remain a bounded region workload.");
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.InRange(1L, 55_000_000L),
                "The feature must stay inside the existing production authoring budget.");

            float3 delta = world.UndergroundCavernCentreMetres - world.UndergroundCavernEntranceMetres;
            Assert.That(math.length(new float2(delta.x, delta.z)), Is.GreaterThan(250f));
            Assert.That(delta.y, Is.LessThan(-70f),
                "The cavern must remain substantially below the natural surface entrance.");

            int motorSteps = WalkProductionRoute(world);

            TestContext.WriteLine(
                $"cavern writes={world.UndergroundCavernVoxelsWritten}; preloadRegions={world.UndergroundCavernPreloadedRegionCount}; " +
                $"traversal={world.UndergroundCavernTraversalDistance}; routeWaypoints={world.UndergroundCavernTraversalWaypointsMetres.Length}; " +
                $"motorSteps={motorSteps}; routeLights={world.UndergroundCavernRouteLightCount}; " +
                $"totalLights={world.UndergroundCavernLocalLightCount}; mouthLobes={world.UndergroundCavernMouthOpeningCount}; " +
                $"directionChanges={world.UndergroundCavernDirectionChangeCount}; statues={world.UndergroundCavernStatueCount}; " +
                $"stalactites={world.UndergroundCavernStalactiteCount}; geologyCategories={world.UndergroundCavernGeologicalCategoryCount}; " +
                $"depthDeltaMetres={delta.y:F1}");

            int lights = world.UndergroundCavernLocalLightCount;
            long writes = world.UndergroundCavernVoxelsWritten;
            float3 centre = world.UndergroundCavernCentreMetres;
            world.GenerateUndergroundCavernRuinsBlocking();
            Assert.That(world.UndergroundCavernLocalLightCount, Is.EqualTo(lights));
            Assert.That(world.UndergroundCavernVoxelsWritten, Is.EqualTo(writes));
            Assert.That(math.all(world.UndergroundCavernCentreMetres == centre), Is.True,
                "The production entry point must remain idempotent for runtime/offline restoration.");
        }

        private static int WalkProductionRoute(ShowcaseWorld world)
        {
            float3[] route = world.UndergroundCavernTraversalWaypointsMetres;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Length, Is.GreaterThanOrEqualTo(25),
                "Production authoring must expose the forced bends plus cavern/ruin approach as one route.");

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
                    motor.Step(world, wish, sprint: true, jumpHeld: false, FixedStep);
                    steps++;
                    totalSteps++;
                    distance = HorizontalDistance(motor.Position, target);
                }

                Assert.That(distance, Is.LessThanOrEqualTo(ArrivalRadiusMetres),
                    $"Normal CharacterMotor traversal stalled before cavern waypoint {waypoint}/{route.Length - 1} " +
                    $"at {motor.Position}, target {target}, remaining {distance:F2} m.");
            }

            Assert.That(motor.Position.y,
                Is.LessThan(((Vector3)route[0]).y - 60f),
                "Normal player traversal must finish substantially below the surface entrance.");
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
