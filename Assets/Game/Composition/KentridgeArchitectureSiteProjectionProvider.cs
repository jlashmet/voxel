using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace Game.Composition.WorldBuilderWorldGen
{
    /// <summary>
    /// Concrete Kentridge WorldGen projection for WorldBuilder. It consumes semantic plots and the
    /// public Architecture handoff, never Voxel realization internals. Sites without a real public
    /// entrance (currently the Well) fail closed instead of coercing an interaction anchor into the
    /// entrance/distance contract.
    /// </summary>
    public sealed class KentridgeArchitectureSiteProjectionProvider :
        ISettlementSiteProjectionProvider,
        ISettlementCutsceneStageEnvelopeProvider
    {
        private readonly SettlementPlan _plan;
        private readonly Dictionary<int, BuildingPlot> _plots;

        public KentridgeArchitectureSiteProjectionProvider(SettlementPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (!string.Equals(plan.Theme.Id, KentridgeDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Kentridge projection requires a Kentridge architecture theme.",
                    nameof(plan));

            _plots = new Dictionary<int, BuildingPlot>(plan.Plots.Count);
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (_plots.ContainsKey(plot.RoleId))
                    throw new InvalidOperationException(
                        "Kentridge settlement plan contains duplicate stable role id '" + plot.RoleId + "'.");
                _plots.Add(plot.RoleId, plot);
            }
        }

        public bool TryProject(PlannedSite site, out SettlementSiteProjection projection)
        {
            StructureIntent intent;
            StructureForm form;
            StructureSiteGeometry geometry;
            if (!TryResolveSite(site, out intent, out form, out geometry))
            {
                projection = default(SettlementSiteProjection);
                return false;
            }

            SiteArchetype archetype = site.RoleId == (int)KentridgeRole.Pub
                ? SiteArchetype.Pub
                : SiteArchetype.Unspecified;

            StructureInteriorEnvelope interior;
            bool hasInterior = StructureSiteGeometryResolver.TryResolveInterior(
                intent,
                _plan.Theme,
                form,
                out interior);

            SiteCapabilityOffer[] capabilities = hasInterior
                ? new[]
                {
                    new SiteCapabilityOffer(SiteCapabilityKind.Interior),
                    new SiteCapabilityOffer(SiteCapabilityKind.PublicExit),
                    new SiteCapabilityOffer(SiteCapabilityKind.ConversationSpace),
                    new SiteCapabilityOffer(SiteCapabilityKind.CutsceneStage),
                }
                : new[]
                {
                    new SiteCapabilityOffer(SiteCapabilityKind.PublicExit),
                };

            projection = new SettlementSiteProjection(
                archetype,
                new SiteFootprintBoundsDm(
                    geometry.FootprintMinDm.X,
                    geometry.FootprintMinDm.Y,
                    geometry.FootprintMaxDm.X,
                    geometry.FootprintMaxDm.Y),
                geometry.PublicEntranceDm,
                capabilities);
            return true;
        }

        public bool TryGetCutsceneStageEnvelope(
            PlannedSite site,
            out CutsceneStageEnvelope envelope)
        {
            StructureIntent intent;
            StructureForm form;
            StructureSiteGeometry geometry;
            if (!TryResolveSite(site, out intent, out form, out geometry))
            {
                envelope = default(CutsceneStageEnvelope);
                return false;
            }

            StructureInteriorEnvelope interior;
            if (!StructureSiteGeometryResolver.TryResolveInterior(
                    intent,
                    _plan.Theme,
                    form,
                    out interior))
            {
                envelope = default(CutsceneStageEnvelope);
                return false;
            }

            envelope = new CutsceneStageEnvelope(
                interior.HalfWidthDm,
                interior.DepthDm);
            return true;
        }

        private bool TryResolveSite(
            PlannedSite site,
            out StructureIntent intent,
            out StructureForm form,
            out StructureSiteGeometry geometry)
        {
            BuildingPlot plot;
            if (!_plots.TryGetValue(site.RoleId, out plot) || !Matches(site, plot))
            {
                intent = default(StructureIntent);
                form = default(StructureForm);
                geometry = default(StructureSiteGeometry);
                return false;
            }

            intent = KentridgeDefinition.StructureIntent(plot);
            form = ArchitectureCompiler.Resolve(intent, _plan.Theme, _plan.Seed);
            return StructureSiteGeometryResolver.TryResolve(
                intent,
                _plan.Theme,
                form,
                out geometry);
        }

        private static bool Matches(PlannedSite site, BuildingPlot plot) =>
            site.Archetype == plot.Archetype
            && site.PositionDm.X == plot.PositionDm.X
            && site.PositionDm.Y == plot.PositionDm.Y
            && site.Orientation == (byte)plot.Frontage;
    }
}
