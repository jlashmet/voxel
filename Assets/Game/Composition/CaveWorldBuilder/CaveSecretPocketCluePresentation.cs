using System;
using Game.Structures.Api;
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
            if (boundaryCoating == Coatings.None)
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
    /// Applies pre-solve visual evidence to the exact physically verified false-wall volume. This
    /// operation changes coating only; it cannot carve, fill, move, or otherwise weaken the retained
    /// barrier that proves the hidden pocket has no accidental bypass.
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
            int centreX = (barrier.Min.x + barrier.MaxExclusive.x - 1) / 2;
            int centreY = (barrier.Min.y + barrier.MaxExclusive.y - 1) / 2;
            int centreZ = (barrier.Min.z + barrier.MaxExclusive.z - 1) / 2;

            for (int y = barrier.Min.y; y < barrier.MaxExclusive.y; y++)
            for (int z = barrier.Min.z; z < barrier.MaxExclusive.z; z++)
            for (int x = barrier.Min.x; x < barrier.MaxExclusive.x; x++)
            {
                if (!authoring.IsSolid(x, y, z))
                    return false;

                bool centreMark = x == centreX && y == centreY && z == centreZ;
                if (!centreMark && Hash(config.Seed, x, y, z) % 100u >= config.CoveragePercent)
                    continue;

                authoring.Coat(x, y, z, config.BoundaryCoating);
                coatedVoxelCount++;
            }

            // Recheck the authoritative occupancy after presentation. A coating implementation is not
            // allowed to turn a clue pass into a topology mutation.
            for (int y = barrier.Min.y; y < barrier.MaxExclusive.y; y++)
            for (int z = barrier.Min.z; z < barrier.MaxExclusive.z; z++)
            for (int x = barrier.Min.x; x < barrier.MaxExclusive.x; x++)
                if (!authoring.IsSolid(x, y, z))
                    return false;

            return coatedVoxelCount > 0;
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
