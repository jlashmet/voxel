using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Structure-authoring backend for decorations that should become world-integrated voxel
    /// geometry rather than presentation-only meshes. The first supported stamp is a campfire.
    /// </summary>
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

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            switch (placement.Family)
            {
                case DecorationPropFamily.Campfire:
                    AuthorCampfire(authoring, in placement, in profile);
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

            authoring.Cylinder(
                centerX,
                bounds.Min.y,
                centerZ,
                radius,
                1,
                profile.AccentMaterial,
                innerRadius);

            int logLength = math.max(3, radius * 2 - 1);
            authoring.Box(
                new int3(centerX - logLength / 2, bounds.Min.y + 1, centerZ),
                new int3(logLength, 1, 1),
                profile.PrimaryMaterial);
            authoring.Box(
                new int3(centerX, bounds.Min.y + 1, centerZ - logLength / 2),
                new int3(1, 1, logLength),
                profile.PrimaryMaterial);

            byte emberMaterial = profile.EmitsLight
                ? profile.EmissiveMaterial
                : GameMaterialIds.DarkStone;
            authoring.Disc(
                centerX,
                bounds.Min.y + 2,
                centerZ,
                math.max(1, radius - 2),
                emberMaterial);
        }
    }
}
