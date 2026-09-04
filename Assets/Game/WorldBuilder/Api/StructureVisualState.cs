namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Coarse authoritative visual state for a semantic structure after local physical changes.
    /// This is intentionally much smaller than voxel state and exists so distant presentation can
    /// remain consistent after detailed regions unload.
    /// </summary>
    public enum StructureVisualState : byte
    {
        Intact = 0,
        Ruined = 1,
        Removed = 2,
    }

    /// <summary>
    /// Read-only semantic structure state keyed by the same stable source ID used by planned
    /// structure presentation. Implementations must not derive state from renderer or GPU output.
    /// </summary>
    public interface IStructureVisualStateSource
    {
        StructureVisualState Get(ulong structureId);
    }

    /// <summary>
    /// Authoritative CPU/world-event update boundary for coarse structure visual state.
    /// </summary>
    public interface IStructureVisualStateStore : IStructureVisualStateSource
    {
        void Set(ulong structureId, StructureVisualState state);
        bool Remove(ulong structureId);
        void Clear();
    }
}
