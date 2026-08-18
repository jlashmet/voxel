using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion320MeshRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion320Kind Kind;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public struct DecorationExpansion320ThinRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion320Kind Kind;
        public DecorationBounds Bounds;
        public uint Variant;
    }

    public static class DecorationExpansion320AuthoringEmitter
    {
        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context,
            DecorationRegionTheme region)
        {
            if (authoring == null || placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationRegionContentPolicy.Presentation(in context, region);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!DecorationExpansion320Variants.IsExpansion320(placement.Variant)) continue;
                if (placement.Backend == DecorationRenderBackend.ProceduralMesh || placement.Backend == DecorationRenderBackend.ThinSurface) continue;
                DecorationExpansion320Recipe recipe = DecorationExpansion320Catalog.Recipe(DecorationExpansion320Variants.KindOf(placement.Variant));
                if (!recipe.IsWellFormed) return false;
                AuthorShape(authoring, in placement, in recipe, in profile);
            }
            return true;
        }

        public static DecorationExpansion320MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion320MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion320Variants.IsExpansion320(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpansion320MeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion320Variants.IsExpansion320(p.Variant) || p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpansion320MeshRequest { Id = p.Id, Kind = DecorationExpansion320Variants.KindOf(p.Variant), Bounds = p.Bounds, Variant = p.Variant };
            }
            return result;
        }

        public static DecorationExpansion320ThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion320ThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion320Variants.IsExpansion320(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new DecorationExpansion320ThinRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion320Variants.IsExpansion320(p.Variant) || p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[output++] = new DecorationExpansion320ThinRequest { Id = p.Id, Kind = DecorationExpansion320Variants.KindOf(p.Variant), Bounds = p.Bounds, Variant = p.Variant };
            }
            return result;
        }

        private static void AuthorShape(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationExpansion320Recipe recipe, in DecorationPresentationProfile profile)
        {
            int3 min = p.Bounds.Min;
            int3 size = p.Bounds.Size;
            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial;
            byte accent = profile.AccentMaterial;
            byte magic = profile.EmissiveMaterial;

            switch (recipe.Kind)
            {
                case DecorationExpansion320Kind.Moonwell:
                    a.Cylinder(cx, min.y, cz, math.max(4, math.min(size.x, size.z) / 2), math.max(3, size.y), primary, math.max(2, math.min(size.x, size.z) / 3));
                    break;
                case DecorationExpansion320Kind.LivingRootArch:
                    a.Box(new int3(min.x, min.y, min.z), new int3(4, size.y, size.z), primary);
                    a.Box(new int3(min.x + size.x - 4, min.y, min.z), new int3(4, size.y, size.z), primary);
                    a.Box(new int3(min.x, min.y + size.y - 5, min.z), new int3(size.x, 5, size.z), primary);
                    break;
                case DecorationExpansion320Kind.PetrifiedMagicTree:
                    a.Cylinder(cx, min.y, cz, math.max(3, math.min(size.x, size.z) / 5), size.y, primary);
                    a.Box(new int3(min.x, min.y + size.y * 2 / 3, min.z), new int3(size.x, 3, size.z), accent);
                    break;
                case DecorationExpansion320Kind.SpiritLanternPlant:
                    a.Cylinder(cx, min.y, cz, math.max(1, size.x / 6), math.max(3, size.y * 2 / 3), primary);
                    a.Box(new int3(cx - 2, min.y + size.y * 2 / 3, cz - 2), new int3(4, 4, 4), magic);
                    break;
                default:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 4), size.z), primary);
                    a.Cylinder(cx, min.y + math.max(2, size.y / 4), cz,
                        math.max(2, math.min(size.x, size.z) / 4), math.max(2, size.y * 3 / 4),
                        (recipe.Interaction & DecorationInteractionFlags.EmitsLight) != 0 ? magic : accent);
                    break;
            }
        }
    }
}
