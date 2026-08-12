using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Controls how authoritative binary voxel occupancy becomes a derived visual surface.
    /// These values are presentation-only and never mutate voxel storage, collision, destruction,
    /// or networking state.
    ///
    /// BlurPasses is intentionally part of the material contract. A renderer must select the
    /// material's filtered field before applying distance recovery/smoothing; globally blurring all
    /// occupancy first destroys manufactured edges and cannot be repaired reliably afterwards.
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
                                   float curveRecovery = 0f)
        {
            Smoothing = math.saturate(smoothing);
            BlurPasses = math.clamp(blurPasses, 0, MaxSupportedBlurPasses);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
            Planarization = math.saturate(planarization);
            PlanarizationThreshold = math.clamp(planarizationThreshold, 0.58f, 0.995f);
            DistanceRecovery = math.saturate(distanceRecovery);
            CurveRecovery = math.saturate(curveRecovery);
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

        public static VoxelSurfaceProfile Legacy => new(
            smoothing: 0.92f,
            blurPasses: 2);

        public static VoxelSurfaceProfile SoftTerrain => new(
            smoothing: 0.94f,
            blurPasses: 2,
            distanceRecovery: 0.10f,
            curveRecovery: 0.35f);

        public static VoxelSurfaceProfile HardManufactured => new(
            smoothing: 0.42f,
            blurPasses: 0,
            planarization: 0.82f,
            planarizationThreshold: 0.76f,
            distanceRecovery: 0.62f,
            curveRecovery: 0.16f);

        /// <summary>
        /// Dressed stone is deliberately occupancy-dominant. It receives no low-pass blur and only
        /// a restrained amount of distance-field reconstruction; the latter exists to clean the
        /// digital circle of a large archivolt, not to soften ashlar into a continuous blob.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(
            smoothing: 0.38f,
            blurPasses: 0,
            densityBias: -0.004f,
            planarization: 0.96f,
            planarizationThreshold: 0.70f,
            distanceRecovery: 0.72f,
            curveRecovery: 0.76f);

        public static VoxelSurfaceProfile RecessedMasonryJoint => new(
            smoothing: 0.38f,
            blurPasses: 0,
            densityBias: -0.085f,
            planarization: 0.96f,
            planarizationThreshold: 0.70f,
            distanceRecovery: 0.72f,
            curveRecovery: 0.76f);

        public static VoxelSurfaceProfile RoughRock => new(
            smoothing: 0.82f,
            blurPasses: 1,
            modificationStrength: 0.035f,
            modificationScaleVoxels: 5.5f,
            distanceRecovery: 0.48f,
            curveRecovery: 0.55f);
    }

    /// <summary>
    /// Dense material-id lookup used by every derived surface renderer. This is the canonical
    /// reconstruction contract for the current 0..17 palette.
    /// </summary>
    public sealed class VoxelSurfaceProfileSet
    {
        private readonly VoxelSurfaceProfile[] _profiles = new VoxelSurfaceProfile[256];

        public VoxelSurfaceProfileSet() : this(VoxelSurfaceProfile.Legacy) { }

        public VoxelSurfaceProfileSet(VoxelSurfaceProfile defaultProfile)
        {
            for (int i = 0; i < _profiles.Length; i++)
                _profiles[i] = defaultProfile;
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
            set.Set(1,  VoxelSurfaceProfile.DressedStone);        // stone
            set.Set(2,  VoxelSurfaceProfile.HardManufactured);    // wood
            set.Set(3,  VoxelSurfaceProfile.SoftTerrain);         // sand
            set.Set(4,  VoxelSurfaceProfile.HardManufactured);    // glass
            set.Set(5,  VoxelSurfaceProfile.RoughRock);           // bedrock
            set.Set(6,  VoxelSurfaceProfile.DressedStone);        // dark stone
            set.Set(7,  VoxelSurfaceProfile.HardManufactured);    // slate
            set.Set(8,  VoxelSurfaceProfile.HardManufactured);    // tile
            set.Set(9,  new VoxelSurfaceProfile(0.55f, 1));        // cloth
            set.Set(10, VoxelSurfaceProfile.SoftTerrain);         // grass
            set.Set(11, new VoxelSurfaceProfile(0.96f, 2));        // water
            set.Set(12, VoxelSurfaceProfile.HardManufactured);    // gold
            set.Set(13, VoxelSurfaceProfile.SoftTerrain);         // dirt
            set.Set(14, new VoxelSurfaceProfile(0.90f, 2,
                                                 modificationStrength: 0.018f,
                                                 modificationScaleVoxels: 4.5f)); // moss
            set.Set(15, VoxelSurfaceProfile.HardManufactured);    // lit window
            set.Set(16, new VoxelSurfaceProfile(0.96f, 2));        // cascade
            set.Set(17, new VoxelSurfaceProfile(0.70f, 1,
                                                 distanceRecovery: 0.55f,
                                                 curveRecovery: 0.45f)); // crystal
            return set;
        }
    }
}
