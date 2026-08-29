using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CastleLowerRiverWaterRepairPlayModeTests
    {
        private const uint ShowcaseSeed = 0x5EED1234u;

        [Test]
        public void ShowcaseMarkedReceivingBankBecomesWaterAndStopsBeforeOuterShore()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(256, 50, 376), ShowcaseSeed);
            int top = plan.Centre.y + plan.PlateauHeight;
            int riverY = top - CastleLayout.LowerRiverDepth;
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int channelZ = CastleLayout.LowerRiverZAt(in plan, streamX);
            int[] markedOffsets = { 39, 50, 55, 67, 79 };
            const int dryOuterShoreOffset = 84;
            const int cascadeOffset = 50;

            using IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 16,
                mixedBrickCapacity: 4096,
                changeJournalCapacity: 128);
            IStructureAuthoringSession authoring =
                VoxelEngineBootstrap.CreateStructureAuthoring(storage, 1_000_000);

            foreach (int offset in markedOffsets)
                SeedGrassShelf(authoring, streamX, riverY, channelZ + offset);
            SeedGrassShelf(authoring, streamX, riverY, channelZ + dryOuterShoreOffset);
            authoring.Set(
                streamX + 1,
                riverY + 2,
                channelZ + cascadeOffset,
                GameMaterialIds.Cascade);

            CastleLowerRiverWaterRepair.Repair(authoring, in plan);

            foreach (int offset in markedOffsets)
            {
                AssertMaterial(
                    storage.SurfaceQuery,
                    new int3(streamX, riverY, channelZ + offset),
                    GameMaterialIds.Water,
                    $"marked receiving-bank offset +{offset} must be water");
                AssertMaterial(
                    storage.SurfaceQuery,
                    new int3(streamX, riverY + 2, channelZ + offset),
                    GameMaterialIds.Empty,
                    $"grass shelf above marked offset +{offset} must be removed");
            }

            AssertMaterial(
                storage.SurfaceQuery,
                new int3(streamX, riverY + 2, channelZ + dryOuterShoreOffset),
                GameMaterialIds.Grass,
                "bounded repair must stop before the dry outer shore");
            AssertMaterial(
                storage.SurfaceQuery,
                new int3(streamX + 1, riverY + 2, channelZ + cascadeOffset),
                GameMaterialIds.Cascade,
                "waterfall cascade material must survive the receiving-bank repair");

            Assert.That(authoring.TotalVoxelsWritten, Is.LessThan(250_000),
                "repair must remain a bounded startup/content-authoring operation");
        }

        private static void SeedGrassShelf(
            IStructureAuthoringSession authoring,
            int x,
            int riverY,
            int z)
        {
            authoring.FillColumnBulk(
                x,
                riverY - 8,
                riverY + 2,
                z,
                GameMaterialIds.Dirt);
            authoring.Set(x, riverY + 2, z, GameMaterialIds.Grass);
        }

        private static void AssertMaterial(
            IVoxelSurfaceQuery query,
            int3 position,
            byte expected,
            string message)
        {
            if (expected == GameMaterialIds.Empty)
            {
                if (!query.TryRead(position, out VoxelCell emptyCell))
                    return;
                Assert.AreEqual(expected, emptyCell.BaseMaterialId, message);
                return;
            }

            Assert.IsTrue(query.TryRead(position, out VoxelCell cell), message);
            Assert.AreEqual(expected, cell.BaseMaterialId, message);
        }
    }
}
