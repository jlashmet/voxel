using System;
using Game.Composition.Campaign.Content;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Outcomes.Api;
using Game.Outcomes.Runtime;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.Kentridge.Runtime
{
    /// <summary>
    /// Production composition root for the authored full Kentridge campaign. It keeps the opening-only
    /// KentridgeCampaignWorldPlanner contract intact while binding the full campaign to the recovered
    /// hierarchy-aware physical plan and the canonical System15 outcome authority.
    /// </summary>
    public sealed class AuthoredFullRunKentridgeComposition
    {
        private static readonly OutcomeAuthorityRef OutcomeAuthority =
            new OutcomeAuthorityRef("main-campaign-story");
        private static readonly OutcomeResolutionId CompletionResolution =
            new OutcomeResolutionId("main-campaign:success");
        private static readonly OutcomeRef CompletionOutcome =
            new OutcomeRef("main-campaign-complete");

        private readonly GameOutcomeRuntime _outcomes;
        private readonly OutcomePolicyRouter _outcomePolicy;

        public AuthoredFullRunCampaignContent Content { get; }
        public AuthoredFullRunPhysicalWorldPlan PhysicalWorld { get; }
        public AuthoredFullRunCampaignGenerationPlan Generation { get; }
        public AuthoredFullRunCampaignWorldRealization World { get; }
        public IGameOutcomeQuery OutcomeQuery => _outcomes;

        private AuthoredFullRunKentridgeComposition(
            AuthoredFullRunCampaignContent content,
            AuthoredFullRunPhysicalWorldPlan physicalWorld,
            AuthoredFullRunCampaignGenerationPlan generation,
            AuthoredFullRunCampaignWorldRealization world)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            PhysicalWorld = physicalWorld ?? throw new ArgumentNullException(nameof(physicalWorld));
            Generation = generation ?? throw new ArgumentNullException(nameof(generation));
            World = world ?? throw new ArgumentNullException(nameof(world));

            _outcomes = new GameOutcomeRuntime(new[] { OutcomeAuthority });
            _outcomePolicy = new OutcomePolicyRouter(_outcomes, new[]
            {
                new OutcomePolicyRule(
                    Content.CompletionCondition,
                    new GameOutcomeResolutionRequest(
                        CompletionResolution,
                        OutcomeAuthority,
                        GameOutcomeDisposition.Success,
                        CompletionOutcome))
            });
        }

        public static AuthoredFullRunKentridgeComposition Build(
            CutsceneDefinition destinationCutsceneDefinition,
            uint seed,
            Int2 kentridgeCentreDm,
            int voxelsPerDecimetre,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));
            if (voxelsPerDecimetre < 1)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));

            AuthoredFullRunCampaignContent content = AuthoredFullRunCampaignContent.Build(
                destinationCutsceneDefinition,
                configureDestinationCutscene);
            AuthoredFullRunPhysicalWorldPlan physical = AuthoredFullRunPhysicalWorldPlanner.Plan(
                content.Blueprint,
                seed,
                kentridgeCentreDm,
                voxelsPerDecimetre);
            AuthoredFullRunCampaignGenerationPlan generation =
                AuthoredFullRunCampaignGenerator.Plan(physical);
            AuthoredFullRunCampaignWorldRealization world =
                AuthoredFullRunCampaignWorldRealizer.Realize(generation);

            return new AuthoredFullRunKentridgeComposition(
                content,
                physical,
                generation,
                world);
        }

        public KentridgeSessionRuntimeGraphFactory CreateSessionFactory(
            IKentridgeCampaignActorHost actors,
            ICutscenePresentation presentation,
            IKentridgeSessionRuntimeExtensionFactory extensionFactory = null)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            return new KentridgeSessionRuntimeGraphFactory(
                Content.Blueprint,
                World,
                actors,
                presentation,
                extensionFactory,
                _outcomes,
                ObserveOutcomeCondition);
        }

        private void ObserveOutcomeCondition(OutcomeConditionRef condition)
        {
            if (!_outcomePolicy.TryObserve(condition, out GameOutcomeResolutionResult result))
                throw new InvalidOperationException(
                    "No System15 outcome policy is configured for authored condition '" + condition + "'.");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "System15 rejected authored outcome condition '" + condition +
                    "' with status " + result.Status + ".");
        }
    }
}
