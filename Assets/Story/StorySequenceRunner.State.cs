using System;

namespace MountingForce.Story
{
    public sealed partial class StorySequenceRunner
    {
        private StorySequenceDefinition _sequence;
        private StoryExecutionContext _context;
        private IStoryOperation _operation;
        private int _waitRemainingMilliseconds;
        private bool _waiting;

        public int CurrentStepIndex { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }

        public void Start(StorySequenceDefinition sequence, StoryExecutionContext context)
        {
            _sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _operation = null;
            _waitRemainingMilliseconds = 0;
            _waiting = false;
            CurrentStepIndex = 0;
            IsComplete = sequence.Steps.Count == 0;
            IsRunning = !IsComplete;
        }
    }
}
