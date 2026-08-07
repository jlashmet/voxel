using System.Runtime.CompilerServices;
using Unity.Collections;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Rate limiter for the BULK channel that reserves the EVENT channel share per
    /// device-matrix.md §Bandwidth budgets.
    ///
    /// Tracks bytes sent per second window on the BULK channel and yields (returns false)
    /// when the total bandwidth would encroach on the EVENT reservation threshold.
    ///
    /// Usage pattern — call <see cref="TryAllow"/> before writing to the BULK pipeline:
    /// <code>
    /// if (throttle.TryAllow(byteCount, currentTick))
    ///     driver.Send(bulkPipeline, connection, span);
    /// </code>
    /// </summary>
    public struct BulkThrottle
    {
        // -- budget constants from device-matrix.md §Bandwidth budgets ---------------

        /// <summary>Sustained downstream budget on wired/Wi-Fi: 256 KB/s (device-matrix.md).</summary>
        public const uint k_SustainedDownstreamWiredKb = 256;

        /// <summary>Sustained downstream budget on mobile-HE cellular: 96 KB/s (device-matrix.md).</summary>
        public const uint k_SustainedDownstreamMobileKb = 96;

        /// <summary>Peak downstream budget on wired/Wi-Fi over a 2 s window: 512 KB/s.</summary>
        public const uint k_PeakDownstreamWiredKb = 512;

        /// <summary>Peak downstream budget on mobile-HE cellular over a 2 s window: 192 KB/s.</summary>
        public const uint k_PeakDownstreamMobileKb = 192;

        // -- internal state --------------------------------------------------------

        /// <summary>Bytes sent on the BULK channel in the current one-second window.</summary>
        private uint _bytesInWindow;

        /// <summary>Timestamp (in ticks) when the current window started.</summary>
        private uint _windowStartTick;

        /// <summary>Maximum BULK bytes per second before exceeding EVENT reservation.
        /// Computed as totalCapacity × (1 - eventShare) where eventShare is ≥ 60% wired /
        /// ≥ 70% mobile (device-matrix.md).</summary>
        private readonly uint _maxBulkBytesPerSecond;

        /// <summary>Peak B allowance per second in bytes, derived from device-matrix.md
        /// peak downstream minus the EVENT reserve.</summary>
        private readonly uint _peakBurstBytes;

        /// <summary>Window duration in ticks. Defaults to 30 ticks (1 second at 30 Hz).</summary>
        private const uint k_WindowTicks = 30;

        // -- constructor -----------------------------------------------------------

        /// <summary>
        /// Constructs a BulkThrottle with the given total downstream capacity and device class.
        /// </summary>
        /// <param name="totalCapacityKbPerSecond">Total downstream bandwidth in KB/s.</param>
        /// <param name="eventShare">Fraction reserved for EVENT (≥ 0.60 wired, ≥ 0.70 mobile).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BulkThrottle(uint totalCapacityKbPerSecond, float eventShare)
        {
            _bytesInWindow = 0;
            _windowStartTick = 0;

            // BULK gets the portion of bandwidth not reserved for EVENT.
            // device-matrix.md: BULK is "remainder, yields to EVENT".
            float bulkShare = 1.0f - eventShare;
            _maxBulkBytesPerSecond = (uint)(totalCapacityKbPerSecond * bulkShare * 1024f);

            // Peak burst allowance: allow up to the full BULK budget in a single tick
            // as long as the rolling window average stays under the cap.
            _peakBurstBytes = _maxBulkBytesPerSecond;
        }

        /// <summary>
        /// Attempts to allow <paramref name="byteCount"/> bytes on the BULK channel.
        /// Returns false when sending would exceed the EVENT reservation threshold.
        /// </summary>
        /// <param name="byteCount">Number of bytes to send on the BULK channel.</param>
        /// <param name="currentTick">Current server tick (used as the time source).</param>
        /// <returns>True if the bytes are allowed; false if the throttle yields.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAllow(int byteCount, uint currentTick)
        {
            // Slide the window forward when past its boundary.
            uint elapsed = currentTick - _windowStartTick;

            if (elapsed >= k_WindowTicks)
            {
                _bytesInWindow = 0;
                _windowStartTick = currentTick;
                elapsed = 0;
            }

            // Check if adding byteCount would exceed the per-second budget.
            uint newTotal = _bytesInWindow + (uint)byteCount;
            return newTotal <= _maxBulkBytesPerSecond;
        }

        /// <summary>
        /// Records that <paramref name="byteCount"/> bytes were successfully sent on BULK,
        /// advancing the throttle's window counter. Call only when <see cref="TryAllow"/> returned true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkUsed(int byteCount)
        {
            _bytesInWindow += (uint)byteCount;
        }

        /// <summary>Current bytes used in the open window. Used for diagnostic logging.</summary>
        public uint BytesInWindow => _bytesInWindow;

        /// <summary>Remaining BULK bandwidth in the current window, in bytes.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint RemainingBytes()
        {
            return _bytesInWindow > _maxBulkBytesPerSecond ? 0u : _maxBulkBytesPerSecond - _bytesInWindow;
        }

        /// <summary>
        /// Resets the throttle window — used when switching between wired and mobile networks
        /// or when recalculating budgets.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _bytesInWindow = 0;
            _windowStartTick = 0;
        }
    }
}
