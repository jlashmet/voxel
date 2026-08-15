using System;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Candidate-time horizontal staging capacity for a generated site. Absolute elevation and final
    /// world transforms are intentionally absent: site selection only needs to know whether the
    /// authored cutscene can fit inside the guaranteed usable interior. Final CutsceneSiteGeometry is
    /// resolved after terrain/structure realization.
    /// </summary>
    public readonly struct CutsceneStageEnvelope
    {
        public int InteriorHalfWidthDecimetres { get; }
        public int InteriorDepthDecimetres { get; }

        public CutsceneStageEnvelope(
            int interiorHalfWidthDecimetres,
            int interiorDepthDecimetres)
        {
            if (interiorHalfWidthDecimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(interiorHalfWidthDecimetres));
            if (interiorDepthDecimetres <= 0)
                throw new ArgumentOutOfRangeException(nameof(interiorDepthDecimetres));

            InteriorHalfWidthDecimetres = interiorHalfWidthDecimetres;
            InteriorDepthDecimetres = interiorDepthDecimetres;
        }
    }

    /// <summary>
    /// Optional richer generated-site facts used when an authored site hosts a cutscene. The normal
    /// site-candidate contract stays small; backends that advertise CutsceneStage must also expose the
    /// guaranteed stage envelope so WorldBuilder can test the actual cutscene before binding the site.
    /// </summary>
    public interface ICutsceneStageCandidateFacts
    {
        bool TryGetCutsceneStageEnvelope(
            ResolvedSiteId candidate,
            out CutsceneStageEnvelope envelope);
    }
}
