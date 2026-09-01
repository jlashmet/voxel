using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Net.Runtime.Transport;

namespace Game.GameplayReplication.Transport
{
    /// <summary>
    /// Private wire codec for semantic gameplay publications carried by the existing Net
    /// S_GameplayState packet family. The gameplay API remains transport-neutral.
    /// </summary>
    public static class GameplayStatePacketCodec
    {
        private const byte WireVersion = 1;
        private const int MaxProjectionCount = 256;
        private const int MaxEntriesPerProjection = 4096;

        public static bool TryEncode(GameplayPublication publication, out byte[] packet)
        {
            packet = null;
            if (publication == null) return false;

            try
            {
                using var stream = new MemoryStream(1024);
                stream.WriteByte(ProtocolEnvelope.CurrentVersion);
                stream.WriteByte((byte)ProtocolMessageKind.S_GameplayState);
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(WireVersion);
                    writer.Write(publication.Revision.Value);
                    writer.Write((byte)publication.Kind);
                    if (publication.Projections.Count > MaxProjectionCount) return false;
                    writer.Write((ushort)publication.Projections.Count);

                    for (int i = 0; i < publication.Projections.Count; i++)
                    {
                        GameplayProjectionState state = publication.Projections[i];
                        writer.Write(state.Descriptor.Id.Value);
                        writer.Write(state.Descriptor.SchemaVersion);
                        writer.Write(state.Descriptor.RequiredForGameplayReady);
                        if (state.Entries.Count > MaxEntriesPerProjection) return false;
                        writer.Write((ushort)state.Entries.Count);
                        for (int e = 0; e < state.Entries.Count; e++)
                        {
                            writer.Write(state.Entries[e].Key);
                            writer.Write(state.Entries[e].Value);
                        }
                    }
                }

                if (stream.Length > ChannelSetup.k_MaxBulkPacketBytes) return false;
                packet = stream.ToArray();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EncoderFallbackException || ex is OverflowException)
            {
                packet = null;
                return false;
            }
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out GameplayPublication publication)
        {
            publication = null;
            if (packet.Length > ChannelSetup.k_MaxBulkPacketBytes ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_GameplayState)
                return false;

            try
            {
                using var stream = new MemoryStream(packet.Slice(payloadOffset).ToArray(), writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                if (reader.ReadByte() != WireVersion) return false;

                long revisionValue = reader.ReadInt64();
                if (revisionValue <= 0) return false;
                byte rawKind = reader.ReadByte();
                if (rawKind > (byte)GameplayPublicationKind.Delta) return false;
                var publicationKind = (GameplayPublicationKind)rawKind;

                int projectionCount = reader.ReadUInt16();
                if (projectionCount > MaxProjectionCount) return false;
                var projections = new GameplayProjectionState[projectionCount];
                for (int i = 0; i < projectionCount; i++)
                {
                    string id = reader.ReadString();
                    int schemaVersion = reader.ReadInt32();
                    bool required = reader.ReadBoolean();
                    int entryCount = reader.ReadUInt16();
                    if (string.IsNullOrWhiteSpace(id) || schemaVersion <= 0 || entryCount > MaxEntriesPerProjection)
                        return false;

                    var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId(id), schemaVersion, required);
                    var entries = new GameplayProjectionEntry[entryCount];
                    for (int e = 0; e < entryCount; e++)
                        entries[e] = new GameplayProjectionEntry(reader.ReadString(), reader.ReadString());
                    projections[i] = new GameplayProjectionState(descriptor, entries);
                }

                if (stream.Position != stream.Length) return false;
                publication = new GameplayPublication(new GameplayRevision(revisionValue), publicationKind, projections);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is DecoderFallbackException || ex is ArgumentException || ex is OverflowException)
            {
                publication = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Game-owned publisher plugged into AuthoritativeServerSession's existing fixed-tick emitter
    /// seam. A new/reconnected authenticated connection causes one full snapshot for every client
    /// at the same global gameplay revision; ordinary ticks emit deltas.
    /// </summary>
    public sealed class GameplayStateServerEmitter : IAuthoritativeGameplayStateEmitter
    {
        private readonly GameplayPublicationBuilder _builder;
        private readonly List<ServerPlayerRegistry.PlayerSession> _players = new List<ServerPlayerRegistry.PlayerSession>(8);
        private readonly HashSet<uint> _knownConnections = new HashSet<uint>();

        public GameplayStateServerEmitter(IEnumerable<IGameplayProjectionSource> sources)
        {
            _builder = new GameplayPublicationBuilder(sources ?? throw new ArgumentNullException(nameof(sources)));
        }

        public GameplayRevision CurrentRevision => _builder.CurrentRevision;
        public int LastSendFailureCount { get; private set; }

        public void Emit(uint serverTick, ServerPlayerRegistry players, IGameplayStatePacketSink sink)
        {
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            players.CopySessions(_players);
            _players.Sort((left, right) => left.ConnectionId.CompareTo(right.ConnectionId));
            if (_players.Count == 0)
            {
                _knownConnections.Clear();
                LastSendFailureCount = 0;
                return;
            }

            bool requiresSnapshot = _builder.CurrentRevision.IsInitial;
            for (int i = 0; i < _players.Count; i++)
            {
                if (!_knownConnections.Contains(_players[i].ConnectionId))
                {
                    requiresSnapshot = true;
                    break;
                }
            }

            GameplayPublication publication = requiresSnapshot
                ? _builder.PublishSnapshot()
                : _builder.PublishDelta();
            if (!GameplayStatePacketCodec.TryEncode(publication, out byte[] packet))
                throw new InvalidOperationException("Gameplay publication exceeds the supported gameplay-state packet contract.");

            int failures = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                if (!sink.SendGameplayStatePacket(_players[i].ConnectionId, packet))
                    failures++;
            }
            LastSendFailureCount = failures;

            _knownConnections.Clear();
            for (int i = 0; i < _players.Count; i++)
                _knownConnections.Add(_players[i].ConnectionId);
        }
    }

    /// <summary>Client Net packet hook that decodes semantic publications into the gameplay read store.</summary>
    public sealed class GameplayStateClientPacketHandler : IGameplayStatePacketHandler
    {
        private readonly IGameplayPublicationSink _sink;

        public GameplayStateClientPacketHandler(IGameplayPublicationSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public GameplayApplyResult? LastApplyResult { get; private set; }
        public event Action<GameplayApplyResult> RepairRequired;

        public bool HandleGameplayStatePacket(ReadOnlySpan<byte> packet)
        {
            if (!GameplayStatePacketCodec.TryDecode(packet, out GameplayPublication publication))
                return false;

            GameplayApplyResult result = _sink.Apply(publication);
            LastApplyResult = result;
            if (result == GameplayApplyResult.GapDetected || result == GameplayApplyResult.IncompatibleProjection)
                RepairRequired?.Invoke(result);
            return true;
        }
    }
}
