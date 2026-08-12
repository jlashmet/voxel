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
    /// </summary>
    public readonly struct VoxelSurfaceProfile
    {
        public VoxelSurfaceProfile(float smoothing, float densityBias = 0f,
                                   float modificationStrength = 0f,
                                   float modificationScaleVoxels = 4f)
        {
            Smoothing = math.saturate(smoothing);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
        }

        /// <summary>0 = preserve voxel occupancy; 1 = use the fully filtered visual field.</summary>
        public float Smoothing { get; }

        /// <summary>Positive expands the visual surface; negative contracts it.</summary>
        public float DensityBias { get; }

        /// <summary>Amplitude of deterministic presentation-only geometric variation.</summary>
        public float ModificationStrength { get; }

        /// <summary>Approximate wavelength of geometric variation, measured in voxels.</summary>
        public float ModificationScaleVoxels { get; }

        /// <summary>Matches the pre-profile hero renderer exactly.</summary>
        public static VoxelSurfaceProfile Legacy => new(0.92f);

        /// <summary>
        /// Dressed masonry: enough filtering to remove voxel stair-stepping without melting broad
        /// faces and corners into the soft terrain treatment.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(0.34f);
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
