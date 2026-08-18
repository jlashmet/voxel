using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public static class UtilityRoomSceneDescriptors
    {
        public static DecorationPropDescriptor Describe(
            UtilityRoomSceneKind kind,
            in DecorationContext context,
            uint sceneId,
            uint slotId)
        {
            switch (kind)
            {
                case UtilityRoomSceneKind.GuardPost:
                    if (slotId == 1) return MartialDisplayPresets.WeaponRack(in context, sceneId, slotId);
                    if (slotId == 2) return RoomScenePropPresets.Bench(in context, sceneId, slotId);
                    if (slotId == 3) return MartialDisplayPresets.ArmorDisplay(in context, sceneId, slotId);
                    if (slotId == 4) return RoomScenePropPresets.WallTorch(in context, sceneId, slotId);
                    return StorageContainerPresets.Crate(in context, sceneId, slotId);

                case UtilityRoomSceneKind.Kitchen:
                    if (slotId == 1) return RoomScenePropPresets.WorkTable(in context, sceneId, slotId, 30);
                    if (slotId == 2) return StorageFurniturePresets.Shelf(in context, sceneId, slotId);
                    if (slotId == 3) return StorageContainerPresets.Barrel(in context, sceneId, slotId);
                    if (slotId == 4) return StorageContainerPresets.Crate(in context, sceneId, slotId);
                    return LightingPropPresets.Candle(in context, sceneId, slotId);

                case UtilityRoomSceneKind.LibraryStudy:
                    if (slotId <= 2) return StorageFurniturePresets.Bookcase(in context, sceneId, slotId);
                    if (slotId == 3) return RoomScenePropPresets.WorkTable(in context, sceneId, slotId, 26);
                    if (slotId == 4) return RoomScenePropPresets.Chair(in context, sceneId, slotId);
                    if (slotId == 5) return LightingPropPresets.StandingLamp(in context, sceneId, slotId);
                    return RoomScenePropPresets.Painting(in context, sceneId, slotId);

                case UtilityRoomSceneKind.ChapelShrine:
                    if (slotId == 1) return RoomScenePropPresets.Altar(in context, sceneId, slotId);
                    if (slotId == 2 || slotId == 3) return LightingPropPresets.Candle(in context, sceneId, slotId);
                    if (slotId == 4) return TextileDisplayPresets.Banner(in context, sceneId, slotId);
                    return RoomScenePropPresets.WallTorch(in context, sceneId, slotId);

                case UtilityRoomSceneKind.Barracks:
                    if (slotId <= 2) return RoomScenePropPresets.Bed(in context, sceneId, slotId);
                    if (slotId == 3) return StorageFurniturePresets.Chest(in context, sceneId, slotId);
                    if (slotId == 4) return MartialDisplayPresets.WeaponRack(in context, sceneId, slotId);
                    return RoomScenePropPresets.Bench(in context, sceneId, slotId);

                case UtilityRoomSceneKind.ThroneRoom:
                    if (slotId == 1) return RoomScenePropPresets.Throne(in context, sceneId, slotId);
                    if (slotId == 2 || slotId == 3) return TextileDisplayPresets.Banner(in context, sceneId, slotId);
                    if (slotId == 4) return LightingPropPresets.Chandelier(in context, sceneId, slotId);
                    return MartialDisplayPresets.ArmorDisplay(in context, sceneId, slotId);

                case UtilityRoomSceneKind.Cellar:
                    if (slotId <= 2) return StorageContainerPresets.Barrel(in context, sceneId, slotId);
                    if (slotId == 3) return StorageContainerPresets.Crate(in context, sceneId, slotId);
                    if (slotId == 4) return StorageFurniturePresets.Shelf(in context, sceneId, slotId);
                    return LightingPropPresets.Candle(in context, sceneId, slotId);

                default:
                    if (slotId == 1) return StorageFurniturePresets.Chest(in context, sceneId, slotId);
                    if (slotId == 2) return StorageContainerPresets.Crate(in context, sceneId, slotId);
                    if (slotId == 3) return StorageContainerPresets.Barrel(in context, sceneId, slotId);
                    if (slotId == 4) return StorageFurniturePresets.Shelf(in context, sceneId, slotId);
                    return StorageFurniturePresets.Bookcase(in context, sceneId, slotId);
            }
        }
    }
}
