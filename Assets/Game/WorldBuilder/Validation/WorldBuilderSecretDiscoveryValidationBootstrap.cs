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
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.30f, 0.36f, 0.34f, 1f);
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.31f, 0.27f, 1f);

            Camera camera = new GameObject("Secret Clue Validation Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(-5.8f, 3.8f, -11.2f);
            camera.transform.LookAt(new Vector3(0.3f, 1.5f, 4.6f));
            camera.fieldOfView = 49f;
            camera.clearFlags = CameraClearFlags.Skybox;

            Light key = new GameObject("Late Afternoon Sun").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.90f, 0.72f, 1f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            Material soil = Material("Forest Soil", new Color(0.16f, 0.19f, 0.13f, 1f));
            Material path = Material("Worn Earth", new Color(0.31f, 0.27f, 0.19f, 1f));
            Material stone = Material("Old Limestone", new Color(0.40f, 0.41f, 0.36f, 1f));
            Material stoneDark = Material("Old Limestone Shadow", new Color(0.28f, 0.30f, 0.26f, 1f));
            Material weathered = Material("Ochre Weathering", new Color(0.46f, 0.34f, 0.20f, 1f));
            Material seam = Material("Deep Masonry Seam", new Color(0.10f, 0.11f, 0.09f, 1f));
            Material moss = Material("Moss", new Color(0.15f, 0.28f, 0.12f, 1f));
            Material mossLight = Material("Sunlit Moss", new Color(0.27f, 0.39f, 0.16f, 1f));
            Material bark = Material("Bark", new Color(0.20f, 0.15f, 0.10f, 1f));
            Material foliage = Material("Foliage", new Color(0.14f, 0.31f, 0.12f, 1f));
            Material foliageLight = Material("Foliage Highlight", new Color(0.25f, 0.43f, 0.18f, 1f));

            Block("Ground", new Vector3(0f, -0.48f, 4.5f), new Vector3(24f, 0.7f, 24f), soil);
            BuildApproachPath(path, stone, weathered);
            BuildRuinFacade(stone, stoneDark, weathered, seam, moss, mossLight);
            BuildForestEdge(bark, foliage, foliageLight, stoneDark);
        }

        private static void BuildApproachPath(Material path, Material stone, Material weathered)
        {
            for (int i = 0; i < 7; i++)
            {
                float z = -4.0f + (i * 1.45f);
                float x = -2.2f + (i * 0.42f);
                Block("Worn Path " + i, new Vector3(x, -0.08f, z), new Vector3(3.6f, 0.06f, 1.7f), path);
            }

            Stone("Displaced Stone A", new Vector3(-2.65f, 0.04f, -2.75f), new Vector3(1.35f, 0.26f, 0.78f), stone, -13f);
            Stone("Displaced Stone B", new Vector3(-1.72f, 0.06f, -1.35f), new Vector3(1.05f, 0.22f, 0.66f), weathered, 18f);
            Stone("Displaced Stone C", new Vector3(-0.82f, 0.05f, 0.05f), new Vector3(1.20f, 0.22f, 0.70f), stone, -7f);
            Stone("Displaced Stone D", new Vector3(0.05f, 0.05f, 1.38f), new Vector3(0.98f, 0.20f, 0.60f), stone, 11f);
            Stone("Displaced Stone E", new Vector3(0.72f, 0.05f, 2.34f), new Vector3(0.78f, 0.17f, 0.50f), weathered, -4f);

            Block("Weathered Threshold", new Vector3(0.72f, 0.10f, 3.35f), new Vector3(3.35f, 0.18f, 1.05f), weathered);
            Block("Threshold Notch Left", new Vector3(0.05f, 0.21f, 3.25f), new Vector3(0.82f, 0.09f, 0.16f), Material("Threshold Cut", new Color(0.12f, 0.12f, 0.10f, 1f)));
            Block("Threshold Notch Right", new Vector3(1.35f, 0.21f, 3.42f), new Vector3(0.72f, 0.09f, 0.16f), Material("Threshold Cut 2", new Color(0.12f, 0.12f, 0.10f, 1f)));
        }

        private static void BuildRuinFacade(
            Material stone,
            Material stoneDark,
            Material weathered,
            Material seam,
            Material moss,
            Material mossLight)
        {
            // Layered masonry creates a believable broken ruin silhouette instead of a box wall.
            MasonryColumn("Left Buttress", -3.15f, 5.75f, 4, stone, stoneDark);
            MasonryColumn("Right Buttress", 3.18f, 5.75f, 4, stone, stoneDark);
            MasonryColumn("Left Inner Pier", -1.85f, 5.92f, 5, stone, stoneDark);
            MasonryColumn("Right Inner Pier", 1.86f, 5.92f, 5, stone, stoneDark);

            for (int row = 0; row < 2; row++)
            {
                for (int col = -2; col <= 2; col++)
                {
                    float x = col * 0.78f;
                    float y = 3.55f + (row * 0.48f);
                    float offset = ((row + col) & 1) == 0 ? 0.08f : -0.06f;
                    Block("Broken Lintel " + row + "-" + col,
                        new Vector3(x + offset, y, 5.92f),
                        new Vector3(0.72f, 0.42f, 0.82f),
                        ((row + col) & 1) == 0 ? stone : stoneDark);
                }
            }

            // Concealed panel is built from individual courses; only construction relationships reveal it.
            for (int row = 0; row < 5; row++)
            {
                int count = row % 2 == 0 ? 3 : 4;
                float width = row % 2 == 0 ? 0.60f : 0.46f;
                for (int col = 0; col < count; col++)
                {
                    float start = -(count - 1) * width * 0.5f;
                    float x = start + (col * width);
                    float y = 0.63f + (row * 0.52f);
                    Material courseMaterial = row == 1 || row == 2 ? weathered : stone;
                    Block("False Wall Course " + row + "-" + col,
                        new Vector3(x, y, 5.72f),
                        new Vector3(width - 0.035f, 0.47f, 0.52f),
                        courseMaterial);
                }
            }

            Block("Masonry Seam Left", new Vector3(-1.02f, 1.55f, 5.43f), new Vector3(0.08f, 2.95f, 0.08f), seam);
            Block("Masonry Seam Right", new Vector3(1.02f, 1.55f, 5.43f), new Vector3(0.08f, 2.95f, 0.08f), seam);
            Block("Masonry Seam Top", new Vector3(0f, 3.05f, 5.43f), new Vector3(2.10f, 0.08f, 0.08f), seam);
            Block("Repeated Weathering", new Vector3(0f, 0.82f, 5.40f), new Vector3(1.34f, 0.11f, 0.09f), weathered);

            Stone("Rubble Left A", new Vector3(-4.15f, 0.18f, 5.12f), new Vector3(1.45f, 0.62f, 1.05f), stoneDark, 18f);
            Stone("Rubble Left B", new Vector3(-3.58f, 0.18f, 4.70f), new Vector3(0.86f, 0.48f, 0.78f), stone, -12f);
            Stone("Rubble Right A", new Vector3(4.02f, 0.17f, 5.22f), new Vector3(1.22f, 0.55f, 0.92f), stone, -15f);
            Stone("Rubble Right B", new Vector3(3.48f, 0.12f, 4.70f), new Vector3(0.78f, 0.40f, 0.70f), stoneDark, 14f);

            Block("Moss Shelf Left", new Vector3(-2.58f, 1.68f, 5.42f), new Vector3(1.05f, 0.08f, 0.38f), moss);
            Block("Moss Shelf Right", new Vector3(2.56f, 1.13f, 5.42f), new Vector3(0.92f, 0.08f, 0.34f), mossLight);
            Vine("Ivy Left", new Vector3(-2.95f, 2.65f, 5.36f), 1.7f, mossLight);
            Vine("Ivy Right", new Vector3(2.78f, 2.15f, 5.36f), 1.35f, moss);
        }

        private static void BuildForestEdge(Material bark, Material foliage, Material foliageLight, Material rock)
        {
            Tree("Oak Left", new Vector3(-7.5f, 0f, 5.7f), 1.15f, bark, foliage, foliageLight);
            Tree("Oak Rear", new Vector3(6.8f, 0f, 8.4f), 1.30f, bark, foliage, foliageLight);
            Tree("Oak Right", new Vector3(8.0f, 0f, 3.2f), 0.95f, bark, foliage, foliageLight);

            Shrub("Shrub Left A", new Vector3(-5.2f, 0.05f, 2.5f), 1.2f, foliage, foliageLight);
            Shrub("Shrub Left B", new Vector3(-4.9f, 0.05f, 6.6f), 1.0f, foliage, foliageLight);
            Shrub("Shrub Right A", new Vector3(5.2f, 0.05f, 3.3f), 1.15f, foliage, foliageLight);
            Shrub("Shrub Right B", new Vector3(4.9f, 0.05f, 7.0f), 0.95f, foliage, foliageLight);

            Stone("Foreground Rock", new Vector3(-6.0f, 0.20f, -0.2f), new Vector3(2.4f, 0.75f, 1.5f), rock, -18f);
            Stone("Right Bank Rock", new Vector3(6.0f, 0.20f, 1.2f), new Vector3(1.8f, 0.65f, 1.3f), rock, 12f);
        }

        private static void MasonryColumn(string name, float x, float z, int courses, Material light, Material dark)
        {
            for (int row = 0; row < courses; row++)
            {
                int count = row % 2 == 0 ? 2 : 3;
                float width = count == 2 ? 0.72f : 0.52f;
                for (int col = 0; col < count; col++)
                {
                    float start = -(count - 1) * width * 0.5f;
                    float offset = ((row + col) & 1) == 0 ? 0.05f : -0.04f;
                    Block(name + " " + row + "-" + col,
                        new Vector3(x + start + (col * width) + offset, 0.42f + (row * 0.55f), z),
                        new Vector3(width - 0.03f, 0.50f, 0.82f),
                        ((row + col) & 1) == 0 ? light : dark);
                }
            }
        }

        private static void Tree(string name, Vector3 root, float scale, Material bark, Material foliage, Material highlight)
        {
            Block(name + " Trunk", root + new Vector3(0f, 1.85f * scale, 0f), new Vector3(0.45f, 3.7f, 0.45f) * scale, bark);
            Stone(name + " Crown A", root + new Vector3(0f, 4.0f * scale, 0f), new Vector3(2.4f, 1.8f, 2.1f) * scale, foliage, 0f);
            Stone(name + " Crown B", root + new Vector3(-1.0f, 3.65f, 0.3f) * scale, new Vector3(1.7f, 1.35f, 1.55f) * scale, highlight, 0f);
            Stone(name + " Crown C", root + new Vector3(1.0f, 3.75f, -0.2f) * scale, new Vector3(1.8f, 1.45f, 1.6f) * scale, foliage, 0f);
        }

        private static void Shrub(string name, Vector3 position, float scale, Material foliage, Material highlight)
        {
            Stone(name + " A", position + new Vector3(-0.35f, 0.45f, 0f) * scale, new Vector3(1.25f, 0.85f, 1.0f) * scale, foliage, 0f);
            Stone(name + " B", position + new Vector3(0.45f, 0.50f, 0.08f) * scale, new Vector3(1.15f, 0.95f, 1.0f) * scale, highlight, 0f);
            Stone(name + " C", position + new Vector3(0.05f, 0.62f, -0.35f) * scale, new Vector3(1.0f, 1.0f, 0.9f) * scale, foliage, 0f);
        }

        private static void Vine(string name, Vector3 position, float height, Material material)
        {
            Block(name + " Stem", position, new Vector3(0.10f, height, 0.08f), material);
            for (int i = 0; i < 4; i++)
            {
                float y = position.y - (height * 0.42f) + (i * height * 0.28f);
                float side = i % 2 == 0 ? -0.16f : 0.16f;
                Stone(name + " Leaf " + i, new Vector3(position.x + side, y, position.z - 0.03f), new Vector3(0.42f, 0.16f, 0.30f), material, side < 0f ? -25f : 25f);
            }
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
