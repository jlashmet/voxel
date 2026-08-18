using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Compatibility renderer for resolved decoration placements using the current structure-authoring
    /// primitives. Placement and scene logic remain backend-independent. Thin surfaces are emitted as
    /// one-voxel sheets here until the presentation layer gains a true sub-voxel surface backend.
    /// </summary>
    public static class DecorationStructureAuthoringEmitter
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements)
        {
            DecorationPresentationProfile profile = DecorationContextProfiles.Compatibility;
            return TryAuthor(authoring, placements, in profile);
        }

        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context)
        {
            if (!context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            return TryAuthor(authoring, placements, in profile);
        }

        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement)
        {
            DecorationPresentationProfile profile = DecorationContextProfiles.Compatibility;
            return TryAuthor(authoring, in placement, in profile);
        }

        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationContext context)
        {
            if (!context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            return TryAuthor(authoring, in placement, in profile);
        }

        private static bool TryAuthor(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationPresentationProfile profile)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (placements == null)
                return false;

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
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (!placement.IsWellFormed)
                return false;

            switch (placement.Backend)
            {
                case DecorationRenderBackend.BoxAssembly:
                    return TryAuthorBoxAssembly(authoring, in placement, in profile);
                case DecorationRenderBackend.ThinSurface:
                    return TryAuthorThinSurfaceCompatibility(authoring, in placement, in profile);
                case DecorationRenderBackend.VoxelStamp:
                case DecorationRenderBackend.ProceduralMesh:
                default:
                    return false;
            }
        }

        private static bool TryAuthorBoxAssembly(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            switch (placement.Family)
            {
                case DecorationPropFamily.Bed:
                    AuthorBed(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Dresser:
                    AuthorDresser(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.WallTorch:
                    AuthorWallTorch(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryAuthorThinSurfaceCompatibility(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            switch (placement.Family)
            {
                case DecorationPropFamily.Rug:
                    AuthorRug(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Painting:
                    AuthorPainting(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorBed(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int baseHeight = math.min(3, size.y);
            int mattressHeight = math.min(3, math.max(1, size.y - baseHeight));

            authoring.Box(
                bounds.Min,
                new int3(size.x, baseHeight, size.z),
                profile.PrimaryMaterial);

            int insetX = math.min(2, math.max(0, (size.x - 2) / 4));
            int insetZ = math.min(2, math.max(0, (size.z - 2) / 4));
            int mattressWidth = math.max(1, size.x - insetX * 2);
            int mattressDepth = math.max(1, size.z - insetZ * 2);
            authoring.Box(
                new int3(bounds.Min.x + insetX, bounds.Min.y + baseHeight, bounds.Min.z + insetZ),
                new int3(mattressWidth, mattressHeight, mattressDepth),
                profile.SoftMaterial);

            int headboardHeight = math.max(baseHeight + mattressHeight,
                math.min(size.y, 8 + profile.Ornamentation / 2 + (int)(placement.Variant & 3u)));
            int headboardThickness = profile.Family == DecorationStyleFamily.Courtly ? 3 : 2;
            AuthorWallBand(
                authoring,
                in bounds,
                placement.Facing,
                math.min(headboardThickness, math.min(size.x, size.z)),
                headboardHeight,
                profile.PrimaryMaterial);

            if (profile.UseBedPosts && size.y >= 8)
                AuthorBedPosts(authoring, in bounds, math.min(size.y, headboardHeight + 2), profile.AccentMaterial);

            if (profile.UseLuxuryTrim && size.y > baseHeight)
            {
                int trimY = math.min(bounds.MaxExclusive.y - 1, bounds.Min.y + baseHeight);
                AuthorFacingTrim(authoring, in bounds, placement.Facing, trimY, profile.AccentMaterial);
            }
        }

        private static void AuthorBedPosts(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int height,
            byte material)
        {
            int3 size = bounds.Size;
            int post = math.max(1, math.min(2, math.min(size.x, size.z) / 4));
            int maxX = bounds.MaxExclusive.x - post;
            int maxZ = bounds.MaxExclusive.z - post;
            authoring.Box(new int3(bounds.Min.x, bounds.Min.y, bounds.Min.z), new int3(post, height, post), material);
            authoring.Box(new int3(maxX, bounds.Min.y, bounds.Min.z), new int3(post, height, post), material);
            authoring.Box(new int3(bounds.Min.x, bounds.Min.y, maxZ), new int3(post, height, post), material);
            authoring.Box(new int3(maxX, bounds.Min.y, maxZ), new int3(post, height, post), material);
        }

        private static void AuthorDresser(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            authoring.Box(bounds.Min, size, profile.PrimaryMaterial);

            if (profile.Ornamentation > 0)
            {
                int trimHeight = math.min(profile.UseLuxuryTrim ? 2 : 1, size.y);
                authoring.Box(
                    new int3(bounds.Min.x, bounds.MaxExclusive.y - trimHeight, bounds.Min.z),
                    new int3(size.x, trimHeight, size.z),
                    profile.AccentMaterial);
            }

            int drawerCount = math.clamp(size.y / 6 + profile.Ornamentation / 4 - profile.DamageLevel / 2, 1, 5);
            for (int drawer = 0; drawer < drawerCount; drawer++)
            {
                int handleY = bounds.Min.y + 3 + drawer * math.max(3, (size.y - 4) / drawerCount);
                handleY = math.min(handleY, bounds.MaxExclusive.y - 2);
                AuthorFacingHandle(authoring, in bounds, placement.Facing, handleY, profile.AccentMaterial);
            }
        }

        private static void AuthorRug(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;

            if (profile.DamageLevel < 3 || size.x < 4)
            {
                authoring.Box(bounds.Min, size, profile.SoftMaterial);
                return;
            }

            // Abandoned/ruined rugs keep the semantic footprint but render as torn sections with a
            // deterministic gap instead of a pristine full sheet.
            int gap = math.min(2, math.max(1, size.x / 5));
            int left = math.max(1, (size.x - gap) / 2);
            int right = size.x - gap - left;
            authoring.Box(bounds.Min, new int3(left, size.y, size.z), profile.SoftMaterial);
            if (right > 0)
            {
                authoring.Box(
                    new int3(bounds.Min.x + left + gap, bounds.Min.y, bounds.Min.z),
                    new int3(right, size.y, size.z),
                    profile.SoftMaterial);
            }
        }

        private static void AuthorFacingHandle(
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
                authoring.Box(new int3(x, y, centerZ - 2), new int3(1, 2, 4), material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.MaxExclusive.z - 1 : bounds.Min.z;
                authoring.Box(new int3(centerX - 2, y, z), new int3(4, 2, 1), material);
            }
        }

        private static void AuthorFacingTrim(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int y,
            byte material)
        {
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.MaxExclusive.x - 1 : bounds.Min.x;
                authoring.Box(new int3(x, y, bounds.Min.z), new int3(1, 1, bounds.Size.z), material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.MaxExclusive.z - 1 : bounds.Min.z;
                authoring.Box(new int3(bounds.Min.x, y, z), new int3(bounds.Size.x, 1, 1), material);
            }
        }

        private static void AuthorPainting(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            authoring.Box(bounds.Min, size, profile.AccentMaterial);

            int inset = profile.Ornamentation >= 4 ? 2 : 1;
            if (math.abs(placement.Facing.x) == 1)
            {
                int innerHeight = size.y - inset * 2;
                int innerWidth = size.z - inset * 2;
                if (innerHeight > 0 && innerWidth > 0)
                {
                    int verticalOffset = profile.DamageLevel >= 3 ? math.min(2, innerHeight - 1) : 0;
                    authoring.Box(
                        new int3(bounds.Min.x, bounds.Min.y + inset + verticalOffset, bounds.Min.z + inset),
                        new int3(size.x, math.max(1, innerHeight - verticalOffset), innerWidth),
                        profile.SoftMaterial);
                }
            }
            else
            {
                int innerHeight = size.y - inset * 2;
                int innerWidth = size.x - inset * 2;
                if (innerHeight > 0 && innerWidth > 0)
                {
                    int verticalOffset = profile.DamageLevel >= 3 ? math.min(2, innerHeight - 1) : 0;
                    authoring.Box(
                        new int3(bounds.Min.x + inset, bounds.Min.y + inset + verticalOffset, bounds.Min.z),
                        new int3(innerWidth, math.max(1, innerHeight - verticalOffset), size.z),
                        profile.SoftMaterial);
                }
            }
        }

        private static void AuthorWallTorch(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            int stemHeight = math.max(2, size.y - 3);

            authoring.Box(
                new int3(centerX, bounds.Min.y, centerZ),
                new int3(1, stemHeight, 1),
                profile.PrimaryMaterial);

            int flameY = math.min(bounds.MaxExclusive.y - 2, bounds.Min.y + stemHeight);
            byte flameMaterial = profile.EmitsLight ? profile.EmissiveMaterial : profile.AccentMaterial;
            authoring.Box(
                new int3(centerX - 1, flameY, centerZ - 1),
                new int3(3, math.min(2, bounds.MaxExclusive.y - flameY), 3),
                flameMaterial);

            if (profile.Ornamentation <= 0)
                return;

            int bracketY = bounds.Min.y + math.max(1, stemHeight / 3);
            if (math.abs(placement.Facing.x) == 1)
            {
                int x = placement.Facing.x > 0 ? bounds.Min.x : bounds.MaxExclusive.x - 2;
                authoring.Box(new int3(x, bracketY, centerZ), new int3(2, 2, 1), profile.AccentMaterial);
            }
            else
            {
                int z = placement.Facing.z > 0 ? bounds.Min.z : bounds.MaxExclusive.z - 2;
                authoring.Box(new int3(centerX, bracketY, z), new int3(1, 2, 2), profile.AccentMaterial);
            }
        }

        private static void AuthorWallBand(
            IStructureAuthoringSession authoring,
            in DecorationBounds bounds,
            int3 facing,
            int thickness,
            int height,
            byte material)
        {
            height = math.min(height, bounds.Size.y);
            if (math.abs(facing.x) == 1)
            {
                int x = facing.x > 0 ? bounds.Min.x : bounds.MaxExclusive.x - thickness;
                authoring.Box(
                    new int3(x, bounds.Min.y, bounds.Min.z),
                    new int3(thickness, height, bounds.Size.z),
                    material);
            }
            else
            {
                int z = facing.z > 0 ? bounds.Min.z : bounds.MaxExclusive.z - thickness;
                authoring.Box(
                    new int3(bounds.Min.x, bounds.Min.y, z),
                    new int3(bounds.Size.x, height, thickness),
                    material);
            }
        }
    }
}
