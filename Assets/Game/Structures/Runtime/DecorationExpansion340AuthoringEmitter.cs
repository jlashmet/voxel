using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct DecorationExpansion340MeshRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion340Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public struct DecorationExpansion340ThinRequest
    {
        public GeneratedPropId Id;
        public DecorationExpansion340Kind Kind;
        public DecorationBounds Bounds;
        public int3 Facing;
        public uint Variant;
    }

    public static class DecorationExpansion340AuthoringEmitter
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
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion340Variants.IsExpansion340(p.Variant)) continue;
                if (p.Backend == DecorationRenderBackend.ProceduralMesh ||
                    p.Backend == DecorationRenderBackend.ThinSurface) continue;
                DecorationExpansion340Kind kind = DecorationExpansion340Variants.KindOf(p.Variant);
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe(kind);
                if (!recipe.IsWellFormed) return false;
                Author(authoring, in p, in profile, kind);
            }
            return true;
        }

        public static DecorationExpansion340MeshRequest[] CollectMeshRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion340MeshRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion340Variants.IsExpansion340(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ProceduralMesh) count++;
            var result = new DecorationExpansion340MeshRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion340Variants.IsExpansion340(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ProceduralMesh) continue;
                result[output++] = new DecorationExpansion340MeshRequest
                {
                    Id = p.Id, Kind = DecorationExpansion340Variants.KindOf(p.Variant),
                    Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant,
                };
            }
            return result;
        }

        public static DecorationExpansion340ThinRequest[] CollectThinRequests(DecorationPlacement[] placements)
        {
            if (placements == null) return new DecorationExpansion340ThinRequest[0];
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
                if (DecorationExpansion340Variants.IsExpansion340(placements[i].Variant) &&
                    placements[i].Backend == DecorationRenderBackend.ThinSurface) count++;
            var result = new DecorationExpansion340ThinRequest[count];
            int output = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion340Variants.IsExpansion340(p.Variant) ||
                    p.Backend != DecorationRenderBackend.ThinSurface) continue;
                result[output++] = new DecorationExpansion340ThinRequest
                {
                    Id = p.Id, Kind = DecorationExpansion340Variants.KindOf(p.Variant),
                    Bounds = p.Bounds, Facing = p.Facing, Variant = p.Variant,
                };
            }
            return result;
        }

        private static void Author(
            IStructureAuthoringSession a,
            in DecorationPlacement p,
            in DecorationPresentationProfile profile,
            DecorationExpansion340Kind kind)
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
                case DecorationExpansion340Kind.PortcullisWinch:
                case DecorationExpansion340Kind.ChainWinch:
                    a.Box(min, new int3(size.x, math.max(3, size.y / 3), size.z), primary);
                    a.Cylinder(cx, min.y + math.max(3, size.y / 3), cz,
                        math.max(2, math.min(size.x, size.z) / 4),
                        math.max(3, size.y / 2), accent);
                    break;
                case DecorationExpansion340Kind.PuzzleLeverPedestal:
                case DecorationExpansion340Kind.RotatingStatuePedestal:
                    a.Box(min, new int3(size.x, math.max(3, size.y / 4), size.z), primary);
                    a.Cylinder(cx, min.y + math.max(3, size.y / 4), cz,
                        math.max(2, math.min(size.x, size.z) / 4),
                        math.max(4, size.y * 3 / 4), accent);
                    break;
                case DecorationExpansion340Kind.MagicSealDoor:
                    a.Box(min, size, GameMaterialIds.DarkStone);
                    a.Box(new int3(min.x + 2, min.y + 2, min.z),
                        new int3(math.max(2, size.x - 4), math.max(2, size.y - 4), math.max(1, size.z)), magic);
                    break;
                case DecorationExpansion340Kind.WardEmitterPillar:
                    a.Cylinder(cx, min.y, cz, math.max(2, math.min(size.x, size.z) / 3), size.y, primary);
                    a.Cylinder(cx, min.y + size.y - math.max(3, size.y / 4), cz,
                        math.max(2, math.min(size.x, size.z) / 2), math.max(3, size.y / 4), magic);
                    break;
                case DecorationExpansion340Kind.TreasureTrapChest:
                    a.Box(min, new int3(size.x, math.max(4, size.y * 2 / 3), size.z), primary);
                    a.Box(new int3(min.x, min.y + math.max(4, size.y * 2 / 3), min.z),
                        new int3(size.x, math.max(2, size.y / 3), size.z), accent);
                    a.Box(new int3(cx - 1, min.y + size.y / 2, min.z), new int3(2, 3, 1), magic);
                    break;
                case DecorationExpansion340Kind.FlameJetNozzle:
                case DecorationExpansion340Kind.PoisonVent:
                case DecorationExpansion340Kind.DartSlit:
                    a.Box(min, size, GameMaterialIds.DarkStone);
                    a.Box(new int3(cx - 1, min.y + size.y / 2, cz - 1), new int3(2, 2, 2), accent);
                    break;
                default:
                    a.Box(min, size, primary);
                    break;
            }
        }
    }
}
