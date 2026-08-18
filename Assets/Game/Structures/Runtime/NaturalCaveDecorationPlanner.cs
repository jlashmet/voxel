using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Dense environmental placement over cave floor/ledge/ceiling candidates. Details are optional:
    /// if one kind cannot fit around navigation/hazards the rest of the natural layer still resolves.
    /// </summary>
    public static class NaturalCaveDecorationPlanner
    {
        public static bool TryPlan(
            in DecorationSpace space,
            in DecorationContext context,
            CaveDecorationCandidate[] candidates,
            DecorationExclusion[] exclusions,
            int instancesPerKind,
            out NaturalCaveDecorationInstance[] instances)
        {
            instances = new NaturalCaveDecorationInstance[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.Kind != DecorationSpaceKind.CaveChamber ||
                context.StructureKind != DecorationStructureKind.Cave ||
                context.SpaceKind != DecorationSpaceKind.CaveChamber ||
                candidates == null || instancesPerKind < 0 || instancesPerKind > 8)
                return false;
            if (instancesPerKind == 0)
                return true;

            int capacity = NaturalCaveDecorationCatalog.KindCount * instancesPerKind;
            var resolved = new NaturalCaveDecorationInstance[capacity];
            int count = 0;

            for (int kindValue = 0; kindValue < NaturalCaveDecorationCatalog.KindCount; kindValue++)
            {
                NaturalCaveDecorationKind kind = (NaturalCaveDecorationKind)kindValue;
                for (int ordinal = 0; ordinal < instancesPerKind; ordinal++)
                {
                    uint slotId = NaturalCaveDecorationCatalog.SlotId(kind, ordinal);
                    NaturalCaveDecorationDescriptor descriptor =
                        NaturalCaveDecorationCatalog.Describe(in context, kind, slotId);
                    if (!descriptor.IsWellFormed)
                        return false;

                    if (TryPlace(in space, in context, candidates, exclusions,
                            in descriptor, slotId, resolved, count, out resolved[count]))
                        count++;
                }
            }

            if (count == 0)
                return false;
            instances = new NaturalCaveDecorationInstance[count];
            for (int i = 0; i < count; i++) instances[i] = resolved[i];
            return true;
        }

        private static bool TryPlace(
            in DecorationSpace space,
            in DecorationContext context,
            CaveDecorationCandidate[] candidates,
            DecorationExclusion[] exclusions,
            in NaturalCaveDecorationDescriptor descriptor,
            uint slotId,
            NaturalCaveDecorationInstance[] occupied,
            int occupiedCount,
            out NaturalCaveDecorationInstance instance)
        {
            instance = default;
            uint seed = DecorationSeed.ForSlot(in context, NaturalCaveDecorationCatalog.SceneId, slotId);
            int start = candidates.Length == 0 ? 0 : (int)(seed % (uint)candidates.Length);

            for (int pass = 0; pass < candidates.Length; pass++)
            {
                CaveDecorationCandidate candidate = candidates[(start + pass) % candidates.Length];
                if (!Supports(in descriptor, candidate.Kind))
                    continue;

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    uint attemptSeed = DecorationSeed.Derive(seed, (uint)(pass * 17 + attempt + 1));
                    if (!TryBounds(in space, in candidate.Socket, in descriptor, attemptSeed,
                            out DecorationBounds bounds, out int3 facing))
                        continue;
                    if (IntersectsExclusions(in bounds, exclusions) ||
                        IntersectsOccupied(in bounds, occupied, occupiedCount))
                        continue;

                    instance = new NaturalCaveDecorationInstance
                    {
                        Id = GeneratedPropIds.Create(
                            in context, NaturalCaveDecorationCatalog.SceneId, slotId),
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

        private static bool Supports(
            in NaturalCaveDecorationDescriptor descriptor,
            CaveDecorationSurfaceKind kind)
        {
            if (descriptor.CeilingMounted)
                return kind == CaveDecorationSurfaceKind.Ceiling;
            return kind == CaveDecorationSurfaceKind.WalkableFloor ||
                   kind == CaveDecorationSurfaceKind.Ledge;
        }

        private static bool TryBounds(
            in DecorationSpace space,
            in DecorationSocket support,
            in NaturalCaveDecorationDescriptor descriptor,
            uint seed,
            out DecorationBounds bounds,
            out int3 facing)
        {
            bounds = default;
            facing = descriptor.CeilingMounted ? new int3(0, -1, 0) : new int3(0, 1, 0);
            int width = support.Bounds.Size.x;
            int depth = support.Bounds.Size.z;
            if (descriptor.Size.x > width || descriptor.Size.z > depth ||
                descriptor.Size.y > space.Bounds.Size.y)
                return false;

            int slackX = width - descriptor.Size.x;
            int slackZ = depth - descriptor.Size.z;
            int offsetX = slackX == 0 ? 0 : (int)(seed % (uint)(slackX + 1));
            int offsetZ = slackZ == 0 ? 0 :
                (int)(DecorationSeed.Derive(seed, 0x2Du) % (uint)(slackZ + 1));
            int minY = descriptor.CeilingMounted
                ? space.Bounds.MaxExclusive.y - descriptor.Size.y
                : support.Bounds.Min.y;
            int3 min = new int3(
                support.Bounds.Min.x + offsetX,
                minY,
                support.Bounds.Min.z + offsetZ);
            bounds = new DecorationBounds { Min = min, MaxExclusive = min + descriptor.Size };
            return space.Bounds.Contains(in bounds);
        }

        private static bool IntersectsExclusions(
            in DecorationBounds bounds,
            DecorationExclusion[] exclusions)
        {
            if (exclusions == null) return false;
            for (int i = 0; i < exclusions.Length; i++)
                if (exclusions[i].IsWellFormed && bounds.Overlaps(in exclusions[i].Bounds))
                    return true;
            return false;
        }

        private static bool IntersectsOccupied(
            in DecorationBounds bounds,
            NaturalCaveDecorationInstance[] occupied,
            int count)
        {
            if (occupied == null) return false;
            int safeCount = math.min(count, occupied.Length);
            for (int i = 0; i < safeCount; i++)
                if (occupied[i].Bounds.IsWellFormed && bounds.Overlaps(in occupied[i].Bounds))
                    return true;
            return false;
        }
    }
}
