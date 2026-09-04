using System;
using System.Reflection;
using System.Text;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonCharacterMotorBlockerDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;
        private const float CollisionBoundaryEpsilon = 1e-4f;

        // Exact current-source standalone runs repeatedly hard-stop here while grounded, targeting
        // resolved-89 at (-108.0, 28.0) metres X/Z. Run 33859073259 proved the earlier nominal-feet
        // overlap was road material 13; post-merge run 33868687506 still stops at the same place, so
        // the next discriminator reproduces the actual raised negative-X step probe before any fix.
        private static readonly Vector3 StallFeet = new Vector3(-104.590f, 45.600f, 28.000f);

        [Test]
        public void UpperApproachRoadSupportFaceDoesNotCountAsCapsuleOverlap()
        {
            using var world = CreateRealizedWorld();

            Type motorType = Type.GetType(
                "VoxelEngine.Showcase.CharacterMotor, VoxelEngine.Showcase",
                throwOnError: true);
            MethodInfo footMin = RequirePrivateInstance(motorType, "FootMin");
            MethodInfo footMax = RequirePrivateInstance(motorType, "FootMax");
            MethodInfo isBlocked = RequirePrivateStatic(motorType, "IsBlocked");
            object motor = Activator.CreateInstance(motorType);

            float height = ReadFloatField(motorType, motor, "Height");
            float stepHeight = ReadFloatField(motorType, motor, "StepHeight");

            Vector3 current = StallFeet;
            Vector3 raised = current + Vector3.up * stepHeight;
            Vector3 overlappingSupport = current + Vector3.down * 0.01f;

            Assert.That(IsBlockedAt(world, motor, footMin, footMax, isBlocked, current, height), Is.False,
                "Standing exactly on the road support face must not count the support voxel as capsule overlap.");
            Assert.That(IsBlockedAt(world, motor, footMin, footMax, isBlocked, raised, height), Is.False,
                "The normal 0.3m step-up position must remain free.");
            Assert.That(IsBlockedAt(world, motor, footMin, footMax, isBlocked, overlappingSupport, height), Is.True,
                "Moving the lower capsule face 1cm into the same authoritative support must still collide.");

            Vector3 groundMin = (Vector3)footMin.Invoke(motor, new object[] { current })
                + new Vector3(0f, -0.02f, 0f);
            Vector3 groundMax = (Vector3)footMax.Invoke(motor, new object[] { current, 0.02f });
            bool groundProbeBlocked = (bool)isBlocked.Invoke(null, new object[] { world, groundMin, groundMax });
            Assert.That(groundProbeBlocked, Is.True,
                "CharacterMotor's deliberate 2cm downward ground-contact probe must still overlap the support road.");
        }

        [Test]
        public void UpperApproachRaisedNegativeXSweepSerializesProductionBlocker()
        {
            using var world = CreateRealizedWorld();

            Type motorType = Type.GetType(
                "VoxelEngine.Showcase.CharacterMotor, VoxelEngine.Showcase",
                throwOnError: true);
            MethodInfo footMin = RequirePrivateInstance(motorType, "FootMin");
            MethodInfo footMax = RequirePrivateInstance(motorType, "FootMax");
            MethodInfo isBlocked = RequirePrivateStatic(motorType, "IsBlocked");
            object motor = Activator.CreateInstance(motorType);

            float height = ReadFloatField(motorType, motor, "Height");
            float radius = ReadFloatField(motorType, motor, "Radius");
            float stepHeight = ReadFloatField(motorType, motor, "StepHeight");
            float halfVoxel = ShowcaseWorld.VoxelSize * 0.5f;

            Vector3 raisedX = StallFeet + Vector3.up * stepHeight + Vector3.left * halfVoxel;
            Vector3 min = (Vector3)footMin.Invoke(motor, new object[] { raisedX });
            Vector3 max = (Vector3)footMax.Invoke(motor, new object[] { raisedX, height });
            bool productionBlocked = (bool)isBlocked.Invoke(null, new object[] { world, min, max });

            int minX = Mathf.FloorToInt((min.x + CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);
            int minY = Mathf.FloorToInt((min.y + CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);
            int minZ = Mathf.FloorToInt((min.z + CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);
            int maxX = Mathf.FloorToInt((max.x - CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);
            int maxY = Mathf.FloorToInt((max.y - CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);
            int maxZ = Mathf.FloorToInt((max.z - CollisionBoundaryEpsilon) / ShowcaseWorld.VoxelSize);

            IVoxelSurfaceQuery surface = world.SurfaceQuery;
            var occupied = new StringBuilder(1024);
            int occupiedCount = 0;
            int lowestOccupiedY = int.MaxValue;
            int highestOccupiedY = int.MinValue;
            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!surface.TryRead(new int3(x, y, z), out VoxelCell cell) ||
                    cell.BaseMaterialId == VoxelGrid.MaterialEmpty)
                    continue;

                if (occupiedCount > 0) occupied.Append(';');
                occupied.Append(x).Append(',').Append(y).Append(',').Append(z)
                    .Append(":m").Append(cell.BaseMaterialId);
                occupiedCount++;
                lowestOccupiedY = Math.Min(lowestOccupiedY, y);
                highestOccupiedY = Math.Max(highestOccupiedY, y);
            }

            var footprint = new StringBuilder(1024);
            int footprintIndex = 0;
            int minSurfaceY = int.MaxValue;
            int maxSurfaceY = int.MinValue;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int top = world.OccupiedSurfaceHeight(x, z);
                if (footprintIndex++ > 0) footprint.Append(';');
                footprint.Append(x).Append(',').Append(z).Append(':').Append(top);
                minSurfaceY = Math.Min(minSurfaceY, top);
                maxSurfaceY = Math.Max(maxSurfaceY, top);
            }

            Debug.Log(
                "MOUNTAIN_DRAGON_RAISED_X_BLOCKER="
                + $"feet={raisedX.x:0.000000},{raisedX.y:0.000000},{raisedX.z:0.000000} "
                + $"radius={radius:0.000} bounds=[{minX},{minY},{minZ}..{maxX},{maxY},{maxZ}] "
                + $"blocked={productionBlocked} occupied={occupiedCount} "
                + $"occupiedY={lowestOccupiedY}..{highestOccupiedY} cells=[{occupied}] "
                + $"surfaceY={minSurfaceY}..{maxSurfaceY} footprint=[{footprint}]");

            Assert.That(productionBlocked, Is.True,
                "The diagnostic must reproduce the exact production raised negative-X blocker before another correction.");
            Assert.That(occupiedCount, Is.GreaterThan(0),
                "The production block must have at least one authoritative voxel inside the exact collision bounds.");
        }

        [Test]
        public void SummitArrivalStaysOutsideDragonPlaceholderWhileRemainingSupportedOnCrest()
        {
            MountainLandformSurface surface = ShowcaseMountainDragonLayout.CreateSurface(Seed);
            WorldRoadNetwork ascent = ShowcaseMountainDragonLayout.CreateAscentNetwork(Seed, surface);
            Assert.That(ascent.TryGetRoute(
                ShowcaseMountainDragonLayout.AscentRouteId,
                out WorldRoadNetworkRoute route), Is.True);
            Assert.That(route.Road.IsResolved, Is.True, route.Road.FailureReason);

            MountainLandformMass summit = surface.GetMass(0);
            ResolvedWorldRoadPoint arrival = ShowcaseMountainDragonLayout.SummitApproach(ascent);
            int dx = Math.Abs(arrival.Xdm - summit.CentreXdm);
            int dz = Math.Abs(arrival.Zdm - summit.CentreZdm);
            int placeholderHalf = ShowcaseMountainDragonLayout.PlaceholderSize / 2;
            int placeholderClearance = Math.Max(dx - placeholderHalf, dz - placeholderHalf);

            Assert.That(
                placeholderClearance,
                Is.GreaterThanOrEqualTo(ShowcaseMountainDragonLayout.PathWidth / 2),
                "The production route must finish beside the solid dragon placeholder instead of driving the player capsule into it.");

            int supportedRadius = ShowcaseMountainDragonLayout.SummitRadius
                - ShowcaseMountainDragonLayout.PathWidth / 2;
            long radialDistanceSquared = (long)dx * dx + (long)dz * dz;
            Assert.That(
                radialDistanceSquared,
                Is.LessThanOrEqualTo((long)supportedRadius * supportedRadius),
                "The terminal road centreline plus half its width must remain on the broad summit crest.");
        }

        private static ShowcaseWorld CreateRealizedWorld()
        {
            var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65_536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);
            var region = new int3(-3, 0, 0);
            world.GenerateRegionBlocking(region);
            Assert.That(world.IsGenerated(region), Is.True);
            return world;
        }

        private static bool IsBlockedAt(
            ShowcaseWorld world,
            object motor,
            MethodInfo footMin,
            MethodInfo footMax,
            MethodInfo isBlocked,
            Vector3 feet,
            float height)
        {
            Vector3 min = (Vector3)footMin.Invoke(motor, new object[] { feet });
            Vector3 max = (Vector3)footMax.Invoke(motor, new object[] { feet, height });
            return (bool)isBlocked.Invoke(null, new object[] { world, min, max });
        }

        private static MethodInfo RequirePrivateInstance(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, $"Missing production CharacterMotor.{name} capsule seam.");
            return method;
        }

        private static MethodInfo RequirePrivateStatic(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Missing production CharacterMotor.{name} collision seam.");
            return method;
        }

        private static float ReadFloatField(Type type, object instance, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Missing CharacterMotor.{name} field.");
            return (float)field.GetValue(instance);
        }
    }
}
