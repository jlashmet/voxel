using Game.Structures.Api;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen.Architecture;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-side owner for semantic landmark visibility. It stores only renderer-neutral
    /// descriptors and can be populated the instant CastlePlan exists, before any castle voxel
    /// region is resident or physically realized.
    /// </summary>
    public sealed class ShowcaseCastleVisibilityManifest
    {
        private readonly WorldVisibilityManifest _manifest;
        private ulong _castleKey;

        public ShowcaseCastleVisibilityManifest(int sectorSizeDm = WorldVisibilityManifest.DefaultSectorSizeDm)
        {
            _manifest = new WorldVisibilityManifest(sectorSizeDm);
        }

        public IWorldVisibilitySource Source => _manifest;
        public int Count => _manifest.Count;
        public ulong CastleKey => _castleKey;

        public StructureFarPresentation Register(in CastlePlan plan)
        {
            StructureFarPresentation descriptor = ShowcaseCastleFarPresentation.FromPlan(in plan);
            _manifest.Upsert(descriptor);
            _castleKey = descriptor.StructureKey;
            return descriptor;
        }

        public bool TryGetCastle(out StructureFarPresentation descriptor)
        {
            if (_castleKey != 0UL && _manifest.TryGet(_castleKey, out descriptor))
                return true;
            descriptor = default;
            return false;
        }

        public void Clear()
        {
            if (_castleKey != 0UL) _manifest.Remove(_castleKey);
            _castleKey = 0UL;
        }
    }
}
