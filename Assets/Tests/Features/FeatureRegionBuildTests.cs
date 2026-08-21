using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Tests.Features.Fixtures;

namespace VoxelEngine.Tests.Features
{
    /// <summary>
    /// Feature generation is spread across frames so a settlement streams in instead of stalling
    /// the frame that finishes its terrain. Slicing is only allowed to change *when* work happens,
    /// never what it produces, and a divergence would show up as a building that differs depending
    /// on how busy the frame was — untraceable from a screenshot. These tests hold the line.
    /// </summary>
    public sealed class FeatureRegionBuildTests
    {
        private const uint Seed = 0x5EED1234;

        /// <summary>The region the cottage fixture's single placement lands in.</summary>
        private static readonly int3 CottageRegion = new(4, 0, 6);

        /// <summary>Three cottages in one region exercise both intra- and inter-instance resume.</summary>
        private const int Placements = 3;

        private static FeatureCatalogue BuildCatalogue()
        {
            FeatureCatalogue catalogue = CottageFixture.Build(Allocator.Persistent, Placements);
            FeatureCatalogueBuilder.Finalise(ref catalogue);
            return catalogue;
        }

        /// <summary>Bounds covering every cottage the fixture places.</summary>
        private static int3 PlacementsMin(in FeatureCatalogue catalogue) =>
            catalogue.ExplicitPlacements[0].Position;

        private static int3 PlacementsMax(in FeatureCatalogue catalogue) =>
            catalogue.ExplicitPlacements[Placements - 1].Position
            + catalogue.Definitions[CottageFixture.CottageId].Footprint;

        private static byte[] Run(in FeatureCatalogue catalogue, int maxTilesPerStep,
                                  out FeatureGenerationReport report, out int steps)
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                using var build = new FeatureRegionBuild(CottageRegion);
                steps = 0;
                while (!build.Step(in catalogue, Seed, reads, mutations, maxTilesPerStep))
                    steps++;
                steps++;
                report = build.Report;

                return SubVolumeEquality.Snapshot(ref table, in pool,
                                                  PlacementsMin(in catalogue),
                                                  PlacementsMax(in catalogue));
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }

