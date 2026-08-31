using System;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using UnityEngine;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Module-local built-player regression for secret discovery planning. This intentionally validates
    /// WorldBuilder's reusable data/planning boundary without depending on the gallery showcase or the
    /// separate interactable/discovery presentation authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldBuilderSecretDiscoveryValidationBootstrap : MonoBehaviour
    {
        private const string ReadyLog = "WORLD_BUILDER_SECRET_DISCOVERY_VALIDATION ready:";
        private const string FailureLog = "WORLD_BUILDER_SECRET_DISCOVERY_VALIDATION FAIL:";

        private void Awake()
        {
            try
            {
                BuildFixture(
                    out SiteRef approach,
                    out SecretRef secret,
                    out ResolvedSecretPlan canonical,
                    out SiteRoleBinding[] sites);

                var naturalRoute = new SecretRouteSpec(
                    new SecretRouteId("garden-climb"),
                    secret,
                    SecretRouteKind.NaturalTraversal,
                    SecretBypassPolicy.SystemicBypassAllowed,
                    "garden-cliff",
                    false,
                    new SecretBypassEvidence(false, 0, 0));
                var breakableRoute = new SecretRouteSpec(
                    new SecretRouteId("recessed-panel"),
                    secret,
                    SecretRouteKind.BreakableBarrier,
                    SecretBypassPolicy.AuthoredBreakablesOnly,
                    "hidden-chamber-panel",
                    true,
                    new SecretBypassEvidence(false, 12, 0));
                var anchors = new[]
                {
                    Anchor("water-ripple", approach, SecretClueAnchorRole.ApproachEvidence, SecretClueChannel.Environmental),
                    Anchor("stone-sightline", approach, SecretClueAnchorRole.SightlineHint, SecretClueChannel.Visual),
                    Anchor("route-scratch", approach, SecretClueAnchorRole.RouteAdjacentEvidence, SecretClueChannel.Navigation)
                };
                var spec = new SecretDiscoverySpec(
                    secret,
                    SecretImportance.Major,
                    new[] { naturalRoute, breakableRoute },
                    anchors);

                SecretDiscoveryPlanningResult first = SecretDiscoveryPlanner.Resolve(164351, spec, new[] { canonical }, sites);
                SecretDiscoveryPlanningResult second = SecretDiscoveryPlanner.Resolve(164351, spec, new[] { canonical }, sites.Reverse().ToArray());
                Require(first.IsResolved, "valid generated secret did not resolve: " + Join(first));
                Require(second.IsResolved, "deterministic replay did not resolve: " + Join(second));
                Require(first.Plan.Clues.Count >= 2 && first.Plan.Clues.Count <= 4,
                    "major secret clue count fell outside the accepted 2-4 range");
                Require(first.Plan.Clues.Select(x => x.Channel).Distinct().Count() >= 2,
                    "major secret did not preserve independent clue channels");
                Require(first.Plan.Routes.Count == 2, "both legal routes were not retained");
                Require(first.Plan.Clues.Select(x => x.Id.Id).SequenceEqual(second.Plan.Clues.Select(x => x.Id.Id)),
                    "same seed/input produced different stable clue ids");
                Require(first.Plan.Routes.Select(x => x.Id.Id).SequenceEqual(second.Plan.Routes.Select(x => x.Id.Id)),
                    "same seed/input produced different route ids");

                var bypassRoute = new SecretRouteSpec(
                    new SecretRouteId("leaking-shell"),
                    secret,
                    SecretRouteKind.Trapdoor,
                    SecretBypassPolicy.ProtectedShell,
                    "secret-shell",
                    true,
                    new SecretBypassEvidence(true, 0, 1));
                SecretDiscoveryPlanningResult bypass = SecretDiscoveryPlanner.Resolve(
                    164351,
                    new SecretDiscoverySpec(secret, SecretImportance.Standard, new[] { bypassRoute }, new[] { anchors[0] }),
                    new[] { canonical },
                    sites);
                Require(!bypass.IsResolved, "protected-shell trivial bypass was accepted");
                Require(bypass.Diagnostics.Any(x => x.Kind == SecretDiscoveryDiagnosticKind.ProtectedShellBypass),
                    "protected-shell bypass rejection did not report the expected diagnostic");

                BuildTableau(first.Plan);
                Debug.Log(
                    ReadyLog +
                    " secret=" + first.Plan.Secret.Id +
                    " clues=" + first.Plan.Clues.Count +
                    " channels=" + first.Plan.Clues.Select(x => x.Channel).Distinct().Count() +
                    " routes=" + first.Plan.Routes.Count +
                    " deterministic=true bypassRejected=true markerShader=Sprites/Default");
            }
            catch (Exception exception)
            {
                Debug.LogError(FailureLog + " " + exception);
            }
        }

        private static void BuildFixture(
            out SiteRef approach,
            out SecretRef secret,
            out ResolvedSecretPlan canonical,
            out SiteRoleBinding[] sites)
        {
            var campaign = Campaign.Create("worldbuilder-secret-discovery-validation");
            RegionHandle validationRegion = campaign.World.Region("validation-region");
            SiteRef localApproach = validationRegion.Site("garden-approach", SiteArchetype.Ruin);
            SiteRef hidden = validationRegion.Site(
                "hidden-chamber",
                SiteArchetype.Ruin,
                value => value.RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = campaign.Loot.Table("secret-reward", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            SecretRef localSecret = campaign.World.RequireSecret("garden-secret", required => required
                .Inside(hidden)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            approach = localApproach;
            secret = localSecret;
            canonical = new ResolvedSecretPlan(
                localSecret,
                hidden,
                new SecretCandidateId("validation/hidden-volume"),
                "validation/recessed-panel",
                ContainerArchetype.TreasureChest,
                reward);
            sites = new[]
            {
                new SiteRoleBinding(localApproach, new ResolvedSiteId("validation/garden-approach")),
                new SiteRoleBinding(hidden, new ResolvedSiteId("validation/hidden-chamber"))
            };
        }

        private static SecretClueAnchorSpec Anchor(
            string id,
            SiteRef site,
            SecretClueAnchorRole role,
            SecretClueChannel channel)
        {
            return new SecretClueAnchorSpec(
                new SecretClueAnchorId(id),
                site,
                role,
                new[] { channel },
                true,
                SecretHiddenVolumeRelation.Outside,
                1f,
                80f);
        }

        private static void BuildTableau(ResolvedSecretDiscoveryPlan plan)
        {
            Camera camera = new GameObject("Validation Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 7f, -13f);
            camera.transform.LookAt(new Vector3(0f, 1f, 1.5f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.09f, 1f);

            Light key = new GameObject("Validation Key Light").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            CreateMarker("Approach", PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f), new Vector3(10f, 0.4f, 8f), new Color(0.18f, 0.25f, 0.20f));
            CreateMarker("Hidden Volume", PrimitiveType.Cube, new Vector3(0f, 1.0f, 3.5f), new Vector3(4.2f, 2.2f, 3.2f), new Color(0.10f, 0.58f, 0.72f));
            CreateMarker("Primary Route", PrimitiveType.Cube, new Vector3(-1.5f, 0.35f, 1.0f), new Vector3(0.7f, 0.7f, 3.2f), new Color(0.72f, 0.47f, 0.18f));
            CreateMarker("Alternate Route", PrimitiveType.Cube, new Vector3(1.5f, 0.35f, 1.0f), new Vector3(0.7f, 0.7f, 3.2f), new Color(0.72f, 0.47f, 0.18f));

            Vector3[] cluePositions =
            {
                new Vector3(-3.1f, 0.55f, -2.0f),
                new Vector3(3.0f, 0.55f, -1.4f),
                new Vector3(0f, 0.55f, -3.0f)
            };
            for (var i = 0; i < plan.Clues.Count && i < cluePositions.Length; i++)
            {
                CreateMarker(
                    "Clue " + plan.Clues[i].Id.Id,
                    PrimitiveType.Sphere,
                    cluePositions[i],
                    Vector3.one * 0.8f,
                    new Color(0.92f, 0.82f, 0.20f));
            }
        }

        private static void CreateMarker(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(primitive);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = scale;

            Renderer renderer = marker.GetComponent<Renderer>();
            Require(renderer != null, "validation marker missing renderer: " + name);
            Shader shader = Shader.Find("Sprites/Default");
            Require(shader != null, "validation marker shader unavailable in player: Sprites/Default");

            var material = new Material(shader)
            {
                color = color
            };
            renderer.sharedMaterial = material;
        }

        private static string Join(SecretDiscoveryPlanningResult result)
        {
            return string.Join(" | ", result.Diagnostics.Select(x => x.ToString()));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
