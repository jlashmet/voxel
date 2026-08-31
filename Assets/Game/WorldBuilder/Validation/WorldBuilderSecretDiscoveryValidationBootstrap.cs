using System;
using System.Linq;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using UnityEngine;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Dedicated built-player feature scene for deterministic secret planning. The scene presents a real
    /// three-stage environmental clue chain ending at a concealed masonry entrance; it deliberately avoids
    /// debug spheres, glowing markers, text labels, or gallery-only composition.
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
                    Anchor("displaced-stones", approach, SecretClueAnchorRole.ApproachEvidence, SecretClueChannel.Environmental),
                    Anchor("weathered-slab", approach, SecretClueAnchorRole.SightlineHint, SecretClueChannel.Visual),
                    Anchor("masonry-seam", approach, SecretClueAnchorRole.RouteAdjacentEvidence, SecretClueChannel.Navigation)
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

                ValidateCanonicalDiscovery(canonical);
                BuildFeatureScene();
                Debug.Log(
                    ReadyLog +
                    " secret=" + first.Plan.Secret.Id +
                    " clues=" + first.Plan.Clues.Count +
                    " channels=" + first.Plan.Clues.Select(x => x.Channel).Distinct().Count() +
                    " routes=" + first.Plan.Routes.Count +
                    " deterministic=true bypassRejected=true canonicalDiscovery=true featureScene=dedicated presentation=environmental");
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

        private static void ValidateCanonicalDiscovery(ResolvedSecretPlan canonical)
        {
            var authority = new SecretDiscoveryState();
            var ledger = new SecretDiscoveryLedger(authority);
            int events = 0;
            authority.Discovered += _ => events++;

            Require(!ledger.IsDiscovered(canonical), "secret started discovered");
            Require(ledger.Discover(canonical), "first canonical discovery was not credited");
            Require(!ledger.Discover(canonical), "revisit duplicated canonical discovery");
            Require(events == 1, "canonical discovery emitted duplicate reward events");

            SecretDiscoverySnapshot snapshot = ledger.Capture();
            var restoredAuthority = new SecretDiscoveryState();
            var restored = new SecretDiscoveryLedger(restoredAuthority);
            int restoredEvents = 0;
            restoredAuthority.Discovered += _ => restoredEvents++;
            restored.Restore(snapshot);
            Require(restored.IsDiscovered(canonical), "restored canonical discovery identity was lost");
            Require(!restored.Discover(canonical), "reload duplicated canonical discovery");
            Require(restoredEvents == 0, "restore/revisit replayed canonical discovery event");
        }

        private static void BuildFeatureScene()
        {
            Camera camera = new GameObject("Secret Clue Validation Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 5.2f, -12.8f);
            camera.transform.LookAt(new Vector3(0f, 1.4f, 3.6f));
            camera.fieldOfView = 48f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.065f, 1f);

            Light key = new GameObject("Late Afternoon Sun").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.35f;
            key.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            Material ground = Material("Ground", new Color(0.17f, 0.20f, 0.16f, 1f));
            Material stone = Material("Old Stone", new Color(0.34f, 0.35f, 0.31f, 1f));
            Material weathered = Material("Weathered Stone", new Color(0.40f, 0.31f, 0.21f, 1f));
            Material seam = Material("Deep Masonry Seam", new Color(0.12f, 0.13f, 0.11f, 1f));
            Material moss = Material("Moss", new Color(0.16f, 0.27f, 0.14f, 1f));

            Block("Ground", new Vector3(0f, -0.35f, 3.5f), new Vector3(14f, 0.5f, 18f), ground);

            // Stage 1: a repeated, grounded trace of displaced stones leads toward the ruin rather than acting as a marker.
            Stone("Displaced Stone A", new Vector3(-2.7f, 0.05f, -2.0f), new Vector3(1.2f, 0.24f, 0.7f), stone, -8f);
            Stone("Displaced Stone B", new Vector3(-1.7f, 0.07f, -0.7f), new Vector3(1.0f, 0.22f, 0.62f), weathered, 12f);
            Stone("Displaced Stone C", new Vector3(-0.7f, 0.04f, 0.5f), new Vector3(1.15f, 0.20f, 0.66f), stone, -5f);
            Stone("Displaced Stone D", new Vector3(0.3f, 0.06f, 1.55f), new Vector3(0.92f, 0.20f, 0.58f), stone, 9f);

            // Stage 2: the same weathering language becomes a deliberate abrasion/notch at the threshold.
            Block("Weathered Threshold", new Vector3(0.65f, 0.08f, 3.0f), new Vector3(3.0f, 0.18f, 1.0f), weathered);
            Block("Threshold Notch Left", new Vector3(0.05f, 0.20f, 2.97f), new Vector3(0.75f, 0.08f, 0.18f), seam);
            Block("Threshold Notch Right", new Vector3(1.22f, 0.20f, 3.04f), new Vector3(0.68f, 0.08f, 0.18f), seam);

            // Ruin facade and concealed opening. The seam is physical construction evidence, not a glowing outline.
            Block("Ruin Left Pier", new Vector3(-2.3f, 1.55f, 6.0f), new Vector3(2.6f, 3.8f, 1.0f), stone);
            Block("Ruin Right Pier", new Vector3(2.3f, 1.55f, 6.0f), new Vector3(2.6f, 3.8f, 1.0f), stone);
            Block("Ruin Lintel", new Vector3(0f, 3.15f, 6.0f), new Vector3(2.2f, 0.6f, 1.0f), stone);
            Block("False Wall", new Vector3(0f, 1.45f, 6.02f), new Vector3(1.9f, 2.7f, 0.72f), stone);
            Block("Masonry Seam Left", new Vector3(-0.98f, 1.45f, 5.60f), new Vector3(0.10f, 2.72f, 0.10f), seam);
            Block("Masonry Seam Right", new Vector3(0.98f, 1.45f, 5.60f), new Vector3(0.10f, 2.72f, 0.10f), seam);
            Block("Masonry Seam Top", new Vector3(0f, 2.83f, 5.60f), new Vector3(2.05f, 0.10f, 0.10f), seam);
            Block("Repeated Weathering", new Vector3(0f, 0.78f, 5.56f), new Vector3(1.25f, 0.12f, 0.12f), weathered);

            // Uneven support and vegetation integrate the clue into the environment without obscuring it.
            Stone("Rubble Left", new Vector3(-3.8f, 0.12f, 5.1f), new Vector3(1.4f, 0.55f, 1.0f), stone, 17f);
            Stone("Rubble Right", new Vector3(3.6f, 0.10f, 5.35f), new Vector3(1.1f, 0.48f, 0.9f), stone, -14f);
            Block("Moss Patch Left", new Vector3(-2.75f, 0.10f, 5.44f), new Vector3(0.95f, 0.08f, 0.42f), moss);
            Block("Moss Patch Right", new Vector3(2.72f, 0.11f, 5.52f), new Vector3(0.82f, 0.08f, 0.40f), moss);
        }

        private static Material Material(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            Require(shader != null, "validation material shader unavailable");
            return new Material(shader)
            {
                name = name,
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void Block(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.position = position;
            value.transform.localScale = scale;
            Renderer renderer = value.GetComponent<Renderer>();
            Require(renderer != null, "validation block missing renderer: " + name);
            renderer.sharedMaterial = material;
        }

        private static void Stone(string name, Vector3 position, Vector3 scale, Material material, float yaw)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            value.name = name;
            value.transform.position = position;
            value.transform.localScale = scale;
            value.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Renderer renderer = value.GetComponent<Renderer>();
            Require(renderer != null, "validation stone missing renderer: " + name);
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
