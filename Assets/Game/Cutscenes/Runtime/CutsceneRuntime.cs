using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;

namespace Game.Cutscenes.Runtime
{
    public sealed class CutsceneActorRegistry : ICutsceneActorController
    {
        private readonly Dictionary<CutsceneActorId, ICutsceneActorRuntime> _actors =
            new Dictionary<CutsceneActorId, ICutsceneActorRuntime>();

        public void Register(CutsceneActorId id, ICutsceneActorRuntime actor)
        {
            _actors[id] = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public bool Unregister(CutsceneActorId id) => _actors.Remove(id);
        public bool Contains(CutsceneActorId id) => _actors.ContainsKey(id);

        public void PlaceAt(CutsceneActorId actor, CutsceneStagePoint destination) =>
            Resolve(actor).PlaceAt(destination);

        public ICutsceneOperation MoveTo(CutsceneActorId actor, CutsceneStagePoint destination, int durationHintMilliseconds) =>
            Resolve(actor).MoveTo(destination, durationHintMilliseconds);

        public ICutsceneOperation FaceActor(CutsceneActorId actor, CutsceneActorId target) =>
            Resolve(actor).FaceTowards(Resolve(target).Position);

        public ICutsceneOperation FacePoint(CutsceneActorId actor, CutsceneStagePoint target) =>
            Resolve(actor).FaceTowards(target.Position);

        private ICutsceneActorRuntime Resolve(CutsceneActorId id)
        {
            if (_actors.TryGetValue(id, out ICutsceneActorRuntime actor)) return actor;
            throw new KeyNotFoundException("Cutscene actor '" + id + "' is not registered.");
        }
    }

    public static class CutsceneStageSetup
    {
        public static void Validate(CutsceneDefinition definition, ICutsceneActorController actors, CutsceneStageBinding stage)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            for (var i = 0; i < definition.Setup.Placements.Count; i++)
            {
                CutsceneActorPlacement placement = definition.Setup.Placements[i];
                if (!actors.Contains(placement.Actor))
                    throw new InvalidOperationException("Cutscene setup actor '" + placement.Actor + "' is not registered.");
                if (!stage.TryResolve(placement.StagePoint, out _))
                    throw new InvalidOperationException("Cutscene setup stage point '" + placement.StagePoint + "' is not bound.");
            }
        }

        public static void Apply(CutsceneDefinition definition, ICutsceneActorController actors, CutsceneStageBinding stage)
        {
            Validate(definition, actors, stage);
            for (var i = 0; i < definition.Setup.Placements.Count; i++)
            {
                CutsceneActorPlacement placement = definition.Setup.Placements[i];
                actors.PlaceAt(placement.Actor, stage.Resolve(placement.StagePoint));
            }
        }
    }

    public static class CutscenePreflight
    {
        public static void Validate(CutsceneDefinition definition, ICutsceneActorController actors, CutsceneStageBinding stage)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            CutsceneStageSetup.Validate(definition, actors, stage);
            for (var i = 0; i < definition.Steps.Count; i++)
                ValidateStep(definition.Steps[i], actors, stage, definition.Id + "[" + i + "]");
        }

        private static void ValidateStep(
            CutsceneStep step,
            ICutsceneActorController actors,
            CutsceneStageBinding stage,
            string path)
        {
            switch (step.Type)
            {
                case CutsceneStepType.Wait:
                    RequireNonNegativeDuration(step.DurationMilliseconds, path);
                    return;
                case CutsceneStepType.MoveActor:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireStagePoint(step.StagePoint, stage, path);
                    RequireNonNegativeDuration(step.DurationMilliseconds, path);
                    return;
                case CutsceneStepType.FaceActor:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireActor(step.TargetActor, actors, path, "target actor");
                    return;
                case CutsceneStepType.FacePoint:
                    RequireActor(step.Actor, actors, path, "actor");
                    RequireStagePoint(step.StagePoint, stage, path);
                    return;
                case CutsceneStepType.Dialogue:
                    if (!string.IsNullOrWhiteSpace(step.Actor.Value))
                        RequireActor(step.Actor, actors, path, "speaker");
                    RequireCue(step.Cue, path);
                    return;
                case CutsceneStepType.Camera:
                case CutsceneStepType.Sound:
                    RequireCue(step.Cue, path);
                    return;
                case CutsceneStepType.Parallel:
                    if (step.Children.Count == 0)
                        throw new InvalidOperationException("Parallel cutscene step " + path + " has no children.");
                    for (var i = 0; i < step.Children.Count; i++)
                        ValidateStep(step.Children[i], actors, stage, path + "/parallel[" + i + "]");
                    return;
                default:
                    throw new InvalidOperationException("Unsupported cutscene step type at " + path + ": " + step.Type + ".");
            }
        }

        private static void RequireActor(CutsceneActorId actor, ICutsceneActorController actors, string path, string role)
        {
            if (string.IsNullOrWhiteSpace(actor.Value))
                throw new InvalidOperationException("Cutscene step " + path + " has no " + role + " id.");
            if (!actors.Contains(actor))
                throw new InvalidOperationException("Cutscene step " + path + " requires unregistered " + role + " '" + actor + "'.");
        }

