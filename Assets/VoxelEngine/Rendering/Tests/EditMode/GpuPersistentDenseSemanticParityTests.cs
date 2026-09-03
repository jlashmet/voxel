using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuPersistentDenseSemanticParityTests
    {
        private const string ResolverShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickCacheResolver.compute";
        private const string ProbeShaderPath =
            "Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPersistentDenseSemanticProbe.compute";

        private struct ResolverRequest
        {
            public int X;
            public int Y;
            public int Z;
            public int OutputBase;

            public ResolverRequest(int3 origin)
            {
                X = origin.x;
                Y = origin.y;
                Z = origin.z;
                OutputBase = 0;
            }
        }

        [Test]
        public void PersistentResolvedDenseEntryMatchesExplicitDenseSemanticInputs()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; persistent GPU semantic parity cannot run.");

            ComputeShader resolver = AssetDatabase.LoadAssetAtPath<ComputeShader>(ResolverShaderPath);
            ComputeShader probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbeShaderPath);
            Assert.NotNull(resolver, $"Resolver shader missing at {ResolverShaderPath}");
            Assert.NotNull(probe, $"Semantic probe shader missing at {ProbeShaderPath}");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 4);
            var materials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var surfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var boundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                Fill(materials, 3);
                Fill(surfaces, 0x0021);
                Fill(boundaries, 0x11);

                int3 brickCoordinate = new(-3, 2, -5);
                int3 localVoxel = new(3, 4, 5);
                int voxelIndex = localVoxel.x
                               + VoxelReadGrid.BlockEdge * (localVoxel.y
                               + VoxelReadGrid.BlockEdge * localVoxel.z);
                materials[voxelIndex] = 23;
                surfaces[voxelIndex] = 0x5A6D;
                boundaries[voxelIndex] = 0xC7;

                VoxelBrickDelta delta = VoxelBrickDelta.MixedAt(
                    brickCoordinate, generation: 7, sourceSlot: 0);
                delta.AddMaterial(3);
                delta.AddMaterial(23);
                Assert.AreEqual(
                    GpuBrickPublish.Uploaded,
                    mirror.Publish(delta, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true));
                Assert.IsTrue(mirror.TryGetSlot(brickCoordinate, out int slot));

                uint packedMixedEntry = 2u | (unchecked((uint)slot) << 16);
                using var explicitDense = Structured(new[] { packedMixedEntry });
                using var preparedDense = ResolvePersistentEntry(resolver, mirror, brickCoordinate);

                var preparedEntry = new uint[1];
                preparedDense.GetData(preparedEntry);
                Assert.AreEqual(packedMixedEntry, preparedEntry[0],
                    "Production persistent lookup must resolve the same packed mixed entry as an explicit dense cache.");

                int3 worldVoxel = brickCoordinate * VoxelReadGrid.BlockEdge + localVoxel;
                uint[] explicitInputs = Sample(probe, mirror, explicitDense, brickCoordinate, worldVoxel);
                uint[] persistentInputs = Sample(probe, mirror, preparedDense, brickCoordinate, worldVoxel);

                Assert.AreEqual(23u, explicitInputs[0], "The probe must select the intended material byte.");
                Assert.AreNotEqual(0u, explicitInputs[1], "The authored surface semantics must survive GPU payload decoding.");
                Assert.AreEqual(0xC7u, explicitInputs[2], "The probe must select the intended boundary byte.");
                CollectionAssert.AreEqual(
                    explicitInputs,
                    persistentInputs,
                    "Persistent directory resolution and explicit dense-cache sampling must expose identical material, surface, and boundary inputs.");
            }
            finally
            {
                if (boundaries.IsCreated) boundaries.Dispose();
                if (surfaces.IsCreated) surfaces.Dispose();
                if (materials.IsCreated) materials.Dispose();
            }
        }

        private static ComputeBuffer ResolvePersistentEntry(
            ComputeShader resolver, GpuVoxelBrickMirror mirror, int3 brickCoordinate)
        {
            int kernel = resolver.FindKernel("CSResolveBrickCache");
            using var header = Structured(new[]
            {
                unchecked((uint)mirror.DirectoryWordOffset),
                unchecked((uint)(mirror.DirectoryCapacity - 1)),
            });
            using var requests = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.Structured);
            requests.SetData(new[] { new ResolverRequest(brickCoordinate) });
            var resolved = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
            resolved.SetData(new uint[1]);

            resolver.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            resolver.SetBuffer(kernel, "_PersistentLookupHeader", header);
            resolver.SetBuffer(kernel, "_ResolvedBrickCacheRequests", requests);
            resolver.SetBuffer(kernel, "_ResolvedBrickCacheWrite", resolved);
            resolver.SetInt("_ResolvedBrickCacheEdge", 1);
            resolver.SetInt("_ResolvedBrickCacheRequestCount", 1);
            resolver.Dispatch(kernel, 1, 1, 1);

            // Synchronize the resolver before the temporary header/request buffers are disposed.
            // The returned dense buffer remains GPU-readable for the semantic probe below.
            var synchronization = new uint[1];
            resolved.GetData(synchronization);
            return resolved;
        }

        private static uint[] Sample(
            ComputeShader probe,
            GpuVoxelBrickMirror mirror,
            ComputeBuffer denseCache,
            int3 brickCoordinate,
            int3 worldVoxel)
        {
            int kernel = probe.FindKernel("CSProbe");
            using var output = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.Structured);
            output.SetData(new uint[3]);

            probe.SetBuffer(kernel, "_BrickMaterials", mirror.Materials);
            probe.SetBuffer(kernel, "_BrickSurfaceSemantics", mirror.SurfaceSemantics);
            probe.SetBuffer(kernel, "_BrickBoundarySamples", mirror.BoundarySamples);
            probe.SetBuffer(kernel, "_BrickCache", denseCache);
            probe.SetInts("_BrickCacheOrigin", brickCoordinate.x, brickCoordinate.y, brickCoordinate.z);
            probe.SetInt("_BrickCacheEdge", 1);
            probe.SetInts("_ProbeVoxel", worldVoxel.x, worldVoxel.y, worldVoxel.z);
            probe.SetBuffer(kernel, "_ProbeOutput", output);
            probe.Dispatch(kernel, 1, 1, 1);

            var values = new uint[3];
            output.GetData(values);
            return values;
        }

        private static ComputeBuffer Structured(uint[] data)
        {
            var buffer = new ComputeBuffer(data.Length, sizeof(uint), ComputeBufferType.Structured);
            buffer.SetData(data);
            return buffer;
        }

        private static void Fill(NativeArray<byte> values, byte value)
        {
            for (int i = 0; i < values.Length; i++) values[i] = value;
        }

        private static void Fill(NativeArray<ushort> values, ushort value)
        {
            for (int i = 0; i < values.Length; i++) values[i] = value;
        }
    }
}
