using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Compatibility authoring for chest, shelf, bookcase, crate, and barrel families.</summary>
    public static class StorageDecorationAuthoringEmitter
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (placements == null || !context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            for (int i = 0; i < placements.Length; i++)
            {
                if (!TryAuthor(authoring, in placements[i], in profile))
                    return false;
            }
            return true;
        }

        private static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            if (!placement.IsWellFormed || placement.Backend != DecorationRenderBackend.BoxAssembly)
                return false;

            switch (placement.Family)
            {
                case DecorationPropFamily.Chest:
                    AuthorChest(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Shelf:
                    AuthorShelf(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Bookcase:
                    AuthorBookcase(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Crate:
                    AuthorCrate(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Barrel:
                    AuthorBarrel(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorChest(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int lid = math.min(2, size.y);
            authoring.Box(bounds.Min, new int3(size.x, size.y - lid, size.z), profile.PrimaryMaterial);
            authoring.Box(
                new int3(bounds.Min.x, bounds.MaxExclusive.y - lid, bounds.Min.z),
                new int3(size.x, lid, size.z),
                profile.Ornamentation >= 2 ? profile.AccentMaterial : profile.PrimaryMaterial);

            int latchY = math.max(bounds.Min.y, bounds.MaxExclusive.y - lid - 2);
            AuthorFacingPlate(authoring, in bounds, placement.Facing, latchY, profile.AccentMaterial);
        }

        private static void AuthorShelf(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int count = math.clamp(size.y / 3, 2, 4);
            for (int shelf = 0; shelf < count; shelf++)
            {
                int y = bounds.Min.y + shelf * math.max(2, (size.y - 1) / math.max(1, count - 1));
                y = math.min(y, bounds.MaxExclusive.y - 1);
                authoring.Box(new int3(bounds.Min.x, y, bounds.Min.z), new int3(size.x, 1, size.z), profile.PrimaryMaterial);
            }

            AuthorWallSupports(authoring, in bounds, placement.Facing, profile.AccentMaterial, 1);
        }

        private static void AuthorBookcase(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int frame = math.min(2, math.max(1, math.min(size.x, size.z) / 3));
            int shelfCount = math.clamp(size.y / 8, 3, 5);

            AuthorWallBack(authoring, in bounds, placement.Facing, profile.PrimaryMaterial);
            AuthorWallSupports(authoring, in bounds, placement.Facing, profile.PrimaryMaterial, frame);

            for (int shelf = 0; shelf <= shelfCount; shelf++)
            {
                int y = bounds.Min.y + shelf * (size.y - 1) / shelfCount;
                y = math.min(y, bounds.MaxExclusive.y - 1);
                authoring.Box(new int3(bounds.Min.x, y, bounds.Min.z), new int3(size.x, 1, size.z),
                    shelf == shelfCount && profile.Ornamentation >= 3 ? profile.AccentMaterial : profile.PrimaryMaterial);
            }
        }

        private static void AuthorCrate(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            authoring.Box(bounds.Min, size, profile.PrimaryMaterial);

            int band = math.min(2, math.max(1, size.x / 5));
            authoring.Box(
                new int3(bounds.Min.x, bounds.MaxExclusive.y - band, bounds.Min.z),
                new int3(size.x, band, size.z),
                profile.AccentMaterial);
            if (size.x >= 6)
            {
                int centre = bounds.Min.x + size.x / 2;
                authoring.Box(
                    new int3(centre, bounds.Min.y, bounds.Min.z),
                    new int3(1, size.y, size.z),
                    profile.AccentMaterial);
            }
        }

        private static void AuthorBarrel(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int radius = math.max(2, math.min(size.x, size.z) / 2);
            int cx = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int cz = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            authoring.Cylinder(cx, bounds.Min.y, cz, radius, size.y, profile.PrimaryMaterial);

            if (size.y >= 6)
            {
                authoring.Disc(cx, bounds.Min.y + 2, cz, radius, profile.AccentMaterial);
                authoring.Disc(cx, bounds.MaxExclusive.y - 3, cz, radius, profile.AccentMaterial);
            }
        }

        private static void AuthorWallBack(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            byte material)
        {
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.Min.x : bounds.MaxExclusive.x - 1;
                authoring.Box(new int3(x, bounds.Min.y, bounds.Min.z), new int3(1, bounds.Size.y, bounds.Size.z), material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.Min.z : bounds.MaxExclusive.z - 1;
                authoring.Box(new int3(bounds.Min.x, bounds.Min.y, z), new int3(bounds.Size.x, bounds.Size.y, 1), material);
            }
        }

        private static void AuthorWallSupports(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            byte material,
            int thickness)
        {
            thickness = math.max(1, thickness);
            if (math.abs(facing.x) == 1)
            {
                authoring.Box(bounds.Min, new int3(bounds.Size.x, bounds.Size.y, thickness), material);
                authoring.Box(
                    new int3(bounds.Min.x, bounds.Min.y, bounds.MaxExclusive.z - thickness),
                    new int3(bounds.Size.x, bounds.Size.y, thickness), material);
            }
            else
            {
                authoring.Box(bounds.Min, new int3(thickness, bounds.Size.y, bounds.Size.z), material);
                authoring.Box(
                    new int3(bounds.MaxExclusive.x - thickness, bounds.Min.y, bounds.Min.z),
                    new int3(thickness, bounds.Size.y, bounds.Size.z), material);
            }
        }

        private static void AuthorFacingPlate(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int y,
            byte material)
        {
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.MaxExclusive.x - 1 : bounds.Min.x;
                authoring.Box(new int3(x, y, centerZ - 1), new int3(1, 2, 3), material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.MaxExclusive.z - 1 : bounds.Min.z;
                authoring.Box(new int3(centerX - 1, y, z), new int3(3, 2, 1), material);
            }
        }
    }
}
