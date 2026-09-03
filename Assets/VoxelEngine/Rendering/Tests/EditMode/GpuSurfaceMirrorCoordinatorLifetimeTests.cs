using System.Reflection;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceMirrorCoordinatorLifetimeTests
    {
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
