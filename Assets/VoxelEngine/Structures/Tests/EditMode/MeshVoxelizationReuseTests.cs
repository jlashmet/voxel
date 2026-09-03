using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MeshVoxelizationReuseTests
    {
        [Test]
        public void IndependentBoxFixture_UsesImporterCodecAndCanonicalAuthoringPath()
        {
            MeshVoxelizationSource source = BuildBox(
                new float3(-1.25f, -0.75f, -0.5f),
                new float3(1.25f, 0.75f, 0.5f),
                material: 11,
                transform: float4x4.TRS(
                    new float3(3.5f, 2f, -4.25f),
                    quaternion.RotateY(math.radians(23f)),
                    new float3(1.1f, 0.9f, 1.3f)));
            var settings = new MeshVoxelizationSettings(
                voxelSize: 0.25f,
                fillInterior: true,
                fallbackMaterial: 4,
                maxDimensions: new int3(127, 511, 127),
                maxDenseCells: 200_000,
                thinFeaturePaddingVoxels: 0);

            BakedVoxelStructure baked = MeshVoxelizer.Voxelize(in source, in settings);
            string encoded = BakedVoxelStructureCodec.Encode(baked);
            BakedVoxelStructure decoded = BakedVoxelStructureCodec.Decode(encoded);
            var session = new RecordingSession();
            int written = decoded.ReplayTo(session, new int3(40, 12, -30));

            Assert.That(decoded.SourceTriangleCount, Is.EqualTo(12));
            Assert.That(decoded.Cells.Length, Is.GreaterThan(0));
            Assert.That(written, Is.EqualTo(decoded.Cells.Length));
            Assert.That(session.Writes.Count, Is.EqualTo(decoded.Cells.Length));
            Assert.That(session.Writes[0].Material, Is.Not.EqualTo(0));
        }

        private static MeshVoxelizationSource BuildBox(
            float3 min, float3 max, byte material, float4x4 transform)
        {
            var vertices = new[]
            {
                new float3(min.x, min.y, min.z), new float3(max.x, min.y, min.z),
                new float3(max.x, max.y, min.z), new float3(min.x, max.y, min.z),
                new float3(min.x, min.y, max.z), new float3(max.x, min.y, max.z),
                new float3(max.x, max.y, max.z), new float3(min.x, max.y, max.z),
            };
            int[] indices =
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
            };
            var triangles = new MeshVoxelTriangle[indices.Length / 3];
            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = new MeshVoxelTriangle(
                    indices[i * 3], indices[i * 3 + 1], indices[i * 3 + 2], material);
            return new MeshVoxelizationSource(vertices, triangles, transform);
        }

        private readonly struct RecordedWrite
        {
            public readonly int3 Position;
            public readonly byte Material;

            public RecordedWrite(int3 position, byte material)
            {
                Position = position;
                Material = material;
            }
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<RecordedWrite> Writes = new();
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Writes.Count;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) =>
                Writes.Add(new RecordedWrite(new int3(x, y, z), material));
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = 0, VoxelSurfaceFlags flags = 0) => Set(x, y, z, material);
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
