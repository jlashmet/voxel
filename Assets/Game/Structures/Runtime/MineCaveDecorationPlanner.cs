using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Occupied-cave placement over semantic cave surfaces. Route infrastructure follows the
    /// EntryForward candidate; wall fixtures use wall/alcove candidates; portable storage uses floor/ledges.
    /// </summary>
    public static class MineCaveDecorationPlanner
    {
        public static bool TryPlan(
            in DecorationSpace space,
            in DecorationContext context,
            CaveDecorationCandidate[] candidates,
            DecorationExclusion[] exclusions,
            int instancesPerKind,
            out MineCaveDecorationInstance[] instances)
        {
            instances = new MineCaveDecorationInstance[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.Kind != DecorationSpaceKind.CaveChamber ||
                context.StructureKind != DecorationStructureKind.Cave ||
                context.SpaceKind != DecorationSpaceKind.CaveChamber ||
                candidates == null || instancesPerKind < 0 || instancesPerKind > 8)
                return false;
            if (instancesPerKind == 0)
                return true;

            int capacity = MineCaveDecorationCatalog.KindCount * instancesPerKind;
            var resolved = new MineCaveDecorationInstance[capacity];
            int count = 0;

            for (int kindValue = 0; kindValue < MineCaveDecorationCatalog.KindCount; kindValue++)
            {
                MineCaveDecorationKind kind = (MineCaveDecorationKind)kindValue;
                for (int ordinal = 0; ordinal < instancesPerKind; ordinal++)
                {
                    uint slotId = MineCaveDecorationCatalog.SlotId(kind, ordinal);
                    MineCaveDecorationDescriptor descriptor =
                        MineCaveDecorationCatalog.Describe(in context, kind, slotId);
                    if (!descriptor.IsWellFormed)
                        return false;

                    if (TryPlace(in space, in context, candidates, exclusions,
                            in descriptor, slotId, resolved, count, out resolved[count]))
                        count++;
                }
            }

            if (count == 0)
                return false;
            instances = new MineCaveDecorationInstance[count];
            for (int i = 0; i < count; i++) instances[i] = resolved[i];
            return true;
        }

        private static bool TryPlace(
            in DecorationSpace space,
            in DecorationContext context,
            CaveDecorationCandidate[] candidates,
            DecorationExclusion[] exclusions,
            in MineCaveDecorationDescriptor descriptor,
            uint slotId,
            MineCaveDecorationInstance[] occupied,
            int occupiedCount,
            out MineCaveDecorationInstance instance)
        {
            instance = default;
            uint seed = DecorationSeed.ForSlot(in context, MineCaveDecorationCatalog.SceneId, slotId);
            int start = candidates.Length == 0 ? 0 : (int)(seed % (uint)candidates.Length);

            for (int pass = 0; pass < candidates.Length; pass++)
            {
                CaveDecorationCandidate candidate = candidates[(start + pass) % candidates.Length];
                if (!Supports(descriptor.Mount, candidate.Kind))
                    continue;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    uint attemptSeed = DecorationSeed.Derive(seed, (uint)(pass * 19 + attempt + 1));
                    if (!TryBounds(in space, in candidate.Socket, in descriptor, attemptSeed,
                            out DecorationBounds bounds, out int3 facing))
                        continue;
                    if (IntersectsExclusions(in bounds, descriptor.Kind, exclusions) ||
                        IntersectsOccupied(in bounds, occupied, occupiedCount))
                        continue;

                    instance = new MineCaveDecorationInstance
                    {
                        Id = GeneratedPropIds.Create(
                            in context, MineCaveDecorationCatalog.SceneId, slotId),
                        Kind = descriptor.Kind,
                        Backend = descriptor.Backend,
                        Interaction = descriptor.Interaction,
                        Bounds = bounds,
                        Facing = facing,
                        Variant = descriptor.Variant,
                    };
                    return true;
                }
            }
            return false;
        }

        private static bool Supports(MineCaveMountKind mount, CaveDecorationSurfaceKind kind)
        {
            switch (mount)
            {
                case MineCaveMountKind.Route:
                    return kind == CaveDecorationSurfaceKind.EntryForward;
                case MineCaveMountKind.Wall:
                    return kind == CaveDecorationSurfaceKind.Wall ||
                           kind == CaveDecorationSurfaceKind.Alcove;
                default:
                    return kind == CaveDecorationSurfaceKind.WalkableFloor ||
                           kind == CaveDecorationSurfaceKind.Ledge;
            }
        }

        private static bool TryBounds(
            in DecorationSpace space,
            in DecorationSocket support,
            in MineCaveDecorationDescriptor descriptor,
            uint seed,
            out DecorationBounds bounds,
            out int3 facing)
        {
            bounds = default;
            facing = support.Facing;
            switch (descriptor.Mount)
            {
                case MineCaveMountKind.Wall:
                    return TryWallBounds(in space, in support, descriptor.Size, seed, out bounds, out facing);
                case MineCaveMountKind.Route:
                    return TryRouteBounds(in space, in support, descriptor.Size, seed, out bounds, out facing);
                default:
                    facing = new int3(0, 1, 0);
                    return TryFloorBounds(in space, in support, descriptor.Size, seed, out bounds);
            }
        }

        private static bool TryFloorBounds(
            in DecorationSpace space,
            in DecorationSocket support,
            int3 size,
            uint seed,
            out DecorationBounds bounds)
        {
            bounds = default;
            if (size.x > support.Bounds.Size.x || size.z > support.Bounds.Size.z ||
                size.y > space.Bounds.Size.y)
                return false;
            int slackX = support.Bounds.Size.x - size.x;
            int slackZ = support.Bounds.Size.z - size.z;
            int offsetX = slackX == 0 ? 0 : (int)(seed % (uint)(slackX + 1));
            int offsetZ = slackZ == 0 ? 0 :
                (int)(DecorationSeed.Derive(seed, 0x31u) % (uint)(slackZ + 1));
            int3 min = new int3(
                support.Bounds.Min.x + offsetX,
                support.Bounds.Min.y,
                support.Bounds.Min.z + offsetZ);
            bounds = new DecorationBounds { Min = min, MaxExclusive = min + size };
            return space.Bounds.Contains(in bounds);
        }

        private static bool TryRouteBounds(
            in DecorationSpace space,
            in DecorationSocket route,
            int3 authoredSize,
            uint seed,
            out DecorationBounds bounds,
            out int3 facing)
        {
            bounds = default;
            facing = route.Facing;
            if (math.abs(facing.x) + math.abs(facing.z) != 1)
                return false;

            int3 size = math.abs(facing.x) == 1
                ? new int3(authoredSize.z, authoredSize.y, authoredSize.x)
                : authoredSize;
            if (size.x > route.Bounds.Size.x || size.z > route.Bounds.Size.z ||
                size.y > space.Bounds.Size.y)
                return false;

            int slackX = route.Bounds.Size.x - size.x;
            int slackZ = route.Bounds.Size.z - size.z;
            int offsetX = slackX == 0 ? 0 : (int)(seed % (uint)(slackX + 1));
            int offsetZ = slackZ == 0 ? 0 :
                (int)(DecorationSeed.Derive(seed, 0x41u) % (uint)(slackZ + 1));
            int3 min = new int3(
                route.Bounds.Min.x + offsetX,
                space.Bounds.Min.y,
                route.Bounds.Min.z + offsetZ);
            bounds = new DecorationBounds { Min = min, MaxExclusive = min + size };
            return space.Bounds.Contains(in bounds);
        }

        private static bool TryWallBounds(
            in DecorationSpace space,
            in DecorationSocket wall,
            int3 authoredSize,
            uint seed,
            out DecorationBounds bounds,
            out int3 facing)
        {
            bounds = default;
            facing = wall.Facing;
            if (math.abs(facing.x) + math.abs(facing.z) != 1)
                return false;

            int width = authoredSize.x;
            int height = authoredSize.y;
            int depth = authoredSize.z;
            if (height > wall.Bounds.Size.y)
                return false;
            int availableY = wall.Bounds.Size.y - height;
            int y = wall.Bounds.Min.y + (availableY == 0 ? 0 :
                (int)(DecorationSeed.Derive(seed, 0x51u) % (uint)(availableY + 1)));

            if (math.abs(facing.x) == 1)
            {
                if (width > wall.Bounds.Size.z || depth > space.Bounds.Size.x)
                    return false;
                int slack = wall.Bounds.Size.z - width;
                int offset = slack == 0 ? 0 : (int)(seed % (uint)(slack + 1));
                int minX = facing.x > 0 ? wall.Bounds.Min.x : wall.Bounds.MaxExclusive.x - depth;
                int minZ = wall.Bounds.Min.z + offset;
                bounds = new DecorationBounds
                {
                    Min = new int3(minX, y, minZ),
                    MaxExclusive = new int3(minX + depth, y + height, minZ + width),
                };
            }
            else
            {
                if (width > wall.Bounds.Size.x || depth > space.Bounds.Size.z)
                    return false;
                int slack = wall.Bounds.Size.x - width;
                int offset = slack == 0 ? 0 : (int)(seed % (uint)(slack + 1));
                int minX = wall.Bounds.Min.x + offset;
                int minZ = facing.z > 0 ? wall.Bounds.Min.z : wall.Bounds.MaxExclusive.z - depth;
                bounds = new DecorationBounds
                {
                    Min = new int3(minX, y, minZ),
                    MaxExclusive = new int3(minX + width, y + height, minZ + depth),
                };
            }
            return space.Bounds.Contains(in bounds);
        }

        private static bool IntersectsExclusions(
            in DecorationBounds bounds,
            MineCaveDecorationKind kind,
            DecorationExclusion[] exclusions)
        {
            if (exclusions == null) return false;
            bool infrastructure = kind == MineCaveDecorationKind.SupportBeam ||
                                  kind == MineCaveDecorationKind.Rail;
            for (int i = 0; i < exclusions.Length; i++)
            {
                DecorationExclusion exclusion = exclusions[i];
                if (!exclusion.IsWellFormed || !bounds.Overlaps(in exclusion.Bounds))
                    continue;
                if (infrastructure &&
                    (exclusion.Kind & DecorationExclusionKind.Hazard) == 0 &&
                    (exclusion.Kind & DecorationExclusionKind.Gameplay) == 0)
                    continue;
                return true;
            }
            return false;
        }

        private static bool IntersectsOccupied(
            in DecorationBounds bounds,
            MineCaveDecorationInstance[] occupied,
            int count)
        {
            if (occupied == null) return false;
            int safeCount = math.min(count, occupied.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (!occupied[i].Bounds.IsWellFormed)
                    continue;
                bool infrastructurePair =
                    (occupied[i].Kind == MineCaveDecorationKind.Rail &&
                     bounds.Size.y > 1) ||
                    (occupied[i].Kind == MineCaveDecorationKind.SupportBeam &&
                     bounds.Size.y == 1);
                if (!infrastructurePair && bounds.Overlaps(in occupied[i].Bounds))
                    return true;
            }
            return false;
        }
    }
}
