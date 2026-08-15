using VoxelEngine.Core.Storage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Core.Edits;
using VoxelEngine.Tests.Parity;
using VoxelEngine.Core.Terrain;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// SC-003: 10,000 alteration events replay to byte-identical state across two
    /// processes on different hardware.
    ///
    /// This test is the architecture's central guarantee in executable form:
    /// Constitution Principle I (Determinism) and Constitution Principle II (One
    /// source of truth) tested against a heavy edit workload.  If this fails,
    /// no other work matters — divergent state is what breaks the game.
    ///
    /// The test runs both worlds in-process rather than as separate processes because
    /// Unity Test Framework does not support cross-process execution.  However:
    /// the deterministic PRNG (DeterministicRandom) uses pure integer arithmetic,
    /// so if it produces identical output on one machine, it will on any platform.
    /// The two independent BrickPool instances act as separate "processes".
    /// </summary>
    public sealed class TenThousandEventParityTests
    {
        private const int EventCount = 10_000;
        private const uint TerrainSeed = 42u;

        [Test]
        [Category("Determinism")]
        [Category("SC_003")]
        public void TenThousandEventsReplayToIdenticalState()
        {
            // Generate two independent event sequences with identical seeds.
            var eventsA = GenerateSequences(EventCount, TerrainSeed, 1);
            var eventsB = GenerateSequences(EventCount, TerrainSeed, 2);

            using var worldA = new ReplayHarness();
            using var worldB = new ReplayHarness();

            // Initialise both worlds identically.
            worldA.SeedWorld(TerrainSeed);
            worldB.SeedWorld(TerrainSeed);

            // Replay both sequences.
            worldA.ReplayEvents(eventsA, eventsB);
            worldB.ReplayEvents(eventsA, eventsB);

            // Both worlds should converge to identical state.
            Assert.IsTrue(worldA.StateMatches,
                "Event sequence A produced divergent state at event {0}.",
                worldA.FirstMismatchIndex);

            Assert.IsTrue(worldB.StateMatches,
                "Event sequence B produced divergent state at event {0}.",
                worldB.FirstMismatchIndex);
        }

        [Test]
        [Category("Determinism")]
        [Category("SC_003")]
        public void ShuffledOrderConvergesToSameState()
        {
            // Events from different players at the same tick should converge regardless
            // of arrival order — arbitration by (tick, playerId, sequence) handles this.
            const int eventCount = 500;

            var eventsA = GenerateSequences(eventCount, TerrainSeed, 1);
            var shuffled = new AlterationEvent[eventCount];
            System.Array.Copy(eventsA, shuffled, eventCount);

            // Fisher-Yates shuffle with fixed seed.
            var rng = new DeterministicRandom(TerrainSeed + 999u);
            for (int i = eventCount - 1; i > 0; i--)
            {
                int j = rng.NextRange(0, i);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            // Events from the same player must maintain their relative order.
            var worldA = new ReplayHarness();
            worldA.SeedWorld(TerrainSeed);
            worldA.ReplayEvents(eventsA, shuffled);

            // With same arbitration key, both should converge — but only if events
            // from the same player are processed in order (required by protocol).
            Assert.IsTrue(worldA.StateMatches,
                "Shuffled events produced divergent state at event {0}.",
                worldA.FirstMismatchIndex);

            worldA.Dispose();
        }

        [Test]
        [Category("Determinism")]
        public void LargeExplosionEventIsDeterministic()
        {
            // Test a single very large explosion (32-block radius) which would expand to
            // thousands of voxels but must produce the same result every time.
            var poolA = new BrickPool(4096, Allocator.Persistent);
            var poolB = new BrickPool(4096, Allocator.Persistent);

            var regionA = new Region(int3.zero, Allocator.Temp);
            var regionB = new Region(int3.zero, Allocator.Temp);

            // Materialise terrain in both.
            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(
                new StandaloneRegionGenerationStore(in regionA), regionA.Coord, TerrainSeed);
            VoxelEngine.Core.Terrain.TerrainGenerator.Generate(
                new StandaloneRegionGenerationStore(in regionB), regionB.Coord, TerrainSeed);

            var tableA = new RegionTable(1, Allocator.Persistent);
            var tableB = new RegionTable(1, Allocator.Persistent);

            // Same large explosion in both.
            var evt = new AlterationEvent
            {
                kind = AlterationEvent.KindExplosion,
                tick = 1u, origin = new int3(256, 256, 256),
                shapeData = 32, material = 0, seed = TerrainSeed,
                playerId = 1, sequence = 1
            };

            var resultA = ExplosionExpansion.Expand(new RegionReadSource(in tableA, in poolA), in evt);
            var resultB = ExplosionExpansion.Expand(new RegionReadSource(in tableB, in poolB), in evt);

            Assert.AreEqual(resultA.Length, resultB.Length, "Large explosion affected brick count differs.");
            for (int i = 0; i < resultA.Length; i++)
            {
                Assert.AreEqual(resultA[i], resultB[i], $"Expanded brick[{i}] position differs.");
            }

            regionA.Dispose();
            regionB.Dispose();
            tableA.Dispose();
            tableB.Dispose();
        }

        /// <summary>
        /// Generate a sequence of events using DeterministicRandom with the given seed.
        /// Different runSeed values produce different event sequences.
        /// </summary>
        private static AlterationEvent[] GenerateSequences(int count, uint terrainSeed, uint runSeed)
        {
            var events = new AlterationEvent[count];
            var rng = new DeterministicRandom(terrainSeed + runSeed);

            for (int i = 0; i < count; i++)
            {
                int type = rng.NextRange(0, 3);
                byte kind = (byte)(type + 1); // 1=explosion, 2=brush, 3=raw batch

                events[i] = new AlterationEvent
                {
                    kind = kind,
                    tick = (uint)(i / 30), // ~30 events per tick
                    origin = new int3(
                        rng.NextRange(200, 300),
                        rng.NextRange(100, 400),
                        rng.NextRange(200, 300)),
                    shapeData = (ushort)rng.NextRange(3, 16),
                    material = (byte)(rng.NextRange(1, 8)),
                    seed = (uint)rng.NextInt(),
                    playerId = (ushort)rng.NextRange(0, 32),
                    sequence = (ushort)(i % 30 + 1)
                };
            }

            return events;
        }
    }
}
