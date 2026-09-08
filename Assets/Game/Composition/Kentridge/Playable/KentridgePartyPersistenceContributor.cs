using System;
using System.IO;
using Game.Characters.Api;
using Game.Persistence.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// System16 contributor for Sessions-owned durable multiplayer identity. It persists applicant,
    /// member, slot, leadership and CharacterId only. Transport connections, readiness, continuity
    /// credentials and network player ids are intentionally excluded and must be rebuilt by a fresh authority.
    /// </summary>
    public sealed class KentridgePartyPersistenceContributor : ISessionSnapshotContributor
    {
        public const string Id = "kentridge.party";
        public const string Type = "Game.Sessions.PartySessionStateCapture";
        private readonly Func<PartySession> _session;

        public KentridgePartyPersistenceContributor(Func<PartySession> session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public string SectionId => Id;
        public int SchemaVersion => 1;
        public int RestoreOrder => 0;
        public bool RequiredForRestore => true;

        public SessionContributorCapture Capture(ulong authoritativeRevision)
        {
            try
            {
                PartySessionStateCapture state = RequireSession().CaptureState();
                return SessionContributorCapture.Success(new SessionSectionSnapshot(
                    SectionId,
                    Type,
                    SchemaVersion,
                    authoritativeRevision,
                    Encode(state)));
            }
            catch (Exception exception)
            {
                return SessionContributorCapture.Reject(exception.Message);
            }
        }

        public SessionContributorResult Validate(SessionSectionSnapshot section)
        {
            return TryDecode(section, out _, out string error)
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject(error);
        }

        public SessionContributorResult Restore(SessionSectionSnapshot section)
        {
            if (!TryDecode(section, out PartySessionStateCapture state, out string error))
                return SessionContributorResult.Reject(error);
            PartySessionRestoreFailure failure = RequireSession().RestoreState(state);
            return failure == PartySessionRestoreFailure.None
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject("Party roster restore rejected: " + failure);
        }

        private PartySession RequireSession() =>
            _session() ?? throw new InvalidOperationException("Kentridge party session is not composed.");

        private static byte[] Encode(PartySessionStateCapture state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(state.SessionId.Value);
                writer.Write(state.NextMemberOrdinal);
                writer.Write(state.Members.Count);
                for (int i = 0; i < state.Members.Count; i++)
                {
                    PartyMemberStateCapture member = state.Members[i];
                    writer.Write(member.MemberId.Value);
                    writer.Write(member.Slot.Value);
                    writer.Write(member.ApplicantKey);
                    writer.Write((byte)member.LeadershipRole);
                    writer.Write(member.HasCharacter);
                    if (member.HasCharacter) writer.Write(member.CharacterId.Value);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static bool TryDecode(
            SessionSectionSnapshot section,
            out PartySessionStateCapture state,
            out string error)
        {
            state = null;
            error = string.Empty;
            if (section == null)
            {
                error = "Party persistence section is missing.";
                return false;
            }
            if (!string.Equals(section.SectionId, Id, StringComparison.Ordinal) ||
                !string.Equals(section.SemanticType, Type, StringComparison.Ordinal) ||
                section.SchemaVersion != 1)
            {
                error = "Party persistence section metadata does not match the Sessions contract.";
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(section.CopyPayload(), false))
                using (var reader = new BinaryReader(stream))
                {
                    var sessionId = new GameSessionId(reader.ReadString());
                    ulong nextOrdinal = reader.ReadUInt64();
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 1024)
                        throw new InvalidDataException("Party member count is invalid: " + count);
                    var members = new PartyMemberStateCapture[count];
                    for (int i = 0; i < count; i++)
                    {
                        var memberId = new PartyMemberId(reader.ReadString());
                        var slot = new PlayerSlot(reader.ReadInt32());
                        string applicant = reader.ReadString();
                        var leadership = (PartyLeadershipRole)reader.ReadByte();
                        bool hasCharacter = reader.ReadBoolean();
                        CharacterId character = hasCharacter ? new CharacterId(reader.ReadString()) : default;
                        members[i] = new PartyMemberStateCapture(
                            memberId,
                            slot,
                            applicant,
                            leadership,
                            character);
                    }
                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Party persistence section contains trailing bytes.");
                    state = new PartySessionStateCapture(sessionId, nextOrdinal, members);
                    return true;
                }
            }
            catch (Exception exception) when (
                exception is IOException || exception is ArgumentException ||
                exception is ArgumentOutOfRangeException || exception is EndOfStreamException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
