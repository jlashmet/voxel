using System.IO;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class MountainDragonStartupBakeAcceptanceTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte MountainMaterial = 1;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;
        private const int RegionVoxelEdgeLog2 = 9;
        private const int BrickEdgeLog2 = 3;
        private const int BrickEdgeMask = 7;
        private const int BricksPerRegionEdgeLog2 = 6;
        private const int VoxelsPerBrick = 512;
        private const int MixedCellBytes = 4;

        [Test]
        public void PreparedStartupBakeContainsMountainPathAndSupportedDragonAndExportsEvidence()
        {
            TextAsset bakeAsset = Resources.Load<TextAsset>(ShowcaseWorldBakeCodec.ResourcePath);
            Assert.That(bakeAsset, Is.Not.Null,
                "The prepared VoxelShowcase startup bake must be present before acceptance runs.");
            TextAsset manifestAsset = Resources.Load<TextAsset>(
                ShowcaseStartupBakeContract.ManifestResourcePath);
            Assert.That(manifestAsset, Is.Not.Null,
                "The startup bake provenance manifest must be generated with the bake.");

            Assert.DoesNotThrow(
                () => ShowcaseStartupBakeContract.Validate(bakeAsset.bytes, manifestAsset.text),
                "The checked/prepared startup image must match the current mountain content contract.");

            ShowcaseWorldBake bake = ShowcaseWorldBakeCodec.Deserialize(bakeAsset.bytes);
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);

            int3 mountainCore = spec.Origin + new int3(
                spec.CentreLocal,
                spec.MountainHeight / 2,
                spec.CentreLocal);
            AssertMaterial(bake, mountainCore, MountainMaterial,
                "The baked image must contain substantial mountain mass above ordinary terrain.");

            int3 pathBase = spec.Origin + new int3(
                spec.PathMinLocalX + 2,
                0,
                spec.FirstRampLocalZ + spec.PathWidth / 2);
            AssertMaterial(bake, pathBase, PathMaterial,
                "The baked image must contain the readable path entrance at ground level.");

            int firstTurnY = spec.PathRise;
            int firstTurnX = spec.PathMinLocalX + spec.PathRun - spec.PathWidth;
            int firstRampZ = spec.RampLocalZ(0);
            int secondRampZ = spec.RampLocalZ(firstTurnY);
            int firstTurnZMin = math.min(firstRampZ, secondRampZ);
            int firstTurnZSize = math.abs(secondRampZ - firstRampZ) + spec.PathWidth;
            int3 representativeTurn = spec.Origin + new int3(
                firstTurnX + spec.PathWidth / 2,
                firstTurnY,
                firstTurnZMin + firstTurnZSize / 2);
            AssertMaterial(bake, representativeTurn, PathMaterial,
                "A representative switchback landing must be present in the baked image.");

            int summitY = spec.Origin.y + spec.MountainHeight;
            int3 summitSupport = new int3(
                spec.Origin.x + spec.CentreLocal,
                summitY,
                spec.Origin.z + spec.CentreLocal);
            Assert.That(ReadMaterial(bake, summitSupport), Is.Not.EqualTo((byte)0),
                "The summit must remain physically supported directly below the dragon.");

            int3 summitPath = new int3(
                spec.SummitApproachWorldX,
                summitY,
                spec.SummitApproachWorldZ);
            AssertMaterial(bake, summitPath, PathMaterial,
                "The final ascent must reach a baked walking surface on the summit.");

            int3 dragonCentre = new int3(
                spec.Origin.x + spec.CentreLocal,
                spec.Origin.y + spec.MountainHeight + 1 + spec.PlaceholderSize / 2,
                spec.Origin.z + spec.CentreLocal);
            AssertMaterial(bake, dragonCentre, DragonMaterial,
                "The baked image must contain the summit dragon placeholder, not only source intent.");

            ExportPreparedBakeEvidence(bakeAsset.bytes, manifestAsset.text, in spec);
        }

        private static void AssertMaterial(
            ShowcaseWorldBake bake,
            int3 worldVoxel,
            byte expectedMaterial,
            string message)
        {
            Assert.That(ReadMaterial(bake, worldVoxel), Is.EqualTo(expectedMaterial),
                message + " Sample: " + worldVoxel + ".");
        }

        private static byte ReadMaterial(ShowcaseWorldBake bake, int3 worldVoxel)
        {
            int3 regionCoord = new int3(
                worldVoxel.x >> RegionVoxelEdgeLog2,
                worldVoxel.y >> RegionVoxelEdgeLog2,
                worldVoxel.z >> RegionVoxelEdgeLog2);
            int3 local = worldVoxel - new int3(
                regionCoord.x << RegionVoxelEdgeLog2,
                regionCoord.y << RegionVoxelEdgeLog2,
                regionCoord.z << RegionVoxelEdgeLog2);

            ShowcaseWorldBakedRegion region = default;
            bool found = false;
            for (int i = 0; i < bake.Regions.Count; i++)
            {
                if (!math.all(bake.Regions[i].Coord == regionCoord)) continue;
                region = bake.Regions[i];
                found = true;
                break;
            }

            Assert.That(found, Is.True,
                "Startup bake is missing the region containing required mountain voxel "
                + worldVoxel + ".");
            byte[] snapshot = ShowcaseWorldBakeCodec.DecodeRegionPayload(region);

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

        private static void ExportPreparedBakeEvidence(
            byte[] bakeBytes,
            string manifestText,
            in MountainLandmarkSpec spec)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string artifactDir = Path.Combine(projectRoot, "Artifacts", "SingleTest");
            Directory.CreateDirectory(artifactDir);

            File.WriteAllBytes(
                Path.Combine(artifactDir, "ShowcaseWorld.bytes"),
                bakeBytes);
            File.WriteAllText(
                Path.Combine(artifactDir, "ShowcaseWorld.manifest.txt"),
                manifestText);
            File.WriteAllText(
                Path.Combine(artifactDir, "mountain-startup-bake-acceptance.txt"),
                "payloadSha256=" + ShowcaseStartupBakeContract.ComputePayloadSha256(bakeBytes) + "\n"
                + "contentSignature=" + ShowcaseStartupBakeContract.RequiredContentSignature.ToString("X8") + "\n"
                + "mountainOrigin=" + spec.Origin + "\n"
                + "mountainRadius=" + spec.MountainRadius + "\n"
                + "mountainHeight=" + spec.MountainHeight + "\n"
                + "pathWidth=" + spec.PathWidth + "\n"
                + "switchbackCount=" + spec.SwitchbackCount + "\n"
                + "placeholderSize=" + spec.PlaceholderSize + "\n");
        }
    }
}
