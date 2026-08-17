using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Relational dining layout. The table is the primary anchor, required benches occupy opposite
    /// long sides, and optional head chairs occupy opposite ends. Exact relative seat regions still
    /// pass through DecorationPlacementResolver, preserving shared exclusion/collision semantics.
    /// </summary>
    public static class DiningSceneResolver
    {
        private const int SeatGap = 5;
        private const int RoomEdgeReserve = 8;

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements) =>
            TryResolve(in space, in context, exclusions, DiningLongAxis.Auto, out placements);

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            DiningLongAxis longAxis,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!IsCompatible(in space, in context, longAxis))
                return false;

            DecorationSceneSlot[] slots = DiningSceneDefinition.CreateSlots();
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context,
                    DiningSceneDefinition.SceneId,
                    slots,
                    DiningSceneDefinition.OptionalSeatBudget(in context),
                    out DecorationSceneSlot[] ordered))
                return false;

            bool alongX = longAxis == DiningLongAxis.X ||
                          (longAxis == DiningLongAxis.Auto && space.Bounds.Size.x >= space.Bounds.Size.z);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;

            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationSceneSlot slot = ordered[i];
                bool placed = slot.Family switch
                {
                    DecorationPropFamily.Table => TryTable(
                        in space, in context, exclusions, alongX, resolved, count, out resolved[count]),
                    DecorationPropFamily.Bench => TryBench(
                        in space, in context, in slot, exclusions, alongX,
                        in resolved[0], resolved, count, out resolved[count]),
                    DecorationPropFamily.Chair => TryChair(
                        in space, in context, in slot, exclusions, alongX,
                        in resolved[0], resolved, count, out resolved[count]),
                    _ => false,
                };

                if (!placed)
                {
                    if (slot.Required)
                        return false;
                    continue;
                }
                count++;
            }

            if (count < 3)
                return false;

            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++)
                placements[i] = resolved[i];
            return true;
        }

        private static bool IsCompatible(
            in DecorationSpace space,
            in DecorationContext context,
            DiningLongAxis axis) =>
            space.IsWellFormed &&
            context.IsWellFormed &&
            space.Kind == DecorationSpaceKind.DiningRoom &&
            context.SpaceKind == DecorationSpaceKind.DiningRoom &&
            (axis == DiningLongAxis.Auto || axis == DiningLongAxis.X || axis == DiningLongAxis.Z);

        private static bool TryTable(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            bool alongX,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor descriptor = DiningPropPresets.Table(in context);
            int roomLong = alongX ? space.Bounds.Size.x : space.Bounds.Size.z;
            int roomCross = alongX ? space.Bounds.Size.z : space.Bounds.Size.x;
            int longSize = math.min(descriptor.Size.x, math.max(24, roomLong - RoomEdgeReserve * 2));
            int crossSize = math.min(descriptor.Size.z, math.max(10, roomCross - 40));
            descriptor.Size = alongX
                ? new int3(longSize, descriptor.Size.y, crossSize)
                : new int3(crossSize, descriptor.Size.y, longSize);

            if (!TryCenteredSpace(in space, descriptor.Size, out DecorationSpace exact))
            {
                placement = default;
                return false;
            }

            if (!TryFloorPlacement(
                    in exact, in context, DiningSceneDefinition.TableSlot, in descriptor,
                    exclusions, occupied, occupiedCount, out placement))
                return false;

            placement.Facing = alongX ? new int3(1, 0, 0) : new int3(0, 0, 1);
            return true;
        }

        private static bool TryBench(
            in DecorationSpace space,
            in DecorationContext context,
            in DecorationSceneSlot slot,
            DecorationExclusion[] exclusions,
            bool alongX,
            in DecorationPlacement table,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor authored = DiningPropPresets.Bench(in context, slot.SlotId);
            int tableLong = alongX ? table.Bounds.Size.x : table.Bounds.Size.z;
            int benchLong = math.max(16, math.min(authored.Size.x, tableLong - 8));
            int3 size = alongX
                ? new int3(benchLong, authored.Size.y, authored.Size.z)
                : new int3(authored.Size.z, authored.Size.y, benchLong);
            bool negative = slot.SlotId == DiningSceneDefinition.BenchNegativeSlot;
            DecorationBounds desired = SideBounds(in table.Bounds, size, alongX, negative);
            int3 facing = alongX
                ? new int3(0, 0, negative ? 1 : -1)
                : new int3(negative ? 1 : -1, 0, 0);

            return TryRelative(
                in space, in context, in slot, in authored, in desired, facing,
                exclusions, occupied, occupiedCount, out placement);
        }

        private static bool TryChair(
            in DecorationSpace space,
            in DecorationContext context,
            in DecorationSceneSlot slot,
            DecorationExclusion[] exclusions,
            bool alongX,
            in DecorationPlacement table,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor authored = DiningPropPresets.Chair(in context, slot.SlotId);
            bool negative = slot.SlotId == DiningSceneDefinition.ChairNegativeSlot;
            int3 size = authored.Size;
            int3 min;
            int3 facing;

            if (alongX)
            {
                int centerZ = (table.Bounds.Min.z + table.Bounds.MaxExclusive.z) / 2;
                min = new int3(
                    negative ? table.Bounds.Min.x - SeatGap - size.x : table.Bounds.MaxExclusive.x + SeatGap,
                    space.Bounds.Min.y,
                    centerZ - size.z / 2);
                facing = new int3(negative ? 1 : -1, 0, 0);
            }
            else
            {
                int centerX = (table.Bounds.Min.x + table.Bounds.MaxExclusive.x) / 2;
                min = new int3(
                    centerX - size.x / 2,
                    space.Bounds.Min.y,
                    negative ? table.Bounds.Min.z - SeatGap - size.z : table.Bounds.MaxExclusive.z + SeatGap);
                facing = new int3(0, 0, negative ? 1 : -1);
            }

            DecorationBounds desired = Bounds(min, size);
            return TryRelative(
                in space, in context, in slot, in authored, in desired, facing,
                exclusions, occupied, occupiedCount, out placement);
        }

        private static bool TryRelative(
            in DecorationSpace parent,
            in DecorationContext context,
            in DecorationSceneSlot slot,
            in DecorationPropDescriptor authored,
            in DecorationBounds desired,
            int3 facing,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            placement = default;
            if (!parent.Bounds.Contains(in desired))
                return false;

            DecorationSpace exact = new DecorationSpace
            {
                SpaceId = parent.SpaceId,
                Kind = parent.Kind,
                Bounds = desired,
            };
            DecorationPropDescriptor floor = authored;
            floor.AcceptedSockets = DecorationSocketKind.Floor;
            floor.MountMode = DecorationMountMode.Floor;
            floor.Size = desired.Size;

            if (!TryFloorPlacement(
                    in exact, in context, slot.SlotId, in floor,
                    exclusions, occupied, occupiedCount, out placement))
                return false;

            placement.AnchorSlotId = slot.AnchorSlotId;
            placement.Facing = facing;
            return true;
        }

        private static bool TryFloorPlacement(
            in DecorationSpace exact,
            in DecorationContext context,
            uint slotId,
            in DecorationPropDescriptor descriptor,
            DecorationExclusion[] exclusions,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in exact);
            return DecorationPlacementResolver.TryPlace(
                in exact,
                in context,
                DiningSceneDefinition.SceneId,
                slotId,
                in descriptor,
                sockets,
                exclusions,
                occupied,
                occupiedCount,
                out placement);
        }

        private static bool TryCenteredSpace(
            in DecorationSpace parent,
            int3 size,
            out DecorationSpace exact)
        {
            exact = default;
            if (math.any(size <= 0) || math.any(size > parent.Bounds.Size))
                return false;

            int3 center = (parent.Bounds.Min + parent.Bounds.MaxExclusive) / 2;
            DecorationBounds bounds = Bounds(
                new int3(center.x - size.x / 2, parent.Bounds.Min.y, center.z - size.z / 2),
                size);
            if (!parent.Bounds.Contains(in bounds))
                return false;

            exact = new DecorationSpace { SpaceId = parent.SpaceId, Kind = parent.Kind, Bounds = bounds };
            return true;
        }

        private static DecorationBounds SideBounds(
            in DecorationBounds table,
            int3 size,
            bool alongX,
            bool negative)
        {
            if (alongX)
            {
                int centerX = (table.Min.x + table.MaxExclusive.x) / 2;
                int minZ = negative ? table.Min.z - SeatGap - size.z : table.MaxExclusive.z + SeatGap;
                return Bounds(new int3(centerX - size.x / 2, table.Min.y, minZ), size);
            }

            int centerZ = (table.Min.z + table.MaxExclusive.z) / 2;
            int minX = negative ? table.Min.x - SeatGap - size.x : table.MaxExclusive.x + SeatGap;
            return Bounds(new int3(minX, table.Min.y, centerZ - size.z / 2), size);
        }

        private static DecorationBounds Bounds(int3 min, int3 size) =>
            new DecorationBounds { Min = min, MaxExclusive = min + size };
    }
}
