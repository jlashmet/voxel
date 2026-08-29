using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class UndergroundCavernDestinationCirculationTests
    {
        private const uint Seed = 0x5EED1234u;
        private const float FixedStep = 1f / 60f;
        private const float ArrivalRadiusMetres = 0.75f;
        private const int MaxStepsPerWaypoint = 900;

        [Test]
        public void PostFinishRearApproachRemainsWalkableThroughRuin()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            world.GenerateUndergroundCavernRuinsBlocking();
            float3[] route = world.UndergroundCavernTraversalWaypointsMetres;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Length, Is.GreaterThanOrEqualTo(8));

            // Start before the terminal approach that the rear visual-finish lobe previously
            // refilled, then use the same normal CharacterMotor path as the full acceptance test.
            int startWaypoint = route.Length - 7;
            var motor = new CharacterMotor { WalkSpeed = 5.5f };
            motor.SnapToGround(world, (Vector3)route[startWaypoint]);

            for (int waypoint = startWaypoint + 1; waypoint < route.Length; waypoint++)
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
                    distance = HorizontalDistance(motor.Position, target);
                }

                Assert.That(distance, Is.LessThanOrEqualTo(ArrivalRadiusMetres),
                    $"Post-finish destination circulation stalled at route waypoint {waypoint}/{route.Length - 1} " +
                    $"at {motor.Position}, target {target}, remaining {distance:F2} m.");
            }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
