using System;
using System.Collections.Generic;

namespace Game.Cutscenes.Api
{
    internal static class CutsceneIdRules
    {
        public static string Require(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Cutscene ids must be non-empty.", paramName);
            return value;
        }
    }

    public readonly struct CutsceneActorId : IEquatable<CutsceneActorId>
    {
        public string Value { get; }
        public CutsceneActorId(string value) => Value = CutsceneIdRules.Require(value, nameof(value));
        public bool Equals(CutsceneActorId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CutsceneActorId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CutsceneActorId left, CutsceneActorId right) => left.Equals(right);
        public static bool operator !=(CutsceneActorId left, CutsceneActorId right) => !left.Equals(right);
    }

    public readonly struct CutsceneStagePointId : IEquatable<CutsceneStagePointId>
    {
        public string Value { get; }
        public CutsceneStagePointId(string value) => Value = CutsceneIdRules.Require(value, nameof(value));
        public bool Equals(CutsceneStagePointId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CutsceneStagePointId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CutsceneStagePointId left, CutsceneStagePointId right) => left.Equals(right);
        public static bool operator !=(CutsceneStagePointId left, CutsceneStagePointId right) => !left.Equals(right);
    }

    public readonly struct CutsceneCueId : IEquatable<CutsceneCueId>
    {
        public string Value { get; }
        public CutsceneCueId(string value) => Value = CutsceneIdRules.Require(value, nameof(value));
        public bool Equals(CutsceneCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CutsceneCueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CutsceneCueId left, CutsceneCueId right) => left.Equals(right);
        public static bool operator !=(CutsceneCueId left, CutsceneCueId right) => !left.Equals(right);
    }

