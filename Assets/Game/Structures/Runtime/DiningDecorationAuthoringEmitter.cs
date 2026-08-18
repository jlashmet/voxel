using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Box-assembly compatibility authoring for the first dining families. Semantic placement remains
    /// independent; this layer only turns table/chair/bench placements into current voxel primitives.
    /// </summary>
    public static class DiningDecorationAuthoringEmitter
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
                case DecorationPropFamily.Table:
                    AuthorTable(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Bench:
                    AuthorBench(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Chair:
                    AuthorChair(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorTable(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int topThickness = math.min(3, math.max(2, size.y / 3));
            int topY = bounds.MaxExclusive.y - topThickness;
            int leg = math.min(4, math.max(2, math.min(size.x, size.z) / 5));
            int inset = math.min(5, math.max(2, math.min(size.x, size.z) / 4));
            int legHeight = math.max(1, topY - bounds.Min.y);

            authoring.Box(new int3(bounds.Min.x, topY, bounds.Min.z),
                new int3(size.x, topThickness, size.z), profile.PrimaryMaterial);

            int minX = bounds.Min.x + inset;
            int maxX = bounds.MaxExclusive.x - inset - leg;
            int minZ = bounds.Min.z + inset;
            int maxZ = bounds.MaxExclusive.z - inset - leg;
            AuthorLeg(authoring, minX, bounds.Min.y, minZ, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, maxX, bounds.Min.y, minZ, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, minX, bounds.Min.y, maxZ, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, maxX, bounds.Min.y, maxZ, leg, legHeight, profile.PrimaryMaterial);

            if (profile.Ornamentation >= 3)
            {
                int trim = profile.Ornamentation >= 6 ? 2 : 1;
                authoring.Box(
                    new int3(bounds.Min.x, math.max(bounds.Min.y, topY - trim), bounds.Min.z),
                    new int3(size.x, trim, math.min(2, size.z)),
                    profile.AccentMaterial);
                authoring.Box(
                    new int3(bounds.Min.x, math.max(bounds.Min.y, topY - trim), bounds.MaxExclusive.z - math.min(2, size.z)),
                    new int3(size.x, trim, math.min(2, size.z)),
                    profile.AccentMaterial);
            }
        }

        private static void AuthorBench(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int seatThickness = math.min(3, math.max(2, size.y / 3));
            int seatY = bounds.Min.y + math.max(3, size.y / 2);
            seatY = math.min(seatY, bounds.MaxExclusive.y - seatThickness);
            authoring.Box(new int3(bounds.Min.x, seatY, bounds.Min.z),
                new int3(size.x, seatThickness, size.z), profile.PrimaryMaterial);

            int legHeight = math.max(1, seatY - bounds.Min.y);
            if (math.abs(placement.Facing.z) == 1)
            {
                int legWidth = math.min(4, math.max(2, size.x / 8));
                int xA = bounds.Min.x + math.min(5, math.max(1, size.x / 8));
                int xB = bounds.MaxExclusive.x - math.min(5, math.max(1, size.x / 8)) - legWidth;
                AuthorLeg(authoring, xA, bounds.Min.y, bounds.Min.z + 1, legWidth, legHeight, profile.PrimaryMaterial);
                AuthorLeg(authoring, xB, bounds.Min.y, bounds.Min.z + 1, legWidth, legHeight, profile.PrimaryMaterial);
            }
            else
            {
                int legWidth = math.min(4, math.max(2, size.z / 8));
                int zA = bounds.Min.z + math.min(5, math.max(1, size.z / 8));
                int zB = bounds.MaxExclusive.z - math.min(5, math.max(1, size.z / 8)) - legWidth;
                AuthorLeg(authoring, bounds.Min.x + 1, bounds.Min.y, zA, legWidth, legHeight, profile.PrimaryMaterial);
                AuthorLeg(authoring, bounds.Min.x + 1, bounds.Min.y, zB, legWidth, legHeight, profile.PrimaryMaterial);
            }

            if (profile.Ornamentation >= 4 && bounds.MaxExclusive.y > seatY + seatThickness)
                AuthorBackrest(authoring, in bounds, placement.Facing, seatY + seatThickness,
                    profile.Ornamentation >= 6 ? 2 : 1, profile.AccentMaterial);
        }

        private static void AuthorChair(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int seatThickness = 2;
            int seatY = bounds.Min.y + math.min(5, math.max(3, size.y / 3));
            authoring.Box(new int3(bounds.Min.x, seatY, bounds.Min.z),
                new int3(size.x, seatThickness, size.z), profile.PrimaryMaterial);

            int leg = math.min(2, math.max(1, math.min(size.x, size.z) / 4));
            int legHeight = math.max(1, seatY - bounds.Min.y);
            AuthorLeg(authoring, bounds.Min.x, bounds.Min.y, bounds.Min.z, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, bounds.MaxExclusive.x - leg, bounds.Min.y, bounds.Min.z, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, bounds.Min.x, bounds.Min.y, bounds.MaxExclusive.z - leg, leg, legHeight, profile.PrimaryMaterial);
            AuthorLeg(authoring, bounds.MaxExclusive.x - leg, bounds.Min.y, bounds.MaxExclusive.z - leg, leg, legHeight, profile.PrimaryMaterial);

            int backY = seatY + seatThickness;
            if (backY < bounds.MaxExclusive.y)
                AuthorBackrest(authoring, in bounds, placement.Facing, backY,
                    profile.Ornamentation >= 5 ? 2 : 1,
                    profile.Ornamentation >= 3 ? profile.AccentMaterial : profile.PrimaryMaterial);
        }

        private static void AuthorBackrest(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int y,
            int thickness,
            byte material)
        {
            int height = bounds.MaxExclusive.y - y;
            if (height <= 0)
                return;

            if (facing.z > 0)
                authoring.Box(new int3(bounds.Min.x, y, bounds.Min.z), new int3(bounds.Size.x, height, thickness), material);
            else if (facing.z < 0)
                authoring.Box(new int3(bounds.Min.x, y, bounds.MaxExclusive.z - thickness), new int3(bounds.Size.x, height, thickness), material);
            else if (facing.x > 0)
                authoring.Box(new int3(bounds.Min.x, y, bounds.Min.z), new int3(thickness, height, bounds.Size.z), material);
            else
                authoring.Box(new int3(bounds.MaxExclusive.x - thickness, y, bounds.Min.z), new int3(thickness, height, bounds.Size.z), material);
        }

        private static void AuthorLeg(
            IStructureAuthoringSession authoring,
            int x,
            int y,
            int z,
            int size,
            int height,
            byte material) =>
            authoring.Box(new int3(x, y, z), new int3(size, height, size), material);
    }
}
