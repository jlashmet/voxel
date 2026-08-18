using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpandedMeshRequest
    {
        public GeneratedPropId Id;
        public DecorationExpandedContentKind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public struct DecorationExpandedThinRequest
    {
        public GeneratedPropId Id;
        public DecorationExpandedContentKind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public static class DecorationExpansion200AuthoringEmitter
    {
        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (authoring == null || placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!DecorationExpandedContentVariants.IsExpanded(placement.Variant)) continue;
                if (placement.Backend != DecorationRenderBackend.BoxAssembly &&
                    placement.Backend != DecorationRenderBackend.VoxelStamp) continue;
                DecorationExpandedContentKind kind = DecorationExpandedContentVariants.KindOf(placement.Variant);
                DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe(kind);
                if (!recipe.IsWellFormed) return false;
                AuthorShape(authoring, in placement, in recipe, in profile);
            }
            return true;
        }

        public static DecorationExpandedMeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpandedMeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpandedContentVariants.IsExpanded(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpandedMeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpandedContentVariants.IsExpanded(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpandedMeshRequest
                {
                    Id = p.Id, Kind = DecorationExpandedContentVariants.KindOf(p.Variant),
                    Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant,
                };
            }
            return result;
        }

        public static DecorationExpandedThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpandedThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpandedContentVariants.IsExpanded(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new DecorationExpandedThinRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpandedContentVariants.IsExpanded(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[output++] = new DecorationExpandedThinRequest
                {
                    Id = p.Id, Kind = DecorationExpandedContentVariants.KindOf(p.Variant),
                    Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant,
                };
            }
            return result;
        }

        private static void AuthorShape(
            IStructureAuthoringSession a,
            in DecorationPlacement p,
            in DecorationExpandedContentRecipe recipe,
            in DecorationPresentationProfile profile)
        {
            int3 min = p.Bounds.Min;
            int3 size = p.Bounds.Size;
            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial;
            byte accent = profile.AccentMaterial;

            switch (recipe.Shape)
            {
                case DecorationContentShape.Hearth:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 3), size.z), GameMaterialIds.DarkStone);
                    a.Box(new int3(cx - 2, min.y + 2, cz - 2), new int3(4, math.max(2, size.y / 3), 4), profile.EmissiveMaterial);
                    break;
                case DecorationContentShape.Fountain:
                case DecorationContentShape.Well:
                    a.Cylinder(cx, min.y, cz, math.max(3, math.min(size.x, size.z) / 2), math.max(3, size.y / 3), primary, math.max(1, math.min(size.x, size.z) / 3));
                    break;
                case DecorationContentShape.Monument:
                case DecorationContentShape.Pedestal:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 4), size.z), primary);
                    a.Box(new int3(cx - math.max(1, size.x / 4), min.y + math.max(2, size.y / 4), cz - math.max(1, size.z / 4)),
                        new int3(math.max(2, size.x / 2), math.max(2, size.y * 3 / 4), math.max(2, size.z / 2)), accent);
                    break;
                case DecorationContentShape.Cage:
                    AuthorCage(a, min, size, primary);
                    break;
                case DecorationContentShape.Post:
                case DecorationContentShape.LampPost:
                    a.Box(new int3(cx - 1, min.y, cz - 1), new int3(2, size.y, 2), primary);
                    a.Box(new int3(min.x, min.y, min.z), new int3(size.x, 2, size.z), primary);
                    break;
                case DecorationContentShape.Coffin:
                    a.Box(min, size, primary);
                    if (size.y >= 4) a.Box(new int3(min.x + 1, min.y + size.y - 2, min.z + 1), new int3(math.max(1, size.x - 2), 2, math.max(1, size.z - 2)), accent);
                    break;
                case DecorationContentShape.Cart:
                    a.Box(new int3(min.x, min.y + 3, min.z), new int3(size.x, math.max(3, size.y / 2), size.z), primary);
                    a.Cylinder(min.x + 1, min.y, cz, math.max(2, size.y / 4), 2, accent);
                    a.Cylinder(min.x + size.x - 2, min.y, cz, math.max(2, size.y / 4), 2, accent);
                    break;
                case DecorationContentShape.Rack:
                case DecorationContentShape.WallRack:
                    a.Box(min, new int3(size.x, math.max(2, size.y), math.max(1, size.z)), primary);
                    for (int y = min.y + 3; y < min.y + size.y; y += math.max(4, size.y / 3))
                        a.Box(new int3(min.x, y, min.z), new int3(size.x, 1, size.z), accent);
                    break;
                case DecorationContentShape.Stack:
                    a.Box(min, size, primary);
                    if (size.x > 6 && size.z > 6)
                        a.Box(new int3(min.x + 2, min.y + size.y, min.z + 2), new int3(size.x - 4, math.max(2, size.y / 3), size.z - 4), accent);
                    break;
                default:
                    AuthorFurniture(a, min, size, primary, accent);
                    break;
            }
        }

        private static void AuthorFurniture(IStructureAuthoringSession a, int3 min, int3 size, byte primary, byte accent)
        {
            int top = math.max(2, size.y / 4);
            a.Box(new int3(min.x, min.y + size.y - top, min.z), new int3(size.x, top, size.z), primary);
            int legH = math.max(1, size.y - top);
            int leg = math.max(1, math.min(2, math.min(size.x, size.z) / 4));
            a.Box(min, new int3(leg, legH, leg), accent);
            a.Box(new int3(min.x + size.x - leg, min.y, min.z), new int3(leg, legH, leg), accent);
            a.Box(new int3(min.x, min.y, min.z + size.z - leg), new int3(leg, legH, leg), accent);
            a.Box(new int3(min.x + size.x - leg, min.y, min.z + size.z - leg), new int3(leg, legH, leg), accent);
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
