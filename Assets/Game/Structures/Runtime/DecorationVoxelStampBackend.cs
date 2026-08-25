using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public static class DecorationVoxelStampBackend
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationContext context)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));
            if (!placement.IsWellFormed || !context.IsWellFormed ||
                placement.Backend != DecorationRenderBackend.VoxelStamp)
                return false;

            if (RavenSculptureWorldBuilderObject.IsRaven(in placement))
            {
                int3 origin = RavenSculptureWorldBuilderObject.ResolveAuthoringOrigin(in placement);
                RavenSculptureAuthoring.Author(authoring, origin);
                return true;
            }

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            switch (placement.Family)
            {
                case DecorationPropFamily.Campfire:
                    AuthorCampfire(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Fireplace:
                    AuthorFireplace(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorCampfire(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds bounds = placement.Bounds;
            int3 size = bounds.Size;
            int centerX = (bounds.Min.x + bounds.MaxExclusive.x) / 2;
            int centerZ = (bounds.Min.z + bounds.MaxExclusive.z) / 2;
            int radius = math.max(2, math.min(size.x, size.z) / 2);
            int innerRadius = math.max(0, radius - 2);
            authoring.Cylinder(centerX, bounds.Min.y, centerZ, radius, 1,
                profile.AccentMaterial, innerRadius);

            int logLength = math.max(3, radius * 2 - 1);
            authoring.Box(new int3(centerX - logLength / 2, bounds.Min.y + 1, centerZ),
                new int3(logLength, 1, 1), profile.PrimaryMaterial);
            authoring.Box(new int3(centerX, bounds.Min.y + 1, centerZ - logLength / 2),
                new int3(1, 1, logLength), profile.PrimaryMaterial);

            byte emberMaterial = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;
            authoring.Disc(centerX, bounds.Min.y + 2, centerZ,
                math.max(1, radius - 2), emberMaterial);
        }

        private static void AuthorFireplace(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = placement.Bounds;
            int3 size = b.Size;
            bool wallAlongZ = math.abs(placement.Facing.x) == 1;
            int wallWidth = wallAlongZ ? size.z : size.x;
            int wallDepth = wallAlongZ ? size.x : size.z;
            int surround = math.max(2, math.min(4, wallWidth / 6));
            int openingHeight = math.max(6, size.y * 2 / 3);
            int openingWidth = math.max(6, wallWidth - surround * 2);
            int openingDepth = math.max(2, wallDepth - 2);
            authoring.Box(b.Min, size, profile.AccentMaterial);

            if (wallAlongZ)
            {
                int minX = placement.Facing.x > 0 ? b.Min.x + 1 : b.MaxExclusive.x - openingDepth - 1;
                int minZ = b.Min.z + (size.z - openingWidth) / 2;
                authoring.Box(new int3(minX, b.Min.y + 2, minZ),
                    new int3(openingDepth, openingHeight, openingWidth), GameMaterialIds.Empty);
                int fireX = placement.Facing.x > 0 ? minX + openingDepth - 2 : minX + 1;
                AuthorFirebed(authoring, fireX, b.Min.y + 2,
                    (b.Min.z + b.MaxExclusive.z) / 2, true, openingWidth, in profile);
            }
            else
            {
                int minZ = placement.Facing.z > 0 ? b.Min.z + 1 : b.MaxExclusive.z - openingDepth - 1;
                int minX = b.Min.x + (size.x - openingWidth) / 2;
                authoring.Box(new int3(minX, b.Min.y + 2, minZ),
                    new int3(openingWidth, openingHeight, openingDepth), GameMaterialIds.Empty);
                int fireZ = placement.Facing.z > 0 ? minZ + openingDepth - 2 : minZ + 1;
                AuthorFirebed(authoring, (b.Min.x + b.MaxExclusive.x) / 2,
                    b.Min.y + 2, fireZ, false, openingWidth, in profile);
            }

            if (profile.Ornamentation >= 3)
            {
                int mantelHeight = math.min(3, math.max(1, size.y / 10));
                authoring.Box(new int3(b.Min.x - 1, b.MaxExclusive.y - mantelHeight, b.Min.z - 1),
                    new int3(size.x + 2, mantelHeight, size.z + 2), profile.PrimaryMaterial);
            }
        }

        private static void AuthorFirebed(
            IStructureAuthoringSession authoring,
            int x, int y, int z, bool alongZ, int span,
            in DecorationPresentationProfile profile)
        {
            int length = math.max(4, span - 4);
            byte ember = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;
            if (alongZ)
            {
                authoring.Box(new int3(x, y, z - length / 2),
                    new int3(2, 2, length), profile.PrimaryMaterial);
                authoring.Box(new int3(x, y + 2, z - math.max(1, length / 4)),
                    new int3(2, 2, math.max(2, length / 2)), ember);
            }
            else
            {
                authoring.Box(new int3(x - length / 2, y, z),
                    new int3(length, 2, 2), profile.PrimaryMaterial);
                authoring.Box(new int3(x - math.max(1, length / 4), y + 2, z),
                    new int3(math.max(2, length / 2), 2, 2), ember);
            }
        }
    }
}
