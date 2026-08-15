using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CanonicalBrushRoutingTests
    {
        [Test]
        public void ExactBrushBoundsDoNotOverRouteIntoAdjacentRegion()
        {
            var pipeline = new EventDrivenReplicationPipeline();
            var sink = new CountingSink();
            pipeline.SetSubscriptions(10, new[] { int3.zero });
            pipeline.SetSubscriptions(20, new[] { new int3(1, 0, 0) });

            // One-brick cube centered at x=507 occupies x=[503..510], entirely inside region 0.
            // The old extent-as-radius router padded by 8 on both sides and incorrectly reached x=515.
            var brush = AlterationEvent.CreateCubeBrush(
                tick: 3,
                origin: new int3(507, 4, 4),
                extentXBricks: 1,
                extentYBricks: 1,
                extentZBricks: 1,
                material: 2,
                seed: 1,
                playerId: 1,
                sequence: 1);

            pipeline.BeginTick(3);
            pipeline.PublishAlteration(in brush);
            pipeline.Flush(sink);

            Assert.That(sink.Connection10Batches, Is.EqualTo(1));
            Assert.That(sink.Connection20Batches, Is.Zero);
        }

        private sealed class CountingSink : IAlterationReplicationSink
        {
            public int Connection10Batches;
            public int Connection20Batches;

            public void SendBatch(uint connectionId, int3 encodingRegion, uint tick, ReadOnlySpan<AlterationEvent> events)
            {
                if (connectionId == 10) Connection10Batches++;
                if (connectionId == 20) Connection20Batches++;
            }
        }
    }
}
