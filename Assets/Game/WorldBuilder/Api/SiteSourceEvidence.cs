using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Identifies the kind of upstream authored evidence that informed a semantic WorldBuilder site.
    /// Evidence is diagnostic/provenance metadata only; it does not constrain procedural realization.
    /// </summary>
    public enum SiteSourceEvidenceKind
    {
        LegacyMap = 0
    }

    /// <summary>
    /// Persistent provenance for a semantic site role. The semantic SiteRef remains authoritative for
    /// gameplay and generation; SourceSystem/SourceId let debug and migration tooling report the exact
    /// upstream artifact that supplied evidence for that role.
    /// </summary>
    public sealed class SiteSourceEvidenceSpec
    {
        public SiteRef Site { get; }
        public SiteSourceEvidenceKind Kind { get; }
        public string SourceSystem { get; }
        public string SourceId { get; }

        internal SiteSourceEvidenceSpec(
            SiteRef site,
            SiteSourceEvidenceKind kind,
            string sourceSystem,
            string sourceId)
        {
            Site = site;
            Kind = kind;
            SourceSystem = WorldIdRules.Require(sourceSystem, nameof(sourceSystem));
            SourceId = WorldIdRules.Require(sourceId, nameof(sourceId));
        }
    }

    public static class SiteSourceEvidenceAuthoringExtensions
    {
        /// <summary>
        /// Records a legacy authored map as source evidence for this semantic site. Multiple calls are
        /// allowed because one semantic site may eventually be supported by more than one recovered map.
        /// </summary>
        public static SiteHandle LegacyMap(
            this SiteHandle site,
            string sourceSystem,
            string mapId)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));

            site.Campaign.SiteSourceEvidence.Add(
                new SiteSourceEvidenceSpec(
                    site.Ref,
                    SiteSourceEvidenceKind.LegacyMap,
                    sourceSystem,
                    mapId));
            return site;
        }
    }

    public sealed partial class CampaignBuilder
    {
        internal readonly List<SiteSourceEvidenceSpec> SiteSourceEvidence =
            new List<SiteSourceEvidenceSpec>();
    }
}
