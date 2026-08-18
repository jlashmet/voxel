using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Breadth-first presentation for IDs 201-260. Rectilinear silhouettes author directly from the
    /// shared shape grammar; mesh/thin backends remain separate presentation requests.
    /// </summary>
    public static class DecorationExpansion260AuthoringEmitter
    {
        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context,
            DecorationRegionTheme region = DecorationRegionTheme.Unknown)
        {
            if (authoring == null || placements == null || !context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = region == DecorationRegionTheme.Unknown
                ? DecorationContextProfiles.ResolvePresentation(in context)
                : DecorationRegionContentPolicy.Presentation(in context, region);

            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement p = placements[i];
                if (!DecorationExpansion260Variants.IsExpansion260(p.Variant))
                    continue;
                if (p.Backend == DecorationRenderBackend.ProceduralMesh ||
                    p.Backend == DecorationRenderBackend.ThinSurface)
                    continue;

                DecorationExpansion260Kind kind = DecorationExpansion260Variants.KindOf(p.Variant);
                DecorationExpansion260Recipe recipe = DecorationExpansion260Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                AuthorShape(authoring, in p.Bounds, recipe.Shape, in profile);
            }
            return true;
        }

        private static void AuthorShape(
            IStructureAuthoringSession a,
            in DecorationBounds bounds,
            DecorationContentShape shape,
            in DecorationPresentationProfile profile)
        {
            int3 min = bounds.Min;
            int3 size = bounds.Size;
            byte primary = profile.PrimaryMaterial;
            byte secondary = profile.SecondaryMaterial;
            byte accent = profile.AccentMaterial;
            byte magic = profile.EmissiveMaterial;
            int top = math.max(2, size.y / 4);

            switch (shape)
            {
                case DecorationContentShape.WorkSurface:
                case DecorationContentShape.Counter:
                    a.Box(new int3(min.x, min.y + size.y - top, min.z), new int3(size.x, top, size.z), primary);
                    a.Box(min, new int3(2, size.y - top, 2), secondary);
                    a.Box(new int3(min.x + size.x - 2, min.y, min.z), new int3(2, size.y - top, 2), secondary);
                    a.Box(new int3(min.x, min.y, min.z + size.z - 2), new int3(2, size.y - top, 2), secondary);
                    a.Box(new int3(min.x + size.x - 2, min.y, min.z + size.z - 2), new int3(2, size.y - top, 2), secondary);
                    break;

                case DecorationContentShape.Rack:
                case DecorationContentShape.WallRack:
                    a.Box(min, new int3(2, size.y, size.z), primary);
                    a.Box(new int3(min.x + size.x - 2, min.y, min.z), new int3(2, size.y, size.z), primary);
                    for (int y = min.y + 2; y < min.y + size.y; y += math.max(4, size.y / 3))
                        a.Box(new int3(min.x, y, min.z), new int3(size.x, 2, size.z), secondary);
                    break;

                case DecorationContentShape.Pedestal:
                case DecorationContentShape.Monument:
                    a.Box(min, new int3(size.x, math.max(2, size.y / 5), size.z), secondary);
                    a.Box(new int3(min.x + size.x / 4, min.y + math.max(2, size.y / 5), min.z + size.z / 4),
                        new int3(math.max(2, size.x / 2), math.max(2, size.y * 3 / 5), math.max(2, size.z / 2)), primary);
                    a.Box(new int3(min.x + size.x / 3, min.y + size.y - 2, min.z + size.z / 3),
                        new int3(math.max(2, size.x / 3), 2, math.max(2, size.z / 3)), magic);
                    break;

                case DecorationContentShape.LampPost:
                case DecorationContentShape.Post:
                    a.Box(new int3(min.x + size.x / 2 - 1, min.y, min.z + size.z / 2 - 1),
                        new int3(2, math.max(2, size.y - 3), 2), primary);
                    a.Box(new int3(min.x + size.x / 2 - 2, min.y + size.y - 4, min.z + size.z / 2 - 2),
                        new int3(4, 4, 4), magic);
                    break;

                case DecorationContentShape.Cage:
                    a.Box(min, new int3(size.x, 1, size.z), secondary);
                    a.Box(new int3(min.x, min.y + size.y - 1, min.z), new int3(size.x, 1, size.z), secondary);
                    for (int x = min.x; x < min.x + size.x; x += math.max(3, size.x / 4))
                        a.Box(new int3(x, min.y, min.z), new int3(1, size.y, 1), primary);
                    break;

                default:
                    // Explicit box-first fallback: every box/voxel recipe remains visible even before
                    // a signature silhouette gets a curved/procedural upgrade.
                    a.Box(min, size, primary);
                    if (profile.Ornamentation > 1 && size.y >= 5)
                        a.Box(new int3(min.x + 1, min.y + size.y - 2, min.z),
                            new int3(math.max(1, size.x - 2), 1, 1), accent);
                    break;
            }
        }
    }
}
