using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private readonly StructureVisualStateStore _structureVisualStates = new();
        private FeaturePresentationManifest _farFeaturePresentation;

        /// <summary>
        /// Derived renderer-neutral presentation data from the same canonical feature catalogue
        /// consumed by physical region generation. Building/querying this manifest never requests
        /// voxel residency, so planned finite structures can exist visually before their detailed
        /// regions are generated.
        /// </summary>
        public IFeaturePresentationSource FarFeaturePresentation =>
            EnsureFarFeaturePresentation();

        public int FarFeaturePresentationCount => EnsureFarFeaturePresentation().Count;

        /// <summary>
        /// Authoritative coarse semantic visual state owned with the world lifetime. Presentation
        /// may read this state, but never derives or writes it from GPU/render output.
        /// </summary>
        public IStructureVisualStateSource StructureVisualStates => _structureVisualStates;

        private FeaturePresentationManifest EnsureFarFeaturePresentation()
        {
            if (_farFeaturePresentation != null) return _farFeaturePresentation;
            _farFeaturePresentation = FeaturePresentationCatalogueBaker.Build(in _catalogue, Seed);
            return _farFeaturePresentation;
        }
    }
}
