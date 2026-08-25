using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// World Builder preset for the high-resolution raven sculpture. Fountain is the existing
    /// coarse family for large floor-mounted exterior ornament; VariantId gives this stamp a stable
    /// content identity without expanding the public family vocabulary.
    /// </summary>
    public static class RavenSculptureWorldBuilderObject
    {
        public const uint VariantId = 0x5241564Eu; // "RAVN"

        public static DecorationPropDescriptor Descriptor => new DecorationPropDescriptor
        {
            Family = DecorationPropFamily.Fountain,
            AcceptedSockets = DecorationSocketKind.Floor,
            MountMode = DecorationMountMode.Floor,
            Backend = DecorationRenderBackend.VoxelStamp,
            Interaction = DecorationInteractionFlags.BlocksNavigation |
                DecorationInteractionFlags.Destructible,
            Size = RavenSculptureAuthoring.LocalSize,
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
            int3 min = origin + RavenSculptureAuthoring.LocalMin;
            return new DecorationPlacement
            {
                Id = id,
                SceneId = sceneId,
                SlotId = slotId,
                AnchorSlotId = 0,
                SocketId = 0,
                Family = DecorationPropFamily.Fountain,
                Backend = DecorationRenderBackend.VoxelStamp,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                    DecorationInteractionFlags.Destructible,
                Bounds = new DecorationBounds
                {
                    Min = min,
                    MaxExclusive = min + RavenSculptureAuthoring.LocalSize,
                },
                Facing = facing,
                Variant = VariantId,
            };
        }

        public static bool IsRaven(in DecorationPlacement placement) =>
            placement.Backend == DecorationRenderBackend.VoxelStamp &&
            placement.Family == DecorationPropFamily.Fountain &&
            placement.Variant == VariantId;

        public static int3 ResolveAuthoringOrigin(in DecorationPlacement placement) =>
            placement.Bounds.Min - RavenSculptureAuthoring.LocalMin;
    }
}
