using System;
using System.Collections.Generic;
using Game.Outcomes.Api;

namespace Game.Outcomes.Runtime
{
    /// <summary>
    /// Engine-independent authority for the one terminal gameplay result of a game session.
    /// Ordinary gameplay facts do not enter this type; composition or a configured policy must
    /// explicitly submit a semantic terminal request from an allowed authority.
    /// </summary>
    public sealed class GameOutcomeRuntime : IGameOutcomeService
    {
        private readonly HashSet<OutcomeAuthorityRef> _authorities =
            new HashSet<OutcomeAuthorityRef>();
        private GameOutcomeSnapshot _snapshot;

        public GameOutcomeRuntime(IEnumerable<OutcomeAuthorityRef> authorities)
            : this(authorities, GameOutcomeSnapshot.Running())
        {
        }

        public GameOutcomeRuntime(
            IEnumerable<OutcomeAuthorityRef> authorities,
            GameOutcomeSnapshot restoredSnapshot)
        {
            if (authorities == null) throw new ArgumentNullException(nameof(authorities));

            foreach (OutcomeAuthorityRef authority in authorities)
            {
                if (!authority.IsValid)
                    throw new ArgumentException(
                        "Outcome authority collection cannot contain an empty authority.",
                        nameof(authorities));
                _authorities.Add(authority);
            }

            if (_authorities.Count == 0)
                throw new ArgumentException(
                    "At least one outcome authority must be configured.",
                    nameof(authorities));

            _snapshot = restoredSnapshot;
        }

        public event Action<GameOutcomeResolved> OutcomeResolved;

        public GameOutcomeSnapshot Snapshot() => _snapshot;

        public GameOutcomeResolutionResult RequestResolution(
            GameOutcomeResolutionRequest request)
        {
            if (_snapshot.Lifecycle == GameOutcomeLifecycle.Resolved)
            {
                if (MatchesCommittedRequest(request))
                {
                    return new GameOutcomeResolutionResult(
                        GameOutcomeResolutionStatus.Idempotent,
                        _snapshot);
                }

                return new GameOutcomeResolutionResult(
                    GameOutcomeResolutionStatus.RejectedAlreadyResolved,
                    _snapshot);
            }

            if (!_authorities.Contains(request.Authority))
            {
                return new GameOutcomeResolutionResult(
                    GameOutcomeResolutionStatus.RejectedUnauthorized,
                    _snapshot);
            }

            if (_snapshot.Revision == ulong.MaxValue)
                throw new InvalidOperationException("Outcome revision cannot advance beyond UInt64.MaxValue.");

            _snapshot = new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Resolved,
                request.Disposition,
                request.Outcome,
                request.ResolutionId,
                request.Authority,
                _snapshot.Revision + 1);

            var resolved = new GameOutcomeResolved(_snapshot);
            OutcomeResolved?.Invoke(resolved);

            return new GameOutcomeResolutionResult(
                GameOutcomeResolutionStatus.Accepted,
                _snapshot);
        }

        private bool MatchesCommittedRequest(GameOutcomeResolutionRequest request) =>
            request.ResolutionId.Equals(_snapshot.ResolutionId) &&
            request.Authority.Equals(_snapshot.Authority) &&
            request.Disposition == _snapshot.Disposition &&
            request.Outcome.Equals(_snapshot.Outcome);
    }

    /// <summary>
    /// Authored mapping from a semantic gameplay condition to an explicit terminal request.
    /// The condition is content/configuration identity, not a scene, boss, entity, or technical
    /// process state. Rule order is authoritative when more than one rule maps the same condition.
    /// </summary>
    public sealed class OutcomePolicyRule
    {
        public OutcomeConditionRef Condition { get; }
        public GameOutcomeResolutionRequest Request { get; }

        public OutcomePolicyRule(
            OutcomeConditionRef condition,
            GameOutcomeResolutionRequest request)
        {
            if (!condition.IsValid)
                throw new ArgumentException("Outcome condition is required.", nameof(condition));
            Condition = condition;
            Request = request;
        }
    }

    /// <summary>
    /// Composition-facing policy seam. Unmapped facts are deliberately inert. This is what keeps
    /// battle loss, character defeat, encounter completion, and technical shutdown nonterminal until
    /// authored content explicitly maps one of those semantic conditions to a game outcome request.
    /// </summary>
    public sealed class OutcomePolicyRouter
    {
        private readonly IGameOutcomeResolver _resolver;
        private readonly OutcomePolicyRule[] _rules;

        public OutcomePolicyRouter(
            IGameOutcomeResolver resolver,
            IEnumerable<OutcomePolicyRule> rules)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var configured = new List<OutcomePolicyRule>();
            foreach (OutcomePolicyRule rule in rules)
            {
                if (rule == null)
                    throw new ArgumentException(
                        "Outcome policy cannot contain a null rule.",
                        nameof(rules));
                configured.Add(rule);
            }
            _rules = configured.ToArray();
        }

        public bool TryObserve(
            OutcomeConditionRef condition,
            out GameOutcomeResolutionResult result)
        {
            if (!condition.IsValid)
                throw new ArgumentException("Outcome condition is required.", nameof(condition));

            for (int i = 0; i < _rules.Length; i++)
            {
                if (!_rules[i].Condition.Equals(condition)) continue;
                result = _resolver.RequestResolution(_rules[i].Request);
                return true;
            }

            result = default;
            return false;
        }
    }
}
