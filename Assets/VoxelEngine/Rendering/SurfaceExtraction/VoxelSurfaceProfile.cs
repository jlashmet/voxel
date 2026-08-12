using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Presentation-only material contract for turning binary voxel occupancy into a rendered
    /// surface. Storage, collision, destruction and networking remain untouched.
    /// </summary>
    public readonly struct VoxelSurfaceProfile
    {
        public const int MaxSupportedBlurPasses = 2;

        public VoxelSurfaceProfile(float smoothing, int blurPasses = 2,
                                   float densityBias = 0f,
                                   float modificationStrength = 0f,
                                   float modificationScaleVoxels = 4f,
                                   float planarization = 0f,
                                   float planarizationThreshold = 0.88f,
                                   float distanceRecovery = 0f,
                                   float curveRecovery = 0f,
                                   float normalPlanarization = -1f,
                                   float planarSnapDistanceVoxels = 0.16f,
                                   float featurePreservation = 0f,
                                   float featureNormalStrength = 0f)
        {
            Smoothing = math.saturate(smoothing);
            BlurPasses = math.clamp(blurPasses, 0, MaxSupportedBlurPasses);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
            Planarization = math.saturate(planarization);
            PlanarizationThreshold = math.clamp(planarizationThreshold, 0.35f, 0.995f);
            DistanceRecovery = math.saturate(distanceRecovery);
            CurveRecovery = math.saturate(curveRecovery);
            NormalPlanarization = normalPlanarization < 0f
                ? Planarization
                : math.saturate(normalPlanarization);
            PlanarSnapDistanceVoxels = math.clamp(planarSnapDistanceVoxels, 0.01f, 0.49f);
            FeaturePreservation = math.saturate(featurePreservation);
            FeatureNormalStrength = math.saturate(featureNormalStrength);
        }

        public float Smoothing { get; }
        public int BlurPasses { get; }
        public float DensityBias { get; }
        public float ModificationStrength { get; }
        public float ModificationScaleVoxels { get; }
        public float Planarization { get; }
        public float PlanarizationThreshold { get; }
        public float DistanceRecovery { get; }
        public float CurveRecovery { get; }
        public float NormalPlanarization { get; }
        public float PlanarSnapDistanceVoxels { get; }

        /// <summary>
        /// Blend from ordinary surface-nets averaging toward a QEF/Hermite vertex that preserves
        /// intersecting planes. Zero is organic/smooth; one strongly preserves manufactured edges.
        /// </summary>
        public float FeaturePreservation { get; }

        /// <summary>How strongly feature-preserving faces use their geometric face normal.</summary>
        public float FeatureNormalStrength { get; }

        public static VoxelSurfaceProfile Legacy => new(0.92f, 2);

        public static VoxelSurfaceProfile SoftTerrain => new(
            smoothing: 0.94f,
            blurPasses: 2,
            distanceRecovery: 0.10f,
            curveRecovery: 0.35f);

        public static VoxelSurfaceProfile HardManufactured => new(
            smoothing: 0.82f,
            blurPasses: 0,
            planarization: 0.90f,
            planarizationThreshold: 0.52f,
            distanceRecovery: 0.82f,
            curveRecovery: 0.18f,
            normalPlanarization: 0.85f,
            planarSnapDistanceVoxels: 0.20f,
            featurePreservation: 0.90f,
            featureNormalStrength: 0.85f);

        /// <summary>
        /// Dressed stone receives no terrain blur. Strong QEF feature preservation allows smooth
        /// curved masonry and sharp plane intersections to coexist in the same physical material.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(
            smoothing: 0.96f,
            blurPasses: 0,
            densityBias: -0.004f,
            planarization: 0.42f,
            planarizationThreshold: 0.55f,
            distanceRecovery: 1.0f,
            curveRecovery: 0.72f,
            normalPlanarization: 0.45f,
            planarSnapDistanceVoxels: 0.12f,
            featurePreservation: 0.96f,
            featureNormalStrength: 0.82f);

        public static VoxelSurfaceProfile RecessedMasonryJoint => new(
            smoothing: 0.96f,
            blurPasses: 0,
            densityBias: -0.085f,
            planarization: 0.42f,
            planarizationThreshold: 0.55f,
            distanceRecovery: 1.0f,
            curveRecovery: 0.72f,
            normalPlanarization: 0.45f,
            planarSnapDistanceVoxels: 0.12f,
            featurePreservation: 0.96f,
            featureNormalStrength: 0.82f);

        public static VoxelSurfaceProfile RoughRock => new(
            smoothing: 0.82f,
            blurPasses: 1,
            modificationStrength: 0.035f,
            modificationScaleVoxels: 5.5f,
            distanceRecovery: 0.48f,
            curveRecovery: 0.55f,
            featurePreservation: 0.16f,
            featureNormalStrength: 0.08f);
    }

    /// <summary>Canonical surface behavior for the current material-id palette.</summary>
    public sealed class VoxelSurfaceProfileSet
    {
        private readonly VoxelSurfaceProfile[] _profiles = new VoxelSurfaceProfile[256];

        public VoxelSurfaceProfileSet() : this(VoxelSurfaceProfile.Legacy) { }

        public VoxelSurfaceProfileSet(VoxelSurfaceProfile defaultProfile)
        {
            for (int i = 0; i < _profiles.Length; i++) _profiles[i] = defaultProfile;
        }

        public VoxelSurfaceProfile Get(byte material) => _profiles[material];

        public VoxelSurfaceProfileSet Set(byte material, VoxelSurfaceProfile profile)
        {
            _profiles[material] = profile;
            return this;
        }

        public static VoxelSurfaceProfileSet Canonical()
        {
            var set = new VoxelSurfaceProfileSet(VoxelSurfaceProfile.Legacy);
            set.Set(0,  VoxelSurfaceProfile.Legacy);
            set.Set(1,  VoxelSurfaceProfile.DressedStone);
            set.Set(2,  VoxelSurfaceProfile.HardManufactured);
            set.Set(3,  VoxelSurfaceProfile.SoftTerrain);
            set.Set(4,  VoxelSurfaceProfile.HardManufactured);
            set.Set(5,  VoxelSurfaceProfile.RoughRock);
            set.Set(6,  VoxelSurfaceProfile.DressedStone);
            set.Set(7,  VoxelSurfaceProfile.HardManufactured);
            set.Set(8,  VoxelSurfaceProfile.HardManufactured);
            set.Set(9,  new VoxelSurfaceProfile(0.55f, 1));
            set.Set(10, VoxelSurfaceProfile.SoftTerrain);
            set.Set(11, new VoxelSurfaceProfile(0.96f, 2));
            set.Set(12, VoxelSurfaceProfile.HardManufactured);
            set.Set(13, VoxelSurfaceProfile.SoftTerrain);
            set.Set(14, new VoxelSurfaceProfile(0.90f, 2,
                                                 modificationStrength: 0.018f,
                                                 modificationScaleVoxels: 4.5f));
            set.Set(15, VoxelSurfaceProfile.HardManufactured);
            set.Set(16, new VoxelSurfaceProfile(0.96f, 2));
            set.Set(17, new VoxelSurfaceProfile(0.70f, 1,
                                                 distanceRecovery: 0.55f,
                                                 curveRecovery: 0.45f,
                                                 featurePreservation: 0.55f,
                                                 featureNormalStrength: 0.40f));
            return set;
        }
    }
}
