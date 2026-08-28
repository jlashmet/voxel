using System.Collections;
using Game.Combat.Api;
using Game.Composition.Kentridge.Playable;
using Game.Input.Api;
using MountingForce.WorldGen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public class KentridgeCombatEncounterTests
    {
        [UnityTest]
        public IEnumerator ForestBandits_ApproachBeginsInPlaceCombatThroughProductionModules()
        {
            yield return SceneManager.LoadSceneAsync("KentridgePlayableSlice", LoadSceneMode.Single);
            Scene loadedScene = SceneManager.GetActiveScene();
            Assert.That(loadedScene.name, Is.EqualTo("KentridgePlayableSlice"));

            KentridgeForestBanditEncounter encounter = null;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                encounter = Object.FindFirstObjectByType<KentridgeForestBanditEncounter>();
                if (encounter != null && encounter.BanditCount == 3) break;
                yield return null;
            }

            Assert.That(encounter, Is.Not.Null, "Kentridge composition did not install the production combat encounter.");
            Assert.That(encounter.BanditCount, Is.EqualTo(3), "The forest ambush must contain exactly three persistent bandits.");
            Assert.That(encounter.AmbushTheme, Is.EqualTo(RegionThemeKind.PineForest), "Bandits must be authored inside the generated PineForest corridor, not relative to a captured camera coordinate.");
            Assert.That(encounter.CombatActive, Is.False, "Combat must not begin before the player enters a bandit's proximity radius.");
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Exploration));

            // Start runs after all scene Awakes. Give the one-shot presentation repair one frame,
            // then prove runtime gear uses the same player-compatible shader as the rigged actor.
            yield return null;
            for (int i = 0; i < encounter.Bandits.Count; i++)
                AssertBanditGearUsesCharacterShader(encounter.Bandits[i]);

            GameObject leadBandit = encounter.Bandits[0];
            Vector3 player = encounter.transform.position;
            leadBandit.transform.position = new Vector3(
                player.x + encounter.TriggerRadiusMetres * 0.45f,
                player.y - 1.7f,
                player.z);

            yield return null;
            yield return null;

            Assert.That(encounter.CombatActive, Is.True, "Approaching a forest bandit must begin combat automatically.");
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Combat), "Combat lifecycle must exclusively own the player input context while active.");
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(loadedScene.handle), "Combat must remain in the normal Kentridge world rather than swapping scenes.");
            Assert.That(encounter.Bandits[0], Is.SameAs(leadBandit), "The same normal-world bandit actor must remain present after combat begins.");
            Assert.That(encounter.CombatService.ActiveParticipants.Count, Is.EqualTo(4));

            int enemies = 0;
            for (int i = 0; i < encounter.CombatService.ActiveParticipants.Count; i++)
                if (encounter.CombatService.ActiveParticipants[i].Team == CombatTeam.Enemy) enemies++;
            Assert.That(enemies, Is.EqualTo(3));
        }

        private static void AssertBanditGearUsesCharacterShader(GameObject bandit)
        {
            Renderer[] renderers = bandit.GetComponentsInChildren<Renderer>(true);
            string characterShader = null;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (IsGear(renderer.gameObject.name)) continue;
                if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) continue;
                characterShader = renderer.sharedMaterial.shader.name;
                break;
            }

            Assert.That(characterShader, Is.Not.Null.And.Not.Empty,
                bandit.name + " has no shipped character material to drive its runtime gear.");

            int gearCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!IsGear(renderer.gameObject.name)) continue;
                gearCount++;
                Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.gameObject.name + " has no material.");
                Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, renderer.gameObject.name + " has no shader.");
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(characterShader),
                    renderer.gameObject.name + " must reuse the rigged character's player-compatible shader rather than the built-in primitive material.");
            }

            Assert.That(gearCount, Is.GreaterThanOrEqualTo(6), bandit.name + " lost its authored outlaw gear.");
        }

        private static bool IsGear(string name)
        {
            return name == "Emergency Body" ||
                   name == "Hood" ||
                   name == "Belt" ||
                   name == "Shoulder Strap" ||
                   name == "Pouch" ||
                   name == "Sword" ||
                   name == "Guard";
        }
    }
}
