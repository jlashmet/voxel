using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatMechanicsV2Tests
    {
        [UnityTest]
        public IEnumerator CascadeLabControllerBootsFourPlayerBattle()
        {
            var root = new GameObject("Cascade Lab Test Root");
            root.AddComponent<ChainCombatLabController>();

            yield return null;

            Assert.That(GameObject.Find("Chain Combat Lab Camera"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Combat Lab Light"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Combat Lab Visuals"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Stephen"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Brutus"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Weldon"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Madeline"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Mira"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Grom"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Skitter"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Ogre"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Cell 0,0"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Cell 13,9"), Is.Not.Null);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AirborneEventHasCompetingClaimsAndFirstValidClaimOwnsIt()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState brutus = Find(board, ChainRecruitKind.Brutus);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            AssertOpportunity(board, ChainReactionKind.Airborne, ogre.Id);
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(1));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(1));

            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(weldon.Id));
            Assert.That(board.PendingReaction.ClaimedByCommandGroup, Is.EqualTo(2));

            // Brutus is also physically eligible here, but cannot steal Weldon's authoritative reservation.
            Assert.That(board.TryClaimReaction(brutus.Id, ChainReactionAbility.CatchThrow), Is.False);
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(weldon.Id));

            Assert.That(board.TryReleaseClaim(weldon.Id), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.IsClaimed, Is.False);
            Assert.That(board.TryClaimReaction(brutus.Id, ChainReactionAbility.CatchThrow), Is.True, board.LastMessage);
            Assert.That(board.TryCatchThrow(brutus.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            Assert.That(brutus.ReactionSpent, Is.True);
            Assert.That(board.CurrentCascadeSteps, Is.GreaterThanOrEqualTo(2));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(1), "Stephen and Brutus are intentionally in the same P1 command group.");
            Assert.That(board.PendingReaction, Is.Not.Null, "Brutus's alternate route should create another physical event in the prepared geometry.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CollisionAndTreeEventsBranchBeforeFourPlayerCascadeFinishes()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState grom = Find(board, ChainRecruitKind.Grom);
            ChainUnitState skitter = Find(board, ChainRecruitKind.Skitter);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");

            // P1 creates a physical fact. The game does not select P2 for us.
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);
            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            AssertOpportunity(board, ChainReactionKind.Collision, ogre.Id);
            Assert.That(board.PendingReaction.SecondaryUnitId, Is.EqualTo(goblinA.Id));
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(2));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(2));

            // Collision is not a Madeline password: Skitter can claim the same event from his current position.
            Assert.That(board.TryClaimReaction(skitter.Id, ChainReactionAbility.HookYank), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.False, "First valid claimant must own the event.");
            Assert.That(board.TryReleaseClaim(skitter.Id), Is.True, board.LastMessage);

            // P3 chooses the other branch and aims Goblin A into the tree.
            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.True, board.LastMessage);
            Assert.That(board.TryRepulse(madeline.Id, goblinA.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            AssertOpportunity(board, ChainReactionKind.TreeImpact, goblinA.Id);
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(3));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(3));

            // The tree event branches too: Skitter can extend the victim elsewhere, or Grom can commit the tree fall.
            Assert.That(board.TryClaimReaction(skitter.Id, ChainReactionAbility.HookYank), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(grom.Id, ChainReactionAbility.Timber), Is.False);
            Assert.That(board.TryReleaseClaim(skitter.Id), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(grom.Id, ChainReactionAbility.Timber), Is.True, board.LastMessage);
            Assert.That(board.TryTimber(grom.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            Assert.That(board.PendingReaction, Is.Null);
            Assert.That(board.LastCascadeSteps, Is.EqualTo(4));
            Assert.That(board.LastCascadePlayers, Is.EqualTo(4));
            Assert.That(board.BestCascadeSteps, Is.EqualTo(4));
            Assert.That(board.BestCascadePlayers, Is.EqualTo(4));
            Assert.That(weldon.ReactionSpent, Is.True);
            Assert.That(madeline.ReactionSpent, Is.True);
            Assert.That(grom.ReactionSpent, Is.True);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ClaimMustMatchCapabilityRangeAndExecutor()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);

            // Repulse describes collisions, so trying it against an airborne event does not reserve anything.
            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.False);
            Assert.That(board.PendingReaction.IsClaimed, Is.False);

            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);

            // The claimant, capability, and executor are all part of the authoritative reservation.
            Assert.That(board.TryFollowThrough(stephen.Id, ogre.Id, new GridPos(13, 4)), Is.False);
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(weldon.Id));
            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

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

        private static void AssertOpportunity(ChainCombatBoard board, ChainReactionKind kind, int primaryUnitId)
        {
            Assert.That(board.PendingReaction, Is.Not.Null, "Expected an unresolved physical event.");
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(kind));
            Assert.That(board.PendingReaction.PrimaryUnitId, Is.EqualTo(primaryUnitId));
            Assert.That(board.PendingReaction.IsClaimed, Is.False, "New physical events should begin unclaimed.");
        }
    }
}
