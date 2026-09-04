using System;
using System.Reflection;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonCharacterMotorBlockerDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;

        // Exact current-source standalone run 33839531278 repeatedly hard-stopped here while
        // grounded, targeting resolved-89 at (-108.0, 28.0) metres X/Z. Exact-SHA diagnostic
        // 33859073259 proved every reported blocker voxel was road material 13 exactly 0.1m below
        // these nominal feet, exposing lower-face voxel-boundary quantization in CharacterMotor.
        private static readonly Vector3 StallFeet = new Vector3(-104.590f, 45.600f, 28.000f);

        [Test]
        public void UpperApproachRoadSupportFaceDoesNotCountAsCapsuleOverlap()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65_536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            var region = new int3(-3, 0, 0);
            world.GenerateRegionBlocking(region);
            Assert.That(world.IsGenerated(region), Is.True);

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
