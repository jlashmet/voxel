using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// End-to-end publication coverage for the persistent GPU brick mirror. Slot-table tests protect
    /// admission policy separately; these regressions prove that admitted authoritative deltas become
    /// the payload and directory state the GPU can actually consume.
    /// </summary>
    public sealed class GpuVoxelBrickMirrorPublicationTests
    {
        private const uint DirectoryOccupied = 1u;
        private const uint DirectoryTombstone = 2u;

        [Test]
        public void MixedPublicationUpdatesGpuPayloadAndRejectsStaleGeneration()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; GPU mirror publication cannot run.");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 4);
            using var materials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var surfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var boundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int3 coordinate = new(-3, 2, -5);
            Fill(materials, 7);
            Fill(surfaces, 0x1234);
            Fill(boundaries, 0xab);

            VoxelBrickDelta first = VoxelBrickDelta.MixedAt(coordinate, generation: 5, sourceSlot: 0);
            first.AddMaterial(7);
            Assert.AreEqual(
                GpuBrickPublish.Uploaded,
                mirror.Publish(first, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true));
            Assert.IsTrue(mirror.TryGetSlot(coordinate, out int slot));

            AssertGpuPayload(mirror, slot, 7, 0x1234, 0xab);
            AssertDirectoryEntry(
                mirror, coordinate, DirectoryOccupied, PackMixed(slot));

            Fill(materials, 9);
            Fill(surfaces, 0x4321);
            Fill(boundaries, 0x5a);
            VoxelBrickDelta newer = VoxelBrickDelta.MixedAt(coordinate, generation: 6, sourceSlot: 0);
            newer.AddMaterial(9);
            Assert.AreEqual(
                GpuBrickPublish.Uploaded,
                mirror.Publish(newer, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true));
            Assert.IsTrue(mirror.TryGetSlot(coordinate, out int replacementSlot));
            Assert.AreEqual(slot, replacementSlot, "A newer generation must replace the same GPU slot in place.");
            AssertGpuPayload(mirror, slot, 9, 0x4321, 0x5a);

            Fill(materials, 3);
            Fill(surfaces, 0x0f0f);
            Fill(boundaries, 0x33);
            VoxelBrickDelta stale = VoxelBrickDelta.MixedAt(coordinate, generation: 4, sourceSlot: 0);
            stale.AddMaterial(3);
            Assert.AreEqual(
                GpuBrickPublish.Stale,
                mirror.Publish(stale, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true),
                "A late older delta must never become GPU-visible after a newer publication.");

            AssertGpuPayload(mirror, slot, 9, 0x4321, 0x5a);
            AssertDirectoryEntry(
                mirror, coordinate, DirectoryOccupied, PackMixed(slot));
        }

        [Test]
        public void UniformAndEmptyPublicationKeepGpuDirectoryReachabilityCoherent()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; GPU mirror publication cannot run.");

            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 4);
            using var materials = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var surfaces = new NativeArray<ushort>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var boundaries = new NativeArray<byte>(
                VoxelReadGrid.VoxelsPerBlock, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int3 uniformCoordinate = new(4, -1, 7);
            VoxelBrickDelta uniform = VoxelBrickDelta.UniformAt(uniformCoordinate, generation: 3, material: 11);
            Assert.AreEqual(
                GpuBrickPublish.MetadataOnly,
                mirror.Publish(
                    uniform,
                    default(NativeArray<byte>),
                    default(NativeArray<ushort>),
                    default(NativeArray<byte>),
                    elementOffset: 0,
                    hasPayload: false));
            Assert.IsFalse(mirror.TryGetSlot(uniformCoordinate, out _),
                "Uniform bricks belong in the compact directory and must not consume a mixed payload slot.");
            AssertDirectoryEntry(
                mirror, uniformCoordinate, DirectoryOccupied, PackUniform(11));

            int3 emptiedCoordinate = new(-6, 1, 2);
            Fill(materials, 13);
            Fill(surfaces, 0x2468);
            Fill(boundaries, 0x7c);
            VoxelBrickDelta mixed = VoxelBrickDelta.MixedAt(emptiedCoordinate, generation: 8, sourceSlot: 0);
            mixed.AddMaterial(13);
            Assert.AreEqual(
                GpuBrickPublish.Uploaded,
                mirror.Publish(mixed, materials, surfaces, boundaries, elementOffset: 0, hasPayload: true));
            Assert.IsTrue(mirror.TryGetSlot(emptiedCoordinate, out int mixedSlot));
            AssertDirectoryEntry(
                mirror, emptiedCoordinate, DirectoryOccupied, PackMixed(mixedSlot));

            VoxelBrickDelta empty = VoxelBrickDelta.EmptyAt(emptiedCoordinate, generation: 9);
            Assert.AreEqual(
                GpuBrickPublish.MetadataOnly,
                mirror.Publish(
                    empty,
                    default(NativeArray<byte>),
                    default(NativeArray<ushort>),
                    default(NativeArray<byte>),
                    elementOffset: 0,
                    hasPayload: false));
            Assert.IsFalse(mirror.TryGetSlot(emptiedCoordinate, out _),
                "Destroying a mixed brick to empty must release its payload slot.");
            AssertDirectoryEntry(
                mirror, emptiedCoordinate, DirectoryTombstone, PackMixed(mixedSlot),
                "The old packed entry may remain physically present, but a tombstone must make it unreachable to GPU lookup.");
        }

        private static void AssertGpuPayload(GpuVoxelBrickMirror mirror, int slot,
                                             byte material, ushort surface, byte boundary)
        {
            uint[] materialWords = new uint[GpuBrickBufferLayout.MaterialWordsPerBrick];
            mirror.Materials.GetData(
                materialWords, 0, GpuBrickBufferLayout.MaterialWordOffset(slot), materialWords.Length);
            AssertAll(materialWords, RepeatByte(material), "material");

            uint[] surfaceWords = new uint[GpuBrickBufferLayout.SurfaceWordsPerBrick];
            mirror.SurfaceSemantics.GetData(
                surfaceWords, 0, GpuBrickBufferLayout.SurfaceWordOffset(slot), surfaceWords.Length);
            AssertAll(surfaceWords, RepeatUShort(surface), "surface");

            uint[] boundaryWords = new uint[GpuBrickBufferLayout.BoundaryWordsPerBrick];
            mirror.BoundarySamples.GetData(
                boundaryWords, 0, GpuBrickBufferLayout.BoundaryWordOffset(slot), boundaryWords.Length);
            AssertAll(boundaryWords, RepeatByte(boundary), "boundary");

            var metadata = new GpuVoxelBrickMirror.BrickMetadata[1];
            mirror.Metadata.GetData(metadata, 0, slot, 1);
            Assert.AreEqual(slot, metadata[0].Slot);
            Assert.AreEqual((uint)VoxelBrickContent.Mixed, metadata[0].Content);
        }

        private static void AssertDirectoryEntry(GpuVoxelBrickMirror mirror, int3 coordinate,
                                                 uint expectedState, uint expectedPackedEntry,
                                                 string message = null)
        {
            ComputeBuffer buffer = mirror.Materials;
            var words = new uint[buffer.count];
            buffer.GetData(words);

            for (int entry = 0; entry < mirror.DirectoryCapacity; entry++)
            {
                int word = mirror.DirectoryWordOffset + entry * GpuVoxelBrickMirror.DirectoryWordsPerEntry;
                if (unchecked((int)words[word + 0]) != coordinate.x
                    || unchecked((int)words[word + 1]) != coordinate.y
                    || unchecked((int)words[word + 2]) != coordinate.z)
                    continue;

                Assert.AreEqual(expectedPackedEntry, words[word + 3], message);
                Assert.AreEqual(expectedState, words[word + 4], message);
                return;
            }

            Assert.Fail(message ?? $"GPU directory contains no entry for {coordinate}.");
        }

        private static void AssertAll(uint[] actual, uint expected, string channel)
        {
            for (int i = 0; i < actual.Length; i++)
                Assert.AreEqual(expected, actual[i], $"{channel} word {i} differs from the published payload.");
        }

        private static uint PackMixed(int slot) => 2u | (unchecked((uint)slot) << 16);
        private static uint PackUniform(byte material) => 1u | ((uint)material << 8);

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
