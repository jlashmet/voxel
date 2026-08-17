using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Builds a constrained semantic sub-space near an existing anchor, then delegates actual
    /// collision/exclusion placement back to DecorationPlacementResolver. This adds composition
    /// without introducing another placement engine.
    /// </summary>
    public static class DecorationContentRelationalPlacement
    {
        public static bool TryPlaceFloorNearAnchor(
            in DecorationSpace space,
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            in DecorationPlacement anchor,
            int gap,
            int forwardDepth,
            int lateralRadius,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            placement = default;
            if (!space.IsWellFormed || !context.IsWellFormed || !anchor.IsWellFormed ||
                descriptor.MountMode != DecorationMountMode.Floor ||
                !descriptor.Accepts(DecorationSocketKind.Floor) ||
                gap < 0 || forwardDepth <= 0 || lateralRadius <= 0)
                return false;

            int3 facing = anchor.Facing;
            if (math.abs(facing.x) + math.abs(facing.z) != 1)
                return false;

            int centerX = (anchor.Bounds.Min.x + anchor.Bounds.MaxExclusive.x) / 2;
            int centerZ = (anchor.Bounds.Min.z + anchor.Bounds.MaxExclusive.z) / 2;
            int minX;
            int maxX;
            int minZ;
            int maxZ;

            if (facing.x > 0)
            {
                minX = anchor.Bounds.MaxExclusive.x + gap;
                maxX = math.min(space.Bounds.MaxExclusive.x, minX + forwardDepth);
                minZ = math.max(space.Bounds.Min.z, centerZ - lateralRadius);
                maxZ = math.min(space.Bounds.MaxExclusive.z, centerZ + lateralRadius);
            }
            else if (facing.x < 0)
            {
                maxX = anchor.Bounds.Min.x - gap;
                minX = math.max(space.Bounds.Min.x, maxX - forwardDepth);
                minZ = math.max(space.Bounds.Min.z, centerZ - lateralRadius);
                maxZ = math.min(space.Bounds.MaxExclusive.z, centerZ + lateralRadius);
            }
            else if (facing.z > 0)
            {
                minZ = anchor.Bounds.MaxExclusive.z + gap;
                maxZ = math.min(space.Bounds.MaxExclusive.z, minZ + forwardDepth);
                minX = math.max(space.Bounds.Min.x, centerX - lateralRadius);
                maxX = math.min(space.Bounds.MaxExclusive.x, centerX + lateralRadius);
            }
            else
            {
                maxZ = anchor.Bounds.Min.z - gap;
                minZ = math.max(space.Bounds.Min.z, maxZ - forwardDepth);
                minX = math.max(space.Bounds.Min.x, centerX - lateralRadius);
                maxX = math.min(space.Bounds.MaxExclusive.x, centerX + lateralRadius);
            }

            if (maxX <= minX || maxZ <= minZ)
                return false;

            var nearSpace = new DecorationSpace
            {
                SpaceId = space.SpaceId,
                Kind = space.Kind,
                Bounds = new DecorationBounds
                {
                    Min = new int3(minX, space.Bounds.Min.y, minZ),
                    MaxExclusive = new int3(maxX, space.Bounds.MaxExclusive.y, maxZ),
                },
            };
            if (!nearSpace.IsWellFormed ||
                descriptor.Size.x > nearSpace.Bounds.Size.x ||
                descriptor.Size.z > nearSpace.Bounds.Size.z)
                return false;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in nearSpace);
            return DecorationPlacementResolver.TryPlace(
                in nearSpace,
                in context,
                sceneId,
                slotId,
                in descriptor,
                sockets,
                exclusions,
                occupied,
                occupiedCount,
                out placement);
        }
    }
}
