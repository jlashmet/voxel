using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public static class DiningPropPresets
    {
        public static DecorationPropDescriptor Table(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(
                in context, DiningSceneDefinition.SceneId, DiningSceneDefinition.TableSlot);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Table,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(
                    56 + wealth * 6 + style.SilhouetteBias * 2 + (int)(seed & 3u) * 4,
                    9 + math.max(0, style.SilhouetteBias),
                    14 + wealth + (int)((seed >> 3) & 1u) * 2),
                Clearance = new int3(4, 0, 4),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x7AB1E001u),
            };
        }

        public static DecorationPropDescriptor Bench(in DecorationContext context, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, DiningSceneDefinition.SceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bench,
                AcceptedSockets = DecorationSocketKind.BesideAnchor | DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.AnchorRelative,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(
                    44 + (int)(seed & 3u) * 4,
                    6 + math.max(0, style.SilhouetteBias),
                    6 + (int)((seed >> 3) & 1u)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBE4C4001u),
            };
        }

        public static DecorationPropDescriptor Chair(in DecorationContext context, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, DiningSceneDefinition.SceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chair,
                AcceptedSockets = DecorationSocketKind.BesideAnchor | DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.AnchorRelative,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Movable,
                Size = new int3(
                    7 + (wealth >= (int)DecorationWealthTier.Wealthy ? 1 : 0),
                    10 + math.max(0, style.SilhouetteBias) + (int)((seed >> 2) & 1u),
                    7 + (int)(seed & 1u)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC4A17001u),
            };
        }
    }

    public static class DiningSceneDefinition
    {
        public const uint SceneId = 0x44494E31u; // DIN1
        public const uint TableSlot = 1;
        public const uint BenchNegativeSlot = 2;
        public const uint BenchPositiveSlot = 3;
        public const uint ChairNegativeSlot = 4;
        public const uint ChairPositiveSlot = 5;

        public static DecorationSceneSlot[] CreateSlots() => new[]
        {
            new DecorationSceneSlot
            {
                SlotId = TableSlot,
                Family = DecorationPropFamily.Table,
                RequestedSocket = DecorationSocketKind.Floor,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = BenchNegativeSlot,
                Family = DecorationPropFamily.Bench,
                RequestedSocket = DecorationSocketKind.BesideAnchor,
                AnchorSlotId = TableSlot,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = BenchPositiveSlot,
                Family = DecorationPropFamily.Bench,
                RequestedSocket = DecorationSocketKind.BesideAnchor,
                AnchorSlotId = TableSlot,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = ChairNegativeSlot,
                Family = DecorationPropFamily.Chair,
                RequestedSocket = DecorationSocketKind.BesideAnchor,
                AnchorSlotId = TableSlot,
                Weight = 2,
                Required = false,
            },
            new DecorationSceneSlot
            {
                SlotId = ChairPositiveSlot,
                Family = DecorationPropFamily.Chair,
                RequestedSocket = DecorationSocketKind.BesideAnchor,
                AnchorSlotId = TableSlot,
                Weight = 2,
                Required = false,
            },
        };

        public static int OptionalSeatBudget(in DecorationContext context)
        {
            if (context.Condition <= DecorationConditionTier.Abandoned)
                return 0;
            if (context.Wealth >= DecorationWealthTier.Wealthy)
                return 2;
            if (context.Wealth >= DecorationWealthTier.Comfortable)
                return 1;
            return 0;
        }
    }

    /// <summary>
    /// Relational dining scene. The table is centered as the primary anchor; benches occupy opposite
    /// long sides and optional head chairs occupy opposite ends. Each exact anchor-relative strip is
    /// resolved through DecorationPlacementResolver so exclusions and occupied-clearance rules stay
    /// shared with every other decoration scene.
    /// </summary>
    public static class DiningSceneResolver
    {
        private const int SeatGap = 5;
        private const int RoomEdgeReserve = 8;

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.Kind != DecorationSpaceKind.DiningRoom ||
                context.SpaceKind != DecorationSpaceKind.DiningRoom)
                return false;

            DecorationSceneSlot[] slots = DiningSceneDefinition.CreateSlots();
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context,
                    DiningSceneDefinition.SceneId,
                    slots,
                    DiningSceneDefinition.OptionalSeatBudget(in context),
                    out DecorationSceneSlot[] orderedSlots))
                return false;

            var resolved = new DecorationPlacement[orderedSlots.Length];
            int resolvedCount = 0;
            bool longAlongX = space.Bounds.Size.x >= space.Bounds.Size.z;

            for (int i = 0; i < orderedSlots.Length; i++)
            {
                DecorationSceneSlot slot = orderedSlots[i];
                bool placed;
                switch (slot.Family)
                {
                    case DecorationPropFamily.Table:
                        placed = TryPlaceTable(
                            in space, in context, exclusions, longAlongX,
                            resolved, resolvedCount, out resolved[resolvedCount]);
                        break;
                    case DecorationPropFamily.Bench:
                        placed = TryPlaceBench(
                            in space, in context, in slot, exclusions, longAlongX,
                            in resolved[0], resolved, resolvedCount, out resolved[resolvedCount]);
                        break;
                    case DecorationPropFamily.Chair:
                        placed = TryPlaceChair(
                            in space, in context, in slot, exclusions, longAlongX,
                            in resolved[0], resolved, resolvedCount, out resolved[resolvedCount]);
                        break;
                    default:
                        placed = false;
                        break;
                }

                if (!placed)
                {
                    if (slot.Required)
                        return false;
                    continue;
                }
                resolvedCount++;
            }

            if (resolvedCount < 3)
                return false;

            if (resolvedCount == resolved.Length)
            {
                placements = resolved;
                return true;
            }

            placements = new DecorationPlacement[resolvedCount];
            for (int i = 0; i < resolvedCount; i++)
                placements[i] = resolved[i];
            return true;
        }

        private static bool TryPlaceTable(
            in DecorationSpace space,
            in DecorationContext context,
            DecorationExclusion[] exclusions,
            bool longAlongX,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor descriptor = DiningPropPresets.Table(in context);
            int maxLong = math.max(24, (longAlongX ? space.Bounds.Size.x : space.Bounds.Size.z) - RoomEdgeReserve * 2);
            int maxCross = math.max(10, (longAlongX ? space.Bounds.Size.z : space.Bounds.Size.x) - 40);
            int longSize = math.min(descriptor.Size.x, maxLong);
            int crossSize = math.min(descriptor.Size.z, maxCross);
            descriptor.Size = longAlongX
                ? new int3(longSize, descriptor.Size.y, crossSize)
                : new int3(crossSize, descriptor.Size.y, longSize);

            if (!TryCenteredExactSpace(in space, descriptor.Size, out DecorationSpace anchorSpace))
            {
                placement = default;
                return false;
            }

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in anchorSpace);
            if (!DecorationPlacementResolver.TryPlace(
                    in anchorSpace,
                    in context,
                    DiningSceneDefinition.SceneId,
                    DiningSceneDefinition.TableSlot,
                    in descriptor,
                    sockets,
                    exclusions,
                    occupied,
                    occupiedCount,
                    out placement))
                return false;

            placement.Facing = longAlongX ? new int3(1, 0, 0) : new int3(0, 0, 1);
            return true;
        }

        private static bool TryPlaceBench(
            in DecorationSpace space,
            in DecorationContext context,
            in DecorationSceneSlot slot,
            DecorationExclusion[] exclusions,
            bool longAlongX,
            in DecorationPlacement table,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor authored = DiningPropPresets.Bench(in context, slot.SlotId);
            int tableLong = longAlongX ? table.Bounds.Size.x : table.Bounds.Size.z;
            int benchLong = math.max(16, math.min(authored.Size.x, tableLong - 8));
            int benchCross = authored.Size.z;
            int3 size = longAlongX
                ? new int3(benchLong, authored.Size.y, benchCross)
                : new int3(benchCross, authored.Size.y, benchLong);
            bool negative = slot.SlotId == DiningSceneDefinition.BenchNegativeSlot;

            DecorationBounds desired = longAlongX
                ? SideBoundsAlongX(in table.Bounds, size, negative)
                : SideBoundsAlongZ(in table.Bounds, size, negative);
            int3 facing = longAlongX
                ? new int3(0, 0, negative ? 1 : -1)
                : new int3(negative ? 1 : -1, 0, 0);

            return TryPlaceExactRelative(
                in space, in context, in slot, in authored, in desired, facing,
                exclusions, occupied, occupiedCount, out placement);
        }

        private static bool TryPlaceChair(
            in DecorationSpace space,
            in DecorationContext context,
            in DecorationSceneSlot slot,
            DecorationExclusion[] exclusions,
            bool longAlongX,
            in DecorationPlacement table,
            DecorationPlacement[] occupied,
            int occupiedCount,
            out DecorationPlacement placement)
        {
            DecorationPropDescriptor authored = DiningPropPresets.Chair(in context, slot.SlotId);
            int3 size = authored.Size;
            bool negative = slot.SlotId == DiningSceneDefinition.ChairNegativeSlot;
            DecorationBounds desired;
            int3 facing;

            if (longAlongX)
            {
                int minX = negative
                    ? table.Bounds.Min.x - SeatGap - size.x
                    : table.Bounds.MaxExclusive.x + SeatGap;
                int centerZ = (table.Bounds.Min.z + table.Bounds.MaxExclusive.z) / 2;
                desired = BoundsAt(new int3(minX, space.Bounds.Min.y, centerZ - size.z / 2), size);
                facing = new int3(negative ? 1 : -1, 0, 0);
            }
            else
            {
                int minZ = negative
                    ? table.Bounds.Min.z - SeatGap - size.z
                    : table.Bounds.MaxExclusive.z + SeatGap;
                int centerX = (table.Bounds.Min.x + table.Bounds.MaxExclusive.x) / 2;
                desired = BoundsAt(new int3(centerX - size.x / 2, space.Bounds.Min.y, minZ), size);
                facing = new int3(0, 0, negative ? 1 : -1);
            }

            return TryPlaceExactRelative(
                in space, in context, in slot, in authored, in desired, facing,
                exclusions, occupied, occupiedCount, out placement);
        }

        private static bool TryPlaceExactRelative(
            in DecorationSpace parentSpace,
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
            if (!parentSpace.Bounds.Contains(in desired))
                return false;

            DecorationSpace exactSpace = new DecorationSpace
            {
                SpaceId = parentSpace.SpaceId,
                Kind = parentSpace.Kind,
                Bounds = desired,
            };
            DecorationPropDescriptor floorDescriptor = authored;
            floorDescriptor.AcceptedSockets = DecorationSocketKind.Floor;
            floorDescriptor.MountMode = DecorationMountMode.Floor;
            floorDescriptor.Size = desired.Size;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in exactSpace);

            if (!DecorationPlacementResolver.TryPlace(
                    in exactSpace,
                    in context,
                    DiningSceneDefinition.SceneId,
                    slot.SlotId,
                    in floorDescriptor,
                    sockets,
                    exclusions,
                    occupied,
                    occupiedCount,
                    out placement))
                return false;

            placement.AnchorSlotId = slot.AnchorSlotId;
            placement.Facing = facing;
            return true;
        }

        private static bool TryCenteredExactSpace(
            in DecorationSpace parent,
            int3 size,
            out DecorationSpace exact)
        {
            exact = default;
            if (math.any(size <= 0) || math.any(size > parent.Bounds.Size))
                return false;

            int3 center = (parent.Bounds.Min + parent.Bounds.MaxExclusive) / 2;
            int3 min = new int3(
                center.x - size.x / 2,
                parent.Bounds.Min.y,
                center.z - size.z / 2);
            DecorationBounds bounds = BoundsAt(min, size);
            if (!parent.Bounds.Contains(in bounds))
                return false;

            exact = new DecorationSpace
            {
                SpaceId = parent.SpaceId,
                Kind = parent.Kind,
                Bounds = bounds,
            };
            return true;
        }

        private static DecorationBounds SideBoundsAlongX(
            in DecorationBounds table,
            int3 size,
            bool negative)
        {
            int centerX = (table.Min.x + table.MaxExclusive.x) / 2;
            int minZ = negative
                ? table.Min.z - SeatGap - size.z
                : table.MaxExclusive.z + SeatGap;
            return BoundsAt(new int3(centerX - size.x / 2, table.Min.y, minZ), size);
        }

        private static DecorationBounds SideBoundsAlongZ(
            in DecorationBounds table,
            int3 size,
            bool negative)
        {
            int centerZ = (table.Min.z + table.MaxExclusive.z) / 2;
            int minX = negative
                ? table.Min.x - SeatGap - size.x
                : table.MaxExclusive.x + SeatGap;
            return BoundsAt(new int3(minX, table.Min.y, centerZ - size.z / 2), size);
        }

        private static DecorationBounds BoundsAt(int3 min, int3 size) =>
            new DecorationBounds { Min = min, MaxExclusive = min + size };
    }
}
