using System;
using System.Collections.Generic;
using Unity.Networking.Transport;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Net.Transport
{
    /// <summary>Concrete Unity Transport 6.5 server lifecycle.</summary>
    public sealed class UtpServerHost : IDisposable, IEventPacketSender
    {
        private const int k_LocalWriteFailure = int.MinValue + 1;

        private readonly Dictionary<uint, NetworkConnection> _connections;
        private readonly List<uint> _disconnectScratch;
        private readonly ClientEphemeralPacketReceiver _ephemeralReceiver;
        private readonly int _maxConnections;

        private NetworkDriver _driver;
        private ChannelSetup _channels;
        private uint _nextConnectionId;
        private bool _disposed;

        public event Action<uint, NetworkEndpoint> ConnectionOpened;
        public event Action<uint> ConnectionClosed;
        public event Action<uint> ProtocolError;
        public event Action<uint, int> SendError;

        public UtpServerHost(int maxConnections = 64)
        {
            if (maxConnections <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConnections));

            _maxConnections = maxConnections;
            _connections = new Dictionary<uint, NetworkConnection>(maxConnections);
            _disconnectScratch = new List<uint>(maxConnections);
            _ephemeralReceiver = new ClientEphemeralPacketReceiver();
            _nextConnectionId = 1;

            _driver = NetworkDriver.Create(ChannelSetup.DefaultSettings());
            _channels = ChannelSetup.Create(ref _driver);
        }

        public bool IsCreated => !_disposed && _driver.IsCreated;
        public bool IsListening => IsCreated && _driver.Listening;
        public int ConnectionCount => _connections.Count;
        public NetworkEndpoint LocalEndpoint => IsCreated && _driver.Bound ? _driver.GetLocalEndpoint() : default;

        public int Listen(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            if (_driver.Listening)
                return 0;

            if (!_driver.Bound)
            {
                int bindResult = _driver.Bind(endpoint);
                if (bindResult != 0)
                    return bindResult;
            }

            return _driver.Listen();
        }

        public void Pump(
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler = null,
            IClientConvergenceCommandHandler convergenceHandler = null)
        {
            ThrowIfDisposed();
            if (eventHandler == null)
                throw new ArgumentNullException(nameof(eventHandler));

            _driver.ScheduleUpdate().Complete();
            AcceptPendingConnections();

            _disconnectScratch.Clear();
            foreach (var pair in _connections)
                PumpConnection(pair.Key, pair.Value, eventHandler, inputHandler, convergenceHandler);

            for (int i = 0; i < _disconnectScratch.Count; i++)
                RemoveConnection(_disconnectScratch[i]);
        }

        public void FlushSends()
        {
            ThrowIfDisposed();
            _driver.ScheduleFlushSend().Complete();
        }

        public bool ContainsConnection(uint connectionId) => _connections.ContainsKey(connectionId);

        public bool Disconnect(uint connectionId)
        {
            ThrowIfDisposed();
            if (!_connections.TryGetValue(connectionId, out NetworkConnection connection))
                return false;

            int result = _driver.Disconnect(connection);
            if (result < 0)
                return false;

            RemoveConnection(connectionId);
            return true;
        }

        public bool TrySend(uint connectionId, UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            ThrowIfDisposed();
            if (!_connections.TryGetValue(connectionId, out NetworkConnection connection) || !connection.IsCreated)
                return false;
            if (packet.Length <= 0 || packet.Length > MaxPacketBytes(channel))
                return false;

            NetworkPipeline pipeline = PipelineFor(channel);
            int beginResult = _driver.BeginSend(pipeline, connection, out var writer, packet.Length);
            if (beginResult < 0)
            {
                SendError?.Invoke(connectionId, beginResult);
                return false;
            }

            if (!UtpPacketIO.TryWrite(ref writer, packet))
            {
                _driver.AbortSend(writer);
                SendError?.Invoke(connectionId, k_LocalWriteFailure);
                return false;
            }

            int endResult = _driver.EndSend(writer);
            if (endResult < 0)
            {
                SendError?.Invoke(connectionId, endResult);
                return false;
            }

            return true;
        }

        void IEventPacketSender.SendEventPacket(uint connectionId, ReadOnlySpan<byte> packet) =>
            TrySend(connectionId, UtpChannel.Event, packet);

        private void AcceptPendingConnections()
        {
            NetworkConnection connection;
            while ((connection = _driver.Accept()) != default)
            {
                if (_connections.Count >= _maxConnections)
                {
                    _driver.Disconnect(connection);
                    continue;
                }

                uint connectionId = AllocateConnectionId();
                _connections.Add(connectionId, connection);
                ConnectionOpened?.Invoke(connectionId, _driver.GetRemoteEndpoint(connection));
            }
        }

        private void PumpConnection(
            uint connectionId,
            NetworkConnection connection,
            IClientEventCommandHandler eventHandler,
            IClientInputCommandHandler inputHandler,
            IClientConvergenceCommandHandler convergenceHandler)
        {
            Span<byte> packetScratch = stackalloc byte[ChannelSetup.k_MaxEventPacketBytes];

            NetworkEvent.Type eventType;
            while ((eventType = _driver.PopEventForConnection(
                       connection,
                       out var reader,
                       out var pipeline)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Data:
                    {
                        bool accepted = false;

                        if (pipeline.Equals(_channels.Event))
                        {
                            accepted = UtpPacketIO.TryRead(ref reader, packetScratch, out int bytesRead) &&
                                       ClientEventPacketReceiver.TryDispatch(
                                           connectionId,
                                           packetScratch.Slice(0, bytesRead),
                                           eventHandler,
                                           convergenceHandler);
                        }
                        else if (pipeline.Equals(_channels.Ephemeral) && inputHandler != null)
                        {
                            accepted = UtpPacketIO.TryRead(ref reader, packetScratch, out int bytesRead) &&
                                       _ephemeralReceiver.TryDispatch(
                                           connectionId,
                                           packetScratch.Slice(0, bytesRead),
                                           inputHandler);
                        }

                        if (!accepted)
                            ProtocolError?.Invoke(connectionId);
                        break;
                    }

                    case NetworkEvent.Type.Disconnect:
                        _disconnectScratch.Add(connectionId);
                        return;
                }
            }
        }

        private uint AllocateConnectionId()
        {
            while (true)
            {
                uint candidate = _nextConnectionId++;
                if (_nextConnectionId == 0)
                    _nextConnectionId = 1;
                if (candidate != 0 && !_connections.ContainsKey(candidate))
                    return candidate;
            }
        }

        private void RemoveConnection(uint connectionId)
        {
            if (!_connections.Remove(connectionId))
                return;

            _ephemeralReceiver.RemoveConnection(connectionId);
            ConnectionClosed?.Invoke(connectionId);
        }

        private NetworkPipeline PipelineFor(UtpChannel channel) => channel switch
        {
            UtpChannel.Event => _channels.Event,
            UtpChannel.Ephemeral => _channels.Ephemeral,
            UtpChannel.Repair => _channels.Repair,
            UtpChannel.Bulk => _channels.Bulk,
            _ => throw new ArgumentOutOfRangeException(nameof(channel)),
        };

        private static int MaxPacketBytes(UtpChannel channel) => channel switch
        {
            UtpChannel.Event => ChannelSetup.k_MaxEventPacketBytes,
            UtpChannel.Ephemeral => ChannelSetup.k_MaxEphemeralPacketBytes,
            UtpChannel.Repair => ChannelSetup.k_MaxRepairPacketBytes,
            UtpChannel.Bulk => ChannelSetup.k_MaxBulkPacketBytes,
            _ => 0,
        };

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UtpServerHost));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_driver.IsCreated)
            {
                if (_connections.Count > 0)
                {
                    foreach (NetworkConnection connection in _connections.Values)
                        _driver.Disconnect(connection);
                    _driver.ScheduleUpdate().Complete();
                }
                _driver.Dispose();
            }

            _connections.Clear();
            _disconnectScratch.Clear();
            _disposed = true;
        }
    }
}
