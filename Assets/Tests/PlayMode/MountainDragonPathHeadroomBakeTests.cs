using System.Collections.Generic;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonPathHeadroomBakeTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte PathMaterial = 13;
        private const int RegionVoxelEdgeLog2 = 9;
        private const int BrickEdgeLog2 = 3;
        private const int BrickEdgeMask = 7;
        private const int BricksPerRegionEdgeLog2 = 6;
        private const int VoxelsPerBrick = 512;
        private const int MixedCellBytes = 4;

        [Test]
        public void PreparedStartupBakeKeepsPlayerClearAirAboveEveryMountainPathTier()
        {
            TextAsset bakeAsset = Resources.Load<TextAsset>(ShowcaseWorldBakeCodec.ResourcePath);
            Assert.That(bakeAsset, Is.Not.Null,
                "The prepared VoxelShowcase startup bake must exist before headroom acceptance runs.");

            ShowcaseWorldBake bake = ShowcaseWorldBakeCodec.Deserialize(bakeAsset.bytes);
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            var snapshots = new Dictionary<int3, byte[]>();

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                int startY = level * spec.PathRise;
                int endY = startY + spec.PathRise;
                int rampWorldX = spec.Origin.x + spec.PathMinLocalX + spec.PathRun / 2;
                int rampWorldZ = spec.Origin.z + spec.RampLocalZ(startY) + spec.PathWidth / 2;
                int rampSurfaceY = FindHighestPathVoxel(
                    bake,
                    snapshots,
                    rampWorldX,
                    spec.Origin.y + startY,
                    spec.Origin.y + endY,
                    rampWorldZ,
                    "switchback ramp " + level);
                AssertHeadroom(
                    bake,
                    snapshots,
                    new int3(rampWorldX, rampSurfaceY, rampWorldZ),
                    "switchback ramp " + level);

                if (level + 1 >= spec.SwitchbackCount) continue;

                bool reverse = (level & 1) != 0;
                int nextZ = spec.RampLocalZ(endY);
                int zMin = math.min(spec.RampLocalZ(startY), nextZ);
                int zSize = math.abs(nextZ - spec.RampLocalZ(startY)) + spec.PathWidth;
                int turnX = reverse
                    ? spec.PathMinLocalX
                    : spec.PathMinLocalX + spec.PathRun - spec.PathWidth;
                int3 landing = spec.Origin + new int3(
                    turnX + spec.PathWidth / 2,
                    endY,
                    zMin + zSize / 2);
                Assert.That(ReadMaterial(bake, snapshots, landing), Is.EqualTo(PathMaterial),
                    "Turn landing " + level + " must retain its walking floor.");
                AssertHeadroom(bake, snapshots, landing, "turn landing " + level);
            }

            int finalStartY = spec.SwitchbackCount * spec.PathRise;
            int lastRampStartY = (spec.SwitchbackCount - 1) * spec.PathRise;
            int lastRampZ = spec.RampLocalZ(lastRampStartY);
            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalZMin = math.min(lastRampZ, summitZ);
            int finalZSize = math.abs(summitZ - lastRampZ) + spec.PathWidth;
            int finalWorldX = spec.Origin.x + spec.PathMinLocalX + spec.PathWidth / 2;
            int finalWorldZ = spec.Origin.z + finalZMin + finalZSize / 2;
            int finalSurfaceY = FindHighestPathVoxel(
                bake,
                snapshots,
                finalWorldX,
                spec.Origin.y + finalStartY,
                spec.Origin.y + spec.MountainHeight,
                finalWorldZ,
                "final summit ascent");
            AssertHeadroom(
                bake,
                snapshots,
                new int3(finalWorldX, finalSurfaceY, finalWorldZ),
                "final summit ascent");

            int3 summitApproach = new int3(
                spec.SummitApproachWorldX,
                spec.Origin.y + spec.MountainHeight,
                spec.SummitApproachWorldZ);
            Assert.That(ReadMaterial(bake, snapshots, summitApproach), Is.EqualTo(PathMaterial),
                "Summit approach must retain its walking floor.");
            AssertHeadroom(bake, snapshots, summitApproach, "summit approach");
        }

        private static int FindHighestPathVoxel(
            ShowcaseWorldBake bake,
            Dictionary<int3, byte[]> snapshots,
            int worldX,
            int minWorldY,
            int maxWorldY,
            int worldZ,
            string label)
        {
            for (int y = maxWorldY; y >= minWorldY; y--)
            {
                if (ReadMaterial(bake, snapshots, new int3(worldX, y, worldZ)) == PathMaterial)
                    return y;
            }

            Assert.Fail(label + " contains no authored path material in its expected vertical range.");
            return minWorldY;
        }

        private static void AssertHeadroom(
            ShowcaseWorldBake bake,
            Dictionary<int3, byte[]> snapshots,
            int3 floorVoxel,
            string label)
        {
            for (int offset = 1; offset <= WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels; offset++)
            {
                int3 sample = floorVoxel + new int3(0, offset, 0);
                Assert.That(ReadMaterial(bake, snapshots, sample), Is.EqualTo((byte)0),
                    label + " is obstructed inside the required "
                    + WorldBuilderMountainLandmarkCatalogue.PathHeadroomVoxels
                    + "-voxel player-clear envelope at " + sample + ".");
            }
        }

        private static byte ReadMaterial(
            ShowcaseWorldBake bake,
            Dictionary<int3, byte[]> snapshots,
            int3 worldVoxel)
        {
            int3 regionCoord = new int3(
                worldVoxel.x >> RegionVoxelEdgeLog2,
                worldVoxel.y >> RegionVoxelEdgeLog2,
                worldVoxel.z >> RegionVoxelEdgeLog2);
            int3 local = worldVoxel - new int3(
                regionCoord.x << RegionVoxelEdgeLog2,
                regionCoord.y << RegionVoxelEdgeLog2,
                regionCoord.z << RegionVoxelEdgeLog2);

            if (!snapshots.TryGetValue(regionCoord, out byte[] snapshot))
            {
                bool found = false;
                ShowcaseWorldBakedRegion region = default;
                for (int i = 0; i < bake.Regions.Count; i++)
                {
                    if (!math.all(bake.Regions[i].Coord == regionCoord)) continue;
                    region = bake.Regions[i];
                    found = true;
                    break;
                }

                Assert.That(found, Is.True,
                    "Startup bake is missing the region containing required headroom voxel "
                    + worldVoxel + ".");
                snapshot = ShowcaseWorldBakeCodec.DecodeRegionPayload(region);
                snapshots.Add(regionCoord, snapshot);
            }

            int brickX = local.x >> BrickEdgeLog2;
            int brickY = local.y >> BrickEdgeLog2;
            int brickZ = local.z >> BrickEdgeLog2;
            int targetBrick = brickX
                            | (brickY << BricksPerRegionEdgeLog2)
                            | (brickZ << (BricksPerRegionEdgeLog2 * 2));
            int innerX = local.x & BrickEdgeMask;
            int innerY = local.y & BrickEdgeMask;
            int innerZ = local.z & BrickEdgeMask;
            int targetVoxel = innerX
                            | (innerY << BrickEdgeLog2)
                            | (innerZ << (BrickEdgeLog2 * 2));

            int coveredBricks = 0;
            int offset = 0;
            while (offset < snapshot.Length)
            {
                byte tag = snapshot[offset++];
                if (tag == 0)
                {
                    Assert.That(offset + 4, Is.LessThanOrEqualTo(snapshot.Length),
                        "Truncated uniform record in semantic startup snapshot.");
                    int run = snapshot[offset] | (snapshot[offset + 1] << 8);
                    byte material = snapshot[offset + 2];
                    offset += 4;
                    if (targetBrick >= coveredBricks && targetBrick < coveredBricks + run)
                        return material;
                    coveredBricks += run;
                    continue;
                }

                Assert.That(tag, Is.EqualTo((byte)1),
                    "Unknown semantic startup snapshot record tag.");
                int recordBytes = 1 + VoxelsPerBrick * MixedCellBytes;
                Assert.That(offset + recordBytes, Is.LessThanOrEqualTo(snapshot.Length),
                    "Truncated mixed record in semantic startup snapshot.");
                if (coveredBricks == targetBrick)
                    return snapshot[offset + 1 + targetVoxel * MixedCellBytes];
                offset += recordBytes;
                coveredBricks++;
            }

            Assert.Fail("Semantic startup snapshot did not cover target brick " + targetBrick + ".");
            return 0;
        }
    }
}
