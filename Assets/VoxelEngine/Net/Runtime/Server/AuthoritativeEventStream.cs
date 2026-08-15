using System;
using System.Collections.Generic;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Tick-scoped stream of authoritative world mutations.
    ///
    /// Gameplay and simulation publish semantic events here after validation. Networking,
    /// persistence, moderation, and replay consume the sealed stream rather than being called
    /// directly from gameplay code. This keeps the fixed simulation clock while making the
    /// systems around it event-driven.
    /// </summary>
    public sealed class AuthoritativeEventStream
    {
        private readonly List<AlterationEvent> _alterations;
        private uint _tick;
        private bool _open;
        private bool _sealed;

        public AuthoritativeEventStream(int initialCapacity = 64)
        {
            _alterations = new List<AlterationEvent>(Math.Max(1, initialCapacity));
        }

        /// <summary>Current tick being authored, or zero before the first BeginTick.</summary>
        public uint Tick => _tick;

        /// <summary>Number of authoritative alterations published in the current tick.</summary>
        public int Count => _alterations.Count;

        /// <summary>Begin authoring a new authoritative tick.</summary>
        public void BeginTick(uint tick)
        {
            if (tick == 0)
                throw new ArgumentOutOfRangeException(nameof(tick), "Authoritative ticks start at 1.");
            if (_open && !_sealed)
                throw new InvalidOperationException("The current authoritative tick must be sealed before beginning another tick.");
            if (_tick != 0 && tick <= _tick)
                throw new ArgumentOutOfRangeException(nameof(tick), "Authoritative ticks must increase monotonically.");

            _alterations.Clear();
            _tick = tick;
            _open = true;
            _sealed = false;
        }

        /// <summary>
        /// Publish a validated alteration. The event must already carry the current server tick.
        /// The stream intentionally does not re-run gameplay validation; it is the post-validation
        /// authority boundary.
        /// </summary>
        public void Publish(in AlterationEvent evt)
        {
            if (!_open)
                throw new InvalidOperationException("BeginTick must be called before publishing authoritative events.");
            if (_sealed)
                throw new InvalidOperationException("Cannot publish after the authoritative tick has been sealed.");
            if (evt.tick != _tick)
                throw new ArgumentException("Published event tick does not match the active authoritative tick.", nameof(evt));

            _alterations.Add(evt);
        }

        /// <summary>
        /// Seal the tick and return events in the server arbitration order (tick, playerId, sequence).
        /// Consumers must process this list synchronously before the next BeginTick because the
        /// backing storage is deliberately reused to avoid per-tick allocations.
        /// </summary>
        public IReadOnlyList<AlterationEvent> SealTick()
        {
            if (!_open)
                throw new InvalidOperationException("BeginTick must be called before sealing an authoritative tick.");

            if (!_sealed)
            {
                _alterations.Sort(AlterationComparer.Instance);
                _sealed = true;
            }

            return _alterations;
        }

        private sealed class AlterationComparer : IComparer<AlterationEvent>
        {
            public static readonly AlterationComparer Instance = new AlterationComparer();

            public int Compare(AlterationEvent a, AlterationEvent b)
            {
                int player = a.playerId.CompareTo(b.playerId);
                if (player != 0)
                    return player;

                return a.sequence.CompareTo(b.sequence);
            }
        }
    }
}
