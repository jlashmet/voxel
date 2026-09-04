namespace Game.Progression
{
    public enum ProgressionApplyStatus
    {
        Applied = 0,
        Replay = 1,
        Rejected = 2
    }

    public readonly struct ProgressionObservation
    {
        public ProgressionObservation(string operationId, ProgressionDomain domain, string progressionId, string eventId, int amount)
        {
            OperationId = operationId ?? string.Empty;
            Domain = domain;
            ProgressionId = progressionId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Amount = amount;
        }
        public string OperationId { get; }
        public ProgressionDomain Domain { get; }
        public string ProgressionId { get; }
        public string EventId { get; }
        public int Amount { get; }
    }

    public enum ProgressionDomain
    {
        QuestObjective = 0,
        StandaloneObjective = 1
    }

    public readonly struct ProgressionApplyResult
    {
        public ProgressionApplyResult(ProgressionApplyStatus status, string reason)
        {
            Status = status;
            Reason = reason ?? string.Empty;
        }
        public ProgressionApplyStatus Status { get; }
        public string Reason { get; }
    }

    public interface IProgressionSink
    {
        ProgressionApplyResult Observe(ProgressionObservation observation);
    }
}
