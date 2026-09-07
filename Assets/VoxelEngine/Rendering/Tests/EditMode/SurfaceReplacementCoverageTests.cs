using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceReplacementCoverageTests
    {
        [Test]
        public void UnrelatedReadyGeometryCannotReplaceAMissingPartOfTheFeature()
        {
            var published = new HashSet<SurfaceLodNodeKey>
            {
                new(1, int3.zero), new(1, new int3(10, 0, 0))
            };
            Assert.False(SurfaceReplacementCoverage.Covers(
                int3.zero, new int3(128, 64, 64), published.Contains, out _));
            published.Add(new SurfaceLodNodeKey(1, new int3(1, 0, 0)));
            Assert.True(SurfaceReplacementCoverage.Covers(
                int3.zero, new int3(128, 64, 64), published.Contains, out _));
        }

        [Test]
        public void ReadyBoundsCornersCannotConcealAnInteriorHole()
        {
            var published = new HashSet<SurfaceLodNodeKey>
            {
                new(1, int3.zero), new(1, new int3(2, 0, 0))
            };
            Assert.False(SurfaceReplacementCoverage.Covers(int3.zero, new int3(192, 64, 64),
                published.Contains, out _));
        }

        [Test]
        public void LargestAdmittedBoundsHaveAFixedQueryCeiling()
        {
            Assert.True(SurfaceReplacementCoverage.Covers(int3.zero, new int3(1024),
                node => node.SourceStep == 8, out int queries));
            Assert.AreEqual(8, queries, "Each complete step-8 subtree needs exactly one proof.");
            Assert.False(SurfaceReplacementCoverage.Covers(int3.zero, new int3(1025),
                _ => AssertUnexpectedQuery(), out queries));
            Assert.AreEqual(0, queries);
        }

        [Test]
        public void SelectedCoarseAndFineGeometryCanTogetherCoverOneFeature()
        {
            var published = new HashSet<SurfaceLodNodeKey>
            {
                new(2, int3.zero), new(1, new int3(2, 0, 0))
            };
            Assert.True(SurfaceReplacementCoverage.Covers(
                int3.zero, new int3(192, 64, 64), published.Contains, out _));
            published.Remove(new SurfaceLodNodeKey(2, int3.zero));
            Assert.False(SurfaceReplacementCoverage.Covers(
                int3.zero, new int3(192, 64, 64), published.Contains, out _),
                "Evicting the coarse replacement must restore the proxy immediately.");
        }

        [Test]
        public void NegativeBoundsAndExactSeamsDoNotDemandAnAdjacentChunk()
        {
            var published = new HashSet<SurfaceLodNodeKey> { new(1, new int3(-1)) };
            Assert.True(SurfaceReplacementCoverage.Covers(
                new int3(-64), int3.zero, published.Contains, out int queries));
            Assert.AreEqual(4, queries);
            Assert.False(SurfaceReplacementCoverage.Covers(
                new int3(-65), int3.zero, published.Contains, out _));
        }

        [Test]
        public void StaleDrawableCannotReplaceCurrentWorldTruth()
        {
            var state = new SurfaceLodCoverageState();
            var node = new SurfaceLodNodeKey(1, int3.zero);
            state.SetDesiredGeneration(node, 1);
            Assert.True(state.TryPublishCompletion(node, 1, SurfaceLodCompletionKind.Ready));
            Assert.True(SurfaceReplacementCoverage.Covers(int3.zero, new int3(64),
                n => state.IsDesiredComplete(n), out _));
            state.SetDesiredGeneration(node, 2);
            Assert.True(state.GetOrDefault(node).HasDrawableProof);
            Assert.False(SurfaceReplacementCoverage.Covers(int3.zero, new int3(64),
                n => state.IsDesiredComplete(n), out _));
            Assert.True(state.TryPublishCompletion(node, 2, SurfaceLodCompletionKind.KnownEmpty));
            Assert.True(SurfaceReplacementCoverage.Covers(int3.zero, new int3(64),
                n => state.IsDesiredComplete(n), out _),
                "A completed destruction result must not resurrect the old proxy.");
        }

        [Test]
        public void OversizedOrInvalidBoundsKeepProxyWithoutUnboundedTraversal()
        {
            Assert.False(SurfaceReplacementCoverage.Covers(new int3(int.MinValue),
                new int3(int.MaxValue), _ => AssertUnexpectedQuery(), out int queries));
            Assert.AreEqual(0, queries);
            Assert.False(SurfaceReplacementCoverage.Covers(int3.zero, int3.zero,
                _ => AssertUnexpectedQuery(), out queries));
            Assert.AreEqual(0, queries);
        }

        [Test]
        public void HierarchicalProofMatchesFineCellOracleAcrossMixedLevelsAndNegativeSeams()
        {
            var random = new System.Random(4137);
            for (int trial = 0; trial < 256; trial++)
            {
                int3 min = new(random.Next(-700, 700), random.Next(-700, 700), random.Next(-700, 700));
                int3 max = min + new int3(random.Next(1, 500), random.Next(1, 500), random.Next(1, 500));
                var proof = new HashSet<SurfaceLodNodeKey>();
                int3 first = min >> 6, last = (max - 1) >> 6;
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                {
                    var node = new SurfaceLodNodeKey(1, new int3(x, y, z));
                    while (true)
                    {
                        if (random.Next(2 + trial % 31) == 0) proof.Add(node);
                        if (!SurfaceLodHierarchy.TryGetParentSourceStep(node.SourceStep, out int step)) break;
                        node = new SurfaceLodNodeKey(step, SurfaceLodHierarchy.ParentCoordinate(node.Coordinate));
                    }
                }
                bool expected = true;
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                {
                    bool covered = false;
                    var node = new SurfaceLodNodeKey(1, new int3(x, y, z));
                    while (true)
                    {
                        if (proof.Contains(node)) { covered = true; break; }
                        if (!SurfaceLodHierarchy.TryGetParentSourceStep(node.SourceStep, out int step)) break;
                        node = new SurfaceLodNodeKey(step, SurfaceLodHierarchy.ParentCoordinate(node.Coordinate));
                    }
                    expected &= covered;
                }
                Assert.AreEqual(expected, SurfaceReplacementCoverage.Covers(min, max, proof.Contains, out _),
                    $"Mixed-level coverage differs on trial {trial}: {min} to {max}");
            }
        }

        private static bool AssertUnexpectedQuery()
        {
            Assert.Fail("Rejected bounds must not query publication state.");
            return true;
        }
    }
}
