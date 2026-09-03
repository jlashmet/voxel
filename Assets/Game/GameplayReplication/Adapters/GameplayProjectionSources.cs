using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Continuity.Api;
using Game.Encounters.Api;
using Game.GameplayReplication.Api;
using Game.Inventory.Api;
using Game.Outcomes.Api;
using Game.Progression.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;

namespace Game.GameplayReplication.Adapters
{
    public sealed class CharactersGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly ICharacterQuery _query;
        public CharactersGameplayProjectionSource(ICharacterQuery query, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            var snapshots = new List<CharacterSnapshot>(_query.GetAll());
            snapshots.Sort((a, b) => a.Id.CompareTo(b.Id));
            var entries = new List<GameplayProjectionEntry>();
            foreach (CharacterSnapshot s in snapshots)
            {
                string p = s.Id.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "lifecycle", s.Lifecycle.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "revision", s.Revision.ToString(CultureInfo.InvariantCulture)));
                entries.Add(new GameplayProjectionEntry(p + "position", Vec(s.Kinematics.Position)));
                entries.Add(new GameplayProjectionEntry(p + "velocity", Vec(s.Kinematics.Velocity)));
                entries.Add(new GameplayProjectionEntry(p + "facing", Vec(s.Kinematics.Facing)));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
        private static string Vec(CharacterVector3 v) =>
            v.X.ToString("R", CultureInfo.InvariantCulture) + "," +
            v.Y.ToString("R", CultureInfo.InvariantCulture) + "," +
            v.Z.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class VitalityGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IVitalityQuery _query;
        public VitalityGameplayProjectionSource(IVitalityQuery query, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("vitality"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            var snapshots = new List<VitalitySnapshot>(_query.GetAll());
            snapshots.Sort((a, b) => a.CharacterId.CompareTo(b.CharacterId));
            var entries = new List<GameplayProjectionEntry>();
            foreach (VitalitySnapshot s in snapshots)
            {
                string p = s.CharacterId.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "current", s.Current.ToString(CultureInfo.InvariantCulture)));
                entries.Add(new GameplayProjectionEntry(p + "maximum", s.Maximum.ToString(CultureInfo.InvariantCulture)));
                entries.Add(new GameplayProjectionEntry(p + "defeated", s.Defeated ? "true" : "false"));
                entries.Add(new GameplayProjectionEntry(p + "revision", s.Revision.ToString(CultureInfo.InvariantCulture)));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class EncounterGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IEncounterQuery _query;
        public EncounterGameplayProjectionSource(IEncounterQuery query, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("encounters"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            var snapshots = new List<EncounterSnapshot>(_query.GetAll());
            snapshots.Sort((a, b) => a.Id.CompareTo(b.Id));
            var entries = new List<GameplayProjectionEntry>();
            foreach (EncounterSnapshot s in snapshots)
            {
                string p = s.Id.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "lifecycle", s.Lifecycle.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "revision", s.Revision.ToString(CultureInfo.InvariantCulture)));
                entries.Add(new GameplayProjectionEntry(p + "kind", s.Definition.SemanticKind));
                entries.Add(new GameplayProjectionEntry(p + "combat-policy", s.Definition.CombatPolicy.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "activation-cause", s.ActivationCause));
                entries.Add(new GameplayProjectionEntry(p + "realization-id", s.RealizationId));
                if (s.Resolution.HasValue)
                {
                    entries.Add(new GameplayProjectionEntry(p + "resolution-result", s.Resolution.Value.Result.ToString()));
                    entries.Add(new GameplayProjectionEntry(p + "resolution-reason", s.Resolution.Value.Reason));
                }
                var participants = new List<EncounterParticipant>(s.Membership.Participants);
                participants.Sort((a, b) => a.CharacterId.CompareTo(b.CharacterId));
                foreach (EncounterParticipant participant in participants)
                {
                    string pp = p + "participant/" + participant.CharacterId.Value + "/";
                    entries.Add(new GameplayProjectionEntry(pp + "ownership", participant.Ownership.ToString()));
                    entries.Add(new GameplayProjectionEntry(pp + "role", participant.Role));
                }
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class CombatGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly ICombatService _combat;
        public CombatGameplayProjectionSource(ICombatService combat, bool requiredForGameplayReady = true)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("combat"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            var entries = new List<GameplayProjectionEntry>
            {
                new GameplayProjectionEntry("active", _combat.IsActive ? "true" : "false"),
                new GameplayProjectionEntry("state", _combat.State.ToString()),
                new GameplayProjectionEntry("session-id", _combat.ActiveSessionId.Value.ToString(CultureInfo.InvariantCulture))
            };
            var participants = new List<CombatParticipant>(_combat.ActiveParticipants);
            participants.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
            foreach (CombatParticipant p in participants)
                entries.Add(new GameplayProjectionEntry("participant/" + p.Id.Value, p.Team.ToString()));
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class InventoryGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IInventoryQuery _inventory;
        public InventoryGameplayProjectionSource(IInventoryQuery inventory, bool requiredForGameplayReady = true)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 2, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            var inventories = new List<InventorySnapshot>(_inventory.GetAllSnapshots());
            inventories.Sort((a, b) => a.Id.CompareTo(b.Id));
            var entries = new List<GameplayProjectionEntry>();
            foreach (InventorySnapshot inventory in inventories)
            {
                string prefix = "inventory/" + inventory.Id.Value + "/";
                entries.Add(new GameplayProjectionEntry(
                    prefix + "revision",
                    inventory.Revision.ToString(CultureInfo.InvariantCulture)));
                var items = new List<InventoryEntry>(inventory.Entries);
                items.Sort((a, b) => a.Item.CompareTo(b.Item));
                foreach (InventoryEntry item in items)
                    entries.Add(new GameplayProjectionEntry(
                        prefix + "item/" + item.Item.Id,
                        item.Quantity.ToString(CultureInfo.InvariantCulture)));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class ProgressionGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IProgressionQuery _query;
        public ProgressionGameplayProjectionSource(IProgressionQuery query, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("progression"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            ProgressionSnapshot snapshot = _query.Snapshot();
            var entries = new List<GameplayProjectionEntry>
            {
                new GameplayProjectionEntry("revision", snapshot.Revision.ToString(CultureInfo.InvariantCulture))
            };
            var quests = new List<QuestProgressSnapshot>(snapshot.Quests);
            quests.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (QuestProgressSnapshot quest in quests)
            {
                string p = "quest/" + quest.Id.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "state", quest.State.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "revision", quest.Revision.ToString(CultureInfo.InvariantCulture)));
                var objectives = new List<ObjectiveProgressSnapshot>(quest.Objectives);
                objectives.Sort((a, b) => a.Id.CompareTo(b.Id));
                foreach (ObjectiveProgressSnapshot objective in objectives)
                {
                    string op = p + "objective/" + objective.Id.Value + "/";
                    entries.Add(new GameplayProjectionEntry(op + "state", objective.State.ToString()));
                    entries.Add(new GameplayProjectionEntry(op + "revision", objective.Revision.ToString(CultureInfo.InvariantCulture)));
                }
            }
            var standalone = new List<ObjectiveProgressSnapshot>(snapshot.StandaloneObjectives);
            standalone.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (ObjectiveProgressSnapshot objective in standalone)
            {
                string p = "objective/" + objective.Id.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "state", objective.State.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "revision", objective.Revision.ToString(CultureInfo.InvariantCulture)));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class SessionsGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IPartySessionQuery _sessions;
        public SessionsGameplayProjectionSource(IPartySessionQuery sessions, bool requiredForGameplayReady = true)
        {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("sessions"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            PartyRosterSnapshot snapshot = _sessions.Snapshot();
            var members = new List<PartyMemberSnapshot>(snapshot.Members);
            members.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            var entries = new List<GameplayProjectionEntry>
            {
                new GameplayProjectionEntry("session-id", snapshot.SessionId.Value)
            };
            foreach (PartyMemberSnapshot member in members)
            {
                string p = "slot/" + member.Slot.Value.ToString(CultureInfo.InvariantCulture) + "/";
                entries.Add(new GameplayProjectionEntry(p + "member-id", member.MemberId.Value));
                entries.Add(new GameplayProjectionEntry(p + "leadership", member.LeadershipRole.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "presence", member.Presence.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "readiness", member.Readiness.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "character-id", member.HasCharacter ? member.CharacterId.Value : string.Empty));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class ContinuityGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IContinuityQuery _query;
        private readonly IPartySessionQuery _sessions;

        public ContinuityGameplayProjectionSource(IContinuityQuery query, IPartySessionQuery sessions, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("continuity"), 1, requiredForGameplayReady);
        }

        public GameplayProjectionDescriptor Descriptor { get; }

        public GameplayProjectionState Capture()
        {
            PartyRosterSnapshot roster = _sessions.Snapshot();
            var members = new List<PartyMemberSnapshot>(roster.Members);
            members.Sort((a, b) => a.MemberId.CompareTo(b.MemberId));
            var entries = new List<GameplayProjectionEntry>();

            foreach (PartyMemberSnapshot member in members)
            {
                if (!_query.TryGetRecovery(member.MemberId, out RecoverySnapshot recovery))
                    continue;

                string p = "member/" + member.MemberId.Value + "/";
                entries.Add(new GameplayProjectionEntry(p + "state", recovery.State.ToString()));
                entries.Add(new GameplayProjectionEntry(p + "grace-deadline", recovery.GraceDeadline.ToString("R", CultureInfo.InvariantCulture)));
            }

            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    public sealed class OutcomesGameplayProjectionSource : IGameplayProjectionSource
    {
        private readonly IGameOutcomeQuery _query;
        public OutcomesGameplayProjectionSource(IGameOutcomeQuery query, bool requiredForGameplayReady = true)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
            Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("outcomes"), 1, requiredForGameplayReady);
        }
        public GameplayProjectionDescriptor Descriptor { get; }
        public GameplayProjectionState Capture()
        {
            GameOutcomeSnapshot snapshot = _query.Snapshot();
            return new GameplayProjectionState(Descriptor, new[]
            {
                new GameplayProjectionEntry("lifecycle", snapshot.Lifecycle.ToString()),
                new GameplayProjectionEntry("disposition", snapshot.Disposition.ToString()),
                new GameplayProjectionEntry("outcome-ref", snapshot.Outcome.IsValid ? snapshot.Outcome.Value : string.Empty),
                new GameplayProjectionEntry("revision", snapshot.Revision.ToString(CultureInfo.InvariantCulture))
            });
        }
    }
}
