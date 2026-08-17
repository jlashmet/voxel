using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion380MeshRequest { public GeneratedPropId Id; public DecorationExpansion380Kind Kind; public DecorationBounds Bounds; public int3 Facing; public uint Variant; }

    public static class DecorationExpansion380AuthoringEmitter
    {
        public static bool TryAuthorGeometry(IStructureAuthoringSession authoring, DecorationPlacement[] placements,
            in DecorationContext context, DecorationRegionTheme region = DecorationRegionTheme.Unknown)
        {
            if (authoring == null || placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = region == DecorationRegionTheme.Unknown
                ? DecorationContextProfiles.ResolvePresentation(in context)
                : DecorationRegionContentPolicy.ResolvePresentation(region, in context);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion380Variants.IsExpansion380(p.Variant) || p.Backend == DecorationRenderBackend.ProceduralMesh) continue;
                DecorationExpansion380Kind kind = DecorationExpansion380Variants.KindOf(p.Variant);
                DecorationExpansion380Recipe recipe = DecorationExpansion380Catalog.Recipe(kind);
                if (!recipe.IsWellFormed) return false;
                Author(authoring, in p, in profile, kind);
            }
            return true;
        }

        public static DecorationExpansion380MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion380MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++) if (DecorationExpansion380Variants.IsExpansion380(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpansion380MeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion380Variants.IsExpansion380(p.Variant) || p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpansion380MeshRequest { Id = p.Id, Kind = DecorationExpansion380Variants.KindOf(p.Variant), Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant };
            }
            return result;
        }

        private static void Author(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationPresentationProfile profile, DecorationExpansion380Kind kind)
        {
            int3 min = p.Bounds.Min; int3 size = p.Bounds.Size; int cx = min.x + size.x / 2; int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial; byte accent = profile.AccentMaterial; byte magic = profile.EmissiveMaterial;
            switch (kind)
            {
                case DecorationExpansion380Kind.StudentSpellDesk:
                case DecorationExpansion380Kind.ApprenticeAlchemyDesk:
                case DecorationExpansion380Kind.ScriptoriumDesk:
                case DecorationExpansion380Kind.FacultyResearchDesk:
                    int top = math.max(2, size.y / 4);
                    a.Box(new int3(min.x, min.y + size.y - top, min.z), new int3(size.x, top, size.z), primary);
                    a.Box(min, new int3(2, size.y - top, 2), accent);
                    a.Box(new int3(min.x + size.x - 2, min.y, min.z + size.z - 2), new int3(2, size.y - top, 2), accent);
                    break;
                case DecorationExpansion380Kind.ForbiddenBookCage:
                    a.Box(min, new int3(size.x, 1, size.z), accent);
                    a.Box(new int3(min.x, min.y + size.y - 1, min.z), new int3(size.x, 1, size.z), accent);
                    for (int x = min.x; x < min.x + size.x; x += math.max(3, size.x / 5)) a.Box(new int3(x, min.y, min.z), new int3(1, size.y, 1), accent);
                    break;
                case DecorationExpansion380Kind.ArcaneArchiveChest:
                    a.Box(min, size, primary);
                    a.Box(new int3(cx - 1, min.y + size.y / 2, min.z), new int3(2, 3, 1), magic);
                    break;
                case DecorationExpansion380Kind.MagicalSpecimenCabinet:
                    a.Box(min, size, primary);
                    a.Box(new int3(min.x + 2, min.y + 2, min.z), new int3(math.max(2,size.x-4), math.max(2,size.y-4), 1), magic);
                    break;
                default:
                    a.Box(min, size, primary);
                    break;
            }
        }
    }
}
