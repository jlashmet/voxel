using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Behavior-first topology contract for SceneIssue 20260829-050700-000.
    /// Open/non-manifold input must never be silently treated as a trustworthy solid.
    /// </summary>
    public sealed class MeshVoxelizationTopologyTests
    {
        [Test]
        public void OpenMesh_SurfaceOnlyFallback_ReportsBoundaryAndDoesNotInventInterior()
        {
            MeshVoxelizationSource source = BuildBoxMissingTop();
            var settings = Settings(MeshVoxelOpenSurfacePolicy.SurfaceOnly);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);

            Assert.That(bake.BoundaryEdgeCount, Is.GreaterThan(0));
            Assert.That(bake.NonManifoldEdgeCount, Is.EqualTo(0));
            Assert.That(bake.InteriorFilled, Is.False,
                "FillInterior on an open source must explicitly fall back to surface-only.");
            Assert.That(ContainsLocal(bake, bake.Size / 2), Is.False,
                "An open source must not acquire a synthetic solid centre through accidental raster closure.");
        }

        [Test]
        public void OpenMesh_RejectPolicy_FailsWithTopologyDiagnostic()
        {
            MeshVoxelizationSource source = BuildBoxMissingTop();
            var settings = Settings(MeshVoxelOpenSurfacePolicy.Reject);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => MeshVoxelizer.Voxelize(in source, in settings));

            StringAssert.Contains("boundary", exception.Message.ToLowerInvariant());
        }

        [Test]
        public void ClosedMesh_FillInterior_ReportsClosedTopologyAndFilledInterior()
        {
            MeshVoxelizationSource source = BuildClosedBox();
            var settings = Settings(MeshVoxelOpenSurfacePolicy.Reject);

            BakedVoxelStructure bake = MeshVoxelizer.Voxelize(in source, in settings);

            Assert.That(bake.BoundaryEdgeCount, Is.EqualTo(0));
            Assert.That(bake.NonManifoldEdgeCount, Is.EqualTo(0));
            Assert.That(bake.InteriorFilled, Is.True);
            Assert.That(ContainsLocal(bake, bake.Size / 2), Is.True);
        }

        private static MeshVoxelizationSettings Settings(MeshVoxelOpenSurfacePolicy policy) =>
            new MeshVoxelizationSettings(
                voxelSize: 0.25f,
                fillInterior: true,
                fallbackMaterial: 3,
                maxDimensions: new int3(127, 511, 127),
                maxDenseCells: 2_000_000,
                thinFeaturePaddingVoxels: 0,
                openSurfacePolicy: policy);

        private static MeshVoxelizationSource BuildClosedBox() =>
            BuildBox(includeTop: true);

        private static MeshVoxelizationSource BuildBoxMissingTop() =>
            BuildBox(includeTop: false);

        private static MeshVoxelizationSource BuildBox(bool includeTop)
        {
            var vertices = new[]
            {
                new float3(-2f,-2f,-2f), new float3(2f,-2f,-2f),
                new float3(2f, 2f,-2f), new float3(-2f, 2f,-2f),
                new float3(-2f,-2f, 2f), new float3(2f,-2f, 2f),
                new float3(2f, 2f, 2f), new float3(-2f, 2f, 2f),
            };
            int[] closed =
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                0,4,7, 0,7,3,
                1,2,6, 1,6,5,
            };
            int[] top = { 3,7,6, 3,6,2 };
            int triangleCount = closed.Length / 3 + (includeTop ? top.Length / 3 : 0);
            var triangles = new MeshVoxelTriangle[triangleCount];
            int write = 0;
            for (int i = 0; i < closed.Length; i += 3)
                triangles[write++] = new MeshVoxelTriangle(closed[i], closed[i + 1], closed[i + 2], 6);
            if (includeTop)
                for (int i = 0; i < top.Length; i += 3)
                    triangles[write++] = new MeshVoxelTriangle(top[i], top[i + 1], top[i + 2], 6);
            return new MeshVoxelizationSource(vertices, triangles, float4x4.identity);
        }

        private static bool ContainsLocal(BakedVoxelStructure bake, int3 position)
        {
            for (int i = 0; i < bake.Cells.Length; i++)
                if (math.all(bake.Cells[i].Position == position)) return true;
            return false;
        }
    }
}
