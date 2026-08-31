using System;
using System.Collections;
using System.Reflection;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>Gallery-only realization of deterministic WorldBuilder clues on real tour landmarks.</summary>
    internal static class WorldbuildingGallerySecretClueComposition
    {
        private const string Ready = "WORLD_BUILDER_SECRET_CLUE_GALLERY ready:";
        private const string Fail = "WORLD_BUILDER_SECRET_CLUE_GALLERY FAIL:";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != "WorldbuildingGalleryShowcase") return;
            new GameObject("Worldbuilding Gallery Secret Clues") { hideFlags = HideFlags.DontSave }
                .AddComponent<Driver>();
        }

        private sealed class Driver : MonoBehaviour
        {
            private IEnumerator Start()
            {
                ShowcaseWorld world = null;
                for (float elapsed = 0f; world == null && elapsed < 20f; elapsed += Time.unscaledDeltaTime)
                {
                    WorldbuildingGalleryShowcase showcase = FindFirstObjectByType<WorldbuildingGalleryShowcase>();
                    if (showcase != null)
                    {
                        FieldInfo field = typeof(WorldbuildingGalleryShowcase).GetField(
                            "_world", BindingFlags.Instance | BindingFlags.NonPublic);
                        world = field?.GetValue(showcase) as ShowcaseWorld;
                    }
                    if (world == null) yield return null;
                }

                if (world == null)
                {
                    Debug.LogError(Fail + " gallery-world-not-ready");
                    yield break;
                }

                try { Compose(world, transform); }
                catch (Exception error) { Debug.LogError(Fail + " " + error); }
            }
        }

        private static void Compose(ShowcaseWorld world, Transform root)
        {
            int cave = FindCaveStop(world);
            if (cave < 3) throw new InvalidOperationException("gallery cave needs three pre-solve tour landmarks");
            int[] stops = { cave - 3, cave - 2, cave - 1 };

            var campaign = Campaign.Create("worldbuilding-gallery-secret-clues");
            SiteRef approach = campaign.World.RequireSite("clue-approach", x => x.Archetype(SiteArchetype.Ruin));
            SiteRef middle = campaign.World.RequireSite("clue-middle", x => x.Archetype(SiteArchetype.Ruin));
            SiteRef threshold = campaign.World.RequireSite("clue-threshold", x => x.Archetype(SiteArchetype.Ruin));
            SiteRef target = campaign.World.RequireSite("cave-target", x => x.Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = campaign.Loot.Table("gallery-secret-reward", x => x.RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            SecretRef secret = campaign.World.RequireSecret("gallery-cave-secret", x => x.Inside(target)
                .Entrance(SecretEntranceType.DestroyableFalseWall).RequireHiddenSpace().RewardWith(reward));
            var canonical = new ResolvedSecretPlan(secret, target,
                new SecretCandidateId("worldbuilding-gallery/cave-chamber"),
                "worldbuilding-gallery/cave-entrance", ContainerArchetype.TreasureChest, reward);

            SiteRoleBinding[] sites =
            {
                Bind(approach, world.WorldbuildingGalleryTourStopName(stops[0])),
                Bind(middle, world.WorldbuildingGalleryTourStopName(stops[1])),
                Bind(threshold, world.WorldbuildingGalleryTourStopName(stops[2])),
                Bind(target, world.WorldbuildingGalleryTourStopName(cave))
            };
            SecretClueSpec[] specs =
            {
                SecretClues.Define("cave-trace", secret, x => x.Stage(1).Kind(SecretClueKind.Environmental)
                    .SourceAt(approach).Target(target).Content("gallery/clues/cave-trace")),
                SecretClues.Define("cave-weathering", secret, x => x.Stage(2).Kind(SecretClueKind.Readable)
                    .SourceAt(middle).Target(target).Content("gallery/clues/cave-weathering")),
                SecretClues.Define("cave-masonry", secret, x => x.Stage(3).Kind(SecretClueKind.Inspectable)
                    .SourceAt(threshold).Target(target).Content("gallery/clues/cave-masonry"))
            };
            SecretCluePlanningResult plan = SecretCluePlanner.Resolve(
                164351, specs, new[] { canonical }, sites, Array.Empty<NpcSiteAssignment>());
            if (!plan.IsResolved || plan.Clues.Count != 3)
                throw new InvalidOperationException("gallery clue plan failed: " + string.Join(" | ", plan.Diagnostics));

            var ledger = new SecretDiscoveryLedger(new SecretDiscoveryState());
            for (int i = 0; i < plan.Clues.Count; i++)
            {
                ResolvedSecretCluePlan clue = plan.Clues[i];
                if (!clue.TargetCandidate.Equals(canonical.Candidate) || clue.TargetEntrance != canonical.EntranceId)
                    throw new InvalidOperationException("clue target diverged from canonical cave identity");
                ledger.Observe(clue);
                float3 p = world.WorldbuildingGalleryTourLookTarget(stops[i]);
                BuildCue(root, i, new Vector3(p.x, p.y + 0.35f, p.z));
            }
            if (ledger.IsDiscovered(canonical))
                throw new InvalidOperationException("observing clues incorrectly granted discovery");

            Debug.Log(Ready + " secret=" + secret.Id + " stages=3 cave=" +
                world.WorldbuildingGalleryTourStopName(cave) + " candidate=" + canonical.Candidate +
                " entrance=" + canonical.EntranceId + " discovered=false presentation=environment-geometry");
        }

        private static SiteRoleBinding Bind(SiteRef site, string stop) =>
            new SiteRoleBinding(site, new ResolvedSiteId("gallery-tour/" + Stable(stop)));

        private static int FindCaveStop(ShowcaseWorld world)
        {
            for (int i = 0; i < world.WorldbuildingGalleryTourStopCount; i++)
                if ((world.WorldbuildingGalleryTourStopName(i) ?? string.Empty)
                    .IndexOf("cave", StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        private static string Stable(string value) =>
            string.IsNullOrWhiteSpace(value) ? "unnamed" : value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('/', '-');

        private static void BuildCue(Transform root, int stage, Vector3 p)
        {
            if (stage == 0)
            {
                Prop(root, PrimitiveType.Sphere, "Clue Trace A", p + Vector3.left * 0.3f,
                    new Vector3(0.55f, 0.15f, 0.3f), new Color(0.26f, 0.29f, 0.25f));
                Prop(root, PrimitiveType.Sphere, "Clue Trace B", p + new Vector3(0.3f, 0.03f, 0.2f),
                    new Vector3(0.42f, 0.12f, 0.24f), new Color(0.26f, 0.29f, 0.25f));
                return;
            }
            Color color = stage == 1 ? new Color(0.30f, 0.24f, 0.16f) : new Color(0.22f, 0.25f, 0.23f);
            Prop(root, PrimitiveType.Cube, stage == 1 ? "Clue Weathered Slab" : "Clue Masonry Seam",
                p, stage == 1 ? new Vector3(1.2f, 0.12f, 0.52f) : new Vector3(0.9f, 0.72f, 0.16f), color);
        }

        private static void Prop(Transform root, PrimitiveType type, string name, Vector3 p, Vector3 scale, Color color)
        {
            GameObject value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(root, false);
            value.transform.position = p;
            value.transform.localScale = scale;
            Renderer renderer = value.GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (renderer == null || shader == null) throw new InvalidOperationException("gallery clue presentation unavailable");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }
    }
}
