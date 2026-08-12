using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Controls how authoritative binary voxel occupancy becomes a derived visual surface.
    ///
    /// These values are presentation-only. They never mutate voxel storage, collision, destruction,
    /// or networking state. Smoothing blends from exact binary occupancy (0) toward a reconstructed
    /// field (1). Distance recovery chooses how much of that reconstruction comes from local
    /// coverage/gradient signed-distance estimation instead of the legacy low-pass field. Positive
    /// density bias expands the derived iso-surface; negative bias contracts it. Modification adds
    /// deterministic low-frequency shape variation in density space. Planarization can additionally
    /// pull strongly axis-aligned manufactured faces toward exact voxel-boundary planes.
    /// </summary>
    public readonly struct VoxelSurfaceProfile
    {
        public VoxelSurfaceProfile(float smoothing, float densityBias = 0f,
                                   float modificationStrength = 0f,
                                   float modificationScaleVoxels = 4f,
                                   float planarization = 0f,
                                   float planarizationThreshold = 0.88f,
                                   float distanceRecovery = 0f)
        {
            Smoothing = math.saturate(smoothing);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
            Planarization = math.saturate(planarization);
            PlanarizationThreshold = math.clamp(planarizationThreshold, 0.58f, 0.995f);
            DistanceRecovery = math.saturate(distanceRecovery);
        }

        /// <summary>0 = preserve voxel occupancy; 1 = use the selected reconstructed visual field.</summary>
        public float Smoothing { get; }

        /// <summary>Positive expands the visual surface; negative contracts it.</summary>
        public float DensityBias { get; }

        /// <summary>Amplitude of deterministic presentation-only geometric variation.</summary>
        public float ModificationStrength { get; }

        /// <summary>Approximate wavelength of geometric variation, measured in voxels.</summary>
        public float ModificationScaleVoxels { get; }

        /// <summary>
        /// Strength with which strongly axis-aligned extracted faces return to the nearest exact
        /// half-voxel boundary. Curved and diagonal surfaces are unaffected.
        /// </summary>
        public float Planarization { get; }

        /// <summary>
        /// Minimum dominant normal component before planarization begins. Higher values restrict
        /// the operation to flatter faces; lower values preserve sharper manufactured corners.
        /// </summary>
        public float PlanarizationThreshold { get; }

        /// <summary>
        /// 0 uses the legacy filtered occupancy field; 1 uses local coverage/gradient distance
        /// recovery. Intermediate values provide a stable migration/tuning range per material.
        /// </summary>
        public float DistanceRecovery { get; }

        /// <summary>Matches the pre-profile hero renderer exactly.</summary>
        public static VoxelSurfaceProfile Legacy => new(0.92f);

        /// <summary>
        /// Dressed masonry uses distance recovery to infer the intended sub-voxel plane/curve from
        /// binary occupancy. The radius-one estimator preserves a 90-degree corner far better than
        /// broad Gaussian smoothing while still removing the staircase from a large arch curve.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(
            smoothing: 1.0f,
            densityBias: -0.005f,
            planarization: 0.20f,
            planarizationThreshold: 0.94f,
            distanceRecovery: 1.0f);
    }

    /// <summary>
    /// Dense material-id lookup used by derived surface renderers. The table covers the full byte
    /// material range so adding a palette entry never requires resizing renderer-owned arrays.
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
    }
}
