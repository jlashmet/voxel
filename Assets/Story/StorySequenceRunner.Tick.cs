using System;

namespace MountingForce.Story
{
    public sealed partial class StorySequenceRunner
    {
        public void Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (!IsRunning) return;

            int timeLeft = elapsedMilliseconds;
            while (CurrentStepIndex < _sequence.Steps.Count)
            {
                if (_operation != null)
                {
                    if (!_operation.IsComplete) return;
                    _operation = null;
                    CurrentStepIndex++;
                    continue;
                }

                StoryStep step = _sequence.Steps[CurrentStepIndex];
                if (step.Type == StoryStepType.Wait)
                {
                    if (!_waiting)
                    {
                        _waitRemainingMilliseconds = step.DurationMilliseconds;
                        _waiting = true;
                    }
                    if (timeLeft < _waitRemainingMilliseconds)
                    {
                        _waitRemainingMilliseconds -= timeLeft;
                        return;
                    }
                    timeLeft -= _waitRemainingMilliseconds;
                    _waitRemainingMilliseconds = 0;
                    _waiting = false;
                    CurrentStepIndex++;
                    continue;
                }

                _operation = Execute(step) ?? throw new InvalidOperationException("Story adapter returned a null operation.");
                if (!_operation.IsComplete) return;
                _operation = null;
                CurrentStepIndex++;
            }

            IsRunning = false;
            IsComplete = true;
        }
    }
}
