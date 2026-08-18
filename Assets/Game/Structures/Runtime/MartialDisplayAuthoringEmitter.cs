using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public static class MartialDisplayAuthoringEmitter
    {
        public static bool TryAuthor(IStructureAuthoringSession a, DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));
            if (placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!p.IsWellFormed || p.Family != DecorationPropFamily.WeaponRack ||
                    p.Backend != DecorationRenderBackend.BoxAssembly) return false;
                switch (MartialDisplayVariants.KindOf(p.Variant))
                {
                    case MartialDisplayKind.Shield: Shield(a, in p, in profile); break;
                    case MartialDisplayKind.Weapons: Weapons(a, in p, in profile); break;
                    case MartialDisplayKind.Armor: Armor(a, in p, in profile); break;
                    default: return false;
                }
            }
            return true;
        }

        private static void Shield(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = p.Bounds;
            a.Box(b.Min, b.Size, profile.PrimaryMaterial);
            int3 inset = new int3(math.min(1, b.Size.x / 4), 1, math.min(1, b.Size.z / 4));
            DecorationBounds inner = new DecorationBounds
            {
                Min = b.Min + inset,
                MaxExclusive = b.MaxExclusive - inset,
            };
            if (inner.IsWellFormed) a.Box(inner.Min, inner.Size, profile.AccentMaterial);
        }

        private static void Weapons(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = p.Bounds;
            int3 s = b.Size;
            if (math.abs(p.Facing.x) == 1)
            {
                int x = p.Facing.x > 0 ? b.Min.x : b.MaxExclusive.x - 1;
                a.Box(new int3(x, b.Min.y + s.y / 3, b.Min.z), new int3(1, 2, s.z), profile.PrimaryMaterial);
                a.Box(new int3(x, b.Min.y + s.y * 2 / 3, b.Min.z), new int3(1, 2, s.z), profile.PrimaryMaterial);
                int count = math.clamp(s.z / 5, 2, 6);
                for (int i = 0; i < count; i++)
                {
                    int z = math.min(b.MaxExclusive.z - 1, b.Min.z + 2 + i * math.max(2, (s.z - 4) / count));
                    a.Box(new int3(x, b.Min.y + 1, z), new int3(1, math.max(3, s.y - 2), 1), profile.AccentMaterial);
                }
            }
            else
            {
                int z = p.Facing.z > 0 ? b.Min.z : b.MaxExclusive.z - 1;
                a.Box(new int3(b.Min.x, b.Min.y + s.y / 3, z), new int3(s.x, 2, 1), profile.PrimaryMaterial);
                a.Box(new int3(b.Min.x, b.Min.y + s.y * 2 / 3, z), new int3(s.x, 2, 1), profile.PrimaryMaterial);
                int count = math.clamp(s.x / 5, 2, 6);
                for (int i = 0; i < count; i++)
                {
                    int x = math.min(b.MaxExclusive.x - 1, b.Min.x + 2 + i * math.max(2, (s.x - 4) / count));
                    a.Box(new int3(x, b.Min.y + 1, z), new int3(1, math.max(3, s.y - 2), 1), profile.AccentMaterial);
                }
            }
        }

        private static void Armor(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = p.Bounds;
            int3 s = b.Size;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int pedestal = math.min(3, math.max(1, s.y / 8));
            int torsoY = b.Min.y + pedestal + 3;
            int torsoH = math.max(5, s.y / 3);
            int torsoW = math.max(4, s.x * 2 / 3);
            int torsoD = math.max(3, s.z * 2 / 3);
            a.Box(b.Min, new int3(s.x, pedestal, s.z), profile.PrimaryMaterial);
            a.Box(new int3(cx - 1, b.Min.y + pedestal, cz - 1),
                new int3(2, math.max(2, s.y - pedestal - 3), 2), profile.PrimaryMaterial);
            a.Box(new int3(cx - torsoW / 2, torsoY, cz - torsoD / 2),
                new int3(torsoW, torsoH, torsoD), profile.AccentMaterial);
            a.Box(new int3(cx - torsoW / 2 - 2, torsoY + torsoH - 2, cz - 1),
                new int3(torsoW + 4, 2, 2), profile.AccentMaterial);
            int headY = math.min(b.MaxExclusive.y - 4, torsoY + torsoH + 1);
            a.Box(new int3(cx - 2, headY, cz - 2), new int3(4, 4, 4), profile.AccentMaterial);
        }
    }
}
