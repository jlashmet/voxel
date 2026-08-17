using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion300MeshRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion300Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public struct DecorationExpansion300ThinRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion300Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public static class DecorationExpansion300AuthoringEmitter
    {
        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (authoring == null || placements == null || !context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(p.Variant))
                    continue;
                if (p.Backend == DecorationRenderBackend.ProceduralMesh ||
                    p.Backend == DecorationRenderBackend.ThinSurface)
                    continue;

                DecorationExpansion300Kind kind = DecorationExpansion300Variants.KindOf(p.Variant);
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                AuthorShape(authoring, in p, in recipe, in profile);
            }
            return true;
        }

        public static DecorationExpansion300MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null)
                return new DecorationExpansion300MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion300Variants.IsExpansion300(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ProceduralMesh)
                    count++;

            var result = new DecorationExpansion300MeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;
                result[output++] = new DecorationExpansion300MeshRequest
                {
                    Id = p.Id,
                    Kind = DecorationExpansion300Variants.KindOf(p.Variant),
                    Bounds = p.Bounds,
                    Facing = p.Facing,
                    Variant = p.Variant,
                };
            }
            return result;
        }

        public static DecorationExpansion300ThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null)
                return new DecorationExpansion300ThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion300Variants.IsExpansion300(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ThinSurface)
                    count++;

            var result = new DecorationExpansion300ThinRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ThinSurface)
                    continue;
                result[output++] = new DecorationExpansion300ThinRequest
                {
                    Id = p.Id,
                    Kind = DecorationExpansion300Variants.KindOf(p.Variant),
                    Bounds = p.Bounds,
                    Facing = p.Facing,
                    Variant = p.Variant,
                };
            }
            return result;
        }

        private static void AuthorShape(
            IStructureAuthoringSession a,
            in DecorationPlacement placement,
            in DecorationExpansion300Recipe recipe,
            in DecorationPresentationProfile profile)
        {
            int3 min = placement.Bounds.Min;
            int3 size = placement.Bounds.Size;
            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial;
            byte accent = profile.AccentMaterial;

            switch (recipe.Shape)
            {
                case DecorationContentShape.Cage:
                    AuthorCage(a, min, size, accent);
                    break;
                case DecorationContentShape.Post:
                    a.Cylinder(cx, min.y, cz, math.max(2, math.min(size.x, size.z) / 3), size.y, primary);
                    break;
                case DecorationContentShape.Pedestal:
                case DecorationContentShape.Monument:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 4), size.z), primary);
                    a.Cylinder(cx, min.y + math.max(2, size.y / 4), cz,
                        math.max(2, math.min(size.x, size.z) / 4),
                        math.max(2, size.y * 3 / 4), accent);
                    break;
                case DecorationContentShape.Stack:
                    AuthorStack(a, min, size, primary, accent, placement.Variant);
                    break;
                case DecorationContentShape.Coffin:
                    a.Box(min, size, primary);
                    if (size.x > 4 && size.z > 4)
                        a.Box(new int3(min.x + 2, min.y + size.y - 2, min.z + 2),
                            new int3(size.x - 4, 2, size.z - 4), accent);
                    break;
                case DecorationContentShape.Rack:
                case DecorationContentShape.WallRack:
                    AuthorRack(a, min, size, primary, accent);
                    break;
                default:
                    AuthorFurniture(a, min, size, primary, accent);
                    break;
            }
        }

        private static void AuthorFurniture(
            IStructureAuthoringSession a, int3 min, int3 size, byte primary, byte accent)
        {
            int top = math.max(2, size.y / 4);
            a.Box(new int3(min.x, min.y + size.y - top, min.z), new int3(size.x, top, size.z), primary);
            int leg = math.max(1, math.min(2, math.min(size.x, size.z) / 4));
            int legHeight = math.max(1, size.y - top);
            a.Box(min, new int3(leg, legHeight, leg), accent);
            a.Box(new int3(min.x + size.x - leg, min.y, min.z), new int3(leg, legHeight, leg), accent);
            a.Box(new int3(min.x, min.y, min.z + size.z - leg), new int3(leg, legHeight, leg), accent);
            a.Box(new int3(min.x + size.x - leg, min.y, min.z + size.z - leg), new int3(leg, legHeight, leg), accent);
        }

        private static void AuthorRack(
            IStructureAuthoringSession a, int3 min, int3 size, byte primary, byte accent)
        {
            int thickness = math.max(1, math.min(2, size.z));
            a.Box(min, new int3(size.x, size.y, thickness), primary);
            int step = math.max(4, size.y / 4);
            for (int y = min.y + step; y < min.y + size.y; y += step)
                a.Box(new int3(min.x, y, min.z), new int3(size.x, 1, math.max(1, size.z)), accent);
        }

        private static void AuthorStack(
            IStructureAuthoringSession a, int3 min, int3 size, byte primary, byte accent, uint variant)
        {
            int layers = math.clamp(size.y / 3, 2, 5);
            int y = min.y;
            for (int i = 0; i < layers; i++)
            {
                int inset = (int)((variant >> (i * 2)) & 1u);
                int w = math.max(2, size.x - inset * 2);
                int d = math.max(2, size.z - inset * 2);
                int h = math.max(2, size.y / layers);
                a.Box(new int3(min.x + inset, y, min.z + inset), new int3(w, h, d),
                    (i & 1) == 0 ? primary : accent);
                y += h;
            }
        }

        private static void AuthorCage(IStructureAuthoringSession a, int3 min, int3 size, byte material)
        {
            int step = math.max(3, math.min(size.x, size.z) / 4);
            a.Box(min, new int3(size.x, 1, size.z), material);
            a.Box(new int3(min.x, min.y + size.y - 1, min.z), new int3(size.x, 1, size.z), material);
            for (int x = min.x; x < min.x + size.x; x += step)
            {
                a.Box(new int3(x, min.y, min.z), new int3(1, size.y, 1), material);
                a.Box(new int3(x, min.y, min.z + size.z - 1), new int3(1, size.y, 1), material);
            }
            for (int z = min.z; z < min.z + size.z; z += step)
            {
                a.Box(new int3(min.x, min.y, z), new int3(1, size.y, 1), material);
                a.Box(new int3(min.x + size.x - 1, min.y, z), new int3(1, size.y, 1), material);
            }
        }
    }
}
