using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Terrain.Runtime;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Generates a block of regions in a given order and fingerprints the result.
    ///
    /// This is the single most valuable check in spec 002. The design's central claim is that a
    /// region can generate its slice of the world from `(seed, catalogue, coordinate)` alone —
    /// no neighbours, no shared state, no accumulated structure. If that claim is false anywhere,
    /// generating the same block in a different order produces a different world, and this harness
    /// finds it.
    ///
    /// It matters because the failure it catches is otherwise invisible. Regions stream in
    /// whatever order a player happens to walk, so an order dependency does not crash and does not
    /// look wrong — it just means two players who approached from different directions are
    /// standing in different worlds.
    /// </summary>
    public static class GenerationOrderHarness
    {
        /// <summary>
        /// Generates every region in the block, in the order given, and returns a fingerprint of
        /// the whole block.
        ///
        /// The fingerprint is order-independent by construction: each region's contribution is
        /// hashed with its own coordinate before being combined, so the same regions generated in
        /// a different sequence produce the same value if and only if their *contents* match.
        /// </summary>
        public static ulong GenerateBlock(int3 blockMin, int3 blockSize, uint seed,
                                          int[] visitOrder, int poolCapacity = 4096)
        {
            int total = blockSize.x * blockSize.y * blockSize.z;

            if (visitOrder == null || visitOrder.Length != total)
                throw new ArgumentException($"visit order must contain exactly {total} indices");

            var fingerprints = new ulong[total];

            for (var step = 0; step < total; step++)
            {
                int linear = visitOrder[step];
                int3 coord = blockMin + new int3(
                    linear % blockSize.x,
                    (linear / blockSize.x) % blockSize.y,
                    linear / (blockSize.x * blockSize.y));

                var pool = new BrickPool(poolCapacity, Allocator.Temp);
                var region = new Region(coord, Allocator.Temp);

                TerrainGenerator.Generate(
                    new StandaloneRegionGenerationStore(in region), region.Coord, seed, ParityTerrain.Materials);

                fingerprints[linear] = FingerprintRegion(in region, coord);

                region.Dispose();
                pool.Dispose();
            }

            ulong combined = 0;
            for (var i = 0; i < total; i++) combined ^= fingerprints[i];
            return combined;
        }

        /// <summary>Fingerprint of one region's brick pointers, salted with its coordinate.</summary>
        public static ulong FingerprintRegion(in Region region, int3 coord)
        {
            ulong h = 0xcbf29ce484222325ul;

            h = Combine(h, (ulong)(uint)coord.x);
            h = Combine(h, (ulong)(uint)coord.y);
            h = Combine(h, (ulong)(uint)coord.z);

            for (var i = 0; i < VoxelDimensions.BricksPerRegion; i++)
                h = Combine(h, (ulong)(uint)region.BrickRefs[i].Value);

            return h;
        }

        private static ulong Combine(ulong h, ulong v)
        {
            h ^= v;
            h *= 0x100000001b3ul;
            return h;
        }

        /// <summary>
        /// A shuffled visit order, seeded so a failure can be reproduced.
        ///
        /// Reproducibility matters more than shuffle quality here: an order-dependence bug found
        /// by an unrepeatable shuffle is a bug you get to find twice.
        /// </summary>
        public static int[] ShuffledOrder(int count, uint shuffleSeed)
        {
            var order = new int[count];
            for (var i = 0; i < count; i++) order[i] = i;

            ulong state = shuffleSeed | 1ul;

            for (var i = count - 1; i > 0; i--)
            {
                state ^= state >> 12; state ^= state << 25; state ^= state >> 27;
                int j = (int)((state * 0x2545F4914F6CDD1Dul >> 33) % (ulong)(i + 1));

                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        public static int[] SequentialOrder(int count)
        {
            var order = new int[count];
            for (var i = 0; i < count; i++) order[i] = i;
            return order;
        }
    }
}

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// Terrain material slots for the parity harnesses.
    ///
    /// TerrainGenerator now takes the material set explicitly: the engine generates terrain from
    /// opaque indices and the game owns what they mean. These mirror Game.Materials.Runtime's
    /// GameTerrainMaterials.Default, duplicated rather than referenced because this is an engine
    /// test assembly and must not depend on the game layer.
    /// </summary>
    internal static class ParityTerrain
    {
        internal const byte Bedrock = 5;
        internal const byte Stone = 1;
        internal const byte Sand = 3;

        internal static readonly VoxelEngine.Terrain.Api.TerrainMaterialSet Materials =
            new VoxelEngine.Terrain.Api.TerrainMaterialSet(Bedrock, Stone, Sand);
    }
}
