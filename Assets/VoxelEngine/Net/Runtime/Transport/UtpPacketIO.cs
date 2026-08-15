using System;
using Unity.Collections;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Small allocation-free bridge between Unity Collections data streams and the span-based
    /// protocol codecs used by the rest of the networking layer.
    ///
    /// Keeping this copy explicit gives us one place to enforce packet bounds and avoids creating
    /// managed byte[] objects for every packet. The protocol codecs remain independent of UTP.
    /// </summary>
    internal static class UtpPacketIO
    {
        public static bool TryRead(ref DataStreamReader reader, Span<byte> destination, out int bytesRead)
        {
            bytesRead = reader.Length;
            if (bytesRead < 0 || bytesRead > destination.Length)
            {
                bytesRead = 0;
                return false;
            }

            for (int i = 0; i < bytesRead; i++)
                destination[i] = reader.ReadByte();

            if (reader.HasFailedReads)
            {
                bytesRead = 0;
                return false;
            }

            return true;
        }

        public static bool TryWrite(ref DataStreamWriter writer, ReadOnlySpan<byte> source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (!writer.WriteByte(source[i]))
                    return false;
            }

            return !writer.HasFailedWrites;
        }
    }
}