        [Test]
        public void SameCatalogueAndSeedProduceIdenticalVoxelOutput()
        {
            FeatureCatalogue catalogue = BuildCatalogue();
            try
            {
                byte[] first = Run(in catalogue, int.MaxValue, out var firstReport, out _);
                byte[] second = Run(in catalogue, int.MaxValue, out var secondReport, out _);

                Assert.Greater(firstReport.VoxelsWritten, 0,
                    "The fixture must actually build voxels or determinism comparison is vacuous.");
                Assert.AreEqual(-1, SubVolumeEquality.FirstDifference(first, second),
                    "the same catalogue and world seed produced different authoritative voxels");
                Assert.AreEqual(firstReport.VoxelsWritten, secondReport.VoxelsWritten);
                Assert.AreEqual(firstReport.InstancesRasterised, secondReport.InstancesRasterised);
                Assert.AreEqual(firstReport.InstancesConsidered, secondReport.InstancesConsidered);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void SlicedBuildWritesTheSameVoxelsAsAnUnslicedOne()
        {
            FeatureCatalogue catalogue = BuildCatalogue();
            try
            {
                byte[] whole = Run(in catalogue, int.MaxValue, out var wholeReport, out int wholeSteps);
                byte[] sliced = Run(in catalogue, 1, out var slicedReport, out int slicedSteps);

                Assert.Greater(wholeReport.VoxelsWritten, 0,
                    "The fixture must actually build something or this comparison is vacuous.");
                Assert.AreEqual(1, wholeSteps, "An unbounded budget finishes in one step.");
                Assert.Greater(slicedSteps, slicedReport.PrimitivesEmitted,
                    "At least one primitive must span multiple tiles; yielding only between " +
                    "whole primitives would leave the original frame-stall invariant intact.");

                int difference = SubVolumeEquality.FirstDifference(whole, sliced);
                Assert.AreEqual(-1, difference,
                    $"voxel {difference} differs between a sliced and an unsliced build");

                Assert.AreEqual(wholeReport.VoxelsWritten, slicedReport.VoxelsWritten);
                Assert.AreEqual(wholeReport.InstancesRasterised, slicedReport.InstancesRasterised);
                Assert.AreEqual(wholeReport.InstancesConsidered, slicedReport.InstancesConsidered);
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void WholeRegionEntryPointMatchesTheSlicedBuild()
        {
            // GenerateRegion is implemented as this build driven to completion. Capture tools and
            // the streaming world must not be able to disagree about a region's content.
            FeatureCatalogue catalogue = BuildCatalogue();
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                FeatureGenerationReport direct = FeatureGeneration.GenerateRegion(
                    in catalogue, Seed, CottageRegion, reads, mutations);

                byte[] viaEntryPoint = SubVolumeEquality.Snapshot(
                    ref table, in pool, PlacementsMin(in catalogue), PlacementsMax(in catalogue));

                byte[] sliced = Run(in catalogue, 1, out FeatureGenerationReport slicedReport, out _);

                Assert.AreEqual(-1, SubVolumeEquality.FirstDifference(viaEntryPoint, sliced));
                Assert.AreEqual(direct.VoxelsWritten, slicedReport.VoxelsWritten);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void BuildCompletesImmediatelyWithoutACatalogue()
        {
            // The showcase runs with no catalogue in some configurations; a build that never
            // reported completion would pin the queue head forever and stall every later region.
            using var build = new FeatureRegionBuild(CottageRegion);
            FeatureCatalogue none = default;

            Assert.IsTrue(build.Step(in none, Seed, null, null, 4));
            Assert.IsTrue(build.IsComplete);
        }

        [Test]
        public void CompletedBuildStaysCompleteAndWritesNothingFurther()
        {
            FeatureCatalogue catalogue = BuildCatalogue();
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                using var build = new FeatureRegionBuild(CottageRegion);
                while (!build.Step(in catalogue, Seed, reads, mutations, 1)) { }
                int written = build.Report.VoxelsWritten;

                // A queue that steps a finished build once more must not rebuild the region.
                Assert.IsTrue(build.Step(in catalogue, Seed, reads, mutations, int.MaxValue));
                Assert.AreEqual(written, build.Report.VoxelsWritten);
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void RegionWithNoOverlappingPlacementsBuildsNothing()
        {
            FeatureCatalogue catalogue = BuildCatalogue();
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(1024, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);

                // Skipping a placement is a footprint comparison, so an empty region must not
                // consume slices: it finishes in one call however small the budget is.
                using var build = new FeatureRegionBuild(new int3(-40, 0, -40));
                Assert.IsTrue(build.Step(in catalogue, Seed, reads, mutations, 1));
                Assert.AreEqual(0, build.Report.VoxelsWritten);
                Assert.AreEqual(0, build.Report.InstancesRasterised);
                Assert.Greater(build.Report.InstancesConsidered, 0,
                    "The catalogue was still walked; only the rasterisation was skipped.");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                catalogue.Dispose();
            }
        }

        [Test]
        public void OneTileBudgetYieldsInsideTheFirstLargePrimitive()
        {
            FeatureCatalogue catalogue = BuildCatalogue();
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                using var build = new FeatureRegionBuild(CottageRegion);

                Assert.IsFalse(build.Step(in catalogue, Seed, reads, mutations, 1),
                    "one storage-block tile must not finish a cottage");
                Assert.AreEqual(1, build.Report.InstancesConsidered);
                Assert.Greater(build.Report.PrimitivesEmitted, 0,
                    "the instance should have been evaluated before raster work yielded");
                Assert.AreEqual(0, build.Report.InstancesRasterised,
                    "an instance is reported only after all of its ordered primitive tiles finish");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
                catalogue.Dispose();
            }
        }
    }
}
