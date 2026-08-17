using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Deterministic compact child-detail planner for tabletops and cabinet tops. Items use a fixed
    /// small grid so hundreds of clusters can be regenerated without running the full room placer.
    /// </summary>
    public static class DecorationClutterPlanner
    {
        private const int SurfaceInset = 2;
        private const int CellEdge = 8;
        private const int MaximumItems = 16;

        public static int RecommendedCount(in DecorationContext context)
        {
            if (!context.IsWellFormed || context.Condition == DecorationConditionTier.Ruined)
                return 0;
            if (context.Condition == DecorationConditionTier.Abandoned)
                return 1;

            int count = 2 + (int)context.Wealth;
            if (context.Condition == DecorationConditionTier.Pristine)
                count++;
            else if (context.Condition == DecorationConditionTier.Worn)
                count = math.max(1, count - 1);
            return math.min(MaximumItems, count);
        }

        public static bool TryPopulate(
            in DecorationSpace space,
            in DecorationContext context,
            uint sceneId,
            uint clusterId,
            in DecorationPlacement parent,
            int desiredCount,
            out DecorationClutterInstance[] items)
        {
            items = new DecorationClutterInstance[0];
            if (!space.IsWellFormed || !context.IsWellFormed || sceneId == 0 || clusterId == 0 ||
                !parent.IsWellFormed || desiredCount < 0 || !SupportsTopClutter(parent.Family))
                return false;
            if (desiredCount == 0)
                return true;

            int usableX = parent.Bounds.Size.x - SurfaceInset * 2;
            int usableZ = parent.Bounds.Size.z - SurfaceInset * 2;
            int topY = parent.Bounds.MaxExclusive.y;
            if (usableX < 2 || usableZ < 2 || topY >= space.Bounds.MaxExclusive.y)
                return false;

            int columns = math.max(1, usableX / CellEdge);
            int rows = math.max(1, usableZ / CellEdge);
            int count = math.min(math.min(desiredCount, columns * rows), MaximumItems);
            var resolved = new DecorationClutterInstance[count];
            uint clusterSeed = DecorationSeed.Derive(
                DecorationSeed.ForScene(in context, sceneId), clusterId);
            int firstKind = (int)(clusterSeed % DecorationClutterCatalog.KindCount);

            for (int i = 0; i < count; i++)
            {
                DecorationClutterKind kind =
                    (DecorationClutterKind)((firstKind + i) % DecorationClutterCatalog.KindCount);
                DecorationClutterDescriptor descriptor = DecorationClutterCatalog.Describe(
                    in context, sceneId, clusterId, i, kind);
                if (!descriptor.IsWellFormed)
                    return false;

                int column = i % columns;
                int row = i / columns;
                int cellMinX = parent.Bounds.Min.x + SurfaceInset + column * CellEdge;
                int cellMinZ = parent.Bounds.Min.z + SurfaceInset + row * CellEdge;
                int cellWidth = math.min(CellEdge, parent.Bounds.MaxExclusive.x - SurfaceInset - cellMinX);
                int cellDepth = math.min(CellEdge, parent.Bounds.MaxExclusive.z - SurfaceInset - cellMinZ);
                if (descriptor.Size.x > cellWidth || descriptor.Size.z > cellDepth)
                    return false;

                uint itemSeed = DecorationSeed.ForSlot(
                    in context, sceneId, DecorationClutterCatalog.StableSlotId(clusterId, i));
                int slackX = cellWidth - descriptor.Size.x;
                int slackZ = cellDepth - descriptor.Size.z;
                int offsetX = slackX == 0 ? 0 : (int)(itemSeed % (uint)(slackX + 1));
                int offsetZ = slackZ == 0 ? 0 :
                    (int)(DecorationSeed.Derive(itemSeed, 0xC1u) % (uint)(slackZ + 1));
                var bounds = new DecorationBounds
                {
                    Min = new int3(cellMinX + offsetX, topY, cellMinZ + offsetZ),
                    MaxExclusive = new int3(
                        cellMinX + offsetX + descriptor.Size.x,
                        topY + descriptor.Size.y,
                        cellMinZ + offsetZ + descriptor.Size.z),
                };
                if (!space.Bounds.Contains(in bounds))
                    return false;

                uint slotId = DecorationClutterCatalog.StableSlotId(clusterId, i);
                resolved[i] = new DecorationClutterInstance
                {
                    Id = GeneratedPropIds.Create(in context, sceneId, slotId),
                    ParentId = parent.Id,
                    Kind = kind,
                    Backend = descriptor.Backend,
                    Interaction = descriptor.Interaction,
                    Bounds = bounds,
                    Variant = descriptor.Variant,
                };
            }

            items = resolved;
            return true;
        }

        private static bool SupportsTopClutter(DecorationPropFamily family) =>
            family == DecorationPropFamily.Table ||
            family == DecorationPropFamily.Dresser ||
            family == DecorationPropFamily.Chest;
    }
}
