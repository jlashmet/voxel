using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// Engine-neutral authoring policy for the living content allowed in a world region.
    /// Rendering/runtime composition translates the stable kind ids into the engine-specific
    /// vegetation, tree, and ambient-life enums it owns.
    /// </summary>
    public sealed class RegionEcologyPolicy
    {
        private readonly string[] _vegetationKinds;
        private readonly string[] _treeKinds;
        private readonly string[] _ambientAnimalKinds;

        public RegionEcologyPolicy(
            string[] vegetationKinds,
            string[] treeKinds,
            string[] ambientAnimalKinds,
            float vegetationDensity,
            float vegetationSampleSpacingMetres,
            float maxVegetationSlopeDegrees,
            float routeClearanceMetres,
            uint deterministicSeedSalt = 0u)
        {
            _vegetationKinds = CopyKinds(vegetationKinds);
            _treeKinds = CopyKinds(treeKinds);
            _ambientAnimalKinds = CopyKinds(ambientAnimalKinds);
            VegetationDensity = Clamp01(vegetationDensity);
            VegetationSampleSpacingMetres = Math.Max(0.1f, vegetationSampleSpacingMetres);
            MaxVegetationSlopeDegrees = Math.Max(0f, Math.Min(89f, maxVegetationSlopeDegrees));
            RouteClearanceMetres = Math.Max(0f, routeClearanceMetres);
            DeterministicSeedSalt = deterministicSeedSalt;
        }

        public IReadOnlyList<string> VegetationKinds => _vegetationKinds;
        public IReadOnlyList<string> TreeKinds => _treeKinds;
        public IReadOnlyList<string> AmbientAnimalKinds => _ambientAnimalKinds;
        public float VegetationDensity { get; }
        public float VegetationSampleSpacingMetres { get; }
        public float MaxVegetationSlopeDegrees { get; }
        public float RouteClearanceMetres { get; }
        public uint DeterministicSeedSalt { get; }

        public bool AllowsVegetation(string kind) => Contains(_vegetationKinds, kind);
        public bool AllowsTree(string kind) => Contains(_treeKinds, kind);
        public bool AllowsAmbientAnimal(string kind) => Contains(_ambientAnimalKinds, kind);

        /// <summary>
        /// Derives the stable random stream for this authored ecology area. A zero salt preserves
        /// legacy callers exactly; authored areas can opt into independent deterministic variation
        /// without changing the world seed or relying on scene-local random state.
        /// </summary>
        public uint DeriveSeed(uint worldSeed)
        {
            if (DeterministicSeedSalt == 0u) return worldSeed;

            uint h = worldSeed ^ 0x9E3779B9u;
            h ^= DeterministicSeedSalt + 0x85EBCA6Bu + (h << 6) + (h >> 2);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h == 0u ? 1u : h;
        }

        private static string[] CopyKinds(string[] kinds)
        {
            if (kinds == null || kinds.Length == 0) return Array.Empty<string>();
            var copy = new string[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                string kind = kinds[i];
                if (string.IsNullOrWhiteSpace(kind))
                    throw new ArgumentException("Ecology kind ids must be non-empty.", nameof(kinds));
                copy[i] = kind;
            }
            return copy;
        }

        private static bool Contains(string[] kinds, string kind)
        {
            if (string.IsNullOrEmpty(kind)) return false;
            for (int i = 0; i < kinds.Length; i++)
                if (string.Equals(kinds[i], kind, StringComparison.Ordinal)) return true;
            return false;
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f) return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    /// <summary>
    /// Stable integer coordinate for one authored ecology sampling cell. Connectivity is defined
    /// on these eligible cells rather than on stochastic vegetation survivors, so a random missed
    /// blade cannot incorrectly split one physical meadow into multiple meadows.
    /// </summary>
    public readonly struct RegionEcologyGridCell : IEquatable<RegionEcologyGridCell>
    {
        public RegionEcologyGridCell(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public bool Equals(RegionEcologyGridCell other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is RegionEcologyGridCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
    }

    /// <summary>
    /// Pure connectivity accounting shared by region realizers and regressions. The returned count
    /// is the number of occupied cells in the eligible 4-neighbour component that contains the most
    /// occupied cells.
    /// </summary>
    public static class RegionEcologyConnectivity
    {
        private static readonly RegionEcologyGridCell[] Neighbours =
        {
            new RegionEcologyGridCell(-1, 0),
            new RegionEcologyGridCell(1, 0),
            new RegionEcologyGridCell(0, -1),
            new RegionEcologyGridCell(0, 1),
        };

        public static int LargestConnectedOccupiedCount(
            IReadOnlyCollection<RegionEcologyGridCell> eligibleCells,
            IReadOnlyCollection<RegionEcologyGridCell> occupiedCells)
        {
            if (eligibleCells == null || occupiedCells == null
                || eligibleCells.Count == 0 || occupiedCells.Count == 0)
                return 0;

            var remaining = new HashSet<RegionEcologyGridCell>(eligibleCells);
            var occupied = new HashSet<RegionEcologyGridCell>(occupiedCells);
            var queue = new Queue<RegionEcologyGridCell>();
            int largest = 0;

            while (remaining.Count > 0)
            {
                RegionEcologyGridCell start = default;
                foreach (RegionEcologyGridCell cell in remaining)
                {
                    start = cell;
                    break;
                }

                remaining.Remove(start);
                queue.Enqueue(start);
                int componentOccupied = 0;

                while (queue.Count > 0)
                {
                    RegionEcologyGridCell cell = queue.Dequeue();
                    if (occupied.Contains(cell)) componentOccupied++;

                    for (int i = 0; i < Neighbours.Length; i++)
                    {
                        RegionEcologyGridCell delta = Neighbours[i];
                        var neighbour = new RegionEcologyGridCell(cell.X + delta.X, cell.Z + delta.Z);
                        if (!remaining.Remove(neighbour)) continue;
                        queue.Enqueue(neighbour);
                    }
                }

                if (componentOccupied > largest) largest = componentOccupied;
            }

            return largest;
        }
    }
}
