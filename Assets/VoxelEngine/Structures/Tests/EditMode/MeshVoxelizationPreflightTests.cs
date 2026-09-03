using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime.MeshImport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MeshVoxelizationPreflightTests
    {
        [Test]
        public void NonFiniteVertex_IsRejectedBeforeVoxelization()
        {
            var source = new MeshVoxelizationSource(
                new[]
                {
                    new float3(0f, 0f, 0f),
                    new float3(float.NaN, 0f, 0f),
                    new float3(0f, 1f, 0f),
                },
                new[] { new MeshVoxelTriangle(0, 1, 2, 1) },
                float4x4.identity);
            MeshVoxelizationSettings settings = Settings();

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => MeshVoxelizer.Voxelize(in source, in settings));

            StringAssert.Contains("non-finite", exception.Message.ToLowerInvariant());
        }

        [Test]
        public void NonFiniteTransform_IsRejectedBeforeTransformingVertices()
        {
            float4x4 transform = float4x4.identity;
            transform.c3.x = float.PositiveInfinity;
            var source = new MeshVoxelizationSource(
                new[]
                {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f),
                },
                new[] { new MeshVoxelTriangle(0, 1, 2, 1) },
                transform);
            MeshVoxelizationSettings settings = Settings();

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => MeshVoxelizer.Voxelize(in source, in settings));

            StringAssert.Contains("transform", exception.Message.ToLowerInvariant());
            StringAssert.Contains("finite", exception.Message.ToLowerInvariant());
        }

        private static MeshVoxelizationSettings Settings() =>
            new MeshVoxelizationSettings(
                voxelSize: 0.25f,
                fillInterior: false,
                fallbackMaterial: 1,
                maxDimensions: new int3(127, 511, 127),
                maxDenseCells: 100_000,
                thinFeaturePaddingVoxels: 0);
    }
}
