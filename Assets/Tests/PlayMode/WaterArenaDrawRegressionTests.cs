using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterArenaDrawRegressionTests
    {
        [Test]
        public void SecondArenaLeasePublishesVertexBaseInIndirectDrawRecord()
        {
            var arena = new SurfaceGeometryArena(1024, 2048, 8);
            try
            {
                Assert.That(arena.TryAcquire(3, 3, out SurfaceGeometryLease first), Is.True);
                Assert.That(arena.TryAcquire(3, 3, out SurfaceGeometryLease second), Is.True);
                Assert.That(second.VertexStart, Is.GreaterThan(0),
                    "The discriminator requires a second independently aligned vertex range.");

                arena.UploadArgs(3, in first);
                arena.UploadArgs(3, in second);

                var args = new uint[arena.ArgsRecordCapacity * SurfaceGeometryArena.ArgsWordsPerDraw];
                arena.Args.GetData(args);
                Assert.That(args[second.ArgsWordStart + 3], Is.EqualTo((uint)second.VertexStart),
                    "Water indices stay chunk-local, so the indirect record must carry the lease vertex base for the shader.");
            }
            finally
            {
                arena.Dispose();
            }
        }
    }
}
