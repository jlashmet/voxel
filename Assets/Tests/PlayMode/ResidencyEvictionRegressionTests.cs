using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Streaming.Runtime;
using VoxelEngine.Tiering.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ResidencyEvictionRegressionTests
    {
        [Test]
        public void BoundedScanEvictsHistoricalResidentLeftBehindPlayer()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            var storage = new RegionResidencyStore(in table, in pool);
            try
            {
                int3 historical = int3.zero;
                float3 player = new float3(5000f, 64f, 0f);
                int3 current = ResidencyManager.PositionToRegion(player);
                table.LoadRegion(historical);
                table.LoadRegion(current);
                storage.Refresh(in table, in pool);

                int cursor = 0;
                int unloadBlocks = (int)(ResidencyManager.GetUnloadRadius(DeviceTier.PC) / 0.8f);
                for (int pass = 0; pass < 4 && table.IsResident(historical); pass++)
                    ResidencyManager.EvictFarResidents(
                        player, unloadBlocks, storage, ref cursor, maxRegionsToScan: 8);

                Assert.False(table.IsResident(historical),
                    "A region left behind the player's current unload cube was never considered for eviction.");
                Assert.True(table.IsResident(current),
                    "Bounded historical eviction removed a region inside the unload radius.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
