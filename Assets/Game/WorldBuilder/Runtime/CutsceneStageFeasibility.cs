using System;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Candidate-time feasibility check for authored cutscene staging. It intentionally reuses the
    /// same procedural stage resolver used after realization, but supplies a neutral origin/frame so
    /// only the guaranteed interior dimensions participate in the decision.
    /// </summary>
    public static class CutsceneStageFeasibility
    {
        public static bool CanFit(
            CutsceneStagePlan plan,
            CutsceneStageEnvelope envelope)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var geometry = new CutsceneSiteGeometry(
                new CutsceneInt3(0, 0, 0),
                new CutsceneInt3(0, 0, 1),
                new CutsceneInt3(1, 0, 0),
                envelope.InteriorHalfWidthDecimetres,
                envelope.InteriorDepthDecimetres);

            try
            {
                ProceduralCutsceneStageResolver.Resolve(plan, geometry);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
