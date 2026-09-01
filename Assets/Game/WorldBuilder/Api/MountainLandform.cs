using System;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Broad silhouette family for a reusable mountain landform. This is semantic authoring data,
    /// not a renderer choice: implementations remain free to realize the same shape through voxels,
    /// meshes, HLOD, or analytical queries.
    /// </summary>
    public enum MountainMacroShape : byte
    {
        Massif = 0,
        Pyramidal = 1,
        Ridged = 2,
    }

    /// <summary>Semantic character of the usable high point/crest.</summary>
    public enum MountainSummitCharacter : byte
    {
        Broad = 0,
        Rounded = 1,
        Craggy = 2,
    }

    /// <summary>
    /// Reusable deterministic mountain-shape request in world decimetres.
    ///
    /// The contract deliberately owns only landform semantics: placement, footprint/aspect,
    /// elevation, summit character, macro ridge/asymmetry and bounded roughness. Roads, traversal,
    /// structures, encounters, material ids and scene policy are separate composition concerns.
    /// </summary>
    public readonly struct MountainLandformSpec
    {
        public int OriginXdm { get; }
        public int OriginYdm { get; }
        public int OriginZdm { get; }
        public int RadiusXdm { get; }
        public int RadiusZdm { get; }
        public int HeightDm { get; }
        public int SummitRadiusDm { get; }
        public MountainMacroShape MacroShape { get; }
        public MountainSummitCharacter SummitCharacter { get; }
        public uint Seed { get; }
        public int RidgeCount { get; }
        public int RidgeStrengthPermille { get; }
        public int AsymmetryXPermille { get; }
        public int AsymmetryZPermille { get; }
        public int RoughnessAmplitudeDm { get; }
        public int RoughnessScaleDm { get; }
        public int ErosionStrengthPermille { get; }

        public int FootprintWidthDm => RadiusXdm * 2;
        public int FootprintDepthDm => RadiusZdm * 2;

        public MountainLandformSpec(
            int originXdm,
            int originYdm,
            int originZdm,
            int radiusXdm,
            int radiusZdm,
            int heightDm,
            int summitRadiusDm,
            MountainMacroShape macroShape,
            MountainSummitCharacter summitCharacter,
            uint seed,
            int ridgeCount,
            int ridgeStrengthPermille,
            int asymmetryXPermille,
            int asymmetryZPermille,
            int roughnessAmplitudeDm,
            int roughnessScaleDm,
            int erosionStrengthPermille)
        {
            if (radiusXdm < 1) throw new ArgumentOutOfRangeException(nameof(radiusXdm));
            if (radiusZdm < 1) throw new ArgumentOutOfRangeException(nameof(radiusZdm));
            if (heightDm < 1) throw new ArgumentOutOfRangeException(nameof(heightDm));
            if (summitRadiusDm < 1 || summitRadiusDm > Math.Min(radiusXdm, radiusZdm))
                throw new ArgumentOutOfRangeException(nameof(summitRadiusDm));
            if (!Enum.IsDefined(typeof(MountainMacroShape), macroShape))
                throw new ArgumentOutOfRangeException(nameof(macroShape));
            if (!Enum.IsDefined(typeof(MountainSummitCharacter), summitCharacter))
                throw new ArgumentOutOfRangeException(nameof(summitCharacter));
            if (ridgeCount < 0 || ridgeCount > 12)
                throw new ArgumentOutOfRangeException(nameof(ridgeCount));
            if (ridgeStrengthPermille < 0 || ridgeStrengthPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(ridgeStrengthPermille));
            if (asymmetryXPermille < -500 || asymmetryXPermille > 500)
                throw new ArgumentOutOfRangeException(nameof(asymmetryXPermille));
            if (asymmetryZPermille < -500 || asymmetryZPermille > 500)
                throw new ArgumentOutOfRangeException(nameof(asymmetryZPermille));
            if (roughnessAmplitudeDm < 0 || roughnessAmplitudeDm > heightDm)
                throw new ArgumentOutOfRangeException(nameof(roughnessAmplitudeDm));
            if (roughnessScaleDm < 1)
                throw new ArgumentOutOfRangeException(nameof(roughnessScaleDm));
            if (erosionStrengthPermille < 0 || erosionStrengthPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(erosionStrengthPermille));

            OriginXdm = originXdm;
            OriginYdm = originYdm;
            OriginZdm = originZdm;
            RadiusXdm = radiusXdm;
            RadiusZdm = radiusZdm;
            HeightDm = heightDm;
            SummitRadiusDm = summitRadiusDm;
            MacroShape = macroShape;
            SummitCharacter = summitCharacter;
            Seed = seed;
            RidgeCount = ridgeCount;
            RidgeStrengthPermille = ridgeStrengthPermille;
            AsymmetryXPermille = asymmetryXPermille;
            AsymmetryZPermille = asymmetryZPermille;
            RoughnessAmplitudeDm = roughnessAmplitudeDm;
            RoughnessScaleDm = roughnessScaleDm;
            ErosionStrengthPermille = erosionStrengthPermille;
        }
    }
}
