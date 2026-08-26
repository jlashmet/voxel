using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TransitionMeshJobNormalTests
    {
        [Test]
        public void SlantedFaceFieldEmitsNormalsWithTangentialComponent()
        {
            const int cellsPerAxis = 1;
            const int faceSamplesPerAxis = 3;

            var densities = new float[faceSamplesPerAxis * faceSamplesPerAxis];
            var materials = new byte[densities.Length];
            var surfaces = new uint[densities.Length];
            for (int v = 0; v < faceSamplesPerAxis; v++)
            for (int u = 0; u < faceSamplesPerAxis; u++)
            {
                int index = u + faceSamplesPerAxis * v;
                densities[index] = (u - 1f) + 0.45f * (v - 1f) + 0.13f;
                materials[index] = 1;
            }

            int vertexDataStride = 0;
            for (int i = 0; i < TransvoxelTransitionTables.VertexData.Length; i++)
                vertexDataStride = math.max(vertexDataStride,
                    TransvoxelTransitionTables.VertexData[i].Length);

            int cellIndexStride = 0;
            for (int i = 0; i < TransvoxelTransitionTables.CellData.Length; i++)
                cellIndexStride = math.max(cellIndexStride,
                    TransvoxelTransitionTables.CellData[i].VertexIndices.Length);

            var geometryCounts = new byte[TransvoxelTransitionTables.CellData.Length];
            var cellIndices = new byte[TransvoxelTransitionTables.CellData.Length * cellIndexStride];
            for (int i = 0; i < TransvoxelTransitionTables.CellData.Length; i++)
            {
                RegularCellData data = TransvoxelTransitionTables.CellData[i];
                geometryCounts[i] = (byte)((data.VertexCount << 4) | data.TriangleCount);
                Array.Copy(data.VertexIndices, 0, cellIndices, i * cellIndexStride,
                    data.VertexIndices.Length);
            }

            var vertexData = new ushort[TransvoxelTransitionTables.VertexData.Length * vertexDataStride];
            for (int i = 0; i < TransvoxelTransitionTables.VertexData.Length; i++)
                Array.Copy(TransvoxelTransitionTables.VertexData[i], 0, vertexData,
                    i * vertexDataStride, TransvoxelTransitionTables.VertexData[i].Length);

            using var faceDensity = new NativeArray<float>(densities, Allocator.TempJob);
            using var faceMaterials = new NativeArray<byte>(materials, Allocator.TempJob);
            using var faceSurfaces = new NativeArray<uint>(surfaces, Allocator.TempJob);
            using var cellClass = new NativeArray<byte>(TransvoxelTransitionTables.CellClass,
                Allocator.TempJob);
            using var nativeGeometryCounts = new NativeArray<byte>(geometryCounts, Allocator.TempJob);
            using var nativeCellIndices = new NativeArray<byte>(cellIndices, Allocator.TempJob);
            using var nativeVertexData = new NativeArray<ushort>(vertexData, Allocator.TempJob);
            using var vertices = new NativeList<SmoothSurfaceVertex>(Allocator.TempJob);
            using var indices = new NativeList<uint>(Allocator.TempJob);

            var job = new TransitionMeshJob
            {
                FaceDensity = faceDensity,
                FaceMaterials = faceMaterials,
                FaceSurfaces = faceSurfaces,
                FaceSamplesPerAxis = faceSamplesPerAxis,
                TransitionCellClass = cellClass,
                TransitionGeometryCounts = nativeGeometryCounts,
                TransitionCellIndices = nativeCellIndices,
                TransitionVertexData = nativeVertexData,
                VertexDataStride = vertexDataStride,
                CellIndexStride = cellIndexStride,
                Vertices = vertices,
                Indices = indices,
                ChunkOriginVoxel = int3.zero,
                CellsPerAxis = cellsPerAxis,
                SourceStep = 2,
                VoxelSize = 1f,
                Face = 5,
            };

            job.Execute();

            Assert.Greater(vertices.Length, 0,
                "The slanted one-cell transition fixture must emit geometry.");

            float2 expectedTangent = math.normalize(new float2(-1f, -0.45f));
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 normal = (float3)vertices[i].Normal;
                float2 tangent = new float2(normal.x, normal.y);
                float tangentLength = math.length(tangent);
                Assert.Greater(tangentLength, 0.2f,
                    $"Transition vertex {i} discarded the face density gradient: {normal}.");
                Assert.Greater(math.dot(tangent / tangentLength, expectedTangent), 0.8f,
                    $"Transition vertex {i} tangential normal points away from the density gradient: {normal}.");
            }
        }
    }
}
