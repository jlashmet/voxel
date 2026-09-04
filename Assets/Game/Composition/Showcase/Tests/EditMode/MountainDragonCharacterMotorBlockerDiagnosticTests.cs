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
        private const string LogPrefix = "MOUNTAIN_DRAGON_UPPER_APPROACH_MOTOR_BLOCKER=";

        // Exact current-source standalone run 33839531278 repeatedly hard-stopped here while
        // grounded, targeting resolved-89 at (-108.0, 28.0) metres X/Z. The intended X motion is
        // negative; built-player telemetry reported x=voxel:true/wood:false, raised=voxel:false,
        // and raisedX=voxel:true/wood:false. Coordinates in the player log are rounded to 3 decimals,
        // and telemetry did not directly classify the unshifted current AABB.
        private static readonly Vector3 StallFeet = new Vector3(-104.590f, 45.600f, 28.000f);

        [Test]
        public void CurrentProductionUpperApproachCapsuleSerializesBlockingVoxelForCollisionIsolation()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 65_536,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            // The recorded capsule and its 0.3 m step-up remain within this one production region.
            // GenerateRegionBlocking bypasses streaming eviction, so the larger pool is test-only
            // headroom for exact production realization; shipped streaming policy is unchanged.
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

            float radius = ReadFloatField(motorType, motor, "Radius");
            float height = ReadFloatField(motorType, motor, "Height");
            float stepHeight = ReadFloatField(motorType, motor, "StepHeight");
            float probeDistance = ShowcaseWorld.VoxelSize * 0.5f;

            Vector3 current = StallFeet;
            Vector3 xProbe = current + Vector3.left * probeDistance;
            Vector3 raised = current + Vector3.up * stepHeight;
            Vector3 raisedXProbe = xProbe + Vector3.up * stepHeight;

            ProbeResult currentResult = Probe(world, motor, footMin, footMax, isBlocked, current, radius, height);
            ProbeResult xResult = Probe(world, motor, footMin, footMax, isBlocked, xProbe, radius, height);
            ProbeResult raisedResult = Probe(world, motor, footMin, footMax, isBlocked, raised, radius, height);
            ProbeResult raisedXResult = Probe(world, motor, footMin, footMax, isBlocked, raisedXProbe, radius, height);

            // Emit the authoritative cells before assertions so even a rounded-coordinate mismatch
            // remains useful root-cause evidence instead of hiding the blocker behind a test premise.
            var diagnostic = new StringBuilder(4096);
            diagnostic.Append("radius=").Append(radius.ToString("F3"))
                .Append(" height=").Append(height.ToString("F3"))
                .Append(" step=").Append(stepHeight.ToString("F3"))
                .Append(" voxelSize=").Append(ShowcaseWorld.VoxelSize.ToString("F3"));
            AppendProbe(diagnostic, "current", current, currentResult);
            AppendProbe(diagnostic, "x", xProbe, xResult);
            AppendProbe(diagnostic, "raised", raised, raisedResult);
            AppendProbe(diagnostic, "raisedX", raisedXProbe, raisedXResult);
            Debug.Log(LogPrefix + diagnostic);

            Assert.That(xResult.Blocked, Is.True,
                "The production half-voxel negative-X sweep must reproduce the built-player blocker.");
            Assert.That(raisedResult.Blocked, Is.False,
                "The production step-up position must reproduce the built-player clear raised probe.");
            Assert.That(raisedXResult.Blocked, Is.True,
                "The same negative-X sweep must remain blocked after the production 0.3 m step-up.");
            Assert.That(xResult.OccupiedVoxelCount, Is.GreaterThan(0),
                "Built-player telemetry rejected vegetation; the focused repro must identify the authoritative voxel blocker.");
            Assert.That(raisedXResult.OccupiedVoxelCount, Is.GreaterThan(0),
                "Raised negative-X sweep must identify the authoritative voxel blocker rather than only semantic vegetation.");
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

        private static ProbeResult Probe(
            ShowcaseWorld world,
            object motor,
            MethodInfo footMin,
            MethodInfo footMax,
            MethodInfo isBlocked,
            Vector3 feet,
            float radius,
            float height)
        {
            var min = (Vector3)footMin.Invoke(motor, new object[] { feet });
            var max = (Vector3)footMax.Invoke(motor, new object[] { feet, height });
            bool blocked = (bool)isBlocked.Invoke(null, new object[] { world, min, max });

            Assert.That(min.x, Is.EqualTo(feet.x - radius).Within(1e-5f));
            Assert.That(min.z, Is.EqualTo(feet.z - radius).Within(1e-5f));
            Assert.That(max.x, Is.EqualTo(feet.x + radius).Within(1e-5f));
            Assert.That(max.z, Is.EqualTo(feet.z + radius).Within(1e-5f));

            int minX = Mathf.FloorToInt(min.x / ShowcaseWorld.VoxelSize);
            int minY = Mathf.FloorToInt(min.y / ShowcaseWorld.VoxelSize);
            int minZ = Mathf.FloorToInt(min.z / ShowcaseWorld.VoxelSize);
            int maxX = Mathf.FloorToInt((max.x - 1e-4f) / ShowcaseWorld.VoxelSize);
            int maxY = Mathf.FloorToInt((max.y - 1e-4f) / ShowcaseWorld.VoxelSize);
            int maxZ = Mathf.FloorToInt((max.z - 1e-4f) / ShowcaseWorld.VoxelSize);

            int occupied = 0;
            var cells = new StringBuilder(512);
            IVoxelSurfaceQuery query = world.SurfaceQuery;
            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!query.TryRead(new int3(x, y, z), out VoxelCell cell)
                    || cell.BaseMaterialId == VoxelGrid.MaterialEmpty)
                    continue;

                occupied++;
                if (occupied <= 32)
                {
                    if (cells.Length > 0) cells.Append('|');
                    cells.Append(x).Append(',').Append(y).Append(',').Append(z)
                        .Append(":m").Append(cell.BaseMaterialId)
                        .Append(":dy=").Append((y * ShowcaseWorld.VoxelSize - feet.y).ToString("F3"));
                }
            }

            return new ProbeResult(blocked, minX, minY, minZ, maxX, maxY, maxZ, occupied, cells.ToString());
        }

        private static void AppendProbe(StringBuilder output, string label, Vector3 feet, ProbeResult result)
        {
            output.Append(' ').Append(label)
                .Append("Feet=")
                .Append(feet.x.ToString("F3")).Append(',')
                .Append(feet.y.ToString("F3")).Append(',')
                .Append(feet.z.ToString("F3"))
                .Append(" blocked=").Append(result.Blocked)
                .Append(" bounds=")
                .Append(result.MinX).Append(',').Append(result.MinY).Append(',').Append(result.MinZ)
                .Append("..").Append(result.MaxX).Append(',').Append(result.MaxY).Append(',').Append(result.MaxZ)
                .Append(" occupied=").Append(result.OccupiedVoxelCount)
                .Append(" cells=[").Append(result.Cells).Append(']');
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

        private readonly struct ProbeResult
        {
            public ProbeResult(
                bool blocked,
                int minX,
                int minY,
                int minZ,
                int maxX,
                int maxY,
                int maxZ,
                int occupiedVoxelCount,
                string cells)
            {
                Blocked = blocked;
                MinX = minX;
                MinY = minY;
                MinZ = minZ;
                MaxX = maxX;
                MaxY = maxY;
                MaxZ = maxZ;
                OccupiedVoxelCount = occupiedVoxelCount;
                Cells = cells;
            }

            public bool Blocked { get; }
            public int MinX { get; }
            public int MinY { get; }
            public int MinZ { get; }
            public int MaxX { get; }
            public int MaxY { get; }
            public int MaxZ { get; }
            public int OccupiedVoxelCount { get; }
            public string Cells { get; }
        }
    }
}
