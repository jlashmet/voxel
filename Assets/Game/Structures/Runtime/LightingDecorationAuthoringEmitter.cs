using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Compatibility authoring for fireplace, candle, chandelier, and lantern/lamp fixtures.</summary>
    public static class LightingDecorationAuthoringEmitter
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
                DecorationPlacement placement = placements[i];
                if (placement.Family == DecorationPropFamily.Fireplace)
                {
                    if (!DecorationVoxelStampBackend.TryAuthor(authoring, in placement, in context))
                        return false;
                    continue;
                }

                if (!TryAuthorBoxAssembly(authoring, in placement, in profile))
                    return false;
            }
            return true;
        }

        private static bool TryAuthorBoxAssembly(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            if (!placement.IsWellFormed || placement.Backend != DecorationRenderBackend.BoxAssembly)
                return false;

            switch (placement.Family)
            {
                case DecorationPropFamily.Candle:
                    AuthorCandle(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Chandelier:
                    AuthorChandelier(authoring, in placement, in profile);
                    return true;
                case DecorationPropFamily.Lantern:
                    if (placement.Facing.y > 0)
                        AuthorStandingLamp(authoring, in placement, in profile);
                    else
                        AuthorWallLantern(authoring, in placement, in profile);
                    return true;
                default:
                    return false;
            }
        }

        private static void AuthorCandle(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = placement.Bounds;
            int waxHeight = math.max(1, b.Size.y - 1);
            authoring.Box(new int3(b.Min.x, b.Min.y, b.Min.z),
                new int3(b.Size.x, waxHeight, b.Size.z), profile.SoftMaterial);
            byte flame = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            authoring.Box(new int3(cx, b.MaxExclusive.y - 1, cz), new int3(1, 1, 1), flame);
        }

        private static void AuthorChandelier(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = placement.Bounds;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int chain = math.max(3, b.Size.y / 2);
            int hubY = b.MaxExclusive.y - chain;
            int halfX = math.max(3, b.Size.x / 2 - 2);
            int halfZ = math.max(3, b.Size.z / 2 - 2);
            byte flame = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;

            authoring.Box(new int3(cx, hubY, cz), new int3(1, chain, 1), profile.AccentMaterial);
            authoring.Box(new int3(cx - halfX, hubY, cz), new int3(halfX * 2 + 1, 2, 1), profile.PrimaryMaterial);
            authoring.Box(new int3(cx, hubY, cz - halfZ), new int3(1, 2, halfZ * 2 + 1), profile.PrimaryMaterial);

            AuthorChandelierLight(authoring, cx - halfX, hubY - 2, cz, flame);
            AuthorChandelierLight(authoring, cx + halfX, hubY - 2, cz, flame);
            AuthorChandelierLight(authoring, cx, hubY - 2, cz - halfZ, flame);
            AuthorChandelierLight(authoring, cx, hubY - 2, cz + halfZ, flame);
        }

        private static void AuthorChandelierLight(
            IStructureAuthoringSession authoring,
            int x, int y, int z, byte flame)
        {
            authoring.Box(new int3(x, y, z), new int3(1, 2, 1), flame);
        }

        private static void AuthorStandingLamp(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = placement.Bounds;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            int baseHeight = math.min(2, b.Size.y);
            int globeHeight = math.max(3, math.min(6, b.Size.y / 3));
            int globeY = b.MaxExclusive.y - globeHeight;
            byte glow = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;

            authoring.Box(b.Min, new int3(b.Size.x, baseHeight, b.Size.z), profile.AccentMaterial);
            authoring.Box(new int3(cx, b.Min.y + baseHeight, cz),
                new int3(1, math.max(1, globeY - b.Min.y - baseHeight), 1), profile.PrimaryMaterial);
            authoring.Box(new int3(b.Min.x + 1, globeY, b.Min.z + 1),
                new int3(math.max(1, b.Size.x - 2), globeHeight, math.max(1, b.Size.z - 2)), glow);
        }

        private static void AuthorWallLantern(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationPresentationProfile profile)
        {
            DecorationBounds b = placement.Bounds;
            int cx = (b.Min.x + b.MaxExclusive.x) / 2;
            int cy = (b.Min.y + b.MaxExclusive.y) / 2;
            int cz = (b.Min.z + b.MaxExclusive.z) / 2;
            byte glow = profile.EmitsLight ? profile.EmissiveMaterial : GameMaterialIds.DarkStone;

            if (math.abs(placement.Facing.x) == 1)
            {
                int wallX = placement.Facing.x > 0 ? b.Min.x : b.MaxExclusive.x - 1;
                authoring.Box(new int3(wallX, cy, cz), new int3(1, 1, 2), profile.AccentMaterial);
            }
            else
            {
                int wallZ = placement.Facing.z > 0 ? b.Min.z : b.MaxExclusive.z - 1;
                authoring.Box(new int3(cx, cy, wallZ), new int3(2, 1, 1), profile.AccentMaterial);
            }

            authoring.Box(new int3(cx - 1, b.Min.y + 1, cz - 1),
                new int3(3, math.max(2, b.Size.y - 2), 3), glow);
        }
    }
}
