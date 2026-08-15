using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Post-selection bridge from authored SiteRef to final cutscene stage geometry. It combines the
    /// site-role resolver's concrete binding, WorldGen's exact realized entrance point, and the same
    /// architecture-owned stage envelope used during candidate feasibility. No terrain/Voxel types
    /// cross into WorldBuilder.
    /// </summary>
    public sealed class SettlementCutsceneSiteGeometryProvider : ICutsceneSiteGeometryProvider
    {
        private readonly ISettlementSiteRealizationFacts _realization;
        private readonly ISettlementCutsceneStageEnvelopeProvider _stageEnvelopes;
        private readonly Dictionary<SiteRef, PlannedSite> _sitesByRole;

        public SettlementCutsceneSiteGeometryProvider(
            SettlementPlan plan,
            SiteResolutionResult resolution,
            ISettlementCutsceneStageEnvelopeProvider stageEnvelopes,
            ISettlementSiteRealizationFacts realization)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (resolution == null) throw new ArgumentNullException(nameof(resolution));
            if (!resolution.IsResolved)
                throw new ArgumentException(
                    "Cutscene site geometry requires a successful site-role resolution.",
                    nameof(resolution));

            _stageEnvelopes = stageEnvelopes
                ?? throw new ArgumentNullException(nameof(stageEnvelopes));
            _realization = realization
                ?? throw new ArgumentNullException(nameof(realization));

            var siteByCandidate = new Dictionary<ResolvedSiteId, PlannedSite>();
            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                ResolvedSiteId candidate = SettlementPlanSiteCandidateFacts.CandidateId(
                    plan.Id,
                    site.RoleId);
                siteByCandidate[candidate] = site;
            }

            _sitesByRole = new Dictionary<SiteRef, PlannedSite>();
            for (var i = 0; i < resolution.Bindings.Count; i++)
            {
                SiteRoleBinding binding = resolution.Bindings[i];
                PlannedSite site;
                if (!siteByCandidate.TryGetValue(binding.Site, out site))
                    continue;
                _sitesByRole[binding.Role] = site;
            }
        }

        public bool TryResolve(SiteRef site, out CutsceneSiteGeometry geometry)
        {
            PlannedSite planned;
            if (!_sitesByRole.TryGetValue(site, out planned))
            {
                geometry = default(CutsceneSiteGeometry);
                return false;
            }

            CutsceneStageEnvelope envelope;
            if (!_stageEnvelopes.TryGetCutsceneStageEnvelope(planned, out envelope))
            {
                geometry = default(CutsceneSiteGeometry);
                return false;
            }

            RealizedWorldPoint entrance;
            if (!_realization.TryGetPublicEntrance(planned.RoleId, out entrance))
            {
                geometry = default(CutsceneSiteGeometry);
                return false;
            }

            CutsceneInt3 entranceDm;
            if (!TryConvertExactDecimetres(entrance, out entranceDm))
            {
                geometry = default(CutsceneSiteGeometry);
                return false;
            }

            Int2 inward2;
            Int2 right2;
            KentridgePlacementAxes.Resolve(planned.Orientation, out inward2, out right2);
            var inward = new CutsceneInt3(inward2.X, 0, inward2.Y);
            var right = new CutsceneInt3(right2.X, 0, right2.Y);

            geometry = new CutsceneSiteGeometry(
                entranceDm,
                inward,
                right,
                envelope.InteriorHalfWidthDecimetres,
                envelope.InteriorDepthDecimetres);
            return true;
        }

        private static bool TryConvertExactDecimetres(
            RealizedWorldPoint point,
            out CutsceneInt3 decimetres)
        {
            int scale = point.UnitsPerDecimetre;
            Int3 value = point.Position;
            if (value.X % scale != 0 || value.Y % scale != 0 || value.Z % scale != 0)
            {
                decimetres = default(CutsceneInt3);
                return false;
            }

            decimetres = new CutsceneInt3(
                value.X / scale,
                value.Y / scale,
                value.Z / scale);
            return true;
        }
    }
}
