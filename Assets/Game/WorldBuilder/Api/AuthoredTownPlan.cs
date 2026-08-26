using System;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Stable ids for town definitions that can be authored through WorldBuilder.
    /// Callers choose semantic content by id; backend plan types stay behind the API boundary.
    /// </summary>
    public static class WorldBuilderTownIds
    {
        public const string Kentridge = "kentridge";
    }

    /// <summary>
    /// Opaque result of authoring one town. Game and presentation code may retain semantic identity
    /// and seed, while backend adapters receive the private realization through friend assemblies.
    /// </summary>
    public sealed class AuthoredTownPlan
    {
        public string SettlementId { get; }
        public uint Seed { get; }

        internal object BackendPlan { get; }

        internal AuthoredTownPlan(string settlementId, uint seed, object backendPlan)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
                throw new ArgumentException("A town plan requires a settlement id.", nameof(settlementId));

            SettlementId = settlementId;
            Seed = seed;
            BackendPlan = backendPlan ?? throw new ArgumentNullException(nameof(backendPlan));
        }
    }
}
