using System.Runtime.CompilerServices;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>
    /// Session-state lifecycle only. World storage creation, reset and terrain regeneration belong
    /// to Composition/Storage; Net owns the session transition and notification semantics.
    /// </summary>
    public static class SessionLifecycle
    {
        private const float k_DefaultGracePeriodMs = 5000f;
        private const float k_MinMobileGracePeriodMs = 3000f;

        private static State _state;
        private static uint _terrainSeed;
        private static uint _startTick;
        private static float _gracePeriodEndMs;
        private static long _totalAlterations;
        private static int _activePlayerCount;

        public static State CurrentState => _state;
        public static uint TerrainSeed => _terrainSeed;
        public static uint StartTick => _startTick;
        public static long TotalAlterations => _totalAlterations;

        /// <summary>
        /// Start a session with the seed Composition will use for pristine world generation.
        /// Net records the session identity only; it does not allocate or generate world storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Create(uint terrainSeed)
        {
            _terrainSeed = terrainSeed;
            _startTick = 1;
            _state = State.Active;
            _gracePeriodEndMs = 0f;
            _totalAlterations = 0;
            _activePlayerCount = 0;
        }

        public static void SignalEnd(float gracePeriodMs)
        {
            if (_state != State.Active)
                return;

            _state = State.Ending;
            _gracePeriodEndMs = gracePeriodMs > 0f ? gracePeriodMs : k_DefaultGracePeriodMs;
            NotifyAllPlayers(SessionEndReason.ServerShutdown);
        }

        /// <summary>
        /// Complete Net's side of session teardown. Composition is responsible for discarding or
        /// regenerating world state through Storage/Terrain capabilities before/after this call.
        /// </summary>
        public static void CompleteWorldReset()
        {
            _state = State.Ended;
            _totalAlterations = 0;
        }

        public static void PlayerJoin()
        {
            if (_state == State.Ended)
                return;
            _activePlayerCount++;
        }

        public static bool PlayerLeave()
        {
            if (_activePlayerCount <= 0)
                return false;

            _activePlayerCount--;
            if (_state == State.Ending && _activePlayerCount == 0)
                return false;
            return _state == State.Ended;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecordAlteration()
        {
            if (_state == State.Active)
                _totalAlterations++;
        }

        private static void NotifyAllPlayers(SessionEndReason reason)
        {
            // Transport/session fan-out is wired by the authoritative server host.
        }

        public enum State : byte
        {
            Active = 0,
            Ending = 1,
            Ended = 2,
        }

        public enum SessionEndReason : byte
        {
            ServerShutdown = 0,
            DurationLimit = 1,
            AdminTerminate = 2,
        }
    }
}
