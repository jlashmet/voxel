using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Compatibility facade for the alternate raven naming introduced during development. Stable
    /// identity, bounds, and routing remain owned by RavenSculptureWorldBuilderObject.
    /// </summary>
    public static class RavenStatueWorldBuilderObject
    {
        public const uint VariantId = RavenSculptureWorldBuilderObject.VariantId;

        public static DecorationPropDescriptor Descriptor =>
            RavenSculptureWorldBuilderObject.Descriptor;

        public static DecorationPlacement CreatePlacement(
            GeneratedPropId id,
            int sceneId,
            int slotId,
            int3 origin,
            int3 facing) =>
            RavenSculptureWorldBuilderObject.CreatePlacement(
                id, (uint)sceneId, (uint)slotId, origin, facing);

        public static bool IsRaven(in DecorationPlacement placement) =>
            RavenSculptureWorldBuilderObject.IsRaven(in placement);

        public static int3 ResolveAuthoringOrigin(in DecorationPlacement placement) =>
            RavenSculptureWorldBuilderObject.ResolveAuthoringOrigin(in placement);
    }
}
