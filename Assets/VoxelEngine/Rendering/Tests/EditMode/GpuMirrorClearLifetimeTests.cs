using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuMirrorClearLifetimeTests
    {
        [Test]
        public void ClearPreservesTheGpuDirectoryUntilEverySubmissionCompletes()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            using var mirror = new GpuVoxelBrickMirror(4);
            uint[] empty = ReadDirectory(mirror);
            Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(new int3(-3, 2, 7), 1, 1),
                default(NativeArray<byte>), default(NativeArray<ushort>), default(NativeArray<byte>), 0, false),
                Is.EqualTo(GpuBrickPublish.MetadataOnly));
            uint[] occupied = ReadDirectory(mirror);
            CollectionAssert.AreNotEqual(empty, occupied);
            mirror.RetainSubmission();
            try
            {
                mirror.RetainSubmission();
                try
                {
                    mirror.Clear();
                    Assert.That(mirror.IsClearPending, Is.True);
                    Assert.Throws<InvalidOperationException>(() => mirror.RetainSubmission());
                    Assert.Throws<InvalidOperationException>(() => mirror.Remove(int3.zero));
                    Assert.Throws<InvalidOperationException>(() => mirror.Publish(
                        VoxelBrickDelta.EmptyAt(int3.zero, 2), default(NativeArray<byte>),
                        default(NativeArray<ushort>), default(NativeArray<byte>), 0, false));
                    CollectionAssert.AreEqual(occupied, ReadDirectory(mirror),
                        "Clear overwrote the GPU source directory while submitted readers still owned it.");
                }
                finally { mirror.ReleaseSubmission(); }
                CollectionAssert.AreEqual(occupied, ReadDirectory(mirror));
            }
            finally { mirror.ReleaseSubmission(); }
            CollectionAssert.AreEqual(empty, ReadDirectory(mirror));
            Assert.That(mirror.IsClearPending, Is.False);
            Assert.That(mirror.Publish(VoxelBrickDelta.UniformAt(int3.zero, 2, 1),
                default(NativeArray<byte>), default(NativeArray<ushort>), default(NativeArray<byte>), 0, false),
                Is.EqualTo(GpuBrickPublish.MetadataOnly), "Publication must resume after the pending clear.");
        }

        [Test]
        public void DisposalCancelsAPendingClearWithoutUsingReleasedBuffers()
        {
            using var mirror = new GpuVoxelBrickMirror(4);
            ComputeBuffer buffer = mirror.Materials;
            mirror.RetainSubmission();
            mirror.Clear();
            mirror.Dispose();
            Assert.That(buffer.IsValid(), Is.True);
            Assert.DoesNotThrow(() => mirror.ReleaseSubmission());
            Assert.That(buffer.IsValid(), Is.False);
        }

        internal static uint[] ReadDirectory(GpuVoxelBrickMirror mirror)
        {
            var words = new uint[mirror.DirectoryCapacity * GpuVoxelBrickMirror.DirectoryWordsPerEntry];
            mirror.Materials.GetData(words, 0, mirror.DirectoryWordOffset, words.Length);
            return words;
        }
    }
}
