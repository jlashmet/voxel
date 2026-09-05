using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Production dispatcher for reusable descriptor factories that predate the canonical content-id
    /// catalogues. It delegates to the existing family owners; no showcase policy or identity list is
    /// encoded here. Thin/procedural backends remain presentation-owned and are intentionally rejected.
    /// </summary>
    public static class DecorationReusablePresetAuthoringEmitter
    {
        public static bool TryAuthor(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationContext context)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!placement.IsWellFormed || !context.IsWellFormed)
                return false;

            if (placement.Backend == DecorationRenderBackend.ThinSurface ||
                placement.Backend == DecorationRenderBackend.ProceduralMesh)
                return false;

            var one = new[] { placement };
            switch (placement.Family)
            {
                case DecorationPropFamily.Bed:
                case DecorationPropFamily.Dresser:
                case DecorationPropFamily.WallTorch:
                    return DecorationStructureAuthoringEmitter.TryAuthor(authoring, in placement, in context);

                case DecorationPropFamily.Table:
                case DecorationPropFamily.Bench:
                case DecorationPropFamily.Chair:
                    return DiningDecorationAuthoringEmitter.TryAuthor(authoring, one, in context);

                case DecorationPropFamily.Fireplace:
                case DecorationPropFamily.Candle:
                case DecorationPropFamily.Chandelier:
                case DecorationPropFamily.Lantern:
                    return LightingDecorationAuthoringEmitter.TryAuthor(authoring, one, in context);

                case DecorationPropFamily.Chest:
                case DecorationPropFamily.Shelf:
                case DecorationPropFamily.Bookcase:
                case DecorationPropFamily.Crate:
                case DecorationPropFamily.Barrel:
                    return StorageDecorationAuthoringEmitter.TryAuthor(authoring, one, in context);

                case DecorationPropFamily.WeaponRack:
                    return MartialDisplayAuthoringEmitter.TryAuthor(authoring, one, in context);

                case DecorationPropFamily.Altar:
                    return TryAuthorAltar(authoring, in placement, in context);

                default:
                    if (placement.Backend == DecorationRenderBackend.VoxelStamp)
                        return DecorationVoxelStampBackend.TryAuthor(authoring, in placement, in context);
                    return false;
            }
        }

        private static bool TryAuthorAltar(
            IStructureAuthoringSession authoring,
            in DecorationPlacement placement,
            in DecorationContext context)
        {
            if (placement.Backend != DecorationRenderBackend.BoxAssembly)
                return false;
            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            DecorationBounds b = placement.Bounds;
            int plinth = math.max(2, math.min(4, b.Size.y / 3));
            int inset = math.max(1, math.min(3, math.min(b.Size.x, b.Size.z) / 6));
            authoring.Box(b.Min, new int3(b.Size.x, plinth, b.Size.z), profile.AccentMaterial);
            authoring.Box(
                new int3(b.Min.x + inset, b.Min.y + plinth, b.Min.z + inset),
                new int3(
                    math.max(1, b.Size.x - inset * 2),
                    math.max(1, b.Size.y - plinth),
                    math.max(1, b.Size.z - inset * 2)),
                profile.PrimaryMaterial);
            if (profile.Ornamentation >= 3 && b.Size.y >= 6)
            {
                int cx = (b.Min.x + b.MaxExclusive.x) / 2;
                int cz = (b.Min.z + b.MaxExclusive.z) / 2;
                authoring.Box(
                    new int3(cx - 1, b.MaxExclusive.y - math.max(2, b.Size.y / 3), cz - 1),
                    new int3(2, math.max(2, b.Size.y / 3), 2),
                    profile.EmitsLight ? profile.EmissiveMaterial : profile.AccentMaterial);
            }
            return true;
        }
    }
}
