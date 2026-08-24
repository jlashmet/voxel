using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// World-builder-facing preset for the SDF dragon statue. The coarse decoration family remains
    /// Fountain because that family already represents large floor-mounted civic/exterior ornaments;
    /// VariantId is the stable identity consumed by the voxel-stamp backend.
    /// </summary>
    public static class DragonStatueWorldBuilderObject
    {
        public const uint VariantId = 0x4452474Eu; // "DRGN"

        public static DecorationPropDescriptor Descriptor => new DecorationPropDescriptor
        {
            Family = DecorationPropFamily.Fountain,
            AcceptedSockets = DecorationSocketKind.Floor,
            MountMode = DecorationMountMode.Floor,
            Backend = DecorationRenderBackend.VoxelStamp,
            Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
            Size = DragonStatueAuthoring.LocalSize,
            Clearance = new int3(4, 4, 4),
            Variant = VariantId,
        };

        public static DecorationPlacement CreatePlacement(
            GeneratedPropId id,
            uint sceneId,
            uint slotId,
            int3 origin,
            int3 facing)
        {
            int3 min = origin + DragonStatueAuthoring.LocalMin;
            return new DecorationPlacement
            {
                Id = id,
                SceneId = sceneId,
                SlotId = slotId,
                AnchorSlotId = 0,
                SocketId = 0,
                Family = DecorationPropFamily.Fountain,
                Backend = DecorationRenderBackend.VoxelStamp,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Bounds = new DecorationBounds
                {
                    Min = min,
                    MaxExclusive = min + DragonStatueAuthoring.LocalSize,
                },
                Facing = facing,
                Variant = VariantId,
            };
        }

        public static bool IsDragon(in DecorationPlacement placement) =>
            placement.Backend == DecorationRenderBackend.VoxelStamp &&
            placement.Family == DecorationPropFamily.Fountain &&
            placement.Variant == VariantId;

        public static int3 ResolveAuthoringOrigin(in DecorationPlacement placement) =>
            placement.Bounds.Min - DragonStatueAuthoring.LocalMin;
    }
}
