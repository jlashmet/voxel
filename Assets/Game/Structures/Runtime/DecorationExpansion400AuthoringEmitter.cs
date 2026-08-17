using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion400MeshRequest { public GeneratedPropId Id; public DecorationExpansion400Kind Kind; public DecorationBounds Bounds; public int3 Facing; public uint Variant; }
    public struct DecorationExpansion400ThinRequest { public GeneratedPropId Id; public DecorationExpansion400Kind Kind; public DecorationBounds Bounds; public int3 Facing; public uint Variant; }

    public static class DecorationExpansion400AuthoringEmitter
    {
        public static bool TryAuthorGeometry(IStructureAuthoringSession authoring, DecorationPlacement[] placements, in DecorationContext context)
        {
            if (authoring == null || placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion400Variants.IsExpansion400(p.Variant) || p.Backend == DecorationRenderBackend.ProceduralMesh || p.Backend == DecorationRenderBackend.ThinSurface) continue;
                DecorationExpansion400Kind kind = DecorationExpansion400Variants.KindOf(p.Variant);
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe(kind);
                if (!recipe.IsWellFormed) return false;
                Author(authoring, in p, in profile, kind);
            }
            return true;
        }

        public static DecorationExpansion400MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion400MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++) if (DecorationExpansion400Variants.IsExpansion400(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpansion400MeshRequest[count]; int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion400Variants.IsExpansion400(p.Variant) || p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpansion400MeshRequest { Id = p.Id, Kind = DecorationExpansion400Variants.KindOf(p.Variant), Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant };
            }
            return result;
        }

        public static DecorationExpansion400ThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion400ThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++) if (DecorationExpansion400Variants.IsExpansion400(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new DecorationExpansion400ThinRequest[count]; int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion400Variants.IsExpansion400(p.Variant) || p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[output++] = new DecorationExpansion400ThinRequest { Id = p.Id, Kind = DecorationExpansion400Variants.KindOf(p.Variant), Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant };
            }
            return result;
        }

        private static void Author(IStructureAuthoringSession a, in DecorationPlacement p, in DecorationPresentationProfile profile, DecorationExpansion400Kind kind)
        {
            int3 min = p.Bounds.Min; int3 size = p.Bounds.Size; int cx = min.x + size.x / 2; int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial; byte accent = profile.AccentMaterial; byte magic = profile.EmissiveMaterial;
            switch (kind)
            {
                case DecorationExpansion400Kind.BrokenPortalFrame:
                    a.Box(min, new int3(4, size.y, size.z), GameMaterialIds.DarkStone);
                    a.Box(new int3(min.x + size.x - 4, min.y, min.z), new int3(4, size.y * 2 / 3, size.z), GameMaterialIds.DarkStone);
                    a.Box(new int3(min.x, min.y + size.y - 4, min.z), new int3(size.x * 2 / 3, 4, size.z), magic);
                    break;
                case DecorationExpansion400Kind.BrokenRunePillar:
                    a.Cylinder(cx, min.y, cz, math.max(2, math.min(size.x, size.z) / 3), math.max(4, size.y * 2 / 3), primary);
                    a.Box(new int3(cx - 2, min.y + size.y * 2 / 3, cz - 2), new int3(4, math.max(2, size.y / 5), 4), magic);
                    break;
                case DecorationExpansion400Kind.ShatteredMagicStatue:
                    a.Box(min, new int3(size.x, math.max(3, size.y / 4), size.z), primary);
                    a.Box(new int3(cx - 3, min.y + size.y / 4, cz - 3), new int3(6, math.max(4, size.y / 3), 6), accent);
                    break;
                case DecorationExpansion400Kind.PossessedFurniture:
                    a.Box(new int3(min.x, min.y + 2, min.z), new int3(size.x, math.max(3, size.y / 3), size.z), primary);
                    a.Box(new int3(min.x + 2, min.y, min.z + 2), new int3(2, math.max(2, size.y / 2), 2), accent);
                    break;
                case DecorationExpansion400Kind.SealedCursedChest:
                    a.Box(min, size, primary);
                    a.Box(new int3(cx - 2, min.y + size.y / 2, min.z), new int3(4, 4, 1), magic);
                    break;
                default:
                    a.Box(min, size, primary);
                    break;
            }
        }
    }
}
