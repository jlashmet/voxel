using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatImpactV3Tests
    {
        [UnityTest]
        public IEnumerator ClaimedEventCannotBePassedUntilReleased()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);

            int opportunityId = board.PendingReaction.Id;
            Assert.That(board.PassReaction(), Is.False, "A global pass must not steal an event from the player who claimed it.");
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Id, Is.EqualTo(opportunityId));
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(weldon.Id));

            Assert.That(board.TryReleaseClaim(weldon.Id), Is.True, board.LastMessage);
            Assert.That(board.PassReaction(), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null, "Passing the airborne claim should resume motion and reach the prepared collision.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator ForceMultiplierTurnsSameLaunchIntoHarderImpact()
        {
            var weakBoard = new ChainCombatBoard();
            ChainUnitState weakStephen = Find(weakBoard, ChainRecruitKind.Stephen);
            ChainUnitState weakOgre = Find(weakBoard, ChainRecruitKind.Ogre);
            ChainUnitState weakGoblin = FindByName(weakBoard, "Goblin A");
            int weakGoblinHp = weakGoblin.Hp;

            Assert.That(weakBoard.TryUppercut(weakStephen.Id, weakOgre.Id), Is.True, weakBoard.LastMessage);
            Assert.That(weakBoard.PassReaction(), Is.True, weakBoard.LastMessage);
            Assert.That(weakBoard.PendingReaction, Is.Not.Null);
            Assert.That(weakBoard.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            int weakForce = weakBoard.PendingReaction.ImpactForce;
            int weakDamage = weakGoblinHp - weakGoblin.Hp;

            var amplifiedBoard = new ChainCombatBoard();
            ChainUnitState mira = Find(amplifiedBoard, ChainRecruitKind.Mira);
            ChainUnitState strongStephen = Find(amplifiedBoard, ChainRecruitKind.Stephen);
            ChainUnitState strongOgre = Find(amplifiedBoard, ChainRecruitKind.Ogre);
            ChainUnitState strongGoblin = FindByName(amplifiedBoard, "Goblin A");
            int strongGoblinHp = strongGoblin.Hp;

            Assert.That(amplifiedBoard.TryPlaceAmplifier(mira.Id, new GridPos(4, 4)), Is.True, amplifiedBoard.LastMessage);
            Assert.That(amplifiedBoard.TryUppercut(strongStephen.Id, strongOgre.Id), Is.True, amplifiedBoard.LastMessage);
            Assert.That(amplifiedBoard.PassReaction(), Is.True, amplifiedBoard.LastMessage);
            Assert.That(amplifiedBoard.PendingReaction, Is.Not.Null);
            Assert.That(amplifiedBoard.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            int strongForce = amplifiedBoard.PendingReaction.ImpactForce;
            int strongDamage = strongGoblinHp - strongGoblin.Hp;

            Assert.That(weakForce, Is.EqualTo(1));
            Assert.That(strongForce, Is.EqualTo(5));
            Assert.That(strongForce, Is.GreaterThan(weakForce));
            Assert.That(strongDamage, Is.GreaterThan(weakDamage), "The multiplier should matter to the eventual consequence, not only travel distance.");
            Assert.That(weakDamage, Is.EqualTo(1));
            Assert.That(strongDamage, Is.EqualTo(3));

            yield return null;
        }

        [UnityTest]
        public IEnumerator FourPlayerRouteRecordsThreeActualHandoffs()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState grom = Find(board, ChainRecruitKind.Grom);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);
            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.True, board.LastMessage);
            Assert.That(board.TryRepulse(madeline.Id, goblinA.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact));
            Assert.That(board.PendingReaction.ImpactForce, Is.EqualTo(2));

            Assert.That(board.TryClaimReaction(grom.Id, ChainReactionAbility.Timber), Is.True, board.LastMessage);
            Assert.That(board.TryTimber(grom.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            Assert.That(board.LastCascadeSteps, Is.EqualTo(4));
            Assert.That(board.LastCascadePlayers, Is.EqualTo(4));
            Assert.That(board.LastHandoffs, Is.EqualTo(3));
            Assert.That(board.BestHandoffs, Is.EqualTo(3));

            yield return null;
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                if (board.Units[i].Kind == kind) return board.Units[i];
            }
            Assert.Fail($"Could not find unit kind {kind}.");
            return null;
        }

        private static ChainUnitState FindByName(ChainCombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                if (board.Units[i].Name == name) return board.Units[i];
            }
            Assert.Fail($"Could not find unit {name}.");
            return null;
        }
    }
}
