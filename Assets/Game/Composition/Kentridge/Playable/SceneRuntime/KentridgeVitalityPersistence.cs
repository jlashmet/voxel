using System;
using System.Collections.Generic;
using System.IO;
using Game.Characters.Api;
using Game.Persistence.Api;
using Game.Persistence.Runtime;
using Game.Vitality.Api;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Composition-owned persistence of the existing Vitality authority. Resolve the service for
    /// each operation so Continue restores its fresh graph, never the disposed source registry.
    /// </summary>
    internal static class KentridgeVitalityPersistence
    {
        private const int MaximumSnapshotCount = 65536;

        internal static ISessionSnapshotContributor CreateContributor(
            Func<IVitalityQuery> query,
            Func<CharacterId> playerId)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (playerId == null) throw new ArgumentNullException(nameof(playerId));

            IVitalityService Service() => query() as IVitalityService
                ?? throw new InvalidOperationException("The composed Vitality persistence service is unavailable.");
            SessionContributorResult Validate(VitalitySnapshot[] state) => ValidateState(state, playerId());
            VitalitySnapshot[] Capture()
            {
                VitalitySnapshot[] state = Service().Capture();
                SessionContributorResult validation = Validate(state);
                if (!validation.Succeeded) throw new InvalidOperationException(validation.Detail);
                return state;
            }
            SessionContributorResult Restore(VitalitySnapshot[] state)
            {
                SessionContributorResult validation = Validate(state);
                if (!validation.Succeeded) return validation;
                VitalityRestoreResult result = Service().Restore(state);
                return result.Accepted
                    ? SessionContributorResult.Success()
                    : SessionContributorResult.Reject("Vitality restore failed: " + result.RejectionReason + ".");
            }

            return new DelegateSessionSnapshotContributor<VitalitySnapshot[]>(
                "vitality", "VitalityState", 1, 350, true,
                Capture, Encode, Decode, Validate, Restore);
        }

        private static SessionContributorResult ValidateState(VitalitySnapshot[] state, CharacterId playerId)
        {
            if (!playerId.IsValid || state == null || state.Length == 0 || state.Length > MaximumSnapshotCount)
                return SessionContributorResult.Reject("A bounded vitality snapshot including the player is required.");
            var ids = new HashSet<CharacterId>();
            for (int i = 0; i < state.Length; i++)
            {
                VitalitySnapshot value = state[i];
                if (!value.CharacterId.IsValid || value.Maximum <= 0 || value.Current < 0 ||
                    value.Current > value.Maximum || value.Defeated != (value.Current == 0) ||
                    !ids.Add(value.CharacterId))
                    return SessionContributorResult.Reject("Vitality snapshot contains invalid or duplicate character state.");
            }
            return ids.Contains(playerId)
                ? SessionContributorResult.Success()
                : SessionContributorResult.Reject("Vitality snapshot is missing the production player.");
        }

        private static byte[] Encode(VitalitySnapshot[] state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(state.Length);
                for (int i = 0; i < state.Length; i++)
                {
                    VitalitySnapshot value = state[i];
                    writer.Write(value.CharacterId.Value);
                    writer.Write(value.Current);
                    writer.Write(value.Maximum);
                    writer.Write(value.Defeated);
                    writer.Write(value.Revision);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static VitalitySnapshot[] Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                if (count <= 0 || count > MaximumSnapshotCount)
                    throw new InvalidDataException("Vitality snapshot count is invalid: " + count + ".");
                var state = new VitalitySnapshot[count];
                for (int i = 0; i < count; i++)
                    state[i] = new VitalitySnapshot(
                        new CharacterId(reader.ReadString()), reader.ReadInt32(), reader.ReadInt32(),
                        reader.ReadBoolean(), reader.ReadUInt64());
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Vitality snapshot contains trailing data.");
                return state;
            }
        }
    }
}
