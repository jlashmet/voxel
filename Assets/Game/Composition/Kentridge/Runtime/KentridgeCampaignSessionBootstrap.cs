using System;
using Game.Composition.Campaign.Content;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Api;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.Kentridge.Runtime
{
    /// <summary>
    /// Fully realized campaign session after generated-world placement and authoritative gameplay
    /// actors have been connected. The bootstrap does not own the character or voxel runtimes.
    /// </summary>
    public sealed class KentridgeCampaignSession
    {
        public KnownOpeningCampaignContent Content { get; }
        public KentridgeCampaignGenerationPlan Generation { get; }
        public KentridgeCampaignWorldRealization World { get; }
        public CampaignRuntime Runtime { get; }

        internal KentridgeCampaignSession(
            KnownOpeningCampaignContent content,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignWorldRealization world,
            CampaignRuntime runtime)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public int StartNewGame() => Runtime.StartNewGame();
    }

    /// <summary>
    /// Concrete application-level Kentridge bootstrap. Plan is called before voxel emission so the
    /// backend can include Generation.HiddenSpaces. CreateSession is called after the backend has
    /// exact site/hidden-space realization facts. Character creation remains behind
    /// IKentridgeCampaignActorHost and presentation remains behind ICutscenePresentation.
    /// </summary>
    public static class KentridgeCampaignSessionBootstrap
    {
        public static KentridgeCampaignGenerationPlan Plan(
            KnownOpeningCampaignContent content,
            SettlementPlan settlement,
            RegionRef region,
            SettlementRef settlementRef)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));

            return KentridgeCampaignWorldPlanner.Plan(
                content.Blueprint,
                settlement,
                region,
                settlementRef);
        }

        public static KentridgeCampaignSession CreateSession(
            KnownOpeningCampaignContent content,
            KentridgeCampaignGenerationPlan generation,
            ISettlementSiteRealizationFacts siteFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IHiddenSpaceRealizationFacts hiddenSpaceFacts = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (siteFacts == null) throw new ArgumentNullException(nameof(siteFacts));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (!ReferenceEquals(content.Blueprint, generation.Blueprint))
                throw new InvalidOperationException(
                    "Kentridge session content does not own the supplied campaign generation plan.");

            KentridgeCampaignWorldRealization world =
                KentridgeCampaignWorldRealizer.Realize(
                    generation,
                    siteFacts,
                    hiddenSpaceFacts);

            // Player actors are session-owned and must already exist. Check them before preparing any
            // NPCs so a missing local/network player cannot leave the actor host partially mutated.
            ValidatePlayerBindings(content.Blueprint, actors);

            for (var i = 0; i < world.Npcs.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = world.Npcs[i]
                    ?? throw new InvalidOperationException(
                        "Kentridge world realization contains a null NPC placement at index " + i + ".");
                actors.PrepareNpc(placement);

                ICutsceneActorRuntime prepared;
                if (!actors.TryResolveNpc(placement.Npc, out prepared) || prepared == null)
                    throw new InvalidOperationException(
                        "Campaign actor host prepared NPC '" + placement.Npc +
                        "' but did not expose it through TryResolveNpc.");
            }

            ValidateAllCutsceneBindings(content.Blueprint, actors);

            var runtime = new CampaignRuntime(
                content.Blueprint,
                world.CutsceneStages,
                actors,
                presentation);

            return new KentridgeCampaignSession(
                content,
                generation,
                world,
                runtime);
        }

        private static void ValidatePlayerBindings(
            CampaignBlueprint blueprint,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind != CutsceneActorTargetKind.PlayerSlot) continue;

                    ICutsceneActorRuntime player;
                    if (!actors.TryResolvePlayer(binding.Target.PlayerSlot, out player) || player == null)
                        throw new InvalidOperationException(
                            "Campaign requires player slot " + binding.Target.PlayerSlot +
                            " for cutscene '" + cutscene.Ref +
                            "', but the actor host has no authoritative player runtime.");
                }
            }
        }

        private static void ValidateAllCutsceneBindings(
            CampaignBlueprint blueprint,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    ICutsceneActorRuntime actor;
                    bool resolved;
                    switch (binding.Target.Kind)
                    {
                        case CutsceneActorTargetKind.Npc:
                            resolved = actors.TryResolveNpc(binding.Target.Npc, out actor);
                            break;
                        case CutsceneActorTargetKind.PlayerSlot:
                            resolved = actors.TryResolvePlayer(binding.Target.PlayerSlot, out actor);
                            break;
                        default:
                            throw new InvalidOperationException(
                                "Cutscene '" + cutscene.Ref +
                                "' contains unsupported actor target kind '" + binding.Target.Kind + "'.");
                    }

                    if (!resolved || actor == null)
                        throw new InvalidOperationException(
                            "Campaign actor host cannot resolve cutscene actor '" + binding.Actor +
                            "' for cutscene '" + cutscene.Ref + "'.");
                }
            }
        }
    }
}
