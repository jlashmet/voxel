using System;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class MountainDragonCharacterMotorBlockerDiagnosticTests
    {
        private const uint Seed = 0x5EED1234;
        private const string LogPrefix = "MOUNTAIN_DRAGON_CHARACTER_MOTOR_BLOCKER=";

        // Fresh built-player run 33831759558 repeatedly hard-stopped here while grounded,
        // targeting resolved-94 at (-112, 20) metres X/Z.
        private static readonly Vector3 StallFeet = new Vector3(-112.000f, 49.400f, 21.608f);

        [Test]
        public void CurrentProductionTerminalCapsuleSerializesBlockingVoxelForCollisionIsolation()
        {
            using var world = new ShowcaseWorld(
                Seed,
                brickPoolCapacity: 131_072,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2);

            // The grounded capsule spans voxel y=494..511 and its 0.3 m step-up reaches y=514,
            // so reproduce both vertical regions touched by the production collision query.
            var lowerRegion = new int3(-3, 0, 0);
            var upperRegion = new int3(-3, 1, 0);
            world.GenerateRegionBlocking(lowerRegion);
            world.GenerateRegionBlocking(upperRegion);
            Assert.That(world.IsGenerated(lowerRegion), Is.True);
            Assert.That(world.IsGenerated(upperRegion), Is.True);

            Type motorType = Type.GetType("VoxelEngine.Showcase.CharacterMotor, VoxelEngine.Showcase", throwOnError: true);
            MethodInfo footMin = RequirePrivateStatic(motorType, "FootMin");
            MethodInfo footMax = RequirePrivateStatic(motorType, "FootMax");
            MethodInfo isBlocked = RequirePrivateStatic(motorType, "IsBlocked");
            object motor = Activator.CreateInstance(motorType);

            float radius = ReadFloatField(motorType, motor, "Radius");
            float height = ReadFloatField(motorType, motor, "Height");
            float stepHeight = ReadFloatField(motorType, motor, "StepHeight");
            float probeDistance = ShowcaseWorld.VoxelSize * 0.5f;

            Vector3 current = StallFeet;
            Vector3 zProbe = current + Vector3.back * probeDistance;
            Vector3 raised = current + Vector3.up * stepHeight;
            Vector3 raisedZProbe = zProbe + Vector3.up * stepHeight;

            ProbeResult currentResult = Probe(world, footMin, footMax, isBlocked, current, radius, height);
            ProbeResult zResult = Probe(world, footMin, footMax, isBlocked, zProbe, radius, height);
            ProbeResult raisedResult = Probe(world, footMin, footMax, isBlocked, raised, radius, height);
            ProbeResult raisedZResult = Probe(world, footMin, footMax, isBlocked, raisedZProbe, radius, height);

            Assert.That(currentResult.Blocked, Is.False,
                "The exact built-player stall position must itself remain occupiable.");
            Assert.That(zResult.Blocked, Is.True,
                "The production half-voxel Z sweep must reproduce the built-player blocker.");
            Assert.That(raisedResult.Blocked, Is.False,
                "The production step-up position must itself remain occupiable.");
            Assert.That(raisedZResult.Blocked, Is.True,
                "The same forward sweep must remain blocked after the production 0.3 m step-up.");
            Assert.That(zResult.OccupiedVoxelCount, Is.GreaterThan(0),
                "Built-player telemetry already rejected vegetation; the focused repro must identify the authoritative voxel blocker.");
            Assert.That(raisedZResult.OccupiedVoxelCount, Is.GreaterThan(0),
                "Raised forward sweep must identify the authoritative voxel blocker rather than only semantic vegetation.");

            var diagnostic = new StringBuilder(4096);
            diagnostic.Append("radius=").Append(radius.ToString("F3"))
                .Append(" height=").Append(height.ToString("F3"))
                .Append(" step=").Append(stepHeight.ToString("F3"))
                .Append(" voxelSize=").Append(ShowcaseWorld.VoxelSize.ToString("F3"));
            AppendProbe(diagnostic, "current", current, currentResult);
            AppendProbe(diagnostic, "z", zProbe, zResult);
            AppendProbe(diagnostic, "raised", raised, raisedResult);
            AppendProbe(diagnostic, "raisedZ", raisedZProbe, raisedZResult);
            Debug.Log(LogPrefix + diagnostic);
        }

        private static ProbeResult Probe(
            ShowcaseWorld world,
            MethodInfo footMin,
            MethodInfo footMax,
            MethodInfo isBlocked,
            Vector3 feet,
            float radius,
            float height)
        {
            var min = (Vector3)footMin.Invoke(null, new object[] { feet, radius });
            var max = (Vector3)footMax.Invoke(null, new object[] { feet, radius, height });
            bool blocked = (bool)isBlocked.Invoke(null, new object[] { world, min, max });

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
                if (occupied <= 24)
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
