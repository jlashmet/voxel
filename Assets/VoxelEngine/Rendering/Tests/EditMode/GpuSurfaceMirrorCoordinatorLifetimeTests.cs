using System;
using System.Reflection;
using Unity.Mathematics;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceMirrorCoordinatorLifetimeTests
    {
        [Test]
        public void CoarseDemandDoesNotRepeatFailedEvictionWithinSameFrame()
        {
            var type = typeof(GpuSurfaceMirrorCoordinator);
            var add = (Action<int3>)type.GetMethod("AddReadyBlock", BindingFlags.Static | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action<int3>));
            var clear = (Action)type.GetMethod("ClearReadyBlocks", BindingFlags.Static | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Action));
            clear();
            ulong epoch = GpuSurfaceMirrorCoordinator.RequestCoverage(int3.zero, 66, int3.zero, new int3(512));
            try
            {
                for (int i = 0; i < 65536; i++) add(new int3(i % 66, (i / 66) % 66, i / (66 * 66)));
                ulong before = GpuSurfaceMirrorCoordinator.ReadyEvictionChecks;
                for (int i = 65536; i < 65552; i++) add(new int3(i % 66, (i / 66) % 66, i / (66 * 66)));
                Assert.That(GpuSurfaceMirrorCoordinator.ReadyBlockCount, Is.EqualTo(65552),
                    "A demanded source must remain ready; cleanup cannot evict it to meet its target.");
                Assert.That(GpuSurfaceMirrorCoordinator.ReadyEvictionChecks - before, Is.LessThanOrEqualTo(64UL),
                    "A failed pinned-only cleanup slice must not repeat for every brick admitted in the same frame.");
                ulong after = GpuSurfaceMirrorCoordinator.ReadyEvictionChecks;
                type.GetMethod("TryEvictInactiveReadyBlock", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { UnityEngine.Time.frameCount + 1 });
                Assert.That(GpuSurfaceMirrorCoordinator.ReadyEvictionChecks - after, Is.EqualTo(64UL),
                    "The next frame must resume the cleanup cursor rather than permanently suppressing reclamation.");
            }
            finally
            {
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(int3.zero, 66, int3.zero, new int3(512), epoch);
                clear();
            }
        }

        [Test]
        public void DetachPageArenaWithoutConfiguredArenaIsNoOp()
        {
            FieldInfo pageArenaField = typeof(GpuSurfaceMirrorCoordinator).GetField(
                "s_PageArena", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(pageArenaField, Is.Not.Null);

            object previous = pageArenaField.GetValue(null);
            pageArenaField.SetValue(null, null);
            try
            {
                Assert.DoesNotThrow(() =>
                    GpuSurfaceMirrorCoordinator.DetachPageArena(null, frame: 0));
                Assert.That(GpuSurfaceMirrorCoordinator.HasPageArena, Is.False);
            }
            finally
            {
                pageArenaField.SetValue(null, previous);
            }
        }
    }
}
