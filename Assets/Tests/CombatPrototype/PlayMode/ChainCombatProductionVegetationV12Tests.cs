using System.Collections;
using Game.Composition.CombatEnvironment.Runtime;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;

// UnityEngine also defines a TreeInstance (the legacy terrain one), and both namespaces are
// in scope here, so the unqualified name is ambiguous.
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatProductionVegetationV12Tests
    {
        [TearDown]
        public void TearDown()
        {
            TreeWorldRuntime.Clear();
        }

        [UnityTest]
        public IEnumerator PlayableCascadeMutatesRealProceduralTreeRuntime()
        {
            TreeWorldRuntime.Replace(new[]
            {
                new TreeInstance
                {
                    PositionMetres = new float3(11f, 0f, 4f),
                    Species = TreeSpecies.Oak,
                    Seed = 0xC0FFEEu,
                    Scale = 1f,
                },
            });

            Assert.That(TreeWorldRuntime.Instances.Count, Is.EqualTo(1));
            Assert.That(TreeWorldRuntime.Damage.Count, Is.EqualTo(1));
            Assert.That(TreeWorldRuntime.Damage[0].Severed, Is.False);

            var root = new GameObject("Combat Production Vegetation Integration Test Root");
            root.AddComponent<ChainCombatLabController>();
            root.AddComponent<ChainExecutionPlanner>();
            root.AddComponent<ChainPlanApprovalCoordinator>();
            root.AddComponent<ChainCombatActivationOverlay>();
            root.AddComponent<ChainCombatEventMarker>();
            root.AddComponent<ChainCombatMotionPlayback>();
            root.AddComponent<ChainEnemyIntentOverlay>();
            ChainCombatDemoGuide guide = root.AddComponent<ChainCombatDemoGuide>();
            ChainCombatVegetationComposition composition = root.AddComponent<ChainCombatVegetationComposition>();

            yield return null;
            Assert.That(composition, Is.Not.Null);

            guide.ResetGuidedDemo();
            yield return null;

            guide.AdvanceOneStep();
            yield return null;
            guide.AdvanceOneStep();
            yield return null;
            guide.AdvanceOneStep();
            yield return null;

            Assert.That(ProceduralTreeDamageService.LastBroadphaseCandidateCount, Is.GreaterThan(0),
                "Madeline's real tree impact must query the production procedural-tree broadphase.");
            Assert.That(ProceduralTreeDamageService.ResidentSkeletonCount, Is.GreaterThan(0),
                "The production vegetation service should build a semantic tree skeleton for the impacted tree.");
            Assert.That(TreeWorldRuntime.Damage[0].Severed, Is.False,
                "The impact handoff should query the tree without prematurely severing it before Grom acts.");

            guide.AdvanceOneStep();
            yield return null;

            Assert.That(TreeWorldRuntime.Damage[0].Severed, Is.True,
                "Grom's environmental finisher must sever the real procedural tree, not only the combat board's synthetic tree.");
            Assert.That(TreeWorldRuntime.RemovedBranches(0).Count, Is.GreaterThan(0),
                "The real vegetation runtime should record structural branch removal from the combat finisher.");

            Object.Destroy(root);
            yield return null;
        }
    }
}
