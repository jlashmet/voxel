using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Runtime counterpart to <see cref="DecorationCanonicalCatalog"/>. Stable IDs remain the query
    /// identity, while each production catalog continues to own deterministic variant encoding and
    /// size variation used by placement and authoring.
    /// </summary>
    public static class DecorationCanonicalPlacementCatalog
    {
        private const uint VariantMarker = 0xC0000000u;
        private const uint VariantMarkerMask = 0xC0000000u;
        private const uint StableIdMask = 0x3FF00000u;
        private const int StableIdShift = 20;

        public static bool TryDescribe(
            in DecorationContext context,
            uint sceneId,
            uint slotId,
            ushort stableId,
            out DecorationPropDescriptor descriptor)
        {
            descriptor = default;
            if (stableId >= 1 && stableId <= 114)
                descriptor = DecorationContentCatalog.Describe(
                    in context, sceneId, slotId, (DecorationContentKind)stableId);
            else if (stableId <= 200)
                descriptor = DecorationExpansion200Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpandedContentKind)stableId);
            else if (stableId <= 260)
                descriptor = DecorationExpansion260Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion260Kind)stableId);
            else if (stableId <= 300)
                descriptor = DecorationExpansion300Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion300Kind)stableId);
            else if (stableId <= 320)
                descriptor = DecorationExpansion320Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion320Kind)stableId);
            else if (stableId <= 340)
                descriptor = DecorationExpansion340Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion340Kind)stableId);
            else if (stableId <= 360)
                descriptor = DecorationExpansion360Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion360Kind)stableId);
            else if (stableId <= 380)
                descriptor = DecorationExpansion380Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion380Kind)stableId);
            else if (stableId <= 400)
                descriptor = DecorationExpansion400Catalog.Describe(
                    in context, sceneId, slotId, (DecorationExpansion400Kind)stableId);
            else
                return false;

            return descriptor.IsWellFormed && StableIdOfVariant(descriptor.Variant) == stableId;
        }

        public static ushort StableIdOfVariant(uint variant)
        {
            if ((variant & VariantMarkerMask) != VariantMarker)
                return 0;
            ushort stableId = (ushort)((variant & StableIdMask) >> StableIdShift);
            return stableId >= 1 && stableId <= 400 ? stableId : (ushort)0;
        }
    }
}
