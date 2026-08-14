namespace VoxelEngine.Tests.EditMode
{
    internal static class ByteArraySpanExtensions
    {
        public static System.Span<byte> AsSpan(this byte[] bytes, int start) =>
            new System.Span<byte>(bytes, start, bytes.Length - start);
    }
}
