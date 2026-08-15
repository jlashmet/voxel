using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Optional richer projection supplied by architecture-aware settlement adapters. A provider may
    /// expose ordinary site facts without this contract; sites advertised as CutsceneStage candidates
    /// are expected to publish a guaranteed usable envelope through it.
    /// </summary>
    public interface ISettlementCutsceneStageEnvelopeProvider
    {
        bool TryGetCutsceneStageEnvelope(
            PlannedSite site,
            out CutsceneStageEnvelope envelope);
    }

    /// <summary>
    /// Complete WorldBuilder-facing fact set for one semantic SettlementPlan. Ordinary candidate,
    /// hierarchy, distance, and traversal facts are delegated to SettlementPlanSiteCandidateFacts;
    /// cutscene stage envelopes are added only for projected sites whose architecture explicitly
    /// publishes them.
    /// </summary>
    public sealed class SettlementPlanWorldBuilderFacts :
        ISiteCandidateFacts,
        ICutsceneStageCandidateFacts
    {
        private readonly SettlementPlanSiteCandidateFacts _sites;
        private readonly Dictionary<ResolvedSiteId, CutsceneStageEnvelope> _stageEnvelopes;

        public IReadOnlyList<SiteCandidate> Candidates => _sites.Candidates;

        public SettlementPlanWorldBuilderFacts(
            SettlementPlan plan,
            RegionRef region,
            SettlementRef settlement,
            ISettlementSiteProjectionProvider projections,
            ISettlementTraversalFacts traversal,
            ISettlementCutsceneStageEnvelopeProvider stageEnvelopes)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (projections == null) throw new ArgumentNullException(nameof(projections));
            if (stageEnvelopes == null) throw new ArgumentNullException(nameof(stageEnvelopes));

            _sites = new SettlementPlanSiteCandidateFacts(
                plan,
                region,
                settlement,
                projections,
                traversal);
            _stageEnvelopes = new Dictionary<ResolvedSiteId, CutsceneStageEnvelope>();

            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                if (!projections.TryProject(site, out projection))
                    continue;

                CutsceneStageEnvelope envelope;
                if (!stageEnvelopes.TryGetCutsceneStageEnvelope(site, out envelope))
                    continue;

                ResolvedSiteId id = SettlementPlanSiteCandidateFacts.CandidateId(plan.Id, site.RoleId);
                _stageEnvelopes[id] = envelope;
            }
        }

        public bool TryGetCutsceneStageEnvelope(
            ResolvedSiteId candidate,
            out CutsceneStageEnvelope envelope) =>
            _stageEnvelopes.TryGetValue(candidate, out envelope);

        public bool IsInRegion(ResolvedSiteId candidate, RegionRef region) =>
            _sites.IsInRegion(candidate, region);

        public bool IsInSettlement(ResolvedSiteId candidate, SettlementRef settlement) =>
            _sites.IsInSettlement(candidate, settlement);

        public bool IsReachable(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal) =>
            _sites.IsReachable(subject, target, traversal);

        public int BoundaryDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) =>
            _sites.BoundaryDistanceMetres(subject, target);

        public int PublicEntranceDistanceMetres(ResolvedSiteId subject, ResolvedSiteId target) =>
            _sites.PublicEntranceDistanceMetres(subject, target);

        public int TraversalDistanceMetres(
            ResolvedSiteId subject,
            ResolvedSiteId target,
            TraversalProfile traversal) =>
            _sites.TraversalDistanceMetres(subject, target, traversal);
    }
}
