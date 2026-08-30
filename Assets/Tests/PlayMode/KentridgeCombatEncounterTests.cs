using System.Collections;
using System.Collections.Generic;
using Game.Combat.Api;
using Game.Combat.Runtime;
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
        private const int BattleSeed = 20260829;
        private const int MaximumBattleActions = 64;

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

        [Test]
        public void ForestBanditBattle_FixedSeedAiBothTeamsCompletesDeterministicallyAcrossRepeatedRuns()
        {
            BattleSnapshot expected = RunDeterministicBattle(BattleSeed);
            Assert.That(expected.Winner, Is.Not.Null, expected.Diagnostic);
            Assert.That(expected.ActionCount, Is.GreaterThan(0).And.LessThanOrEqualTo(MaximumBattleActions), expected.Diagnostic);
            Assert.That(expected.PlayerAlive ^ expected.EnemyAlive, Is.True,
                "Exactly one team must have living combatants at terminal state. " + expected.Diagnostic);
            Assert.That(expected.PendingWork, Is.False, expected.Diagnostic);

            for (int repeat = 0; repeat < 4; repeat++)
            {
                BattleSnapshot actual = RunDeterministicBattle(BattleSeed);
                Assert.That(actual.Winner, Is.EqualTo(expected.Winner), actual.Diagnostic);
                Assert.That(actual.ActionCount, Is.EqualTo(expected.ActionCount), actual.Diagnostic);
                Assert.That(actual.TurnNumber, Is.EqualTo(expected.TurnNumber), actual.Diagnostic);
                Assert.That(actual.PlayerAlive, Is.EqualTo(expected.PlayerAlive), actual.Diagnostic);
                Assert.That(actual.EnemyAlive, Is.EqualTo(expected.EnemyAlive), actual.Diagnostic);
                Assert.That(actual.ActorsSeen, Is.EquivalentTo(expected.ActorsSeen), actual.Diagnostic);
                Assert.That(actual.PendingWork, Is.False, actual.Diagnostic);
            }
        }

        [UnityTest]
        public IEnumerator ForestBanditBattle_ExactKentridgeSceneMakesForwardProgressAndSettlesCleanly()
        {
            yield return SceneManager.LoadSceneAsync("KentridgePlayableSlice", LoadSceneMode.Single);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("KentridgePlayableSlice"));

            KentridgeForestBanditEncounter encounter = null;
            float installDeadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < installDeadline)
            {
                encounter = Object.FindFirstObjectByType<KentridgeForestBanditEncounter>();
                if (encounter != null && encounter.BanditCount == 3) break;
                yield return null;
            }
            Assert.That(encounter, Is.Not.Null, "Kentridge combat composition failed to install.");
            Assert.That(encounter.BattleSeed, Is.EqualTo(BattleSeed));

            GameObject leadBandit = encounter.Bandits[0];
            Vector3 player = encounter.transform.position;
            leadBandit.transform.position = new Vector3(
                player.x + encounter.TriggerRadiusMetres * 0.40f,
                player.y - 1.7f,
                player.z);

            float activationDeadline = Time.realtimeSinceStartup + 2f;
            while (!encounter.CombatActive && Time.realtimeSinceStartup < activationDeadline)
                yield return null;
            Assert.That(encounter.CombatActive, Is.True, "The exact Kentridge ambush did not enter battle. " + encounter.BattleDiagnostic);
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Combat));

            CombatSessionId session = encounter.CombatService.ActiveSessionId;
            int lastAction = encounter.CombatActionCount;
            int lastTurn = encounter.CombatTurnNumber;
            float noProgressDeadline = Time.realtimeSinceStartup + 1.5f;
            float battleDeadline = Time.realtimeSinceStartup + 8f;

            while (!encounter.CombatResolved && Time.realtimeSinceStartup < battleDeadline)
            {
                int action = encounter.CombatActionCount;
                int turn = encounter.CombatTurnNumber;
                if (action > lastAction || turn > lastTurn)
                {
                    Assert.That(action, Is.GreaterThanOrEqualTo(lastAction), encounter.BattleDiagnostic);
                    Assert.That(turn, Is.GreaterThanOrEqualTo(lastTurn), encounter.BattleDiagnostic);
                    lastAction = action;
                    lastTurn = turn;
                    noProgressDeadline = Time.realtimeSinceStartup + 1.5f;
                }

                Assert.That(Time.realtimeSinceStartup, Is.LessThan(noProgressDeadline),
                    "Kentridge battle entered a repeated no-progress state. " + encounter.BattleDiagnostic);
                Assert.That(action, Is.LessThanOrEqualTo(MaximumBattleActions),
                    "Kentridge battle exceeded its action bound. " + encounter.BattleDiagnostic);
                yield return null;
            }

            Assert.That(encounter.CombatResolved, Is.True,
                "Kentridge battle did not reach a terminal result before the watchdog. " + encounter.BattleDiagnostic);
            Assert.That(encounter.CombatActive, Is.False, encounter.BattleDiagnostic);
            Assert.That(encounter.CombatService.State, Is.EqualTo(CombatLifecycleState.Completed));
            Assert.That(encounter.WinningTeam.HasValue, Is.True, encounter.BattleDiagnostic);
            Assert.That(encounter.CombatActionCount, Is.GreaterThan(0).And.LessThanOrEqualTo(MaximumBattleActions));
            Assert.That(encounter.HasPendingCombatWork, Is.False, encounter.BattleDiagnostic);
            Assert.That(encounter.ActiveInputContext, Is.EqualTo(InputContextId.Exploration),
                "Terminal combat must release exclusive Combat input ownership.");

            var authority = encounter.CombatService as CombatService;
            Assert.That(authority, Is.Not.Null, "Kentridge must use the production CombatService authority.");
            AssertTerminalTeams(authority, encounter.BattleDiagnostic);
            Assert.That(authority.ActiveParticipant.IsValid, Is.False, "Terminal combat cannot retain an active turn owner.");

            // Stay in the trigger volume after terminal state. The resolved encounter must remain settled rather than
            // immediately starting a fresh session from the same nearby bandit.
            float settleDeadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < settleDeadline)
                yield return null;

            Assert.That(encounter.CombatResolved, Is.True);
            Assert.That(encounter.CombatActive, Is.False, "Resolved proximity combat restarted itself.");
            Assert.That(encounter.CombatService.ActiveSessionId, Is.EqualTo(session),
                "Settled Kentridge combat created a second session while the player remained beside the bandit.");
            Assert.That(encounter.HasPendingCombatWork, Is.False, encounter.BattleDiagnostic);
        }

        private static BattleSnapshot RunDeterministicBattle(int seed)
        {
            var participants = new CombatParticipant[]
            {
                new CombatParticipant(new CombatParticipantId("kentridge-player"), CombatTeam.Player),
                new CombatParticipant(new CombatParticipantId("forest-bandit-1"), CombatTeam.Enemy),
                new CombatParticipant(new CombatParticipantId("forest-bandit-2"), CombatTeam.Enemy),
                new CombatParticipant(new CombatParticipantId("forest-bandit-3"), CombatTeam.Enemy)
            };
            var combat = new CombatService();
            combat.BeginCombat(new CombatEncounterRequest("kentridge-forest-bandits", participants));
            var driver = new CombatAiBattleDriver(combat, seed);
            var actorsSeen = new HashSet<string>();

            while (combat.IsActive && driver.StepCount < MaximumBattleActions)
            {
                CombatParticipantId actor = combat.ActiveParticipant;
                Assert.That(actor.IsValid, Is.True, driver.Diagnostic("Active turn owner is invalid."));
                actorsSeen.Add(actor.Value);
                int beforeActions = combat.ActionCount;
                int beforeTurn = combat.TurnNumber;
                Assert.That(driver.Step(), Is.True, driver.Diagnostic("AI step refused to execute."));
                Assert.That(combat.ActionCount, Is.EqualTo(beforeActions + 1), driver.Diagnostic("Action counter did not advance exactly once."));
                Assert.That(!combat.IsActive || combat.TurnNumber >= beforeTurn, Is.True, driver.Diagnostic("Turn number regressed."));
            }

            if (combat.IsActive)
                Assert.Fail(driver.Diagnostic("Battle exceeded bounded action watchdog."));
            Assert.That(combat.WinningTeam.HasValue, Is.True, driver.Diagnostic("Terminal battle has no winner."));
            Assert.That(driver.HasPendingAction, Is.False, driver.Diagnostic("AI retained pending target after completion."));
            Assert.That(combat.HasPendingBattleWork, Is.False, driver.Diagnostic("Combat authority retained pending work after completion."));
            Assert.That(combat.ActiveParticipant.IsValid, Is.False, driver.Diagnostic("Combat authority retained active participant after completion."));

            bool playerAlive;
            bool enemyAlive;
            GetLivingTeams(combat, out playerAlive, out enemyAlive);
            return new BattleSnapshot(
                combat.WinningTeam,
                combat.ActionCount,
                combat.TurnNumber,
                playerAlive,
                enemyAlive,
                actorsSeen,
                driver.HasPendingAction || combat.HasPendingBattleWork,
                driver.Diagnostic("terminal"));
        }

        private static void AssertTerminalTeams(CombatService combat, string diagnostic)
        {
            bool playerAlive;
            bool enemyAlive;
            GetLivingTeams(combat, out playerAlive, out enemyAlive);
            Assert.That(playerAlive ^ enemyAlive, Is.True,
                "Terminal combat must leave exactly one team alive. " + diagnostic);
            Assert.That(combat.WinningTeam, Is.EqualTo(playerAlive ? CombatTeam.Player : CombatTeam.Enemy), diagnostic);
        }

        private static void GetLivingTeams(CombatService combat, out bool playerAlive, out bool enemyAlive)
        {
            playerAlive = false;
            enemyAlive = false;
            for (int i = 0; i < combat.ActiveParticipants.Count; i++)
            {
                CombatParticipant participant = combat.ActiveParticipants[i];
                if (!combat.IsAlive(participant.Id)) continue;
                if (participant.Team == CombatTeam.Player) playerAlive = true;
                else enemyAlive = true;
            }
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

        private readonly struct BattleSnapshot
        {
            public BattleSnapshot(
                CombatTeam? winner,
                int actionCount,
                int turnNumber,
                bool playerAlive,
                bool enemyAlive,
                HashSet<string> actorsSeen,
                bool pendingWork,
                string diagnostic)
            {
                Winner = winner;
                ActionCount = actionCount;
                TurnNumber = turnNumber;
                PlayerAlive = playerAlive;
                EnemyAlive = enemyAlive;
                ActorsSeen = actorsSeen;
                PendingWork = pendingWork;
                Diagnostic = diagnostic;
            }

            public CombatTeam? Winner { get; }
            public int ActionCount { get; }
            public int TurnNumber { get; }
            public bool PlayerAlive { get; }
            public bool EnemyAlive { get; }
            public HashSet<string> ActorsSeen { get; }
            public bool PendingWork { get; }
            public string Diagnostic { get; }
        }
    }
}
