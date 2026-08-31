using Game.Structures.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Composition-owned lifetime for authoritative gameplay objects that belong to the streamed showcase world.
    /// Voxel storage owns the baked/static world image; Unity presentation owns proxy GameObjects separately.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        // Stable semantic owner for the single showcase castle. Object IDs still include Seed, so identical local
        // keys in worlds with different seeds remain distinct while persistence uses one deterministic parent key.
        public const uint ShowcaseCastleWorldObjectParentId = 0x43415354u; // CAST

        private readonly WorldObjectSceneRegistry _worldObjectScenes = new WorldObjectSceneRegistry();

        public WorldObjectSceneRegistry WorldObjectScenes => _worldObjectScenes;

        /// <summary>
        /// Ensures the baked showcase castle has exactly one authoritative WorldObject scene. Runtime startup uses
        /// emission mode None because the castle voxels already came from the bake; dynamic visual state is handled
        /// by the separate Unity presentation host rather than rewriting voxel bounds.
        /// </summary>
        public WorldObjectGeneratedScene EnsureCastleWorldObjectSceneLoaded()
        {
            if (!_hasCastlePlan)
                throw new System.InvalidOperationException(
                    "Castle WorldObjects cannot load before the showcase castle plan is available.");

            if (_worldObjectScenes.TryGetLoaded(ShowcaseCastleWorldObjectParentId, out WorldObjectGeneratedScene existing))
                return existing;

            return _worldObjectScenes.LoadCastle(
                geometry: null,
                worldSeed: Seed,
                parentId: ShowcaseCastleWorldObjectParentId,
                // The showcase wrapper's implicit conversion does not apply to an `in` parameter,
                // so the game-owned plan it wraps is passed directly.
                plan: in _castlePlan.Value,
                emissionMode: WorldObjectGeometryEmissionMode.None);
        }

        public bool TryGetCastleWorldObjectScene(out WorldObjectGeneratedScene scene) =>
            _worldObjectScenes.TryGetLoaded(ShowcaseCastleWorldObjectParentId, out scene);

        public int TickWorldObjects(int ticks = 1) => _worldObjectScenes.TickLoaded(ticks);

        public bool UnloadCastleWorldObjectScene() =>
            _worldObjectScenes.Unload(ShowcaseCastleWorldObjectParentId);
    }
}
