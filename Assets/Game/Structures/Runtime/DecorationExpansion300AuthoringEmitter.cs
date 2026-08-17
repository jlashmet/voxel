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
            if (!context.IsWellFormed)
                return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            return TryAuthorGeometry(authoring, placements, in profile);
        }

        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context,
            DecorationRegionTheme region)
        {
            if (!context.IsWellFormed)
                return false;
            DecorationPresentationProfile profile = DecorationRegionContentPolicy.Presentation(in context, region);
            return TryAuthorGeometry(authoring, placements, in profile);
        }

        private static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationPresentationProfile profile)
        {
            if (authoring == null || placements == null)
                return false;

            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(placement.Variant))
                    continue;
                if (placement.Backend == DecorationRenderBackend.ProceduralMesh ||
                    placement.Backend == DecorationRenderBackend.ThinSurface)
                    continue;

                DecorationExpansion300Kind kind = DecorationExpansion300Variants.KindOf(placement.Variant);
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                AuthorShape(authoring, in placement, in recipe, in profile);
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
                DecorationPlacement placement = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(placement.Variant) ||
                    placement.Backend != DecorationRenderBackend.ProceduralMesh)
                    continue;
                result[output++] = new DecorationExpansion300MeshRequest
                {
                    Id = placement.Id,
                    Kind = DecorationExpansion300Variants.KindOf(placement.Variant),
                    Bounds = placement.Bounds,
                    Facing = placement.Facing,
                    Variant = placement.Variant,
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
                DecorationPlacement placement = placements[i];
                if (!DecorationExpansion300Variants.IsExpansion300(placement.Variant) ||
                    placement.Backend != DecorationRenderBackend.ThinSurface)
                    continue;
                result[output++] = new DecorationExpansion300ThinRequest
                {
                    Id = placement.Id,
                    Kind = DecorationExpansion300Variants.KindOf(placement.Variant),
                    Bounds = placement.Bounds,
                    Facing = placement.Facing,
                    Variant = placement.Variant,
                };
            }
            return result;
        }

        private static void AuthorShape(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationExpansion300Recipe recipe,
            in DecorationPresentationProfile profile)
        {
            int3 min = placement.Bounds.Min;
            int3 size = placement.Bounds.Size;
            int centerX = min.x + size.x / 2;
            int centerZ = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial;
            byte accent = profile.AccentMaterial;

            switch (recipe.Shape)
            {
                case DecorationContentShape.Cage:
                    AuthorCage(authoring, min, size, accent);
                    break;
                case DecorationContentShape.Post:
                    authoring.Cylinder(centerX, min.y, centerZ,
                        math.max(2, math.min(size.x, size.z) / 3), size.y, primary);
                    break;
                case DecorationContentShape.Pedestal:
                case DecorationContentShape.Monument:
                    authoring.Box(min, new int3(size.x, math.max(2, size.y / 4), size.z), primary);
                    authoring.Cylinder(centerX, min.y + math.max(2, size.y / 4), centerZ,
                        math.max(2, math.min(size.x, size.z) / 4),
                        math.max(2, size.y * 3 / 4), accent);
                    break;
                case DecorationContentShape.Stack:
                    AuthorStack(authoring, min, size, primary, accent, placement.Variant);
                    break;
                case DecorationContentShape.Coffin:
                    authoring.Box(min, size, primary);
                    if (size.x > 4 && size.z > 4)
                        authoring.Box(new int3(min.x + 2, min.y + size.y - 2, min.z + 2),
                            new int3(size.x - 4, 2, size.z - 4), accent);
                    break;
                case DecorationContentShape.Rack:
                case DecorationContentShape.WallRack:
                    AuthorRack(authoring, min, size, primary, accent);
                    break;
                default:
                    AuthorFurniture(authoring, min, size, primary, accent);
                    break;
            }
        }

        private static void AuthorFurniture(
            IStructureAuthoringSession authoring, int3 min, int3 size, byte primary, byte accent)
        {
            int top = math.max(2, size.y / 4);
            authoring.Box(new int3(min.x, min.y + size.y - top, min.z),
                new int3(size.x, top, size.z), primary);
            int leg = math.max(1, math.min(2, math.min(size.x, size.z) / 4));
            int legHeight = math.max(1, size.y - top);
            authoring.Box(min, new int3(leg, legHeight, leg), accent);
            authoring.Box(new int3(min.x + size.x - leg, min.y, min.z),
                new int3(leg, legHeight, leg), accent);
            authoring.Box(new int3(min.x, min.y, min.z + size.z - leg),
                new int3(leg, legHeight, leg), accent);
            authoring.Box(new int3(min.x + size.x - leg, min.y, min.z + size.z - leg),
                new int3(leg, legHeight, leg), accent);
        }

        private static void AuthorRack(
            IStructureAuthoringSession authoring, int3 min, int3 size, byte primary, byte accent)
        {
            int thickness = math.max(1, math.min(2, size.z));
            authoring.Box(min, new int3(size.x, size.y, thickness), primary);
            int step = math.max(4, size.y / 4);
            for (int y = min.y + step; y < min.y + size.y; y += step)
                authoring.Box(new int3(min.x, y, min.z), new int3(size.x, 1, math.max(1, size.z)), accent);
        }

        private static void AuthorStack(
            IStructureAuthoringSession authoring,
            int3 min,
            int3 size,
            byte primary,
            byte accent,
            uint variant)
        {
            int layers = math.clamp(size.y / 3, 2, 5);
            int y = min.y;
            for (int i = 0; i < layers; i++)
            {
                int inset = (int)((variant >> (i * 2)) & 1u);
                int width = math.max(2, size.x - inset * 2);
                int depth = math.max(2, size.z - inset * 2);
                int height = math.max(2, size.y / layers);
                authoring.Box(new int3(min.x + inset, y, min.z + inset),
                    new int3(width, height, depth),
                    (i & 1) == 0 ? primary : accent);
                y += height;
            }
        }

        private static void AuthorCage(
            IStructureAuthoringSession authoring, int3 min, int3 size, byte material)
        {
            int step = math.max(3, math.min(size.x, size.z) / 4);
            authoring.Box(min, new int3(size.x, 1, size.z), material);
            authoring.Box(new int3(min.x, min.y + size.y - 1, min.z),
                new int3(size.x, 1, size.z), material);
            for (int x = min.x; x < min.x + size.x; x += step)
            {
                authoring.Box(new int3(x, min.y, min.z), new int3(1, size.y, 1), material);
                authoring.Box(new int3(x, min.y, min.z + size.z - 1), new int3(1, size.y, 1), material);
            }
            for (int z = min.z; z < min.z + size.z; z += step)
            {
                authoring.Box(new int3(min.x, min.y, z), new int3(1, size.y, 1), material);
                authoring.Box(new int3(min.x + size.x - 1, min.y, z), new int3(1, size.y, 1), material);
            }
        }
    }
}
