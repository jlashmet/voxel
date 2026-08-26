using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatVegetationV11Tests
    {
        [UnityTest]
        public IEnumerator GuidedCascadeCrossesProductionVegetationApiBoundary()
        {
            var treeDamage = new RecordingTreeDamageService();
            var environment = new ChainCombatVegetationBridge(treeDamage);
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            var scenario = new ChainCombatDemoScenario(board, reservations, environment);

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);

            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact));
            Assert.That(treeDamage.SweepCalls, Is.EqualTo(1),
                "The real combat cascade must cross the stable Vegetation.Api collision boundary when a body hits a tree.");
            Assert.That(treeDamage.LastSweepRadius, Is.GreaterThan(0f));
            Assert.That(math.distance(treeDamage.LastSweepTo, treeDamage.LastSweepFrom), Is.GreaterThan(0f));

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);

            Assert.That(scenario.IsComplete, Is.True);
            Assert.That(treeDamage.BlastCalls, Is.EqualTo(1),
                "Felling the tree must be mirrored into the production vegetation damage capability, not remain demo-only state.");
            Assert.That(treeDamage.LastBlastRadius, Is.GreaterThan(0f));
            Assert.That(math.length(treeDamage.LastImpulse), Is.GreaterThan(0f));
            Assert.That(treeDamage.LastImpulse.x, Is.LessThan(0f),
                "The production vegetation impulse must preserve the demo tree's westward fall direction.");
            Assert.That(board.LastCascadePlayers, Is.EqualTo(4));
            Assert.That(board.LastHandoffs, Is.EqualTo(3));
            yield return null;
        }

        private sealed class RecordingTreeDamageService : ITreeDamageService
        {
            public int SweepCalls { get; private set; }
            public int BlastCalls { get; private set; }
            public float3 LastSweepFrom { get; private set; }
            public float3 LastSweepTo { get; private set; }
            public float LastSweepRadius { get; private set; }
            public float3 LastBlastImpact { get; private set; }
            public float LastBlastRadius { get; private set; }
            public float3 LastImpulse { get; private set; }

            public bool OverlapsWoodAabb(float3 minMetres, float3 maxMetres) => false;

            public bool TrySweepImpact(
                float3 fromMetres,
                float3 toMetres,
                float sweepRadiusMetres,
                out float3 hitMetres,
                out int treeIndex)
            {
                SweepCalls++;
                LastSweepFrom = fromMetres;
                LastSweepTo = toMetres;
                LastSweepRadius = sweepRadiusMetres;
                hitMetres = toMetres;
                treeIndex = 42;
                return true;
            }

            public void ApplyBlast(float3 impactMetres, float blastRadiusMetres, float3 impulse)
            {
                BlastCalls++;
                LastBlastImpact = impactMetres;
                LastBlastRadius = blastRadiusMetres;
                LastImpulse = impulse;
            }
        }
    }
}
