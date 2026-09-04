using System;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;

namespace Game.Composition.CaveWorldBuilder
{
    /// <summary>
    /// Projects physically verified cave-pocket topology into the WorldBuilder bypass-policy facts.
    /// The cave authoring layer owns the voxel scan/readback proof; this composition adapter only
    /// translates those verified facts into the semantic planner contract.
    /// </summary>
    public static class CaveSecretPocketBypassEvidence
    {
        public static SecretBypassEvidence AuthoredBreakable(in CaveSecretPocket pocket)
        {
            if (!pocket.IsWellFormed || !pocket.SeparatesHiddenSpaceBeforeOpen || !pocket.SupportsDestruction)
                throw new ArgumentException(
                    "Authored-breakable evidence requires a physically verified destructible cave pocket.",
                    nameof(pocket));

            long designated = (long)pocket.Barrier.Size.x * pocket.Barrier.Size.y * pocket.Barrier.Size.z;
            if (designated <= 0 || designated > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(pocket), "Cave barrier voxel count is out of range.");

            // CaveSecretPocketAuthoring verifies the retained barrier and a one-voxel solid envelope
            // around the future hidden volume before mutation, then reads barrier/connector/pocket
            // cells back after carving. Therefore the verified pocket has no pre-existing trivial
            // bypass and no undesignated destructible region in this entrance projection.
            return new SecretBypassEvidence(
                hasTrivialUnintendedBypass: false,
                designatedBreakableVoxelCount: (int)designated,
                undesignatedBreakableVoxelCount: 0);
        }
    }
}
