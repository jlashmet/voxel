using System.Collections.Generic;
using System.Linq;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Player-facing acceptance for the boundary the opening slice actually needs: story control begins
    /// in the generated pub, is released after the authored intro, and voxel collision permits a player
    /// capsule to cross the generated pub doorway into the same generated Kentridge exterior.
    /// </summary>
    public sealed class KentridgePubExitPlayTests
    {
        private const uint Seed = 0x4B454E54u;
        private const float DecimetresToMetres = 0.1f;

        private sealed class MotorActor : ICutsceneActorRuntime
        {
            private readonly CharacterMotor _motor;
            public CutsceneInt3 Position => ToCutscene(_motor.Position);

            public MotorActor(CharacterMotor motor) => _motor = motor;

            public void PlaceAt(CutsceneStagePoint destination)
            {
                _motor.Position = ToMetres(destination.Position);
                _motor.Velocity = Vector3.zero;
            }

            public ICutsceneOperation MoveTo(
                CutsceneStagePoint destination,
                int durationHintMilliseconds)
            {
                PlaceAt(destination);
                return CompletedCutsceneOperation.Instance;
            }

            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class MemoryActor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }
            public MemoryActor(CutsceneInt3 position) => Position = position;
            public void PlaceAt(CutsceneStagePoint destination) => Position = destination.Position;
            public ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }
            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) =>
                CompletedCutsceneOperation.Instance;
        }

        private sealed class ActorHost : IKentridgeCampaignActorHost
        {
            private readonly MotorActor _player;
            private readonly Dictionary<NpcRef, MemoryActor> _npcs =
                new Dictionary<NpcRef, MemoryActor>();

            public ActorHost(CharacterMotor motor) => _player = new MotorActor(motor);

            public void PrepareNpcs(IReadOnlyList<ResolvedNpcWorldPlacement> placements)
            {
                _npcs.Clear();
                for (int i = 0; i < placements.Count; i++)
                {
                    ResolvedNpcWorldPlacement placement = placements[i];
                    Int3 point = placement.Position.Position;
                    _npcs.Add(placement.Npc, new MemoryActor(new CutsceneInt3(point.X, point.Y, point.Z)));
                }
            }

            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                MemoryActor value;
                bool found = _npcs.TryGetValue(npc, out value);
                actor = value;
                return found;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = playerSlot == 0 ? _player : null;
                return actor != null;
            }
        }

        private sealed class Presentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }

        [Test]
        public void NewGame_StartsInKentridgePub_PlaysIntro_ThenPlayerCanWalkThroughDoorIntoTown()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                DialogueOnly("destination-conversation"));
            SettlementPlan settlement = KentridgeDefinition.Build(Seed);
            KentridgeCampaignGenerationPlan generation = KentridgeCampaignSessionBootstrap.Plan(
                content.Blueprint,
                settlement);

            KentridgeGameplaySiteAccess access;
            Assert.That(
                KentridgeGameplaySiteAccessResolver.TryResolve(
                    settlement,
                    (int)KentridgeRole.Pub,
                    1,
                    out access),
                Is.True,
                "The generated pub must expose its exact physical entrance to gameplay.");

            var motor = new CharacterMotor { WalkSpeed = 5.5f };
            var actors = new ActorHost(motor);
            KentridgeCampaignSession session = KentridgeCampaignSessionBootstrap.CreateSession(
                content.Blueprint,
                generation,
                new KentridgeVoxelSiteRealizationFacts(settlement, 1),
                actors,
                new Presentation());

            FeatureCatalogue catalogue = default(FeatureCatalogue);
            ShowcaseWorld world = null;
            try
            {
                world = new ShowcaseWorld(Seed, 65536, 2, 3);
                catalogue = KentridgeCombinedVoxelCatalogue.Build(
                    settlement,
                    BuildSettings(),
                    generation.HiddenSpaces,
                    Allocator.Persistent);
                world.ConfigureGeneratedContentForGameplay(catalogue);
                catalogue = default(FeatureCatalogue); // owned by world

                CutsceneStageRealization stage = session.World.CutsceneStages
                    .Single(value => value.Cutscene.Equals(content.IntroCutscene));
                GenerateAt(world, stage.Binding.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                GenerateAt(world, stage.Binding.Resolve(KentridgeOpeningCutscene.LeadStage).Position);
                GenerateAt(world, access.InteriorApproach);
                GenerateAt(world, access.Entrance);
                GenerateAt(world, access.ExteriorApproach);

                Assert.That(session.StartNewGame(), Is.EqualTo(1));
                Assert.That(session.Runtime.HasActiveCutscene, Is.True,
                    "New Game must take gameplay control for the opening cutscene.");
                Vector3 expectedStart = ToMetres(
                    stage.Binding.Resolve(KentridgeOpeningCutscene.LeadStart).Position);
                Assert.That(Vector3.Distance(motor.Position, expectedStart), Is.LessThan(0.001f),
                    "The authoritative player motor must start on the generated pub stage.");

                for (int tick = 0; tick < 64 && session.Runtime.HasActiveCutscene; tick++)
                    session.Runtime.Tick(100000);

                Assert.That(session.Runtime.HasActiveCutscene, Is.False,
                    "Completing the authored opening must release gameplay control.");
                Assert.That(session.Runtime.IsCutsceneCompleted(content.IntroCutscene), Is.True);
                Assert.That(session.Runtime.IsObjectiveActive(content.TravelObjective), Is.True);

                Vector3 entrance = ToMetres(access.Entrance);
                Vector3 inward = new Vector3(access.Inward.X, 0f, access.Inward.Y);
                float cutsceneDepth = Vector3.Dot(motor.Position - entrance, inward);
                Assert.That(cutsceneDepth, Is.GreaterThan(0.5f),
                    "The opening must finish with the player physically inside the generated pub.");

                // Isolate the architecture/collision acceptance from interior pathfinding. The player
                // remains inside the same pub; from this point the straight route to the target passes
                // through the real carved doorway used by Kentridge voxel generation.
                motor.Position = ToMetres(access.InteriorApproach);
                motor.Velocity = Vector3.zero;
                Vector3 exteriorTarget = ToMetres(access.ExteriorApproach);

                bool reachedExterior = WalkTo(motor, world, exteriorTarget, 600);
                Assert.That(reachedExterior, Is.True,
                    "Voxel collision blocked the player from crossing the generated Kentridge pub doorway.");

                float exteriorDepth = Vector3.Dot(motor.Position - entrance, inward);
                Assert.That(exteriorDepth, Is.LessThan(-0.75f),
                    "After crossing the pub doorway the player must be on the Kentridge-town side of the entrance.");
            }
            finally
            {
                world?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
            }
        }

        private static bool WalkTo(
            CharacterMotor motor,
            ShowcaseWorld world,
            Vector3 target,
            int maxSteps)
        {
            const float dt = 1f / 60f;
            for (int step = 0; step < maxSteps; step++)
            {
                Vector3 delta = target - motor.Position;
                delta.y = 0f;
                if (delta.magnitude <= 0.4f) return true;
                motor.Step(world, delta.normalized, false, false, dt);
            }
            return false;
        }

        private static void GenerateAt(ShowcaseWorld world, CutsceneInt3 point) =>
            world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(ToMetres(point)));

        private static void GenerateAt(ShowcaseWorld world, RealizedWorldPoint point) =>
            world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(ToMetres(point)));

        private static Vector3 ToMetres(CutsceneInt3 point) =>
            new Vector3(
                point.X * DecimetresToMetres,
                point.Y * DecimetresToMetres,
                point.Z * DecimetresToMetres);

        private static Vector3 ToMetres(RealizedWorldPoint point)
        {
            float scale = DecimetresToMetres / point.UnitsPerDecimetre;
            return new Vector3(
                point.Position.X * scale,
                point.Position.Y * scale,
                point.Position.Z * scale);
        }

        private static CutsceneInt3 ToCutscene(Vector3 metres) =>
            new CutsceneInt3(
                Mathf.RoundToInt(metres.x / DecimetresToMetres),
                Mathf.RoundToInt(metres.y / DecimetresToMetres),
                Mathf.RoundToInt(metres.z / DecimetresToMetres));

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
    }
}