    /// <summary>Integer world coordinate used by cutscene staging; scene Transforms are not authoritative.</summary>
    public readonly struct CutsceneInt3 : IEquatable<CutsceneInt3>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public CutsceneInt3(int x, int y, int z) { X = x; Y = y; Z = z; }
        public bool Equals(CutsceneInt3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is CutsceneInt3 other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((X * 397) ^ Y) * 397 ^ Z; }
        }
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }

    public readonly struct CutsceneStagePoint
    {
        public CutsceneInt3 Position { get; }
        public CutsceneInt3 Forward { get; }

        public CutsceneStagePoint(CutsceneInt3 position, CutsceneInt3 forward)
        {
            Position = position;
            Forward = forward;
        }
    }

    /// <summary>Semantic region a procedural site must provide for a stage point; never a coordinate.</summary>
    public enum CutsceneStageRegion
    {
        Unspecified = 0,
        SiteInterior = 1,
        PublicEntrance = 2,
        EntranceApproach = 3,
        InteriorGatheringArea = 4,
        PlayerSpawnArea = 5,
        /// <summary>
        /// Interior approach near an established gathering area without joining that region's
        /// occupied lateral packing. Useful for a character walking up to an existing group.
        /// </summary>
        ConversationApproach = 6
    }

    /// <summary>Orientation hint resolved by site generation after the concrete layout exists.</summary>
    public enum CutsceneStageFacingHint
    {
        SiteDefault = 0,
        IntoSite = 1,
        TowardEntrance = 2,
        TowardStageCenter = 3
    }

    public readonly struct CutsceneStagePointRequirement
    {
        public CutsceneStagePointId Point { get; }
        public CutsceneStageRegion Region { get; }
        public CutsceneStageFacingHint Facing { get; }
        public int MinimumClearanceDecimetres { get; }

        public CutsceneStagePointRequirement(
            CutsceneStagePointId point,
            CutsceneStageRegion region,
            int minimumClearanceDecimetres,
            CutsceneStageFacingHint facing = CutsceneStageFacingHint.SiteDefault)
        {
            if (string.IsNullOrWhiteSpace(point.Value))
                throw new ArgumentException("Stage requirement requires a point id.", nameof(point));
            if (minimumClearanceDecimetres < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumClearanceDecimetres));
            Point = point;
            Region = region;
            Facing = facing;
            MinimumClearanceDecimetres = minimumClearanceDecimetres;
        }
    }

    /// <summary>
    /// Per-instance mapping from semantic stage points to deterministic world positions.
    /// World generation owns how the points are chosen; cutscene definitions only consume them.
    /// </summary>
    public sealed class CutsceneStageBinding
    {
        private readonly Dictionary<CutsceneStagePointId, CutsceneStagePoint> _points =
            new Dictionary<CutsceneStagePointId, CutsceneStagePoint>();

        public CutsceneStageBinding Bind(CutsceneStagePointId id, CutsceneStagePoint point)
        {
            _points[id] = point;
            return this;
        }

        public bool TryResolve(CutsceneStagePointId id, out CutsceneStagePoint point) =>
            _points.TryGetValue(id, out point);

        public CutsceneStagePoint Resolve(CutsceneStagePointId id)
        {
            if (_points.TryGetValue(id, out CutsceneStagePoint point)) return point;
            throw new KeyNotFoundException("Cutscene stage point '" + id + "' was not bound for this sequence instance.");
        }
    }

    public enum CutsceneStepType
    {
        Wait,
        MoveActor,
        FaceActor,
        FacePoint,
        Dialogue,
        Camera,
        Sound,
        Parallel
    }

    public readonly struct CutsceneActorPlacement
    {
        public CutsceneActorId Actor { get; }
        public CutsceneStagePointId StagePoint { get; }

        public CutsceneActorPlacement(CutsceneActorId actor, CutsceneStagePointId stagePoint)
        {
            if (string.IsNullOrWhiteSpace(actor.Value))
                throw new ArgumentException("Cutscene setup actor id cannot be empty.", nameof(actor));
            if (string.IsNullOrWhiteSpace(stagePoint.Value))
                throw new ArgumentException("Cutscene setup stage point id cannot be empty.", nameof(stagePoint));
            Actor = actor;
            StagePoint = stagePoint;
        }
    }

    public sealed class CutsceneStageSetupDefinition
    {
        public static readonly CutsceneStageSetupDefinition Empty =
            new CutsceneStageSetupDefinition(Array.Empty<CutsceneActorPlacement>());

        private readonly CutsceneActorPlacement[] _placements;
        public IReadOnlyList<CutsceneActorPlacement> Placements => _placements;

        public CutsceneStageSetupDefinition(IEnumerable<CutsceneActorPlacement> placements)
        {
            if (placements == null) throw new ArgumentNullException(nameof(placements));

            var copy = new List<CutsceneActorPlacement>();
            var actors = new HashSet<CutsceneActorId>();
            foreach (CutsceneActorPlacement placement in placements)
            {
                if (!actors.Add(placement.Actor))
                    throw new ArgumentException("Cutscene setup contains actor more than once: " + placement.Actor, nameof(placements));
                copy.Add(placement);
            }
            _placements = copy.ToArray();
        }
    }

    public readonly struct CutsceneStep
    {
        private static readonly CutsceneStep[] EmptyChildren = Array.Empty<CutsceneStep>();
        private readonly CutsceneStep[] _children;

        public CutsceneStepType Type { get; }
        public CutsceneActorId Actor { get; }
        public CutsceneActorId TargetActor { get; }
        public CutsceneStagePointId StagePoint { get; }
        public CutsceneCueId Cue { get; }
        public int DurationMilliseconds { get; }
        public IReadOnlyList<CutsceneStep> Children => _children ?? EmptyChildren;

        private CutsceneStep(
            CutsceneStepType type,
            CutsceneActorId actor,
            CutsceneActorId targetActor,
            CutsceneStagePointId stagePoint,
            CutsceneCueId cue,
            int durationMilliseconds,
            CutsceneStep[] children = null)
        {
            Type = type;
            Actor = actor;
            TargetActor = targetActor;
            StagePoint = stagePoint;
            Cue = cue;
            DurationMilliseconds = durationMilliseconds;
            _children = children;
        }

        public static CutsceneStep Wait(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            return new CutsceneStep(CutsceneStepType.Wait, default, default, default, default, milliseconds);
        }

        public static CutsceneStep Move(CutsceneActorId actor, CutsceneStagePointId point, int durationHintMilliseconds)
        {
            if (durationHintMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(durationHintMilliseconds));
            return new CutsceneStep(CutsceneStepType.MoveActor, actor, default, point, default, durationHintMilliseconds);
        }

        public static CutsceneStep FaceActor(CutsceneActorId actor, CutsceneActorId target) =>
            new CutsceneStep(CutsceneStepType.FaceActor, actor, target, default, default, 0);

        public static CutsceneStep FacePoint(CutsceneActorId actor, CutsceneStagePointId point) =>
            new CutsceneStep(CutsceneStepType.FacePoint, actor, default, point, default, 0);

        public static CutsceneStep Dialogue(CutsceneCueId cue) =>
            new CutsceneStep(CutsceneStepType.Dialogue, default, default, default, cue, 0);

        public static CutsceneStep Dialogue(CutsceneActorId speaker, CutsceneCueId cue) =>
            new CutsceneStep(CutsceneStepType.Dialogue, speaker, default, default, cue, 0);

        public static CutsceneStep Camera(CutsceneCueId cue) =>
            new CutsceneStep(CutsceneStepType.Camera, default, default, default, cue, 0);

        public static CutsceneStep Sound(CutsceneCueId cue) =>
            new CutsceneStep(CutsceneStepType.Sound, default, default, default, cue, 0);

        public static CutsceneStep Parallel(params CutsceneStep[] children)
        {
            if (children == null) throw new ArgumentNullException(nameof(children));
            if (children.Length == 0)
                throw new ArgumentException("Parallel cutscene work must contain at least one child step.", nameof(children));

            var copy = new CutsceneStep[children.Length];
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].Type == CutsceneStepType.Wait)
                    throw new ArgumentException("Wait steps cannot be children of a parallel cutscene step.", nameof(children));
                copy[i] = children[i];
            }
            return new CutsceneStep(CutsceneStepType.Parallel, default, default, default, default, 0, copy);
        }
    }

    /// <summary>
    /// Engine-independent authored choreography. Required actors and semantic stage points are
    /// derived once from setup + steps. Optional stage requirements describe what procedural
    /// geometry must provide without choosing coordinates.
    /// </summary>
    public sealed class CutsceneDefinition
    {
        private readonly CutsceneStep[] _steps;
        private readonly CutsceneActorId[] _requiredActors;
        private readonly CutsceneStagePointId[] _requiredStagePoints;
        private readonly CutsceneStagePointRequirement[] _stageRequirements;

        public string Id { get; }
        public CutsceneStageSetupDefinition Setup { get; }
        public IReadOnlyList<CutsceneStep> Steps => _steps;
        public IReadOnlyList<CutsceneActorId> RequiredActors => _requiredActors;
        public IReadOnlyList<CutsceneStagePointId> RequiredStagePoints => _requiredStagePoints;
        public IReadOnlyList<CutsceneStagePointRequirement> StageRequirements => _stageRequirements;

        public CutsceneDefinition(
            string id,
            CutsceneStageSetupDefinition setup,
            IEnumerable<CutsceneStep> steps)
            : this(id, setup, steps, null)
        {
        }

        public CutsceneDefinition(
            string id,
            CutsceneStageSetupDefinition setup,
            IEnumerable<CutsceneStep> steps,
            IEnumerable<CutsceneStagePointRequirement> stageRequirements)
        {
            Id = CutsceneIdRules.Require(id, nameof(id));
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            _steps = new List<CutsceneStep>(steps).ToArray();

            var actors = new List<CutsceneActorId>();
            var actorSet = new HashSet<CutsceneActorId>();
            var points = new List<CutsceneStagePointId>();
            var pointSet = new HashSet<CutsceneStagePointId>();

            for (var i = 0; i < Setup.Placements.Count; i++)
            {
                AddActor(Setup.Placements[i].Actor, actors, actorSet);
                AddPoint(Setup.Placements[i].StagePoint, points, pointSet);
            }

            for (var i = 0; i < _steps.Length; i++)
                CollectRequirements(_steps[i], actors, actorSet, points, pointSet);

            _requiredActors = actors.ToArray();
            _requiredStagePoints = points.ToArray();
            _stageRequirements = BuildStageRequirements(stageRequirements, _requiredStagePoints);
        }

        private static CutsceneStagePointRequirement[] BuildStageRequirements(
            IEnumerable<CutsceneStagePointRequirement> declared,
            CutsceneStagePointId[] requiredPoints)
        {
            if (declared == null)
            {
                var fallback = new CutsceneStagePointRequirement[requiredPoints.Length];
                for (var i = 0; i < requiredPoints.Length; i++)
                    fallback[i] = new CutsceneStagePointRequirement(requiredPoints[i], CutsceneStageRegion.Unspecified, 0);
                return fallback;
            }

            var result = new List<CutsceneStagePointRequirement>();
            var seen = new HashSet<CutsceneStagePointId>();
            foreach (CutsceneStagePointRequirement requirement in declared)
            {
                if (!seen.Add(requirement.Point))
                    throw new ArgumentException("Stage requirement declared more than once: " + requirement.Point, nameof(declared));
                result.Add(requirement);
            }

            var required = new HashSet<CutsceneStagePointId>(requiredPoints);
            for (var i = 0; i < result.Count; i++)
            {
                if (!required.Contains(result[i].Point))
                    throw new ArgumentException("Stage requirement is not referenced by choreography: " + result[i].Point, nameof(declared));
            }
            for (var i = 0; i < requiredPoints.Length; i++)
            {
                if (!seen.Contains(requiredPoints[i]))
                    throw new ArgumentException("Missing stage requirement for choreography point: " + requiredPoints[i], nameof(declared));
            }
            return result.ToArray();
        }

        private static void CollectRequirements(
            CutsceneStep step,
            List<CutsceneActorId> actors,
            HashSet<CutsceneActorId> actorSet,
            List<CutsceneStagePointId> points,
            HashSet<CutsceneStagePointId> pointSet)
        {
            switch (step.Type)
            {
                case CutsceneStepType.MoveActor:
                    AddActor(step.Actor, actors, actorSet);
                    AddPoint(step.StagePoint, points, pointSet);
                    break;
                case CutsceneStepType.FaceActor:
                    AddActor(step.Actor, actors, actorSet);
                    AddActor(step.TargetActor, actors, actorSet);
                    break;
                case CutsceneStepType.FacePoint:
                    AddActor(step.Actor, actors, actorSet);
                    AddPoint(step.StagePoint, points, pointSet);
                    break;
                case CutsceneStepType.Dialogue:
                    if (!string.IsNullOrWhiteSpace(step.Actor.Value))
                        AddActor(step.Actor, actors, actorSet);
                    break;
                case CutsceneStepType.Parallel:
                    for (var i = 0; i < step.Children.Count; i++)
                        CollectRequirements(step.Children[i], actors, actorSet, points, pointSet);
                    break;
            }
        }

        private static void AddActor(CutsceneActorId actor, List<CutsceneActorId> values, HashSet<CutsceneActorId> seen)
        {
            if (string.IsNullOrWhiteSpace(actor.Value) || !seen.Add(actor)) return;
            values.Add(actor);
        }

        private static void AddPoint(CutsceneStagePointId point, List<CutsceneStagePointId> values, HashSet<CutsceneStagePointId> seen)
        {
            if (string.IsNullOrWhiteSpace(point.Value) || !seen.Add(point)) return;
            values.Add(point);
        }
    }
}
