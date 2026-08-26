using MountingForce.WorldGen.Content.Hightown;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// The plot envelope for an archetype in a given settlement.
    ///
    /// Envelopes are settlement content, not global constants: Hightown's houses sit on broader
    /// lots than Kentridge's, which is half of why it reads as a wealthier town. The voxel pass had
    /// twenty-one places that asked <c>KentridgeDefinition</c> directly, so a plot laid out with one
    /// town's envelope was measured with the other's — and dressing placed inside a Hightown plot
    /// fell outside the Kentridge-sized box used to look its owner up, which failed generation
    /// outright rather than merely looking wrong.
    /// </summary>
    public static class SettlementFootprints
    {
        public static Int3 For(SettlementPlan plan, StructureArchetype archetype) =>
            plan != null && plan.Theme.Id == HightownDefinition.Id
                ? HightownDefinition.FootprintDm(archetype)
                : KentridgeDefinition.FootprintDm(archetype);
    }
}
