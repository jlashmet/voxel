using System.Collections;
using Game.Combat.Api;
using Game.Composition.Kentridge.Playable;
using Game.Input.Api;
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
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                encounter = Object.FindFirstObjectByType<KentridgeForestBanditEncounter>();
                if (encounter != null && encounter.BanditCount == 3) break;
                yield return null;
            }

            Assert.That(encounter, Is.Not.Null, "Kentridge composition did not install the production combat encounter.");
            Assert.That(encounter.BanditCount, Is.EqualTo(3), "The forest ambush must contain exactly three persistent bandits.");
            Assert.That(encounter.CombatActive, Is.False, "Combat must not begin before the player enters a bandit's proximity radius.");
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Exploration));

            GameObject leadBandit = encounter.Bandits[0];
            int leadBanditInstance = leadBandit.GetInstanceID();
            Vector3 player = encounter.transform.position;
            Vector3 approach = player - leadBandit.transform.position;
            approach.y = 0f;
            if (approach.sqrMagnitude < 0.01f) approach = Vector3.back;
            approach.Normalize();
            Vector3 nearBandit = leadBandit.transform.position + approach * (encounter.TriggerRadiusMetres * 0.45f);
            nearBandit.y = player.y;
            encounter.transform.position = nearBandit;

            yield return null;
            yield return null;

            Assert.That(encounter.CombatActive, Is.True, "Approaching a forest bandit must begin combat automatically.");
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Combat), "Combat lifecycle must own the player input context while active.");
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(loadedScene.handle), "Combat must remain in the normal Kentridge world rather than swapping scenes.");
            Assert.That(encounter.Bandits[0].GetInstanceID(), Is.EqualTo(leadBanditInstance), "The same normal-world bandit actor must remain present after combat begins.");
            Assert.That(encounter.CombatService.ActiveParticipants.Count, Is.EqualTo(4));

            int enemies = 0;
            for (int i = 0; i < encounter.CombatService.ActiveParticipants.Count; i++)
                if (encounter.CombatService.ActiveParticipants[i].Team == CombatTeam.Enemy) enemies++;
            Assert.That(enemies, Is.EqualTo(3));
        }
    }
}
