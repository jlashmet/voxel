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
                AssertSupportedFloorAndHeadroom(
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
                AssertLandingColumns(
                    bake,
                    snapshots,
                    spec.Origin.x + turnX + spec.PathWidth / 2,
                    spec.Origin.y + endY,
                    spec.Origin.z + zMin,
                    zSize,
                    "turn landing " + level);
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
            AssertSupportedFloorAndHeadroom(
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
            AssertSupportedFloorAndHeadroom(bake, snapshots, summitApproach, "summit approach");
        }

        private static void AssertLandingColumns(
            ShowcaseWorldBake bake,
            Dictionary<int3, byte[]> snapshots,
            int worldX,
            int worldY,
            int worldZMin,
            int zSize,
            string label)
        {
            // Prove the turn is not represented by one lucky midpoint voxel. Three separated
            // interior columns must retain the authored floor and the complete player-clear band.
            int[] numerators = { 1, 2, 3 };
            for (int i = 0; i < numerators.Length; i++)
            {
                int worldZ = worldZMin + zSize * numerators[i] / 4;
                int3 floor = new int3(worldX, worldY, worldZ);
                Assert.That(ReadMaterial(bake, snapshots, floor), Is.EqualTo(PathMaterial),
                    label + " must retain a continuous walking floor at " + floor + ".");
                AssertHeadroom(bake, snapshots, floor, label + " column " + i);
            }

            int3 centre = new int3(worldX, worldY, worldZMin + zSize / 2);
            Assert.That(ReadMaterial(bake, snapshots, centre - new int3(0, 1, 0)), Is.Not.EqualTo((byte)0),
                label + " must remain physically supported beneath its centre walking column.");
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

        private static void AssertSupportedFloorAndHeadroom(
            ShowcaseWorldBake bake,
            Dictionary<int3, byte[]> snapshots,
            int3 floorVoxel,
            string label)
        {
            Assert.That(ReadMaterial(bake, snapshots, floorVoxel), Is.EqualTo(PathMaterial),
                label + " must retain its authored walking material at " + floorVoxel + ".");
            Assert.That(ReadMaterial(bake, snapshots, floorVoxel - new int3(0, 1, 0)), Is.Not.EqualTo((byte)0),
                label + " must remain physically occupied beneath its walking surface.");
            AssertHeadroom(bake, snapshots, floorVoxel, label);
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

                // Startup bakes are sparse: LoadBake installs only captured resident regions.
                // A vertical layer that was never materialised therefore contains no authored
                // voxels and is semantically clear air. Returning Air keeps headroom probes
                // correct while floor/support probes remain strict because they explicitly
                // require path or occupied material and will still fail on zero.
                if (!found)
                    return 0;

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
