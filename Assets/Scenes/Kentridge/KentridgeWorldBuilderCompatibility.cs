using System;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Voxel;
using LegacyKentridgeDefinition = MountingForce.WorldGen.Content.Kentridge.KentridgeDefinition;
using RuntimeSessionBootstrap = Game.Composition.Kentridge.Runtime.KentridgeCampaignSessionBootstrap;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Scene-local compatibility surface for the legacy Kentridge names still used by the playable
    /// slice. Town construction enters through WorldBuilder exactly once; the returned backend plan
    /// is retained only so existing voxel, access, corridor and survey adapters all consume that
    /// exact authored settlement rather than independently rebuilding Kentridge from the seed.
    /// </summary>
    internal static class KentridgeDefinition
    {
        public const string Id = WorldBuilderTownIds.Kentridge;
        public static readonly Int2 TownCentreDm = LegacyKentridgeDefinition.TownCentreDm;
        public static ArchitectureTheme Theme => LegacyKentridgeDefinition.Theme;
        public static Int3 FootprintDm(StructureArchetype archetype) =>
            LegacyKentridgeDefinition.FootprintDm(archetype);

        public static SettlementPlan Build(uint seed)
        {
            AuthoredTownPlan town = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Kentridge,
                seed);
            if (!(town.BackendPlan is SettlementPlan settlement))
                throw new InvalidOperationException(
                    "WorldBuilder Kentridge authoring did not produce the expected settlement realization.");

            KentridgePlayableWorldBuilderBridge.Remember(settlement, town);
            return settlement;
        }
    }

    /// <summary>
    /// Keeps the large playable-slice integration source on its existing call shape while routing
    /// campaign planning/session realization through the canonical public Game composition APIs.
    /// These methods are scene-local; no legacy backend type is reintroduced on a public game API.
    /// </summary>
    internal static class KentridgeCampaignSessionBootstrap
    {
        public static KentridgeCampaignGenerationPlan Plan(
            CampaignBlueprint blueprint,
            SettlementPlan settlement)
        {
            return RuntimeSessionBootstrap.Plan(
                blueprint,
                KentridgePlayableWorldBuilderBridge.Resolve(settlement));
        }

        public static KentridgeCampaignSession CreateSession(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeVoxelSiteRealizationFacts siteFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation)
        {
            return RuntimeSessionBootstrap.CreateSession(
                blueprint,
                generation,
                new KentridgeCampaignRealizationFacts(siteFacts),
                actors,
                presentation);
        }
    }

    internal static class KentridgePlayableWorldBuilderBridge
    {
        private static SettlementPlan s_Settlement;
        private static AuthoredTownPlan s_Town;

        public static void Remember(SettlementPlan settlement, AuthoredTownPlan town)
        {
            s_Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            s_Town = town ?? throw new ArgumentNullException(nameof(town));
        }

        public static AuthoredTownPlan Resolve(SettlementPlan settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(settlement, s_Settlement) || s_Town == null)
                throw new InvalidOperationException(
                    "Kentridge playable-slice planning must consume the settlement authored through WorldBuilder in this scene session.");
            return s_Town;
        }
    }
}
