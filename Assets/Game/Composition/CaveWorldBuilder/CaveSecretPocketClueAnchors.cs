using System;
using Game.WorldBuilder.Api;

namespace Game.Composition.CaveWorldBuilder
{
    /// <summary>
    /// Repository-native semantic clue anchors for generated cave secrets. The cave composition layer
    /// knows which semantic evidence exists around a generated route, but intentionally does not own
    /// transforms, prefabs, scene objects, discovery state, or clue selection policy.
    /// </summary>
    public static class CaveSecretPocketClueAnchors
    {
        public static SecretClueAnchorSpec[] ForAuthoredBreakable(
            SiteRef approachSite,
            SecretRouteId route)
        {
            if (string.IsNullOrEmpty(approachSite.Id))
                throw new ArgumentException("Cave clue anchors require a valid approach site.", nameof(approachSite));
            if (string.IsNullOrEmpty(route.Id))
                throw new ArgumentException("Cave clue anchors require a valid route id.", nameof(route));

            return new[]
            {
                new SecretClueAnchorSpec(
                    new SecretClueAnchorId(route.Id + "/approach-environment"),
                    approachSite,
                    SecretClueAnchorRole.ApproachEvidence,
                    new[] { SecretClueChannel.Environmental, SecretClueChannel.Navigation },
                    preSolveObservable: true,
                    hiddenVolumeRelation: SecretHiddenVolumeRelation.Outside,
                    usefulDistanceMin: 2f,
                    usefulDistanceMax: 60f,
                    explainedRoute: route,
                    hasExplainedRoute: true),
                new SecretClueAnchorSpec(
                    new SecretClueAnchorId(route.Id + "/barrier-surface"),
                    approachSite,
                    SecretClueAnchorRole.RouteAdjacentEvidence,
                    new[] { SecretClueChannel.Visual, SecretClueChannel.Mechanical },
                    preSolveObservable: true,
                    hiddenVolumeRelation: SecretHiddenVolumeRelation.Boundary,
                    usefulDistanceMin: 0f,
                    usefulDistanceMax: 18f,
                    explainedRoute: route,
                    hasExplainedRoute: true),
            };
        }

        public static SecretClueAnchorSpec[] ForNaturalTraversal(
            SiteRef approachSite,
            SecretRouteId route)
        {
            if (string.IsNullOrEmpty(approachSite.Id))
                throw new ArgumentException("Cave clue anchors require a valid approach site.", nameof(approachSite));
            if (string.IsNullOrEmpty(route.Id))
                throw new ArgumentException("Cave clue anchors require a valid route id.", nameof(route));

            return new[]
            {
                new SecretClueAnchorSpec(
                    new SecretClueAnchorId(route.Id + "/approach-navigation"),
                    approachSite,
                    SecretClueAnchorRole.ApproachEvidence,
                    new[] { SecretClueChannel.Navigation, SecretClueChannel.Environmental },
                    preSolveObservable: true,
                    hiddenVolumeRelation: SecretHiddenVolumeRelation.Outside,
                    usefulDistanceMin: 3f,
                    usefulDistanceMax: 80f,
                    explainedRoute: route,
                    hasExplainedRoute: true),
                new SecretClueAnchorSpec(
                    new SecretClueAnchorId(route.Id + "/traversal-shape"),
                    approachSite,
                    SecretClueAnchorRole.TraversalHint,
                    new[] { SecretClueChannel.Spatial, SecretClueChannel.Visual },
                    preSolveObservable: true,
                    hiddenVolumeRelation: SecretHiddenVolumeRelation.Outside,
                    usefulDistanceMin: 0f,
                    usefulDistanceMax: 30f,
                    explainedRoute: route,
                    hasExplainedRoute: true),
            };
        }
    }
}
