using System;
using System.Collections.Generic;
using Game.Composition.Campaign.Runtime;
using Game.Composition.Kentridge.Api;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Kentridge.Runtime
{
    /// <summary>
    /// Fully realized campaign session after generated-world placement and authoritative gameplay
    /// actors/secrets have been connected. The bootstrap does not own authored content, character
    /// runtime, voxel runtime, or secret interaction implementation.
    /// </summary>
    public sealed class KentridgeCampaignSession
    {
        public CampaignBlueprint Blueprint { get; }
        public KentridgeCampaignGenerationPlan Generation { get; }
        public KentridgeCampaignWorldRealization World { get; }
        public CampaignRuntime Runtime { get; }

        internal KentridgeCampaignSession(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignWorldRealization world,
            CampaignRuntime runtime)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public int StartNewGame() => Runtime.StartNewGame();
    }

    /// <summary>
    /// Concrete application-level Kentridge bootstrap. Plan is called before voxel emission so the
    /// backend can include Generation.HiddenSpaces. CreateSession is called after the backend has
    /// exact site/hidden-space realization facts. Town authoring enters through WorldBuilder and the
    /// legacy backend representation stays behind the integration boundary.
    /// </summary>
    public static class KentridgeCampaignSessionBootstrap
    {
        public static KentridgeCampaignGenerationPlan Plan(
            CampaignBlueprint blueprint,
            AuthoredTownPlan town)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (town == null) throw new ArgumentNullException(nameof(town));

            return KentridgeCampaignWorldPlanner.Plan(blueprint, town);
        }

        public static KentridgeCampaignSession CreateSession(
            CampaignBlueprint blueprint,
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts realizationFacts,
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IKentridgeCampaignSecretHost secretHost = null)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (realizationFacts == null) throw new ArgumentNullException(nameof(realizationFacts));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (!ReferenceEquals(blueprint, generation.Blueprint))
                throw new InvalidOperationException(
                    "Kentridge session blueprint does not own the supplied campaign generation plan.");

            KentridgeCampaignWorldRealization world =
                KentridgeCampaignWorldRealizationBoundary.Realize(
                    generation,
                    realizationFacts);

            // Finish every non-mutating integration preflight before touching gameplay-owned state.
            if (world.Secrets.Count > 0 && secretHost == null)
                throw new ArgumentNullException(
                    nameof(secretHost),
                    "Campaign selected physical secrets but no gameplay secret host was supplied.");
            ValidatePlayerBindings(blueprint, actors);
            ValidateNpcPlacements(blueprint, world.Npcs);

            // Both external hosts receive their campaign state as batches. Each implementation owns
            // atomic application within its subsystem; Composition never creates half of an NPC set.
            if (world.Secrets.Count > 0)
                secretHost.PrepareSecrets(world.Secrets);

            actors.PrepareNpcs(world.Npcs);
            ValidatePreparedNpcs(world.Npcs, actors);
            ValidateAllCutsceneBindings(blueprint, actors);

            var runtime = new CampaignRuntime(
                blueprint,
                world.CutsceneStages,
                actors,
                presentation);

            return new KentridgeCampaignSession(
                blueprint,
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

        private static void ValidateNpcPlacements(
            CampaignBlueprint blueprint,
            IReadOnlyList<ResolvedNpcWorldPlacement> placements)
        {
            var realized = new HashSet<NpcRef>();
            for (var i = 0; i < placements.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = placements[i]
                    ?? throw new InvalidOperationException(
                        "Kentridge world realization contains a null NPC placement at index " + i + ".");
                if (!realized.Add(placement.Npc))
                    throw new InvalidOperationException(
                        "Kentridge world realization contains more than one placement for NPC '" +
                        placement.Npc + "'.");
            }

            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                for (var j = 0; j < cutscene.ActorBindings.Count; j++)
                {
                    CutsceneActorBindingSpec binding = cutscene.ActorBindings[j];
                    if (binding.Target.Kind != CutsceneActorTargetKind.Npc) continue;
                    if (!realized.Contains(binding.Target.Npc))
                        throw new InvalidOperationException(
                            "Cutscene '" + cutscene.Ref + "' requires NPC '" + binding.Target.Npc +
                            "', but the generated world contains no physical placement for it.");
                }
            }
        }

        private static void ValidatePreparedNpcs(
            IReadOnlyList<ResolvedNpcWorldPlacement> placements,
            IKentridgeCampaignActorHost actors)
        {
            for (var i = 0; i < placements.Count; i++)
            {
                ResolvedNpcWorldPlacement placement = placements[i];
                ICutsceneActorRuntime prepared;
                if (!actors.TryResolveNpc(placement.Npc, out prepared) || prepared == null)
                    throw new InvalidOperationException(
                        "Campaign actor host prepared NPC batch but did not expose NPC '" +
                        placement.Npc + "' through TryResolveNpc.");
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
