namespace Game.Sessions.Api
{
    /// <summary>
    /// Optional nonblocking formation capability for providers whose authority admission completes
    /// later. Application prefers this capability when present; existing synchronous providers keep
    /// ISessionFormationService behavior. Begin methods return one distinct operation per attempt,
    /// not an admitted member, and must clean up any resources if they throw before returning.
    /// </summary>
    public interface IAsyncSessionFormationService : ISessionFormationService
    {
        ISessionFormationOperation BeginHost(HostSessionRequest request);
        ISessionFormationOperation BeginJoin(JoinSessionRequest request);
    }

    /// <summary>
    /// Caller-owned attempt, observed on the application's owning thread. Methods must not block or
    /// spin; provider transport pumping and configured admission deadlines remain provider-owned.
    /// A completed result contains only authority-issued identity or a semantic, credential-free
    /// rejection. Completion is not synchronization or GameplayReady. No socket/credential escapes.
    /// </summary>
    public interface ISessionFormationOperation
    {
        /// <summary>
        /// False means pending and the result is ignored. True exposes this attempt's immutable
        /// terminal result, never another attempt's result. The default result is not valid success.
        /// </summary>
        bool TryGetResult(out SessionFormationResult result);

        /// <summary>
        /// Idempotently abandon an unadopted attempt and release its provider resources. Also clean
        /// up admission that completed concurrently but was not adopted; a late reply must never
        /// attach a cancelled attempt to a later request. After accepted success, normal party Leave
        /// owns teardown instead. Cancellation must not affect another attempt or an existing party.
        /// </summary>
        void Cancel();
    }
}
