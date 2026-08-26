using System;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Content.Kentridge;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Canonical town-authoring entry point. Application code selects semantic content through
    /// WorldBuilder; backend-specific planning remains an implementation detail of this assembly.
    /// </summary>
    public static class WorldBuilderTownAuthoring
    {
        public static AuthoredTownPlan Author(string settlementId, uint seed)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
                throw new ArgumentException("A town id is required.", nameof(settlementId));

            switch (settlementId)
            {
                case WorldBuilderTownIds.Kentridge:
                    return new AuthoredTownPlan(
                        WorldBuilderTownIds.Kentridge,
                        seed,
                        KentridgeDefinition.Build(seed));

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(settlementId),
                        settlementId,
                        "WorldBuilder has no registered town authoring backend for this settlement id.");
            }
        }
    }
}
