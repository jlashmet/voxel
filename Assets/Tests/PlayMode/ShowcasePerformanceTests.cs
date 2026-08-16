using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression coverage for the two streaming operations that are visible as frame hitches in
    /// the showcase: terrain generation and landmark construction. These are wall-clock budgets,
    /// deliberately loose enough for CI hardware but tight enough to catch accidental return to
    /// whole-region / whole-castle blocking work.
    /// </summary>
    public sealed class ShowcasePerformanceTests
    {
        private const uint Seed = 0xC0FFEEu;
        private const int PoolCapacity = 1 << 17;

        [UnityTest]
        public IEnumerator StreamingStep_RespectsInteractiveFrameBudget()
        {
            using var world = new ShowcaseWorld(Seed, PoolCapacity, 1, 2);
            var camera = new float3(25f, 30f, 25f);

            // Prime the wanted set. The first call may pay static/JIT setup on some players, so
            // warm once before timing; subsequent calls are the representative steady-state path.
            world.StepStreaming(camera, 2.0);
            yield return null;

            double worstMs = 0.0;
            for (int i = 0; i < 120; i++)
            {
                var sw = Stopwatch.StartNew();
                world.StepStreaming(camera, 2.0);
                sw.Stop();
                worstMs = math.max(worstMs, sw.Elapsed.TotalMilliseconds);
                yield return null;
            }

            Assert.That(worstMs, Is.LessThan(100.0),
                $"A 2 ms streaming slice blocked the player loop for {worstMs:F1} ms. " +
                "Streaming work must remain interruptible rather than completing a whole region synchronously.");
        }

        [UnityTest]
        public IEnumerator CastleBuild_IsIncrementalAndDoesNotFreezeThePlayerLoop()
        {
            using var world = new ShowcaseWorld(Seed, PoolCapacity, 1, 2);
            var camera = new float3(25f, 30f, 25f);

            // Drive until the castle build starts. The landmark owns writes once active, so this
            // also exercises the exact path the interactive showcase uses during startup.
            int guard = 0;
            while (world.CastleBuildStage == 0 && world.CastleVoxels == 0 && guard++ < 3000)
            {
                world.StepStreaming(camera, 6.0);
                yield return null;
            }

            Assert.That(world.CastleBuildStage > 0 || world.CastleVoxels > 0, Is.True,
                "Castle construction never started during the streaming window.");

            double worstMs = 0.0;
            int sampledFrames = 0;
            while (world.CastleVoxels == 0 && sampledFrames++ < 2000)
            {
                var sw = Stopwatch.StartNew();
                world.StepStreaming(camera, 12.0);
                sw.Stop();
                worstMs = math.max(worstMs, sw.Elapsed.TotalMilliseconds);
                yield return null;
            }

            Assert.That(world.CastleVoxels, Is.GreaterThan(0),
                "Castle construction did not finish inside the PlayMode guard window.");
            Assert.That(worstMs, Is.LessThan(120.0),
                $"Castle construction blocked one player-loop iteration for {worstMs:F1} ms. " +
                "Large authoring stages must remain sliced across frames.");
        }
    }
}
