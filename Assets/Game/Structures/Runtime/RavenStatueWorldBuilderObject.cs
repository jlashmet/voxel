using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable World Builder identity and placement adapter for the procedural raven sculpture.
    /// </summary>
    public static class RavenStatueWorldBuilderObject
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
            Size = RavenStatueAuthoring.LocalSize,
            Clearance = new int3(4, 4, 4),
            Variant = VariantId,
        };

        public static DecorationPlacement CreatePlacement(
            GeneratedPropId id,
            int sceneId,
            int slotId,
            int3 origin,
            int3 facing)
        {
            int3 min = origin + RavenStatueAuthoring.LocalMin;
            return new DecorationPlacement
            {
                Id = id,
                SceneId = sceneId,
                SlotId = slotId,
                Family = DecorationPropFamily.Fountain,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.VoxelStamp,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible,
                Bounds = new DecorationBounds
                {
                    Min = min,
                    MaxExclusive = min + RavenStatueAuthoring.LocalSize,
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
            placement.Bounds.Min - RavenStatueAuthoring.LocalMin;
    }
}
