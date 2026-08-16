using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Frozen river/gorge cross-section recipe. These values decide authored terrain shape and
    /// surface bands, so spatial Runtime consumes them rather than owning hidden castle policy.
    /// </summary>
    public readonly struct CastleRiverCrossSectionPlan
    {
        public readonly float BankBlendStart;
        public readonly float BankBlendEnd;
        public readonly int OutsideTerraceDrop;
        public readonly int InsideTerraceDrop;
        public readonly float LooseBankThreshold;
        public readonly float DeepSoilThreshold;
        public readonly float GrassThreshold;
        public readonly int ShallowSoilDepth;
        public readonly int DeepSoilDepth;
        public readonly int BedDepth;
        public readonly int BedRise;
        public readonly int ExistingSurfaceRejectDepth;
        public readonly int SurfaceClearance;

        public CastleRiverCrossSectionPlan(
            float bankBlendStart,
            float bankBlendEnd,
            int outsideTerraceDrop,
            int insideTerraceDrop,
            float looseBankThreshold,
            float deepSoilThreshold,
            float grassThreshold,
            int shallowSoilDepth,
            int deepSoilDepth,
            int bedDepth,
            int bedRise,
            int existingSurfaceRejectDepth,
            int surfaceClearance)
        {
            BankBlendStart = bankBlendStart;
            BankBlendEnd = bankBlendEnd;
            OutsideTerraceDrop = outsideTerraceDrop;
            InsideTerraceDrop = insideTerraceDrop;
            LooseBankThreshold = looseBankThreshold;
            DeepSoilThreshold = deepSoilThreshold;
            GrassThreshold = grassThreshold;
            ShallowSoilDepth = shallowSoilDepth;
            DeepSoilDepth = deepSoilDepth;
            BedDepth = bedDepth;
            BedRise = bedRise;
            ExistingSurfaceRejectDepth = existingSurfaceRejectDepth;
            SurfaceClearance = surfaceClearance;
        }

        public static CastleRiverCrossSectionPlan Historical => new CastleRiverCrossSectionPlan(
            bankBlendStart: 0.18f,
            bankBlendEnd: 1f,
            outsideTerraceDrop: 32,
            insideTerraceDrop: 1,
            looseBankThreshold: 0.38f,
            deepSoilThreshold: 0.46f,
            grassThreshold: 0.56f,
            shallowSoilDepth: 2,
            deepSoilDepth: 5,
            bedDepth: 10,
            bedRise: 4,
            existingSurfaceRejectDepth: 20,
            surfaceClearance: 8);
    }

    /// <summary>
    /// Frozen geometry recipe for the authored castle outcrop and primary approach. These are
    /// planning values rather than voxel-mutation details: changing them changes the site's shape,
    /// so spatial Runtime must consume them rather than choose its own castle terrain recipe.
    /// </summary>
    public readonly struct CastleSiteGeometryPlan
    {
        public readonly float EdgeFrequencyA;
        public readonly float EdgeAmplitudeA;
        public readonly float EdgeFrequencyB;
        public readonly float EdgeAmplitudeB;
        public readonly float EdgeFrequencyC;
        public readonly float EdgeAmplitudeC;
        public readonly float CliffFalloffExponent;
        public readonly float CliffNoiseAngularFrequency;
        public readonly float CliffNoiseProgressFrequency;
        public readonly float CliffNoiseAmplitude;
        public readonly int CliffGroundInset;
        public readonly int GrassEdgeInset;
        public readonly int ApproachReachInset;
        public readonly int RiverOffset;
        public readonly int RiverHalfWidth;
        public readonly int WaterHalfWidth;
        public readonly int RiverDepth;
        public readonly float MeanderFrequencyA;
        public readonly float MeanderAmplitudeA;
        public readonly float MeanderFrequencyB;
        public readonly float MeanderAmplitudeB;
        public readonly CastleRiverCrossSectionPlan RiverCrossSection;

        public CastleSiteGeometryPlan(
            float edgeFrequencyA,
            float edgeAmplitudeA,
            float edgeFrequencyB,
            float edgeAmplitudeB,
            float edgeFrequencyC,
            float edgeAmplitudeC,
            float cliffFalloffExponent,
            float cliffNoiseAngularFrequency,
            float cliffNoiseProgressFrequency,
            float cliffNoiseAmplitude,
            int cliffGroundInset,
            int grassEdgeInset,
            int approachReachInset,
            int riverOffset,
            int riverHalfWidth,
            int waterHalfWidth,
            int riverDepth,
            float meanderFrequencyA,
            float meanderAmplitudeA,
            float meanderFrequencyB,
            float meanderAmplitudeB)
            : this(
                edgeFrequencyA,
                edgeAmplitudeA,
                edgeFrequencyB,
                edgeAmplitudeB,
                edgeFrequencyC,
                edgeAmplitudeC,
                cliffFalloffExponent,
                cliffNoiseAngularFrequency,
                cliffNoiseProgressFrequency,
                cliffNoiseAmplitude,
                cliffGroundInset,
                grassEdgeInset,
                approachReachInset,
                riverOffset,
                riverHalfWidth,
                waterHalfWidth,
                riverDepth,
                meanderFrequencyA,
                meanderAmplitudeA,
                meanderFrequencyB,
                meanderAmplitudeB,
                CastleRiverCrossSectionPlan.Historical)
        {
        }

        public CastleSiteGeometryPlan(
            float edgeFrequencyA,
            float edgeAmplitudeA,
            float edgeFrequencyB,
            float edgeAmplitudeB,
            float edgeFrequencyC,
            float edgeAmplitudeC,
            float cliffFalloffExponent,
            float cliffNoiseAngularFrequency,
            float cliffNoiseProgressFrequency,
            float cliffNoiseAmplitude,
            int cliffGroundInset,
            int grassEdgeInset,
            int approachReachInset,
            int riverOffset,
            int riverHalfWidth,
            int waterHalfWidth,
            int riverDepth,
            float meanderFrequencyA,
            float meanderAmplitudeA,
            float meanderFrequencyB,
            float meanderAmplitudeB,
            in CastleRiverCrossSectionPlan riverCrossSection)
        {
            EdgeFrequencyA = edgeFrequencyA;
            EdgeAmplitudeA = edgeAmplitudeA;
            EdgeFrequencyB = edgeFrequencyB;
            EdgeAmplitudeB = edgeAmplitudeB;
            EdgeFrequencyC = edgeFrequencyC;
            EdgeAmplitudeC = edgeAmplitudeC;
            CliffFalloffExponent = cliffFalloffExponent;
            CliffNoiseAngularFrequency = cliffNoiseAngularFrequency;
            CliffNoiseProgressFrequency = cliffNoiseProgressFrequency;
            CliffNoiseAmplitude = cliffNoiseAmplitude;
            CliffGroundInset = cliffGroundInset;
            GrassEdgeInset = grassEdgeInset;
            ApproachReachInset = approachReachInset;
            RiverOffset = riverOffset;
            RiverHalfWidth = riverHalfWidth;
            WaterHalfWidth = waterHalfWidth;
            RiverDepth = riverDepth;
            MeanderFrequencyA = meanderFrequencyA;
            MeanderAmplitudeA = meanderAmplitudeA;
            MeanderFrequencyB = meanderFrequencyB;
            MeanderAmplitudeB = meanderAmplitudeB;
            RiverCrossSection = riverCrossSection;
        }

        /// <summary>Behavior-preserving recipe extracted from the historical castle site realizer.</summary>
        public static CastleSiteGeometryPlan Historical
        {
            get
            {
                CastleRiverCrossSectionPlan crossSection = CastleRiverCrossSectionPlan.Historical;
                return new CastleSiteGeometryPlan(
                    edgeFrequencyA: 3.7f,
                    edgeAmplitudeA: 18f,
                    edgeFrequencyB: 8.3f,
                    edgeAmplitudeB: 9f,
                    edgeFrequencyC: 17.1f,
                    edgeAmplitudeC: 4f,
                    cliffFalloffExponent: 1.7f,
                    cliffNoiseAngularFrequency: 11f,
                    cliffNoiseProgressFrequency: 6f,
                    cliffNoiseAmplitude: 0.10f,
                    cliffGroundInset: 14,
                    grassEdgeInset: 12,
                    approachReachInset: 8,
                    riverOffset: 92,
                    riverHalfWidth: 90,
                    waterHalfWidth: 42,
                    riverDepth: CastleLayout.LowerRiverDepth,
                    meanderFrequencyA: 0.028f,
                    meanderAmplitudeA: 8f,
                    meanderFrequencyB: 0.071f,
                    meanderAmplitudeB: 3f,
                    in crossSection);
            }
        }
    }

    /// <summary>
    /// Planned site geometry and surface styling. Runtime consumes this as immutable realization
    /// input; spatial builds do not own castle-site shape policy or mutable random streams.
    /// </summary>
    public readonly struct CastleSitePlan
    {
        public readonly uint GrassPatternSeed;
        public readonly byte GrassCoveragePercent;
        public readonly uint CourtyardPatternSeed;
        public readonly byte CourtyardStonePercent;
        public readonly CastleSiteGeometryPlan Geometry;

        public CastleSitePlan(uint grassPatternSeed, byte grassCoveragePercent)
            : this(grassPatternSeed, grassCoveragePercent, 0u, 0, CastleSiteGeometryPlan.Historical)
        {
        }

        public CastleSitePlan(
            uint grassPatternSeed,
            byte grassCoveragePercent,
            uint courtyardPatternSeed,
            byte courtyardStonePercent)
            : this(
                grassPatternSeed,
                grassCoveragePercent,
                courtyardPatternSeed,
                courtyardStonePercent,
                CastleSiteGeometryPlan.Historical)
        {
        }

        public CastleSitePlan(
            uint grassPatternSeed,
            byte grassCoveragePercent,
            uint courtyardPatternSeed,
            byte courtyardStonePercent,
            in CastleSiteGeometryPlan geometry)
        {
            GrassPatternSeed = grassPatternSeed;
            GrassCoveragePercent = ClampPercent(grassCoveragePercent);
            CourtyardPatternSeed = courtyardPatternSeed;
            CourtyardStonePercent = ClampPercent(courtyardStonePercent);
            Geometry = geometry;
        }

        public bool ShouldGrassCap(int localX, int localZ) =>
            PercentHit(GrassPatternSeed, localX, localZ, GrassCoveragePercent);

        public bool ShouldUseCourtyardStone(int localX, int localZ) =>
            PercentHit(CourtyardPatternSeed, localX, localZ, CourtyardStonePercent);

        private static bool PercentHit(uint seed, int localX, int localZ, byte percent)
        {
            if (percent == 0) return false;
            if (percent >= 100) return true;

            unchecked
            {
                uint value = seed;
                value ^= (uint)localX * 0x8DA6B343u;
                value ^= (uint)localZ * 0xD8163841u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value % 100u < percent;
            }
        }

        private static byte ClampPercent(byte value) => value > 100 ? (byte)100 : value;
    }

    public enum CastleSitePlanIssue : byte
    {
        None,
        MissingGrassPatternSeed,
        MissingCourtyardPatternSeed,
        InvalidGeometry,
        InvalidEdgeRecipe,
        InvalidCliffRecipe,
        InvalidApproachRecipe,
        InvalidRiverCrossSection,
    }

    public static class CastleSitePlanValidator
    {
        public static bool TryValidate(in CastleSitePlan plan, out CastleSitePlanIssue issue)
        {
            if (plan.GrassCoveragePercent > 0 && plan.GrassPatternSeed == 0u)
            {
                issue = CastleSitePlanIssue.MissingGrassPatternSeed;
                return false;
            }

            if (plan.CourtyardStonePercent > 0 && plan.CourtyardPatternSeed == 0u)
            {
                issue = CastleSitePlanIssue.MissingCourtyardPatternSeed;
                return false;
            }

            CastleSiteGeometryPlan geometry = plan.Geometry;
            if (!PositiveFinite(geometry.EdgeFrequencyA) || geometry.EdgeAmplitudeA < 0f ||
                !PositiveFinite(geometry.EdgeFrequencyB) || geometry.EdgeAmplitudeB < 0f ||
                !PositiveFinite(geometry.EdgeFrequencyC) || geometry.EdgeAmplitudeC < 0f ||
                !math.isfinite(geometry.EdgeAmplitudeA) ||
                !math.isfinite(geometry.EdgeAmplitudeB) ||
                !math.isfinite(geometry.EdgeAmplitudeC))
            {
                issue = CastleSitePlanIssue.InvalidEdgeRecipe;
                return false;
            }

            if (!PositiveFinite(geometry.CliffFalloffExponent) ||
                !PositiveFinite(geometry.CliffNoiseAngularFrequency) ||
                !PositiveFinite(geometry.CliffNoiseProgressFrequency) ||
                geometry.CliffNoiseAmplitude < 0f || !math.isfinite(geometry.CliffNoiseAmplitude) ||
                geometry.CliffGroundInset < 0 || geometry.GrassEdgeInset < 0)
            {
                issue = CastleSitePlanIssue.InvalidCliffRecipe;
                return false;
            }

            if (geometry.ApproachReachInset < 0 || geometry.RiverOffset <= 0 ||
                geometry.RiverHalfWidth <= 0 || geometry.WaterHalfWidth <= 0 ||
                geometry.WaterHalfWidth > geometry.RiverHalfWidth || geometry.RiverDepth <= 0 ||
                geometry.MeanderFrequencyA < 0f || !math.isfinite(geometry.MeanderFrequencyA) ||
                geometry.MeanderAmplitudeA < 0f || !math.isfinite(geometry.MeanderAmplitudeA) ||
                geometry.MeanderFrequencyB < 0f || !math.isfinite(geometry.MeanderFrequencyB) ||
                geometry.MeanderAmplitudeB < 0f || !math.isfinite(geometry.MeanderAmplitudeB))
            {
                issue = CastleSitePlanIssue.InvalidApproachRecipe;
                return false;
            }

            CastleRiverCrossSectionPlan crossSection = geometry.RiverCrossSection;
            if (!UnitInterval(crossSection.BankBlendStart) ||
                !UnitInterval(crossSection.BankBlendEnd) ||
                crossSection.BankBlendStart >= crossSection.BankBlendEnd ||
                !UnitInterval(crossSection.LooseBankThreshold) ||
                !UnitInterval(crossSection.DeepSoilThreshold) ||
                !UnitInterval(crossSection.GrassThreshold) ||
                crossSection.LooseBankThreshold > crossSection.DeepSoilThreshold ||
                crossSection.DeepSoilThreshold > crossSection.GrassThreshold ||
                crossSection.OutsideTerraceDrop < 0 ||
                crossSection.InsideTerraceDrop < 0 ||
                crossSection.ShallowSoilDepth <= 0 ||
                crossSection.DeepSoilDepth < crossSection.ShallowSoilDepth ||
                crossSection.BedDepth <= 0 ||
                crossSection.BedRise < 0 ||
                crossSection.ExistingSurfaceRejectDepth < 0 ||
                crossSection.SurfaceClearance < 0)
            {
                issue = CastleSitePlanIssue.InvalidRiverCrossSection;
                return false;
            }

            issue = CastleSitePlanIssue.None;
            return true;
        }

        private static bool PositiveFinite(float value) => value > 0f && math.isfinite(value);

        private static bool UnitInterval(float value) =>
            value >= 0f && value <= 1f && math.isfinite(value);
    }

    public static class CastleSitePlanner
    {
        private const uint GrassPatternElementId = 0x53495445u; // "SITE"
        private const uint CourtyardPatternElementId = 0x43545944u; // "CTYD"

        public static CastleSitePlan Create(uint rootSeed)
        {
            CastleSiteGeometryPlan geometry = CastleSiteGeometryPlan.Historical;
            var plan = new CastleSitePlan(
                CastleSeedPartition.Derive(
                    rootSeed, CastleSeedDomain.Decor, GrassPatternElementId),
                92,
                CastleSeedPartition.Derive(
                    rootSeed, CastleSeedDomain.Decor, CourtyardPatternElementId),
                82,
                in geometry);

            if (!CastleSitePlanValidator.TryValidate(in plan, out _))
                throw new System.InvalidOperationException("Castle site planner produced an invalid recipe.");
            return plan;
        }
    }
}
