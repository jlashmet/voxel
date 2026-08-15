namespace VoxelEngine.Edits.Api
{
    public enum AlterationEventKind : byte
    {
        None = 0,
        Explosion = AlterationEvent.KindExplosion,
        Brush = AlterationEvent.KindBrush,
        RawBatch = AlterationEvent.KindRawBatch,
    }
}
