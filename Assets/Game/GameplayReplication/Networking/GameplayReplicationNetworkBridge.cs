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

namespace Game.GameplayReplication.Networking
{
    internal static class GameplayStatePacketCodec
    {
        private const int MaxProjectionCount = 64;
        private const int MaxEntriesPerProjection = 4096;
        private const int MaxStringBytes = 4096;

        public static bool TryEncode(GameplayPublication publication, out byte[] packet)
        {
            packet = null;
            if (publication == null)
                return false;

            try
            {
                using var stream = new MemoryStream(Math.Min(ChannelSetup.k_MaxBulkPacketBytes, 4096));
                stream.WriteByte(0);
                stream.WriteByte(0);
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(publication.Revision.Value);
                    writer.Write((byte)publication.Kind);
                    writer.Write(publication.Projections.Count);
                    for (int p = 0; p < publication.Projections.Count; p++)
                    {
                        GameplayProjectionState projection = publication.Projections[p];
                        WriteString(writer, projection.Descriptor.Id.Value);
                        writer.Write(projection.Descriptor.SchemaVersion);
                        writer.Write(projection.Descriptor.RequiredForGameplayReady);
                        writer.Write(projection.Entries.Count);
                        for (int e = 0; e < projection.Entries.Count; e++)
                        {
                            GameplayProjectionEntry entry = projection.Entries[e];
                            WriteString(writer, entry.Key);
                            WriteString(writer, entry.Value);
                        }
                    }
                }

                if (stream.Length > ChannelSetup.k_MaxBulkPacketBytes)
                    return false;

                packet = stream.ToArray();
                return ProtocolEnvelope.TryWriteHeader(packet, ProtocolMessageKind.S_GameplayState);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is OverflowException)
            {
                packet = null;
                return false;
            }
        }

        public static bool TryDecode(ReadOnlySpan<byte> packet, out GameplayPublication publication)
        {
            publication = null;
            if (packet.Length < ProtocolEnvelope.HeaderSize || packet.Length > ChannelSetup.k_MaxBulkPacketBytes ||
                !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset) ||
                kind != ProtocolMessageKind.S_GameplayState)
                return false;

            try
            {
                byte[] copy = packet.ToArray();
                using var stream = new MemoryStream(copy, payloadOffset, copy.Length - payloadOffset, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                long revisionValue = reader.ReadInt64();
                byte kindValue = reader.ReadByte();
                if (revisionValue <= 0 || kindValue > (byte)GameplayPublicationKind.Delta)
                    return false;

                int projectionCount = reader.ReadInt32();
                if (projectionCount < 0 || projectionCount > MaxProjectionCount)
                    return false;

                var projections = new GameplayProjectionState[projectionCount];
                for (int p = 0; p < projectionCount; p++)
                {
                    string id = ReadString(reader);
                    int schemaVersion = reader.ReadInt32();
                    bool required = reader.ReadBoolean();
                    int entryCount = reader.ReadInt32();
                    if (schemaVersion <= 0 || entryCount < 0 || entryCount > MaxEntriesPerProjection)
                        return false;

                    var entries = new GameplayProjectionEntry[entryCount];
                    for (int e = 0; e < entryCount; e++)
                        entries[e] = new GameplayProjectionEntry(ReadString(reader), ReadString(reader));

                    var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId(id), schemaVersion, required);
                    projections[p] = new GameplayProjectionState(descriptor, entries);
                }

                if (stream.Position != stream.Length)
                    return false;

                publication = new GameplayPublication(
                    new GameplayRevision(revisionValue),
                    (GameplayPublicationKind)kindValue,
                    projections);
                return true;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is ArgumentException || ex is OverflowException)
            {
                publication = null;
                return false;
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaxStringBytes)
                throw new ArgumentException("Gameplay replication string exceeds protocol bound.", nameof(value));
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            if (length > MaxStringBytes)
                throw new IOException("Gameplay replication string exceeds protocol bound.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public sealed class GameplayReplicationServerEmitter : IAuthoritativeGameplayStateEmitter
    {
        public const uint DefaultSnapshotIntervalTicks = 30;

        private readonly GameplayPublicationBuilder _builder;
        private readonly uint _snapshotIntervalTicks;
        private readonly List<ServerPlayerRegistry.PlayerSession> _sessions = new List<ServerPlayerRegistry.PlayerSession>(64);
        private readonly HashSet<uint> _knownConnections = new HashSet<uint>();
        private readonly HashSet<uint> _currentConnections = new HashSet<uint>();

        public GameplayReplicationServerEmitter(
            GameplayPublicationBuilder builder,
            uint snapshotIntervalTicks = DefaultSnapshotIntervalTicks)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            if (snapshotIntervalTicks == 0) throw new ArgumentOutOfRangeException(nameof(snapshotIntervalTicks));
            _snapshotIntervalTicks = snapshotIntervalTicks;
        }

        public GameplayRevision CurrentRevision => _builder.CurrentRevision;
        public long PublicationsSent { get; private set; }
        public long SendFailures { get; private set; }

        public void Emit(uint serverTick, ServerPlayerRegistry players, IGameplayStatePacketSink sink)
        {
            if (serverTick == 0) throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            players.CopySessions(_sessions);
            if (_sessions.Count == 0)
            {
                _knownConnections.Clear();
                return;
            }

            _sessions.Sort((a, b) => a.ConnectionId.CompareTo(b.ConnectionId));
            _currentConnections.Clear();
            bool hasNewConnection = false;
            for (int i = 0; i < _sessions.Count; i++)
            {
                uint connectionId = _sessions[i].ConnectionId;
                _currentConnections.Add(connectionId);
                if (!_knownConnections.Contains(connectionId))
                    hasNewConnection = true;
            }

            bool snapshot = _builder.CurrentRevision.IsInitial || hasNewConnection || serverTick % _snapshotIntervalTicks == 0;
            GameplayPublication publication = snapshot ? _builder.PublishSnapshot() : _builder.PublishDelta();
            if (!GameplayStatePacketCodec.TryEncode(publication, out byte[] packet))
            {
                SendFailures += _sessions.Count;
                ReplaceKnownConnections();
                return;
            }

            for (int i = 0; i < _sessions.Count; i++)
            {
                if (sink.SendGameplayStatePacket(_sessions[i].ConnectionId, packet))
                    PublicationsSent++;
                else
                    SendFailures++;
            }

            ReplaceKnownConnections();
        }

        private void ReplaceKnownConnections()
        {
            _knownConnections.Clear();
            foreach (uint connectionId in _currentConnections)
                _knownConnections.Add(connectionId);
        }
    }

    public sealed class GameplayReplicationClientHandler : IGameplayStatePacketHandler
    {
        private readonly IGameplayPublicationSink _sink;

        public GameplayReplicationClientHandler(IGameplayPublicationSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public GameplayApplyResult LastApplyResult { get; private set; } = GameplayApplyResult.DuplicateOrStale;
        public long PacketsAccepted { get; private set; }
        public long PacketsRejected { get; private set; }

        public bool HandleGameplayStatePacket(ReadOnlySpan<byte> packet)
        {
            if (!GameplayStatePacketCodec.TryDecode(packet, out GameplayPublication publication))
            {
                PacketsRejected++;
                return false;
            }

            LastApplyResult = _sink.Apply(publication);
            PacketsAccepted++;
            return true;
        }
    }
}
