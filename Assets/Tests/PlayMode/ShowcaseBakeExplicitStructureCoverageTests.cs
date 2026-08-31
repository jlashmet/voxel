using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseBakeExplicitStructureCoverageTests
    {
        private const uint Seed = 0x5EED1234u;
        private const int RegionVoxelEdgeLog2 = 9;

        [Test]
        public void PlannerIncludesUpperDragonStructureLayerWithoutExpandingMountainSky()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 9,
                allocator: Allocator.Temp);
            try
            {
                var regions = ShowcaseWorld.PlanExplicitFixedStructureBakeRegions(
                    in mountain,
                    int3.zero,
                    startupRadiusRegions: 8);

                int3 dragonRegion = DragonRegion(in spec);

                CollectionAssert.Contains(regions, dragonRegion,
                    "The startup bake must materialise the upper region containing the dragon placeholder.");
                CollectionAssert.Contains(regions, new int3(dragonRegion.x, 0, dragonRegion.z),
                    "A fixed structure crossing the vertical boundary must preserve its lower region too.");
                Assert.AreEqual(2, regions.Count,
                    "Bake coverage must follow the explicit fixed-altitude structure bounds, not materialise unrelated mountain/headroom sky regions.");
            }
            finally
            {
                mountain.Dispose();
            }
        }

        [Test]
        public void SparseUpperDragonStructureBuildMatchesFullCatalogueSemanticOutput()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            FeatureCatalogue mountain = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                mountainMaterial: 1,
                pathMaterial: 13,
                placeholderMaterial: 9,
                allocator: Allocator.Temp);
            try
            {
                int3 dragonRegion = DragonRegion(in spec);
                RegionSemanticSnapshot full = BuildSparseRegionSnapshot(
                    in mountain,
                    dragonRegion,
                    FeatureRegionBuildScope.All,
                    out FeatureGenerationReport fullReport);
                RegionSemanticSnapshot scoped = BuildSparseRegionSnapshot(
                    in mountain,
                    dragonRegion,
                    FeatureRegionBuildScope.FixedAltitudeStructures,
                    out FeatureGenerationReport scopedReport);

                Assert.AreEqual(full.SemanticHash, scoped.SemanticHash,
                    "Skipping output-neutral landform work in the otherwise empty upper layer must preserve the authoritative region hash.");
                CollectionAssert.AreEqual(full.Bytes, scoped.Bytes,
                    "The bake-only fixed-structure scope must be byte-for-byte equivalent to the generic full catalogue on canonical-empty upper storage.");
                Assert.Less(scopedReport.InstancesConsidered, fullReport.InstancesConsidered,
                    "The optimized path must actually avoid considering the landform definition rather than merely reproducing its cost.");
                Assert.AreEqual(1, scopedReport.InstancesConsidered,
                    "The mountain catalogue has exactly one fixed-altitude Structure placement: the dragon placeholder.");
                Assert.AreEqual(1, scopedReport.InstancesRasterised,
                    "The scoped upper-layer build must rasterise exactly the dragon placeholder instance.");
            }
            finally
            {
                mountain.Dispose();
            }
        }

        private static RegionSemanticSnapshot BuildSparseRegionSnapshot(
            in FeatureCatalogue catalogue,
            int3 regionCoord,
            FeatureRegionBuildScope scope,
            out FeatureGenerationReport report)
        {
            var table = new RegionTable(2, Allocator.Temp);
            var pool = new BrickPool(4096, Allocator.Temp);
            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                using (var build = new FeatureRegionBuild(regionCoord, scope))
                {
                    while (!build.Step(in catalogue, Seed, reads, mutations, int.MaxValue)) { }
                    report = build.Report;
                }

                reads.Refresh(in table, in pool);
                NativeArray<int3> resident = reads.GetResidentRegionCoords(Allocator.Temp);
                try
                {
                    Assert.AreEqual(1, resident.Length,
                        "A sparse fixed-structure build must materialise only its requested vertical region.");
                    Assert.AreEqual(regionCoord, resident[0]);
                }
                finally
                {
                    resident.Dispose();
                }

                RegionSnapshotCaptureResult result = reads.CaptureSemanticSnapshot(
                    regionCoord,
                    RegionSemanticSnapshotLimits.DefaultMaxSnapshotBytes,
                    out RegionSemanticSnapshot snapshot);
                Assert.AreEqual(RegionSnapshotCaptureResult.Ok, result,
                    "The authored dragon region must be resident and semantically snapshot-able.");
                return snapshot;
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }

        private static int3 DragonRegion(in MountainLandmarkSpec spec)
        {
            int3 dragonCentre = new int3(
                spec.Origin.x + spec.CentreLocal,
                spec.Origin.y + spec.MountainHeight + 1 + spec.PlaceholderSize / 2,
                spec.Origin.z + spec.CentreLocal);
            return new int3(
                FloorDivRegion(dragonCentre.x),
                FloorDivRegion(dragonCentre.y),
                FloorDivRegion(dragonCentre.z));
        }

        private static int FloorDivRegion(int voxel)
        {
            int edge = 1 << RegionVoxelEdgeLog2;
            int quotient = voxel / edge;
            int remainder = voxel % edge;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
