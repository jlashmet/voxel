namespace Game.WorldBuilder.Api
{
    public enum StructureVisualState : byte
    {
        Intact = 0,
        Removed = 1,
    }

    /// <summary>
    /// Read-only coarse visual state keyed by the existing semantic structure ID. This is CPU/world
    /// state for far presentation only; it does not retain voxel geometry or derive truth from rendering.
    /// </summary>
    public interface IStructureVisualStateSource
    {
        StructureVisualState Get(ulong structureKey);
        ulong Revision { get; }
    }
}
