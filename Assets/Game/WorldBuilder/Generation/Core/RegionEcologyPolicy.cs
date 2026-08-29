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
            float routeClearanceMetres)
        {
            _vegetationKinds = CopyKinds(vegetationKinds);
            _treeKinds = CopyKinds(treeKinds);
            _ambientAnimalKinds = CopyKinds(ambientAnimalKinds);
            VegetationDensity = Clamp01(vegetationDensity);
            VegetationSampleSpacingMetres = Math.Max(0.1f, vegetationSampleSpacingMetres);
            MaxVegetationSlopeDegrees = Math.Max(0f, Math.Min(89f, maxVegetationSlopeDegrees));
            RouteClearanceMetres = Math.Max(0f, routeClearanceMetres);
        }

        public IReadOnlyList<string> VegetationKinds => _vegetationKinds;
        public IReadOnlyList<string> TreeKinds => _treeKinds;
        public IReadOnlyList<string> AmbientAnimalKinds => _ambientAnimalKinds;
        public float VegetationDensity { get; }
        public float VegetationSampleSpacingMetres { get; }
        public float MaxVegetationSlopeDegrees { get; }
        public float RouteClearanceMetres { get; }

        public bool AllowsVegetation(string kind) => Contains(_vegetationKinds, kind);
        public bool AllowsTree(string kind) => Contains(_treeKinds, kind);
        public bool AllowsAmbientAnimal(string kind) => Contains(_ambientAnimalKinds, kind);

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
}
