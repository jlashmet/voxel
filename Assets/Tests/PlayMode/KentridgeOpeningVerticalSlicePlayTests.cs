using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Cutscenes.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine.TestTools;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Production vertical-slice acceptance test. This deliberately crosses the subsystem boundaries
    /// that matter to a player: recovered campaign authoring -> WorldBuilder resolution -> Kentridge
    /// settlement planning -> voxel building geometry -> authoritative NPC placement -> cutscene/story
    /// runtime. It is not a mocked campaign-planner test: every stable Kentridge structure is evaluated
    /// through the production shape program before the opening cutscene is played.
    /// </summary>
    public sealed class KentridgeOpeningVerticalSlicePlayTests
    {
        private const uint Seed = 0x4B454E54u;

        private sealed class Actor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public Actor(CutsceneInt3 position) => Position = position;

            public void PlaceAt(CutsceneStagePoint destination) =>
                Position = destination.Position;

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class ActorHost : IKentridgeCampaignActorHost
        {
            private readonly Dictionary<NpcRef, Actor> _npcs = new Dictionary<NpcRef, Actor>();
            private readonly Dictionary<int, Actor> _players = new Dictionary<int, Actor>();

            public void AddPlayer(int slot, Actor actor) => _players.Add(slot, actor);

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                for (var i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    _npcs[placement.Npc] = new Actor(ToCutscene(placement.Position.Position));
                }
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                Actor value;
                bool found = _npcs.TryGetValue(npc, out value);
                actor = value;
                return found;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                Actor value;
                bool found = _players.TryGetValue(playerSlot, out value);
                actor = value;
                return found;
            }
        }

        private sealed class CameraCueRuntime : ICutsceneCameraCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneCueId cue) => CompletedCutsceneOperation.Instance;
        }

        private sealed class DialogueCueRuntime : ICutsceneDialogueCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneActorId speaker, CutsceneCueId cue) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class SoundCueRuntime : ICutsceneSoundCueRuntime
        {
            public ICutsceneOperation Execute(CutsceneCueId cue) => CompletedCutsceneOperation.Instance;
        }

        [UnityTest]
        public IEnumerator ProductionOpeningGeneratesKentridgeBuildingsAndPlaysThroughIntroCutscene()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));

            Assert.That(content.Blueprint.Hierarchy.Regions.Count, Is.EqualTo(1),
                "The current production Kentridge generator consumes a one-region vertical slice.");
            Assert.That(content.Blueprint.Hierarchy.Regions[0].Ref.Id, Is.EqualTo("kentridge-overworld"));
            Assert.That(content.Blueprint.Hierarchy.Settlements.Count, Is.EqualTo(1));
            Assert.That(content.Blueprint.Hierarchy.Settlements[0].Ref.Id, Is.EqualTo("kentridge"));

            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            Assert.That(settlement.Plots.Count, Is.EqualTo(17),
                "The production Kentridge plan must retain all 17 stable building roles.");

            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                content.Blueprint,
                settlement);
            Assert.That(generation.Sites.IsResolved, Is.True,
                generation.Sites.Diagnostics.Count == 0
                    ? string.Empty
                    : string.Join("\n", generation.Sites.Diagnostics.Select(value => value.ToString())));

            ResolvedSiteId expectedPub = SettlementPlanSiteCandidateFacts.CandidateId(
                settlement.Id,
                (int)KentridgeRole.Pub);
            Assert.That(
                generation.Sites.Bindings.Single(value => value.Role.Equals(content.StartingPub)).Site,
                Is.EqualTo(expectedPub),
                "The semantic opening pub must resolve to the generated Kentridge pub building.");

            GenerateAndVerifyAllKentridgeBuildings();

            var actors = new ActorHost();
            var player = new Actor(new CutsceneInt3(-999, -999, -999));
            actors.AddPlayer(0, player);

            KentridgeCampaignSession session = KentridgeCampaignSessionBootstrap.CreateSession(
                content.Blueprint,
                generation,
                new KentridgeVoxelSiteRealizationFacts(settlement, 1),
                actors,
                Presentation());

            Assert.That(session.World.Npcs.Count, Is.EqualTo(4),
                "Madeline, Steven, Logan, and the destination NPC must all have generated-world placements.");
            CollectionAssert.AreEquivalent(
                new[] { "madeline", "steven", "logan", "destination-npc" },
                session.World.Npcs.Select(value => value.Npc.Id).ToArray());

            int matched = session.StartNewGame();
            Assert.That(matched, Is.EqualTo(1));
            Assert.That(session.Runtime.HasActiveCutscene, Is.True);
            Assert.That(session.Runtime.ActiveCutscene, Is.EqualTo(content.IntroCutscene));

            CutsceneStageRealization openingStage = session.World.CutsceneStages
                .Single(value => value.Cutscene.Equals(content.IntroCutscene));
            Assert.That(
                player.Position,
                Is.EqualTo(openingStage.Binding.Resolve(KentridgeOpeningCutscene.LeadStart).Position),
                "Starting a new game must put the player at the generated Kentridge pub cutscene stage.");

            for (var frame = 0; frame < 64 && session.Runtime.HasActiveCutscene; frame++)
            {
                session.Runtime.Tick(100000);
                yield return null;
            }

            Assert.That(session.Runtime.HasActiveCutscene, Is.False,
                "The Kentridge opening cutscene did not finish within the PlayMode frame budget.");
            Assert.That(session.Runtime.IsCutsceneCompleted(content.IntroCutscene), Is.True,
                "A player must be able to play through the opening cutscene to completion.");
            Assert.That(session.Runtime.IsObjectiveActive(content.TravelObjective), Is.True,
                "Finishing the opening cutscene must advance the playable story into the travel objective.");
        }

        private static void GenerateAndVerifyAllKentridgeBuildings()
        {
            FeatureCatalogue catalogue = KentridgeCombinedVoxelCatalogue.Build(
                Seed,
                BuildSettings(),
                Allocator.Temp);
            var primitives = new NativeList<Primitive>(256, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Temp);

            try
            {
                var generatedStructures = 0;
                var primitiveCount = 0;

                for (var ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    if (definition.Kind != FeatureKind.Structure) continue;

                    for (var i = 0; i < rule.ExplicitCount; i++)
                    {
                        ExplicitPlacement placement = catalogue.ExplicitPlacements[rule.ExplicitOffset + i];
                        primitives.Clear();
                        anchors.Clear();

                        ParameterSet parameters = FeatureGeneration.ResolveParameters(
                            in catalogue,
                            in definition,
                            in placement,
                            rule.DefinitionId,
                            placement.Position,
                            Seed);
                        ulong instanceSeed = FeatureGeneration.InstanceSeed(
                            Seed,
                            rule.DefinitionId,
                            placement.Position);

                        EvaluationResult result = ShapeProgram.Evaluate(
                            in catalogue,
                            rule.DefinitionId,
                            in parameters,
                            placement.Position,
                            placement.Orientation,
                            Seed,
                            instanceSeed,
                            primitives,
                            anchors);

                        Assert.That(result, Is.EqualTo(EvaluationResult.Ok),
                            definition.Name + " failed to generate during the production PlayMode slice.");
                        Assert.That(primitives.Length, Is.GreaterThan(0),
                            definition.Name + " emitted no voxel geometry.");

                        generatedStructures++;
                        primitiveCount += primitives.Length;
                    }
                }

                Assert.That(generatedStructures, Is.EqualTo(17),
                    "Every stable Kentridge building role must emit geometry in the playable slice.");
                Assert.That(primitiveCount, Is.GreaterThan(100),
                    "The generated Kentridge buildings emitted implausibly little geometry.");
            }
            finally
            {
                primitives.Dispose();
                anchors.Dispose();
                catalogue.Dispose();
            }
        }

        private static CutscenePresentationRouter Presentation() =>
            new CutscenePresentationRouter(
                new CameraCueRuntime(),
                new DialogueCueRuntime(),
                new SoundCueRuntime());

        private static CutsceneDefinition DialogueOnly(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId(id + ".dialogue")) });

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1,
                masonry: 1,
                darkMasonry: 6,
                timber: 2,
                glass: 4,
                warmWindow: 15,
                roofTile: 8,
                slate: 7,
                cloth: 9,
                moss: 14,
                water: 11,
                roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
        }

        private static CutsceneInt3 ToCutscene(Int3 value) =>
            new CutsceneInt3(value.X, value.Y, value.Z);
    }
}
