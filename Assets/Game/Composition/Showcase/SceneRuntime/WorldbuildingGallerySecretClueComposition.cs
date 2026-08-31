using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-local realization of WorldBuilder secret clues against authoritative gallery content.
    /// WorldBuilder owns deterministic semantic planning; this composition owns only presentation.
    /// </summary>
    internal static class WorldbuildingGallerySecretClueComposition
    {
        private const string ReadyLog = "WORLD_BUILDER_SECRET_CLUE_GALLERY ready:";
        private const string FailureLog = "WORLD_BUILDER_SECRET_CLUE_GALLERY FAIL:";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying ||
                !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    "WorldbuildingGalleryShowcase", StringComparison.Ordinal))
                return;

            var host = new GameObject("Worldbuilding Gallery Secret Clue Composition")
            {
                hideFlags = HideFlags.DontSave
            };
            host.AddComponent<Driver>();
        }

        private sealed class Driver : MonoBehaviour
        {
            private IEnumerator Start()
            {
                WorldbuildingGalleryShowcase showcase = null;
                ShowcaseWorld world = null;
                float elapsed = 0f;
                while ((showcase == null || world == null) && elapsed < 20f)
                {
                    showcase = UnityEngine.Object.FindFirstObjectByType<WorldbuildingGalleryShowcase>();
                    if (showcase != null)
                    {
                        FieldInfo field = typeof(WorldbuildingGalleryShowcase).GetField(
                            "_world", BindingFlags.Instance | BindingFlags.NonPublic);
                        world = field?.GetValue(showcase) as ShowcaseWorld;
                    }

                    if (world != null) break;
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }

                if (world == null)
                {
                    Debug.LogError(FailureLog + " gallery-world-not-ready");
                    yield break;
                }

                try
                {
                    Compose(world, transform);
                }
                catch (Exception exception)
                {
                    Debug.LogError(FailureLog + " " + exception);
                }
            }
        }

        private static void Compose(ShowcaseWorld world, Transform root)
        {
            int caveStop = FindCaveStop(world);
            if (caveStop < 0)
                throw new InvalidOperationException("authoritative gallery cave tour stop was not found");
            if (caveStop < 3)
                throw new InvalidOperationException("gallery cave stop does not have three pre-solve landmark stops");

            int[] sourceStops = { caveStop - 3, caveStop - 2, caveStop - 1 };
            var campaign = Campaign.Create("worldbuilding-gallery-secret-clue-composition");
            SiteRef source1 = campaign.World.RequireSite("gallery-clue-approach", value => value.Archetype(SiteArchetype.Ruin));
            SiteRef source2 = campaign.World.RequireSite("gallery-clue-shelter", value => value.Archetype(SiteArchetype.Ruin));
            SiteRef source3 = campaign.World.RequireSite("gallery-clue-threshold", value => value.Archetype(SiteArchetype.Ruin));
            SiteRef hidden = campaign.World.RequireSite("gallery-cave-hidden-space", value => value
                .Archetype(SiteArchetype.Ruin)
                .RequireCapability(SiteCapability.SecretCandidateHost));
            LootTableRef reward = campaign.Loot.Table("gallery-cave-reward", loot => loot
                .RollCount(1, 1)
                .Guaranteed(LootCategory.Currency));
            SecretRef secret = campaign.World.RequireSecret("gallery-cave-secret", required => required
                .Inside(hidden)
                .Entrance(SecretEntranceType.DestroyableFalseWall)
                .RequireHiddenSpace()
                .RewardWith(reward));

            string caveName = world.WorldbuildingGalleryTourStopName(caveStop);
            var canonical = new ResolvedSecretPlan(
                secret,
                hidden,
                new SecretCandidateId("worldbuilding-gallery/cave-chamber"),
                "worldbuilding-gallery/cave-entrance",
                ContainerArchetype.TreasureChest,
                reward);
            SiteRoleBinding[] sites =
            {
                new SiteRoleBinding(source1, new ResolvedSiteId("gallery-tour/" + StableName(world.WorldbuildingGalleryTourStopName(sourceStops[0])))),
                new SiteRoleBinding(source2, new ResolvedSiteId("gallery-tour/" + StableName(world.WorldbuildingGalleryTourStopName(sourceStops[1])))),
                new SiteRoleBinding(source3, new ResolvedSiteId("gallery-tour/" + StableName(world.WorldbuildingGalleryTourStopName(sourceStops[2])))),
                new SiteRoleBinding(hidden, new ResolvedSiteId("gallery-tour/" + StableName(caveName)))
            };
            SecretClueSpec[] specs =
            {
                SecretClues.Define("gallery-cave-trace", secret, clue => clue
                    .Stage(1).Kind(SecretClueKind.Environmental).SourceAt(source1).Target(hidden)
                    .Content("gallery/clues/cave-trace")),
                SecretClues.Define("gallery-cave-weathering", secret, clue => clue
                    .Stage(2).Kind(SecretClueKind.Readable).SourceAt(source2).Target(hidden)
                    .Content("gallery/clues/cave-weathering")),
                SecretClues.Define("gallery-cave-masonry", secret, clue => clue
                    .Stage(3).Kind(SecretClueKind.Inspectable).SourceAt(source3).Target(hidden)
                    .Content("gallery/clues/cave-masonry"))
            };

            SecretCluePlanningResult plan = SecretCluePlanner.Resolve(
                164351,
                specs,
                new[] { canonical },
                sites,
                Array.Empty<NpcSiteAssignment>());
            if (!plan.IsResolved || plan.Clues.Count != 3)
                throw new InvalidOperationException("deterministic gallery clue chain did not resolve: " + Join(plan));

            for (int i = 0; i < plan.Clues.Count; i++)
            {
                ResolvedSecretClue clue = plan.Clues[i];
                if (!clue.TargetCandidate.Equals(canonical.Candidate) || clue.TargetEntrance != canonical.EntranceId)
                    throw new InvalidOperationException("gallery clue diverged from canonical cave identity: " + clue.Id.Id);

                float3 anchor = world.WorldbuildingGalleryTourLookTarget(sourceStops[i]);
                BuildEnvironmentalCue(root, i, new Vector3(anchor.x, anchor.y + 0.35f, anchor.z));
            }

            var discovery = new SecretDiscoveryState();
            var ledger = new SecretDiscoveryLedger(discovery);
            foreach (ResolvedSecretClue clue in plan.Clues)
                ledger.Observe(clue);
            if (ledger.IsDiscovered(canonical))
                throw new InvalidOperationException("observing gallery clues incorrectly granted discovery");

            Debug.Log(
                ReadyLog +
                " secret=" + secret.Id +
                " stages=" + plan.Clues.Count +
                " sources=" + string.Join(",", SourceNames(world, sourceStops)) +
                " cave=" + caveName +
                " candidate=" + canonical.Candidate.Value +
                " entrance=" + canonical.EntranceId +
                " discovered=false presentation=environment-geometry");
        }

        private static int FindCaveStop(ShowcaseWorld world)
        {
            for (int i = 0; i < world.WorldbuildingGalleryTourStopCount; i++)
            {
                string name = world.WorldbuildingGalleryTourStopName(i);
                if (!string.IsNullOrEmpty(name) && name.IndexOf("cave", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return -1;
        }

        private static IEnumerable<string> SourceNames(ShowcaseWorld world, int[] sourceStops)
        {
            for (int i = 0; i < sourceStops.Length; i++)
                yield return StableName(world.WorldbuildingGalleryTourStopName(sourceStops[i]));
        }

        private static string StableName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            return value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('/', '-');
        }

        private static void BuildEnvironmentalCue(Transform root, int stage, Vector3 position)
        {
            // These are deliberately environmental props rather than signs, labels, or universal
            // glowing markers: each stage changes silhouette/material treatment while remaining
            // ordinary scenery that can be read before the hidden volume is reached.
            switch (stage)
            {
                case 0:
                    CreateStone(root, "Clue Trace A", position + new Vector3(-0.35f, 0f, 0f), new Vector3(0.55f, 0.16f, 0.32f));
                    CreateStone(root, "Clue Trace B", position + new Vector3(0.28f, 0.04f, 0.18f), new Vector3(0.42f, 0.12f, 0.24f));
                    CreateStone(root, "Clue Trace C", position + new Vector3(0.05f, 0.02f, -0.32f), new Vector3(0.35f, 0.10f, 0.20f));
                    break;
                case 1:
                    CreateSlab(root, "Clue Weathered Slab", position, new Vector3(1.2f, 0.12f, 0.52f), new Color(0.30f, 0.24f, 0.16f, 1f));
                    CreateSlab(root, "Clue Weathered Notch", position + new Vector3(0.12f, 0.12f, 0.02f), new Vector3(0.55f, 0.035f, 0.08f), new Color(0.10f, 0.09f, 0.07f, 1f));
                    break;
                default:
                    CreateSlab(root, "Clue Masonry Seam Left", position + new Vector3(-0.38f, 0.28f, 0f), new Vector3(0.20f, 0.75f, 0.18f), new Color(0.22f, 0.25f, 0.23f, 1f));
                    CreateSlab(root, "Clue Masonry Seam Right", position + new Vector3(0.38f, 0.28f, 0f), new Vector3(0.20f, 0.75f, 0.18f), new Color(0.22f, 0.25f, 0.23f, 1f));
                    CreateSlab(root, "Clue Masonry Lintel", position + new Vector3(0f, 0.66f, 0f), new Vector3(0.95f, 0.16f, 0.18f), new Color(0.18f, 0.21f, 0.19f, 1f));
                    break;
            }
        }

        private static void CreateStone(Transform root, string name, Vector3 position, Vector3 scale)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            value.name = name;
            value.transform.SetParent(root, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            ApplyMaterial(value, new Color(0.26f, 0.29f, 0.25f, 1f));
        }

        private static void CreateSlab(Transform root, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(root, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            ApplyMaterial(value, color);
        }

        private static void ApplyMaterial(GameObject value, Color color)
        {
            Renderer renderer = value.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException("environmental clue renderer missing");
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("environmental clue shader unavailable: Sprites/Default");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }

        private static string Join(SecretCluePlanningResult result)
        {
            var values = new List<string>(result.Diagnostics.Count);
            for (int i = 0; i < result.Diagnostics.Count; i++) values.Add(result.Diagnostics[i].ToString());
            return string.Join(" | ", values);
        }
    }
}
