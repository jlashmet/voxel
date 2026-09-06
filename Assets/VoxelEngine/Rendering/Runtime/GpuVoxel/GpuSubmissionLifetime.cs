using System;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    // Main-thread ownership for resources referenced by submitted GPU commands. Logical disposal
    // rejects new use immediately; physical release waits for the last completion callback.
    internal sealed class GpuSubmissionLifetime
    {
        private readonly Action _release;
        private int _users;
        private bool _disposalRequested;
        private bool _released;

        internal GpuSubmissionLifetime(Action release) => _release = release;
        internal bool HasUsers => _users != 0;

        internal void Retain()
        {
            if (_disposalRequested) throw new ObjectDisposedException(nameof(GpuSubmissionLifetime));
            _users = checked(_users + 1);
        }
        internal void Release()
        {
            if (_users == 0) throw new InvalidOperationException("GPU submission ownership underflow.");
            _users--;
            TryRelease();
        }
        internal void Dispose()
        {
            _disposalRequested = true;
            TryRelease();
        }
        private void TryRelease()
        {
            if (!_disposalRequested || _users != 0 || _released) return;
            _released = true;
            _release();
        }
    }
}
