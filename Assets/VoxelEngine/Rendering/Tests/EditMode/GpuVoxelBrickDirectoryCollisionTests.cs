using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuVoxelBrickDirectoryCollisionTests
    {
        private const uint DirectoryOccupied = 1u;

        [Test]
        public void NegativeCoordinateCollisionKeepsBothMixedBricksReachable()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; GPU directory lookup cannot run.");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 4);
            using var materials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var surfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var boundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int3 aroundZero = int3.zero;
            int3 negative = new(-8, 0, -5);
            Assert.AreEqual(
                GpuVoxelBrickMirror.HashCoordinate(aroundZero) & (uint)mirror.DirectoryMask,
                GpuVoxelBrickMirror.HashCoordinate(negative) & (uint)mirror.DirectoryMask,
                "The fixture must exercise a real open-addressing collision in the production directory.");

            PublishMixed(mirror, aroundZero, 7, 0x1021, 0x31, materials, surfaces, boundaries);
            PublishMixed(mirror, negative, 19, 0x5a6d, 0xc7, materials, surfaces, boundaries);

            Assert.IsTrue(mirror.TryGetSlot(aroundZero, out int zeroSlot));
            Assert.IsTrue(mirror.TryGetSlot(negative, out int negativeSlot));
            Assert.AreNotEqual(zeroSlot, negativeSlot);

            AssertDirectoryEntry(mirror, aroundZero, PackMixed(zeroSlot));
            AssertDirectoryEntry(mirror, negative, PackMixed(negativeSlot));
            AssertPayload(mirror, zeroSlot, 7, 0x1021, 0x31);
            AssertPayload(mirror, negativeSlot, 19, 0x5a6d, 0xc7);
        }

        private static void PublishMixed(
            GpuVoxelBrickMirror mirror, int3 coordinate,
            byte material, ushort surface, byte boundary,
            NativeArray<byte> materials, NativeArray<ushort> surfaces,
            NativeArray<byte> boundaries)
        {
            Fill(materials, material);
            Fill(surfaces, surface);
            Fill(boundaries, boundary);
            VoxelBrickDelta delta = VoxelBrickDelta.MixedAt(coordinate, generation: 1, sourceSlot: 0);
            delta.AddMaterial(material);
            Assert.AreEqual(
                GpuBrickPublish.Uploaded,
                mirror.Publish(delta, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true));
        }

        private static void AssertDirectoryEntry(
            GpuVoxelBrickMirror mirror, int3 coordinate, uint packedEntry)
        {
            ComputeBuffer buffer = mirror.Materials;
            var words = new uint[buffer.count];
            buffer.GetData(words);

            for (int entry = 0; entry < mirror.DirectoryCapacity; entry++)
            {
                int word = mirror.DirectoryWordOffset
                         + entry * GpuVoxelBrickMirror.DirectoryWordsPerEntry;
                if (unchecked((int)words[word + 0]) != coordinate.x
                    || unchecked((int)words[word + 1]) != coordinate.y
                    || unchecked((int)words[word + 2]) != coordinate.z)
                    continue;

                Assert.AreEqual(packedEntry, words[word + 3]);
                Assert.AreEqual(DirectoryOccupied, words[word + 4]);
                return;
            }

            Assert.Fail($"GPU directory contains no occupied entry for {coordinate}.");
        }

        private static void AssertPayload(
            GpuVoxelBrickMirror mirror, int slot, byte material, ushort surface, byte boundary)
        {
            var materialWords = new uint[GpuBrickBufferLayout.MaterialWordsPerBrick];
            mirror.Materials.GetData(
                materialWords, 0, GpuBrickBufferLayout.MaterialWordOffset(slot), materialWords.Length);
            for (int i = 0; i < materialWords.Length; i++)
                Assert.AreEqual(RepeatByte(material), materialWords[i]);

            var surfaceWords = new uint[GpuBrickBufferLayout.SurfaceWordsPerBrick];
            mirror.SurfaceSemantics.GetData(
                surfaceWords, 0, GpuBrickBufferLayout.SurfaceWordOffset(slot), surfaceWords.Length);
            for (int i = 0; i < surfaceWords.Length; i++)
                Assert.AreEqual(RepeatUShort(surface), surfaceWords[i]);

            var boundaryWords = new uint[GpuBrickBufferLayout.BoundaryWordsPerBrick];
            mirror.BoundarySamples.GetData(
                boundaryWords, 0, GpuBrickBufferLayout.BoundaryWordOffset(slot), boundaryWords.Length);
            for (int i = 0; i < boundaryWords.Length; i++)
                Assert.AreEqual(RepeatByte(boundary), boundaryWords[i]);
        }

        private static uint PackMixed(int slot) => 2u | (unchecked((uint)slot) << 16);

        private static uint RepeatByte(byte value)
        {
            uint word = value;
            return word | (word << 8) | (word << 16) | (word << 24);
        }

        private static uint RepeatUShort(ushort value) => (uint)value | ((uint)value << 16);

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
