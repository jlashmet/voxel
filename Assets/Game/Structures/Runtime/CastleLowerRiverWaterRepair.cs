using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Finishes the lower river where the waterfall pool drains into it.
    ///
    /// The site pass owns the broad gorge and its 42-voxel core channel. Around the waterfall
    /// receiver that left the north bank rising through the water footprint, so a wide grass shelf
    /// could sit in front of the pool. This bounded pass extends only that receiving side, leaving
    /// the south bank and the outer ten voxels of the gorge untouched.
    /// </summary>
    public static class CastleLowerRiverWaterRepair
    {
        private const int ExistingWaterHalfWidth = 42;
        private const int ReceivingWaterHalfWidth = 80;
        private const int ReceivingHalfSpanX = 120;
        private const int OuterWaterRise = 4;

        public static void Repair(IStructureAuthoringSession authoring, in CastlePlan plan)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));

            int top = plan.Centre.y + plan.PlateauHeight;
            int riverY = top - CastleLayout.LowerRiverDepth;
            int streamX = CastleLayout.WaterfallStreamX(in plan);
            int addedWidth = ReceivingWaterHalfWidth - ExistingWaterHalfWidth;

            for (int x = streamX - ReceivingHalfSpanX;
                 x <= streamX + ReceivingHalfSpanX;
                 x++)
            {
                int channelZ = CastleLayout.LowerRiverZAt(in plan, x);
                for (int dz = ExistingWaterHalfWidth + 1;
                     dz <= ReceivingWaterHalfWidth;
                     dz++)
                {
                    int z = channelZ + dz;

                    // The old channel reaches riverY-6 at dz=42. Continue that shallow bank
                    // outward rather than restarting the cross-section and creating a trench seam.
                    int bed = riverY - 6
                            + (int)math.round(
                                (dz - ExistingWaterHalfWidth) * OuterWaterRise
                                / (float)addedWidth);

                    // This footprint is outside the castle walls and is explicitly the waterfall
                    // receiving bank. Remove the old terrain column above the waterline, but keep
                    // cascade voxels so a baked waterfall is not erased by the compatibility repair.
                    for (int y = riverY + 1; y <= top + 8; y++)
                    {
                        byte material = authoring.Get(x, y, z);
                        if (material == GameMaterialIds.Empty
                            || material == GameMaterialIds.Water
                            || material == GameMaterialIds.Cascade)
                            continue;

                        authoring.Set(x, y, z, GameMaterialIds.Empty);
                    }

                    authoring.FillColumnBulk(
                        x,
                        bed,
                        riverY + 1,
                        z,
                        GameMaterialIds.Water);
                }
            }
        }
    }
}
