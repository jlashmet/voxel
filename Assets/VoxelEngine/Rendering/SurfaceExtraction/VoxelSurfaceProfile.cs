using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Controls how authoritative binary voxel occupancy becomes a derived visual surface.
    ///
    /// These values are presentation-only. They never mutate voxel storage, collision, destruction,
    /// or networking state. Smoothing blends from exact binary occupancy (0) toward the renderer's
    /// filtered field (1). Positive density bias expands the derived iso-surface; negative bias
    /// contracts it. Modification adds deterministic low-frequency shape variation in density space.
    /// Planarization lets manufactured materials keep broad dressed faces while curved silhouettes
    /// still receive enough filtering to hide the underlying voxel staircase.
    /// </summary>
    public readonly struct VoxelSurfaceProfile
    {
        public VoxelSurfaceProfile(float smoothing, float densityBias = 0f,
                                   float modificationStrength = 0f,
                                   float modificationScaleVoxels = 4f,
                                   float planarization = 0f,
                                   float planarizationThreshold = 0.88f)
        {
            Smoothing = math.saturate(smoothing);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
            Planarization = math.saturate(planarization);
            PlanarizationThreshold = math.clamp(planarizationThreshold, 0.58f, 0.995f);
        }

        /// <summary>0 = preserve voxel occupancy; 1 = use the fully filtered visual field.</summary>
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

        /// <summary>Matches the pre-profile hero renderer exactly.</summary>
        public static VoxelSurfaceProfile Legacy => new(0.92f);

        /// <summary>
        /// Dressed masonry keeps a strongly filtered curve but recovers planar block faces after
        /// extraction. This avoids choosing between a smooth arch and soap-bar ashlar.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(
            smoothing: 0.80f,
            densityBias: -0.01f,
            planarization: 0.86f,
            planarizationThreshold: 0.86f);
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
