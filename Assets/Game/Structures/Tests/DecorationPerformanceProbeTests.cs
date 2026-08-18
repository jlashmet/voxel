using System;
using System.Diagnostics;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    /// <summary>
    /// Operational DEC122 probe. This deliberately records rather than asserts wall-clock thresholds:
    /// CI/editor hardware varies, but backend batch count, dynamic isolation, and metadata count are
    /// deterministic. Run in Unity and capture the emitted timing/memory line in the validation record.
    /// </summary>
    public sealed class DecorationPerformanceProbeTests
    {
        private const int PlacementCount = 25000;

        [Test]
        public void RuntimePlannerRecordsLargeSyntheticSetCost()
        {
            DecorationPlacement[] placements = BuildPlacements(PlacementCount);

            // Warm the managed/JIT path before recording the representative pass.
            Assert.IsTrue(DecorationRuntimePlanner.TryBuild(placements, out _));
            long memoryBefore = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();
            Assert.IsTrue(DecorationRuntimePlanner.TryBuild(
                placements, out DecorationRuntimePlan plan));
            stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);

            int expectedDynamic = 0;
            for (int i = 0; i < placements.Length; i++)
                if ((placements[i].Interaction & DecorationInteractionFlags.Movable) != 0)
                    expectedDynamic++;

            Assert.Multiple(() =>
            {
                Assert.AreEqual(PlacementCount, plan.Metadata.Length);
                Assert.AreEqual(expectedDynamic, plan.DynamicProps.Length);
                Assert.LessOrEqual(plan.StaticBatches.Length, 4,
                    "Static render grouping regressed to more than one group per backend.");
            });

            TestContext.WriteLine(
                $"DEC122 runtime-plan probe: placements={PlacementCount:N0}, " +
                $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}, " +
                $"managedMemoryDeltaBytes={memoryAfter - memoryBefore:N0}, " +
                $"staticBatches={plan.StaticBatches.Length}, dynamicProps={plan.DynamicProps.Length}");
        }

        [Test]
        public void DetailPolicyRecordsCullCostForLargeSyntheticSet()
        {
            DecorationPlacement[] placements = BuildPlacements(PlacementCount);
            var stopwatch = Stopwatch.StartNew();
            DecorationPlacement[] near = DecorationDetailPolicy.Filter(placements, 100f);
            DecorationPlacement[] mid = DecorationDetailPolicy.Filter(placements, 500f);
            DecorationPlacement[] far = DecorationDetailPolicy.Filter(placements, 1000f);
            stopwatch.Stop();

            Assert.Multiple(() =>
            {
                Assert.Greater(near.Length, mid.Length);
                Assert.Greater(mid.Length, far.Length);
                Assert.Greater(far.Length, 0);
                Assert.Less(far.Length, PlacementCount);
            });

            TestContext.WriteLine(
                $"DEC122 detail-policy probe: source={PlacementCount:N0}, near={near.Length:N0}, " +
                $"mid={mid.Length:N0}, far={far.Length:N0}, elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F3}");
        }

        private static DecorationPlacement[] BuildPlacements(int count)
        {
            var placements = new DecorationPlacement[count];
            DecorationPropFamily[] families =
            {
                DecorationPropFamily.Bed,
                DecorationPropFamily.Bookcase,
                DecorationPropFamily.Chandelier,
                DecorationPropFamily.Banner,
                DecorationPropFamily.Crate,
                DecorationPropFamily.Barrel,
            };

            for (int i = 0; i < count; i++)
            {
                DecorationPropFamily family = families[i % families.Length];
                DecorationRenderBackend backend = (DecorationRenderBackend)(i % 4);
                DecorationInteractionFlags interaction =
                    DecorationInteractionFlags.Destructible;
                if (i % 11 == 0)
                    interaction |= DecorationInteractionFlags.Movable;

                int x = (i % 250) * 6;
                int z = (i / 250) * 6;
                placements[i] = new DecorationPlacement
                {
                    Id = new GeneratedPropId((ulong)(i + 1)),
                    SceneId = 0x50455246u,
                    SlotId = (uint)(i + 1),
                    Family = family,
                    Backend = backend,
                    Interaction = interaction,
                    Bounds = new DecorationBounds
                    {
                        Min = new int3(x, 10, z),
                        MaxExclusive = new int3(x + 4, 14, z + 4),
                    },
                    Facing = new int3(0, 1, 0),
                    Variant = (uint)(i + 1),
                };
            }
            return placements;
        }
    }
}
