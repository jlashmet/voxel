using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Behavior-first contract for SceneIssue 20260829-050700-000.
    /// These tests intentionally land before the production mesh-import implementation so the
    /// required behavior is fixed independently of the implementation details.
    /// </summary>
    public sealed class MeshVoxelizationFeatureTests
    {
        [Test]
        public void ClosedCurvedMesh_ConservativelyCoversSurfaceAndFillsInterior()
        {
            MeshVoxelizationSource sphere = BuildUvSphere(
                radius: 2.4f, latitudeSegments: 18, longitudeSegments: 32, material: 6,
                transform: float4x4.identity);
            var settings = Settings(voxelSize: 0.4f, fillInterior: true);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in sphere, in settings);

            Assert.That(bake.Cells.Length, Is.GreaterThan(400));
            Assert.That(bake.Size.x, Is.GreaterThan(8));
            Assert.That(bake.Size.y, Is.GreaterThan(8));
            Assert.That(bake.Size.z, Is.GreaterThan(8));
            Assert.That(ContainsLocal(bake, bake.Size / 2), Is.True,
                "A closed curved source must have a filled interior, not only a shell.");

            // A conservative rasterizer must keep the extrema in every axis. Vertex-only
            // quantization tends to leave holes around the curved silhouette.
            Assert.That(HasBoundaryCell(bake, axis: 0, upper: false), Is.True);
            Assert.That(HasBoundaryCell(bake, axis: 0, upper: true), Is.True);
            Assert.That(HasBoundaryCell(bake, axis: 1, upper: false), Is.True);
            Assert.That(HasBoundaryCell(bake, axis: 1, upper: true), Is.True);
            Assert.That(HasBoundaryCell(bake, axis: 2, upper: false), Is.True);
            Assert.That(HasBoundaryCell(bake, axis: 2, upper: true), Is.True);
        }

        [Test]
        public void SameSourceAndSettings_ProduceStableOrderedVoxelAndMaterialOutput()
        {
            MeshVoxelizationSource source = BuildUvSphere(
                1.75f, 12, 24, 7,
                float4x4.TRS(new float3(2.125f, -1.75f, 5.5f),
                             quaternion.EulerXYZ(0.31f, -0.57f, 0.19f),
                             new float3(1.2f, 0.8f, 1.1f)));
            var settings = Settings(0.35f, true);

            BakedVoxelStructure first = MeshVoxelizer.Voxelize(in source, in settings);
            BakedVoxelStructure second = MeshVoxelizer.Voxelize(in source, in settings);

            AssertBakesEqual(first, second);
            for (int i = 1; i < first.Cells.Length; i++)
                Assert.That(Compare(first.Cells[i - 1].Position, first.Cells[i].Position), Is.LessThan(0),
                    "Serialized cells must be in stable lexicographic order.");
        }

        [Test]
        public void OffOriginRotatedMirroredTransform_IsAppliedBeforeGridQuantization()
        {
            MeshVoxelizationSource local = BuildBox(
                new float3(-1f, -0.5f, -0.75f), new float3(1f, 0.5f, 0.75f), 4,
                float4x4.identity);
            float4x4 transformedMatrix = float4x4.TRS(
                new float3(23.4f, 7.2f, -11.8f),
                quaternion.RotateY(math.radians(37f)),
                new float3(-1.7f, 2.1f, 0.65f));
            MeshVoxelizationSource transformed = BuildBox(
                new float3(-1f, -0.5f, -0.75f), new float3(1f, 0.5f, 0.75f), 4,
                transformedMatrix);
            var settings = Settings(0.25f, true);

            BakedVoxelStructure localBake = MeshVoxelizer.Voxelize(in local, in settings);
            BakedVoxelStructure transformedBake = MeshVoxelizer.Voxelize(in transformed, in settings);

            Assert.That(transformedBake.GridOrigin, Is.Not.EqualTo(localBake.GridOrigin));
            Assert.That(transformedBake.GridOrigin.x, Is.GreaterThan(70));
            Assert.That(transformedBake.GridOrigin.z, Is.LessThan(-30));
            Assert.That(transformedBake.Size.y, Is.GreaterThan(localBake.Size.y),
                "Non-uniform scale must affect the authored grid before voxelization.");
            Assert.That(transformedBake.Cells.Length, Is.GreaterThan(0));
        }

        [Test]
        public void SurfaceMaterialRegions_ArePreservedDeterministically()
        {
            // Two closed boxes overlap slightly so the result is one volumetric authored form,
            // while each half carries a distinct source material region.
            MeshVoxelizationSource left = BuildBox(
                new float3(-2f, -1f, -1f), new float3(0.2f, 1f, 1f), 6,
                float4x4.identity);
            MeshVoxelizationSource right = BuildBox(
                new float3(-0.2f, -1f, -1f), new float3(2f, 1f, 1f), 8,
                float4x4.identity);
            MeshVoxelizationSource combined = Combine(left, right);
            var settings = Settings(0.3f, true, fallbackMaterial: 3);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in combined, in settings);

            Assert.That(CountMaterial(bake, 6), Is.GreaterThan(25));
            Assert.That(CountMaterial(bake, 8), Is.GreaterThan(25));
            Assert.That(CountMaterial(bake, 3), Is.GreaterThan(0),
                "Interior fill uses the configured deterministic fallback when no source surface owns a cell.");
        }

        [Test]
        public void ThinTriangleSheet_IsRetainedWithoutGlobalSolidification()
        {
            var vertices = new[]
            {
                new float3(-3f, 0f, -1.2f), new float3(3f, 0f, -1.2f),
                new float3(3f, 0f, 1.2f), new float3(-3f, 0f, 1.2f)
            };
            var triangles = new[]
            {
                new MeshVoxelTriangle(0, 1, 2, 9),
                new MeshVoxelTriangle(0, 2, 3, 9)
            };
            var source = new MeshVoxelizationSource(vertices, triangles, float4x4.identity);
            var settings = Settings(0.25f, fillInterior: false, thinFeaturePaddingVoxels: 0);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);

            Assert.That(bake.Cells.Length, Is.GreaterThan(100));
            Assert.That(bake.Size.y, Is.LessThanOrEqualTo(3),
                "A thin wing/membrane policy must preserve the sheet without inflating the whole volume.");
            Assert.That(CountMaterial(bake, 9), Is.EqualTo(bake.Cells.Length));
        }

        [Test]
        public void Preflight_RejectsInvalidIndicesAndOversizedDenseGridBeforeRasterization()
        {
            var badSource = new MeshVoxelizationSource(
                new[] { new float3(0f), new float3(1f, 0f, 0f), new float3(0f, 1f, 0f) },
                new[] { new MeshVoxelTriangle(0, 1, 99, 1) },
                float4x4.identity);
            var normalSettings = Settings(0.1f, false);
            Assert.Throws<ArgumentException>(() => MeshVoxelizer.Voxelize(in badSource, in normalSettings));

            MeshVoxelizationSource huge = BuildBox(
                new float3(0f), new float3(100f, 100f, 100f), 1, float4x4.identity);
            var bounded = new MeshVoxelizationSettings(
                voxelSize: 0.1f,
                fillInterior: true,
                fallbackMaterial: 1,
                maxDimensions: new int3(127, 511, 127),
                maxDenseCells: 200_000,
                thinFeaturePaddingVoxels: 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshVoxelizer.Voxelize(in huge, in bounded));
        }

        [Test]
        public void SparseCodec_RoundTripsExactBakeAndRejectsOutOfBoundsCells()
        {
            MeshVoxelizationSource source = BuildBox(
                new float3(-1f), new float3(1f), 5,
                float4x4.TRS(new float3(4f, 3f, 2f), quaternion.identity, new float3(1f)));
            var settings = Settings(0.35f, true);
            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);

            string encoded = BakedVoxelStructureCodec.Encode(bake);
            BakedVoxelStructure decoded = BakedVoxelStructureCodec.Decode(encoded);
            AssertBakesEqual(bake, decoded);

            string corrupt = "MVX1|0.25|0,0,0|2,2,2|1|2,0,0,5";
            Assert.Throws<FormatException>(() => BakedVoxelStructureCodec.Decode(corrupt));
        }

        [Test]
        public void BakedCells_ReplayThroughCanonicalAuthoringSessionAtRequestedOrigin()
        {
            var bake = new BakedVoxelStructure(
                voxelSize: 0.1f,
                gridOrigin: new int3(-2, 4, 8),
                size: new int3(3, 2, 2),
                cells: new[]
                {
                    new BakedVoxelCell(new int3(0, 0, 0), 6),
                    new BakedVoxelCell(new int3(1, 0, 0), 7),
                    new BakedVoxelCell(new int3(2, 1, 1), 8),
                },
                sourceTriangleCount: 20000,
                voxelizationMilliseconds: 12.5);
            var session = new RecordingAuthoringSession();

            int written = bake.ReplayTo(session, new int3(100, 20, -50));

            Assert.That(written, Is.EqualTo(3));
            Assert.That(session.Writes.Count, Is.EqualTo(3));
            Assert.That(session.Writes[0], Is.EqualTo(new RecordedWrite(new int3(100, 20, -50), 6)));
            Assert.That(session.Writes[2], Is.EqualTo(new RecordedWrite(new int3(102, 21, -49), 8)));
        }

        [Test]
        public void StructureSelection_ScrollThenSpaceCommit_IsEdgeTriggeredAndCannotDuplicateOnIdle()
        {
            var selection = new StructurePlacementSelection(new[] { "arch", "dragon", "tower" });
            int commits = 0;
            int committedIndex = -1;

            selection.Begin();
            selection.Scroll(+1); // arch -> dragon
            Assert.That(selection.SelectedName, Is.EqualTo("dragon"));

            Assert.That(selection.TryCommitSelected(index =>
            {
                commits++;
                committedIndex = index;
                return true;
            }), Is.True);
            Assert.That(committedIndex, Is.EqualTo(1));
            Assert.That(selection.Committed, Is.True);

            // These calls model repeated idle Update frames after the Space key-down edge.
            for (int i = 0; i < 120; i++)
                Assert.That(selection.TryCommitSelected(_ => { commits++; return true; }), Is.False);

            Assert.That(commits, Is.EqualTo(1));
        }

        private static MeshVoxelizationSettings Settings(
            float voxelSize,
            bool fillInterior,
            byte fallbackMaterial = 2,
            int thinFeaturePaddingVoxels = 0)
        {
            return new MeshVoxelizationSettings(
                voxelSize,
                fillInterior,
                fallbackMaterial,
                new int3(127, 511, 127),
                2_000_000,
                thinFeaturePaddingVoxels);
        }

        private static MeshVoxelizationSource BuildBox(
            float3 min, float3 max, byte material, float4x4 transform)
        {
            var v = new[]
            {
                new float3(min.x,min.y,min.z), new float3(max.x,min.y,min.z),
                new float3(max.x,max.y,min.z), new float3(min.x,max.y,min.z),
                new float3(min.x,min.y,max.z), new float3(max.x,min.y,max.z),
                new float3(max.x,max.y,max.z), new float3(min.x,max.y,max.z),
            };
            int[] raw =
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7,
                0,1,5, 0,5,4, 3,7,6, 3,6,2,
                0,4,7, 0,7,3, 1,2,6, 1,6,5,
            };
            var t = new MeshVoxelTriangle[raw.Length / 3];
            for (int i = 0; i < t.Length; i++)
                t[i] = new MeshVoxelTriangle(raw[i * 3], raw[i * 3 + 1], raw[i * 3 + 2], material);
            return new MeshVoxelizationSource(v, t, transform);
        }

        private static MeshVoxelizationSource BuildUvSphere(
            float radius, int latitudeSegments, int longitudeSegments, byte material,
            float4x4 transform)
        {
            var vertices = new List<float3>();
            var triangles = new List<MeshVoxelTriangle>();
            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float theta = math.PI * lat / latitudeSegments;
                float y = math.cos(theta) * radius;
                float ring = math.sin(theta) * radius;
                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float phi = 2f * math.PI * lon / longitudeSegments;
                    vertices.Add(new float3(math.cos(phi) * ring, y, math.sin(phi) * ring));
                }
            }
            int stride = longitudeSegments + 1;
            for (int lat = 0; lat < latitudeSegments; lat++)
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int a = lat * stride + lon;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                if (lat != 0) triangles.Add(new MeshVoxelTriangle(a, c, b, material));
                if (lat != latitudeSegments - 1)
                    triangles.Add(new MeshVoxelTriangle(b, c, d, material));
            }
            return new MeshVoxelizationSource(vertices.ToArray(), triangles.ToArray(), transform);
        }

        private static MeshVoxelizationSource Combine(
            MeshVoxelizationSource a, MeshVoxelizationSource b)
        {
            var vertices = new float3[a.Vertices.Length + b.Vertices.Length];
            Array.Copy(a.Vertices, 0, vertices, 0, a.Vertices.Length);
            Array.Copy(b.Vertices, 0, vertices, a.Vertices.Length, b.Vertices.Length);
            var triangles = new MeshVoxelTriangle[a.Triangles.Length + b.Triangles.Length];
            Array.Copy(a.Triangles, 0, triangles, 0, a.Triangles.Length);
            for (int i = 0; i < b.Triangles.Length; i++)
            {
                MeshVoxelTriangle t = b.Triangles[i];
                triangles[a.Triangles.Length + i] = new MeshVoxelTriangle(
                    t.A + a.Vertices.Length, t.B + a.Vertices.Length,
                    t.C + a.Vertices.Length, t.Material);
            }
            return new MeshVoxelizationSource(vertices, triangles, float4x4.identity);
        }

        private static bool ContainsLocal(BakedVoxelStructure bake, int3 position)
        {
            for (int i = 0; i < bake.Cells.Length; i++)
                if (math.all(bake.Cells[i].Position == position)) return true;
            return false;
        }

        private static bool HasBoundaryCell(BakedVoxelStructure bake, int axis, bool upper)
        {
            int expected = upper ? bake.Size[axis] - 1 : 0;
            for (int i = 0; i < bake.Cells.Length; i++)
                if (bake.Cells[i].Position[axis] == expected) return true;
            return false;
        }

        private static int CountMaterial(BakedVoxelStructure bake, byte material)
        {
            int count = 0;
            for (int i = 0; i < bake.Cells.Length; i++)
                if (bake.Cells[i].Material == material) count++;
            return count;
        }

        private static void AssertBakesEqual(BakedVoxelStructure a, BakedVoxelStructure b)
        {
            Assert.That(b.VoxelSize, Is.EqualTo(a.VoxelSize));
            Assert.That(b.GridOrigin, Is.EqualTo(a.GridOrigin));
            Assert.That(b.Size, Is.EqualTo(a.Size));
            Assert.That(b.SourceTriangleCount, Is.EqualTo(a.SourceTriangleCount));
            Assert.That(b.Cells.Length, Is.EqualTo(a.Cells.Length));
            for (int i = 0; i < a.Cells.Length; i++)
            {
                Assert.That(b.Cells[i].Position, Is.EqualTo(a.Cells[i].Position));
                Assert.That(b.Cells[i].Material, Is.EqualTo(a.Cells[i].Material));
            }
        }

        private static int Compare(int3 a, int3 b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        private readonly struct RecordedWrite : IEquatable<RecordedWrite>
        {
            public readonly int3 Position;
            public readonly byte Material;
            public RecordedWrite(int3 position, byte material)
            {
                Position = position;
                Material = material;
            }
            public bool Equals(RecordedWrite other) =>
                math.all(Position == other.Position) && Material == other.Material;
            public override bool Equals(object obj) => obj is RecordedWrite other && Equals(other);
            public override int GetHashCode() => Position.GetHashCode() * 397 ^ Material;
            public override string ToString() => $"{Position}:{Material}";
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
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
                                  byte coating = 0, VoxelEngine.Storage.Api.VoxelSurfaceFlags flags = 0) =>
                Set(x, y, z, material);
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
