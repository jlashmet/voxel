using System;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Composition.CaveWorldBuilder
{
    /// <summary>
    /// Semantic presentation configuration for a verified cave-secret boundary. The clue is authored
    /// as a normal voxel coating on the retained host rock: no emissive material, helper mesh, scene
    /// object, or alternate renderer is introduced.
    /// </summary>
    public readonly struct CaveSecretPocketCluePresentationConfig
    {
        public readonly byte BoundaryCoating;
        public readonly int CoveragePercent;
        public readonly uint Seed;

        public CaveSecretPocketCluePresentationConfig(byte boundaryCoating, int coveragePercent, uint seed)
        {
            if (boundaryCoating == 0)
                throw new ArgumentOutOfRangeException(nameof(boundaryCoating));
            if (coveragePercent < 1 || coveragePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(coveragePercent));
            if (seed == 0u)
                throw new ArgumentOutOfRangeException(nameof(seed));

            BoundaryCoating = boundaryCoating;
            CoveragePercent = coveragePercent;
            Seed = seed;
        }
    }

    /// <summary>
    /// Applies pre-solve visual evidence to the exact physically verified false-wall volume. The
    /// presentation is a deterministic branching fracture pattern on the cave-facing surface of
    /// the retained barrier. It changes coating only; it cannot carve, fill, move, or otherwise
    /// weaken the wall that proves the hidden pocket has no accidental bypass.
    /// </summary>
    public static class CaveSecretPocketCluePresentation
    {
        public static bool TryApplyBoundaryEvidence(
            IStructureAuthoringSession authoring,
            in CaveSecretPocketProjection projection,
            in CaveSecretPocketCluePresentationConfig config,
            out int coatedVoxelCount)
        {
            coatedVoxelCount = 0;
            if (authoring == null || !projection.IsWellFormed) return false;

            DecorationBounds barrier = projection.Pocket.Barrier;
            CaveTraversalCandidate terminal = projection.Pocket.Terminal;

            // Presentation must never mint evidence from an already-broken barrier. Validate the
            // complete verified volume first, then restrict coating to the one cave-facing layer.
            if (!AllSolid(authoring, in barrier))
                return false;

            int width = IsZFacing(terminal.ExitFacing) ? barrier.Size.x : barrier.Size.z;
            int height = barrier.Size.y;
            if (width < 3 || height < 5)
                return false;

            int centre = width / 2;
            int mainOffset = 0;
            int branchSpacing = math.max(3, 8 - config.CoveragePercent / 18);
            int branchLength = math.clamp(2 + config.CoveragePercent / 24, 2, math.max(2, width / 2));

            // Main fracture: descend the face with small deterministic lateral changes. Keeping the
            // drift bounded makes it read as one continuous crack rather than random weathering.
            for (int localY = height - 2; localY >= 1; localY--)
            {
                uint h = Hash(config.Seed, localY, centre, height);
                if ((h & 3u) == 0u)
                {
                    int step = ((h >> 2) & 1u) == 0u ? -1 : 1;
                    mainOffset = math.clamp(mainOffset + step, -math.max(1, width / 4), math.max(1, width / 4));
                }

                CoatFace(authoring, in barrier, terminal.ExitFacing,
                    centre + mainOffset, localY, config.BoundaryCoating, ref coatedVoxelCount);

                // Fracture branches fork away from the main line at deterministic intervals. The
                // branch direction and slight vertical wobble come only from the semantic seed.
                if ((height - localY) % branchSpacing != 0)
                    continue;

                int direction = ((h >> 5) & 1u) == 0u ? -1 : 1;
                int branchY = localY;
                int branchX = centre + mainOffset;
                int length = math.min(branchLength + (int)((h >> 8) % 2u), width / 2);
                for (int i = 1; i <= length; i++)
                {
                    branchX += direction;
                    if (((h >> (i & 15)) & 1u) != 0u && i > 1)
                        branchY = math.max(1, branchY - 1);
                    if (branchX < 1 || branchX >= width - 1)
                        break;

                    CoatFace(authoring, in barrier, terminal.ExitFacing,
                        branchX, branchY, config.BoundaryCoating, ref coatedVoxelCount);
                }
            }

            // A small fracture cluster around the centre gives the clue a readable focal point at
            // gameplay distance without making the whole wall look highlighted.
            for (int d = -1; d <= 1; d++)
            {
                CoatFace(authoring, in barrier, terminal.ExitFacing,
                    math.clamp(centre + d, 0, width - 1), height / 2,
                    config.BoundaryCoating, ref coatedVoxelCount);
            }

            // Recheck authoritative occupancy after presentation. A coating implementation is not
            // allowed to turn this clue pass into a topology mutation.
            return coatedVoxelCount > 0 && AllSolid(authoring, in barrier);
        }

        private static void CoatFace(
            IStructureAuthoringSession authoring,
            in DecorationBounds barrier,
            Facing facing,
            int localAcross,
            int localY,
            byte coating,
            ref int count)
        {
            int x;
            int z;
            switch (facing)
            {
                case Facing.North:
                    x = barrier.Min.x + localAcross;
                    z = barrier.Min.z;
                    break;
                case Facing.South:
                    x = barrier.Min.x + localAcross;
                    z = barrier.MaxExclusive.z - 1;
                    break;
                case Facing.East:
                    x = barrier.Min.x;
                    z = barrier.Min.z + localAcross;
                    break;
                case Facing.West:
                    x = barrier.MaxExclusive.x - 1;
                    z = barrier.Min.z + localAcross;
                    break;
                default:
                    return;
            }

            int y = barrier.Min.y + localY;
            if (x < barrier.Min.x || x >= barrier.MaxExclusive.x ||
                y < barrier.Min.y || y >= barrier.MaxExclusive.y ||
                z < barrier.Min.z || z >= barrier.MaxExclusive.z)
                return;

            authoring.Coat(x, y, z, coating);
            count++;
        }

        private static bool IsZFacing(Facing facing) =>
            facing == Facing.North || facing == Facing.South;

        private static bool AllSolid(IStructureAuthoringSession authoring, in DecorationBounds bounds)
        {
            for (int y = bounds.Min.y; y < bounds.MaxExclusive.y; y++)
            for (int z = bounds.Min.z; z < bounds.MaxExclusive.z; z++)
            for (int x = bounds.Min.x; x < bounds.MaxExclusive.x; x++)
                if (!authoring.IsSolid(x, y, z))
                    return false;
            return true;
        }

        private static uint Hash(uint seed, int x, int y, int z)
        {
            uint h = seed ^ 0x9E3779B9u;
            h = Mix(h, unchecked((uint)x));
            h = Mix(h, unchecked((uint)y));
            h = Mix(h, unchecked((uint)z));
            return h;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x85EBCA6Bu + (hash << 6) + (hash >> 2);
            hash ^= hash >> 15;
            hash *= 0x2545F491u;
            hash ^= hash >> 13;
            return hash;
        }
    }
}
