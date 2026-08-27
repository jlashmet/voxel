using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ShowcaseBakeHeightPipelineTests
    {
        [Test]
        public void PipelinedHeightPreparationPreservesOrderedSemanticRegionBytes()
        {
            var regions = new[]
            {
                new int3(0, 0, 0),
                new int3(1, 0, 0),
                new int3(0, 0, 1),
                new int3(1, 0, 0), // The bake footprint may encounter an already-owned region.
            };

            using var serial = new ShowcaseWorld(0x5EED1234u, 32768, 2, 3);
            using var pipelined = new ShowcaseWorld(0x5EED1234u, 32768, 2, 3);

            for (int i = 0; i < regions.Length; i++)
                serial.GenerateRegionBlocking(regions[i]);
            pipelined.GenerateTerrainRegionsForBakeBlocking(regions, pipelineDepth: 4);

            for (int i = 0; i < regions.Length - 1; i++)
            {
                AssertSnapshotEqual(serial, pipelined, regions[i]);
            }
        }

        private static void AssertSnapshotEqual(
            ShowcaseWorld expectedWorld,
            ShowcaseWorld actualWorld,
            int3 coord)
        {
            RegionSnapshotCaptureResult expectedResult =
                expectedWorld.SnapshotStorage.CaptureSemanticSnapshot(
                    coord,
                    ShowcaseWorldBakeCodec.MaxRawRegionPayloadBytes,
                    out RegionSemanticSnapshot expected);
            RegionSnapshotCaptureResult actualResult =
                actualWorld.SnapshotStorage.CaptureSemanticSnapshot(
                    coord,
                    ShowcaseWorldBakeCodec.MaxRawRegionPayloadBytes,
                    out RegionSemanticSnapshot actual);

            Assert.AreEqual(RegionSnapshotCaptureResult.Ok, expectedResult);
            Assert.AreEqual(expectedResult, actualResult);
            Assert.AreEqual(expected.SemanticHash, actual.SemanticHash);
            CollectionAssert.AreEqual(expected.Bytes, actual.Bytes,
                $"Pipelined generation changed authoritative semantic bytes for region {coord}.");
        }
    }
}
