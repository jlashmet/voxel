using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion360MeshRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion360Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public struct DecorationExpansion360ThinRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion360Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public static class DecorationExpansion360AuthoringEmitter
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
                if (!DecorationExpansion360Variants.IsExpansion360(p.Variant)) continue;
                if (p.Backend == DecorationRenderBackend.ProceduralMesh || p.Backend == DecorationRenderBackend.ThinSurface) continue;
                DecorationExpansion360Kind kind = DecorationExpansion360Variants.KindOf(p.Variant);
                DecorationExpansion360Recipe recipe = DecorationExpansion360Catalog.Recipe(kind);
                if (!recipe.IsWellFormed) return false;
                Author(authoring, in p, in profile, kind);
            }
            return true;
        }

        public static DecorationExpansion360MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion360MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion360Variants.IsExpansion360(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpansion360MeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion360Variants.IsExpansion360(p.Variant) || p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpansion360MeshRequest { Id = p.Id, Kind = DecorationExpansion360Variants.KindOf(p.Variant), Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant };
            }
            return result;
        }

        public static DecorationExpansion360ThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion360ThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion360Variants.IsExpansion360(placements[i].Variant) && placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new DecorationExpansion360ThinRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion360Variants.IsExpansion360(p.Variant) || p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[output++] = new DecorationExpansion360ThinRequest { Id = p.Id, Kind = DecorationExpansion360Variants.KindOf(p.Variant), Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant };
            }
            return result;
        }

        private static void Author(IStructureAuthoringSession a, in DecorationPlacement p,
            in DecorationPresentationProfile profile, DecorationExpansion360Kind kind)
        {
            int3 min = p.Bounds.Min;
            int3 size = p.Bounds.Size;
            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            byte primary = profile.PrimaryMaterial;
            byte accent = profile.AccentMaterial;
            byte magic = profile.EmissiveMaterial;

            switch (kind)
            {
                case DecorationExpansion360Kind.HolyWaterFont:
                case DecorationExpansion360Kind.RitualBasin:
                    a.Cylinder(cx, min.y, cz, math.max(3, math.min(size.x, size.z) / 2), math.max(3, size.y / 2), primary,
                        math.max(1, math.min(size.x, size.z) / 3));
                    break;
                case DecorationExpansion360Kind.SacredAltar:
                case DecorationExpansion360Kind.SideShrine:
                case DecorationExpansion360Kind.RelicPedestal:
                case DecorationExpansion360Kind.ReliquaryShrine:
                    a.Box(min, new int3(size.x, math.max(3, size.y / 3), size.z), primary);
                    a.Cylinder(cx, min.y + math.max(3, size.y / 3), cz, math.max(2, math.min(size.x, size.z) / 4),
                        math.max(3, size.y * 2 / 3), accent);
                    break;
                case DecorationExpansion360Kind.VotiveCandleStand:
                case DecorationExpansion360Kind.IncenseStand:
                case DecorationExpansion360Kind.BlessingBrazier:
                    a.Cylinder(cx, min.y, cz, math.max(2, math.min(size.x, size.z) / 3), size.y, primary);
                    a.Box(new int3(cx - 2, min.y + size.y - 3, cz - 2), new int3(4, 3, 4), magic);
                    break;
                case DecorationExpansion360Kind.DivineCrystalFocus:
                    a.Box(new int3(cx - 2, min.y, cz - 2), new int3(4, math.max(4, size.y / 2), 4), accent);
                    a.Cylinder(cx, min.y + math.max(4, size.y / 2), cz, math.max(3, size.x / 4), math.max(4, size.y / 2), magic);
                    break;
                case DecorationExpansion360Kind.OfferingChest:
                    a.Box(min, size, primary);
                    a.Box(new int3(cx - 1, min.y + size.y / 2, min.z), new int3(2, 3, 1), GameMaterialIds.Gold);
                    break;
                default:
                    a.Box(min, size, primary);
                    break;
            }
        }
    }
}
