using System;

namespace VoxelEngine.Net.Runtime.Client
{
    /// <summary>
    /// Sessions-owned reply decoder/queue at the client composition boundary. The payload is borrowed
    /// for this callback only. Validate/copy bounded protocol data here; apply admission/session state
    /// outside the transport pump. A successfully queued message does not grant GameplayReady.
    /// </summary>
    public interface IServerSessionAdmissionHandler
    {
        bool TryEnqueueSessionAdmissionReply(ReadOnlySpan<byte> payload);
    }
}
