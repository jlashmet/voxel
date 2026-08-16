using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Turns clean masonry into weathered masonry by repainting voxels, never by adding geometry.
    ///
    /// Measured against the arch reference, bare stone sits at median saturation 15 and detail
    /// energy 0.016 against targets of 59 and 0.048. Almost all of that difference is vegetation:
    /// the painted arch is a ruin with moss packed into every joint and hanging down the faces,
    /// and moss is both the colour and most of the surface break-up. Chasing those numbers with
    /// lighting would have made the stone wrong in order to fake the plants.
    ///
    /// Deliberately structure-agnostic. It takes a volume and coats whatever exposed surfaces it
    /// finds, so the same call weathers an arch, a wall, a tower or a whole town. Nothing here
    /// knows what an arch is - which is the lesson from the analytic arch shader this replaced.
    /// </summary>
    public static class MasonryWeathering
    {
        /// <summary>
        /// Coats upward-facing exposed surfaces, then lets the coating creep down the faces below.
        ///
        /// Working from "solid voxel with empty above" rather than "highest voxel in the column"
        /// matters for architecture: it catches impost ledges, string courses and the extrados of
        /// an arch ring, not just the silhouette. In the reference those projecting courses carry
        /// the heaviest growth, because they are what actually collects soil.
        /// </summary>
        /// <param name="coverage">0-255. Fraction of eligible surfaces that take the coating.</param>
        /// <param name="dripPasses">
        /// How far growth hangs below an established patch. Each pass extends it one voxel, with
        /// its own thinning roll, so patches taper instead of ending in a straight line.
        /// </param>
        public static int CoatExposedSurfaces(ref VoxelBrush brush, int3 min, int3 size,
                                              byte coatingId, uint seed,
                                              byte coverage = 200, int dripPasses = 10)
        {
            if (math.any(size <= 0)) return 0;

            int3 max = min + size;
            var pending = new List<int3>();

            // Pass 1: upward-facing surfaces. Collected before any write, because painting as we
            // scan would let a freshly coated voxel seed its own neighbours and the growth would
            // march along the surface instead of landing in patches.
            for (int z = min.z; z < max.z; z++)
            for (int y = min.y; y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                if (!brush.IsSolid(x, y, z)) continue;
                if (brush.IsSolid(x, y + 1, z)) continue;
                if (Hash(x, y, z, seed) > coverage) continue;
                pending.Add(new int3(x, y, z));
            }

            int painted = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                int3 v = pending[i];
                if (brush.GetCoating(v.x, v.y, v.z) == coatingId) continue;
                brush.Coat(v.x, v.y, v.z, coatingId);
                painted++;
            }

            // Grow coherent caps sideways over connected exposed masonry. Independent per-cell
            // noise produced confetti; a short deterministic frontier produces the broad soil-
            // collecting mats visible on old ruins while retaining ragged seeded edges.
            var frontier = pending;
            for (int spread = 0; spread < 3 && frontier.Count > 0; spread++)
            {
                byte spreadCoverage = (byte)(coverage * (3 - spread) / 4);
                var next = new List<int3>();
                var unique = new HashSet<int3>();
                for (int i = 0; i < frontier.Count; i++)
                {
                    int3 source = frontier[i];
                    TrySpread(ref brush, source + new int3(1, 0, 0), min, max,
                              coatingId, seed, spread, spreadCoverage, unique, next);
                    TrySpread(ref brush, source + new int3(-1, 0, 0), min, max,
                              coatingId, seed, spread, spreadCoverage, unique, next);
                    TrySpread(ref brush, source + new int3(0, 0, 1), min, max,
                              coatingId, seed, spread, spreadCoverage, unique, next);
                    TrySpread(ref brush, source + new int3(0, 0, -1), min, max,
                              coatingId, seed, spread, spreadCoverage, unique, next);
                }

                for (int i = 0; i < next.Count; i++)
                {
                    int3 v = next[i];
                    brush.Coat(v.x, v.y, v.z, coatingId);
                    painted++;
                    pending.Add(v);
                }
                frontier = next;
            }

            // Pass 2..n: hang the coating down the faces underneath it. Thinning each pass is what
            // gives the ragged lower edge; a fixed depth reads as a painted stripe.
            byte dripCoverage = coverage;
            for (int pass = 0; pass < dripPasses; pass++)
            {
                // Thin slowly. At 3/5 the growth died within three voxels and read as a green rim
                // on each ledge; the reference has it hanging most of a course height down the
                // face. 4/5 keeps patches alive long enough to become the dominant surface, which
                // is what the saturation and detail-energy targets are actually describing.
                dripCoverage = (byte)(dripCoverage * 4 / 5);
                var next = new List<int3>();

                for (int i = 0; i < pending.Count; i++)
                {
                    int3 above = pending[i];
                    int3 v = new int3(above.x, above.y - 1, above.z);
                    if (v.y < min.y) continue;
                    if (!brush.IsSolid(v.x, v.y, v.z)) continue;
                    if (brush.GetCoating(v.x, v.y, v.z) == coatingId) continue;
                    if (!IsExposed(ref brush, v)) continue;
                    if (Hash(v.x, v.y, v.z, seed + 0x9E3779B9u) > dripCoverage) continue;
                    next.Add(v);
                }

                for (int i = 0; i < next.Count; i++)
                {
                    int3 v = next[i];
                    brush.Coat(v.x, v.y, v.z, coatingId);
                    painted++;
                }

                if (next.Count == 0) break;
                pending = next;
            }

            return painted;
        }

        private static bool IsExposed(ref VoxelBrush brush, int3 v) =>
            !brush.IsSolid(v.x + 1, v.y, v.z)
            || !brush.IsSolid(v.x - 1, v.y, v.z)
            || !brush.IsSolid(v.x, v.y + 1, v.z)
            || !brush.IsSolid(v.x, v.y - 1, v.z)
            || !brush.IsSolid(v.x, v.y, v.z + 1)
            || !brush.IsSolid(v.x, v.y, v.z - 1);

        private static void TrySpread(ref VoxelBrush brush, int3 v, int3 min, int3 max,
                                      byte coatingId, uint seed, int spread,
                                      byte spreadCoverage, HashSet<int3> unique,
                                      List<int3> next)
        {
            if (math.any(v < min) || math.any(v >= max)) return;
            if (!brush.IsSolid(v.x, v.y, v.z)) return;
            if (!IsExposed(ref brush, v)) return;
            if (brush.GetCoating(v.x, v.y, v.z) == coatingId) return;
            if (Hash(v.x, v.y, v.z,
                     seed + 0xD1B54A35u + (uint)spread) > spreadCoverage) return;
            if (unique.Add(v)) next.Add(v);
        }

        /// <summary>
        /// Knocks isolated voxels off exposed edges so silhouettes stop reading as machined.
        ///
        /// Only removes voxels with several empty neighbours, so it erodes corners and arrises
        /// rather than punching holes in walls. Structural mass is left alone.
        /// </summary>
        public static int ChipExposedEdges(ref VoxelBrush brush, int3 min, int3 size,
                                           uint seed, byte severity = 40,
                                           int protectedBaseLayers = 0)
        {
            if (math.any(size <= 0) || severity == 0) return 0;

            int3 max = min + size;
            var doomed = new List<int3>();

            for (int z = min.z; z < max.z; z++)
            for (int y = min.y + math.max(0, protectedBaseLayers); y < max.y; y++)
            for (int x = min.x; x < max.x; x++)
            {
                if (!brush.IsSolid(x, y, z)) continue;

                int exposed = 0;
                if (!brush.IsSolid(x + 1, y, z)) exposed++;
                if (!brush.IsSolid(x - 1, y, z)) exposed++;
                if (!brush.IsSolid(x, y + 1, z)) exposed++;
                if (!brush.IsSolid(x, y - 1, z)) exposed++;
                if (!brush.IsSolid(x, y, z + 1)) exposed++;
                if (!brush.IsSolid(x, y, z - 1)) exposed++;

                // Three or more open faces is a corner or an arris. Two is a flat edge and is left
                // intact, or the whole silhouette dissolves rather than weathering.
                if (exposed < 3) continue;
                if (Hash(x, y, z, seed + 0x85EBCA6Bu) > severity) continue;
                doomed.Add(new int3(x, y, z));
            }

            for (int i = 0; i < doomed.Count; i++)
            {
                int3 v = doomed[i];
                brush.Set(v.x, v.y, v.z, VoxelGrid.MaterialEmpty);
            }

            return doomed.Count;
        }

        /// <summary>
        /// Integer hash to 0-255. Seeded and integer-only so two clients weathering the same
        /// structure from the same seed agree voxel for voxel.
        /// </summary>
        private static byte Hash(int x, int y, int z, uint seed)
        {
            uint h = seed;
            h ^= (uint)x * 0x9E3779B9u;
            h ^= (uint)y * 0x85EBCA6Bu;
            h ^= (uint)z * 0xC2B2AE35u;
            h ^= h >> 15;
            h *= 0x2545F491u;
            h ^= h >> 13;
            return (byte)(h & 0xFFu);
        }
    }
}