        private static void RequireStagePoint(CutsceneStagePointId point, CutsceneStageBinding stage, string path)
        {
            if (string.IsNullOrWhiteSpace(point.Value))
                throw new InvalidOperationException("Cutscene step " + path + " has no stage point id.");
            if (!stage.TryResolve(point, out _))
                throw new InvalidOperationException("Cutscene step " + path + " requires unbound stage point '" + point + "'.");
        }

        private static void RequireCue(CutsceneCueId cue, string path)
        {
            if (string.IsNullOrWhiteSpace(cue.Value))
                throw new InvalidOperationException("Cutscene step " + path + " has no cue id.");
        }

        private static void RequireNonNegativeDuration(int durationMilliseconds, string path)
        {
            if (durationMilliseconds < 0)
                throw new InvalidOperationException("Cutscene step " + path + " has a negative duration.");
        }
    }

    public sealed class CutsceneRunner
    {
        private CutsceneDefinition _definition;
        private CutsceneExecutionContext _context;
        private ICutsceneOperation _operation;
        private int _waitRemainingMilliseconds;
        private bool _waiting;

        public int CurrentStepIndex { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsComplete { get; private set; }

        public void Start(CutsceneDefinition definition, CutsceneExecutionContext context)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _operation = null;
            _waitRemainingMilliseconds = 0;
            _waiting = false;
            CurrentStepIndex = 0;
            IsComplete = definition.Steps.Count == 0;
            IsRunning = !IsComplete;
        }

        public void Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (!IsRunning) return;

            var timeLeft = elapsedMilliseconds;
            while (CurrentStepIndex < _definition.Steps.Count)
            {
                if (_operation != null)
                {
                    if (!_operation.IsComplete) return;
                    _operation = null;
                    CurrentStepIndex++;
                    continue;
                }

                CutsceneStep step = _definition.Steps[CurrentStepIndex];
                if (step.Type == CutsceneStepType.Wait)
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

                _operation = Execute(step) ??
                    throw new InvalidOperationException("Cutscene adapter returned a null operation.");
                if (!_operation.IsComplete) return;
                _operation = null;
                CurrentStepIndex++;
            }

            IsRunning = false;
            IsComplete = true;
        }

        private ICutsceneOperation Execute(CutsceneStep step)
        {
            switch (step.Type)
            {
                case CutsceneStepType.MoveActor:
                    return _context.Actors.MoveTo(step.Actor, _context.Stage.Resolve(step.StagePoint), step.DurationMilliseconds);
                case CutsceneStepType.FaceActor:
                    return _context.Actors.FaceActor(step.Actor, step.TargetActor);
                case CutsceneStepType.FacePoint:
                    return _context.Actors.FacePoint(step.Actor, _context.Stage.Resolve(step.StagePoint));
                case CutsceneStepType.Dialogue:
                    return _context.Presentation.ShowDialogue(step.Actor, step.Cue);
                case CutsceneStepType.Camera:
                    return _context.Presentation.SetCamera(step.Cue);
                case CutsceneStepType.Sound:
                    return _context.Presentation.PlaySound(step.Cue);
                case CutsceneStepType.Parallel:
                    return ExecuteParallel(step);
                default:
                    throw new InvalidOperationException("Unsupported cutscene step " + step.Type + ".");
            }
        }

        private ICutsceneOperation ExecuteParallel(CutsceneStep step)
        {
            var operations = new ICutsceneOperation[step.Children.Count];
            for (var i = 0; i < step.Children.Count; i++)
            {
                CutsceneStep child = step.Children[i];
                if (child.Type == CutsceneStepType.Wait)
                    throw new InvalidOperationException("Wait steps cannot execute inside parallel cutscene work.");
                operations[i] = Execute(child) ??
                    throw new InvalidOperationException("Cutscene adapter returned a null operation for parallel child " + i + ".");
            }
            return new ParallelOperation(operations);
        }

        private sealed class ParallelOperation : ICutsceneOperation
        {
            private readonly ICutsceneOperation[] _operations;
            public ParallelOperation(ICutsceneOperation[] operations) =>
                _operations = operations ?? throw new ArgumentNullException(nameof(operations));

            public bool IsComplete
            {
                get
                {
                    for (var i = 0; i < _operations.Length; i++)
                        if (!_operations[i].IsComplete) return false;
                    return true;
                }
            }
        }
    }

    public static class CutscenePlayback
    {
        public static CutsceneRunner Start(
            CutsceneDefinition definition,
            ICutsceneActorController actors,
            ICutscenePresentation presentation,
            CutsceneStageBinding stage)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            CutscenePreflight.Validate(definition, actors, stage);
            CutsceneStageSetup.Apply(definition, actors, stage);

            var runner = new CutsceneRunner();
            runner.Start(definition, new CutsceneExecutionContext(actors, presentation, stage));
            return runner;
        }
    }
}
