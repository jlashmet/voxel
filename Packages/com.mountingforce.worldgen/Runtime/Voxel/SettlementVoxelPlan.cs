using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Resolves which settlement a voxel catalogue pass is building.
    ///
    /// The pass is composed of roughly twenty catalogues that each need the town layout. They used
    /// to obtain it by calling <c>KentridgeDefinition.Build(seed)</c> individually, so the town was
    /// effectively hard-coded in twenty places and rebuilt twenty times per pass. Routing every one
    /// of them through here means a caller can realize any planned settlement — Hightown included —
    /// by binding the plan onto the settings it already passes.
    ///
    /// Kentridge remains the default so existing callers, tests and captures are unaffected.
    /// </summary>
    public static class SettlementVoxelPlan
    {
        public static SettlementPlan Resolve(uint seed, in VoxelWorldGenSettings settings) =>
            settings.Settlement ?? KentridgeDefinition.Build(seed);
    }
}
