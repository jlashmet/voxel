using System;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public readonly struct GuildSignatureMeshRequest
    {
        public readonly GeneratedPropId Id;
        public readonly GuildSignatureKind Kind;
        public readonly DecorationBounds Bounds;
        public readonly uint Variant;
        public GuildSignatureMeshRequest(GeneratedPropId id, GuildSignatureKind kind, DecorationBounds bounds, uint variant)
        { Id = id; Kind = kind; Bounds = bounds; Variant = variant; }
    }

    public readonly struct GuildSignatureThinRequest
    {
        public readonly GeneratedPropId Id;
        public readonly GuildSignatureKind Kind;
        public readonly DecorationBounds Bounds;
        public readonly uint Variant;
        public GuildSignatureThinRequest(GeneratedPropId id, GuildSignatureKind kind, DecorationBounds bounds, uint variant)
        { Id = id; Kind = kind; Bounds = bounds; Variant = variant; }
    }

    public static class GuildSignatureDecorationEmitter
    {
        public static bool TryAuthorGeometry(IStructureAuthoringSession authoring, DecorationPlacement[] placements,
            in DecorationContext context, DecorationRegionTheme region)
        {
            if (authoring == null || placements == null || !context.IsWellFormed) return false;
            DecorationPresentationProfile profile = DecorationRegionContentPolicy.Presentation(in context, region);
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!GuildSignatureVariants.IsGuildSignature(p.Variant)) continue;
                if (p.Backend == DecorationRenderBackend.ProceduralMesh || p.Backend == DecorationRenderBackend.ThinSurface) continue;
                GuildSignatureRecipe recipe = GuildSignatureDecorationCatalog.Recipe(GuildSignatureVariants.KindOf(p.Variant));
                if (!recipe.IsWellFormed) return false;
                Author(authoring, in p, in recipe, in profile);
            }
            return true;
        }

        public static GuildSignatureMeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return Array.Empty<GuildSignatureMeshRequest>();
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (GuildSignatureVariants.IsGuildSignature(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new GuildSignatureMeshRequest[count]; int n = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!GuildSignatureVariants.IsGuildSignature(p.Variant) || p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[n++] = new GuildSignatureMeshRequest(p.Id, GuildSignatureVariants.KindOf(p.Variant), p.Bounds, p.Variant);
            }
            return result;
        }

        public static GuildSignatureThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return Array.Empty<GuildSignatureThinRequest>();
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (GuildSignatureVariants.IsGuildSignature(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new GuildSignatureThinRequest[count]; int n = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!GuildSignatureVariants.IsGuildSignature(p.Variant) || p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[n++] = new GuildSignatureThinRequest(p.Id, GuildSignatureVariants.KindOf(p.Variant), p.Bounds, p.Variant);
            }
            return result;
        }

        private static void Author(IStructureAuthoringSession a, in DecorationPlacement p, in GuildSignatureRecipe recipe,
            in DecorationPresentationProfile profile)
        {
            int3 min = p.Bounds.Min; int3 size = p.Bounds.Size;
            byte primary = profile.PrimaryMaterial; byte secondary = profile.SecondaryMaterial;
            byte accent = profile.AccentMaterial; byte magic = profile.EmissiveMaterial;
            switch (recipe.Shape)
            {
                case DecorationContentShape.Pedestal:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 4), size.z), secondary);
                    a.Box(new int3(min.x + size.x / 4, min.y + size.y / 4, min.z + size.z / 4),
                        new int3(math.max(2,size.x/2), math.max(3,size.y*3/4), math.max(2,size.z/2)), primary);
                    if ((recipe.Interaction & DecorationInteractionFlags.EmitsLight) != 0)
                        a.Box(new int3(min.x + size.x / 3, min.y + size.y - 2, min.z + size.z / 3),
                            new int3(math.max(1,size.x/3), 2, math.max(1,size.z/3)), magic);
                    break;
                case DecorationContentShape.Rack:
                    a.Box(min, new int3(2, size.y, size.z), primary);
                    a.Box(new int3(min.x + size.x - 2, min.y, min.z), new int3(2, size.y, size.z), primary);
                    for (int y = min.y + 2; y < min.y + size.y; y += math.max(4,size.y/3))
                        a.Box(new int3(min.x,y,min.z), new int3(size.x,2,size.z), secondary);
                    break;
                case DecorationContentShape.Cage:
                    a.Box(min, new int3(size.x,1,size.z), secondary);
                    a.Box(new int3(min.x,min.y+size.y-1,min.z), new int3(size.x,1,size.z), secondary);
                    for (int x=min.x;x<min.x+size.x;x+=math.max(3,size.x/4))
                        a.Box(new int3(x,min.y,min.z), new int3(1,size.y,1), primary);
                    break;
                case DecorationContentShape.Hearth:
                    a.Box(min, size, primary);
                    a.Box(new int3(min.x+2,min.y+2,min.z), new int3(math.max(2,size.x-4),math.max(2,size.y-4),1), accent);
                    break;
                case DecorationContentShape.Post:
                    a.Box(new int3(min.x+size.x/2-1,min.y,min.z+size.z/2-1), new int3(2,size.y,2), primary);
                    break;
                default:
                    a.Box(min, size, primary);
                    if (profile.Ornamentation > 0 && size.y > 3)
                        a.Box(new int3(min.x+1,min.y+size.y-2,min.z), new int3(math.max(1,size.x-2),1,1), accent);
                    break;
            }
        }
    }
}
