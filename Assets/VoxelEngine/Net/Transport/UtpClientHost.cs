using System;
using Unity.Networking.Transport;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Consumer for server packets after the UTP host has identified the logical channel and
    /// copied the packet into bounded span memory. Returning false rejects the packet.
    /// </summary>
    public interface IUtpClientPacketHandler
    {
        bool HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet);
    }

    /// <summary>
    /// Concrete Unity Transport 6.5 client lifecycle.
    ///
    /// Owns the driver/connection, pumps transport independently of simulation ticks, and exposes
    /// framed protocol packets to the client world layer without making that layer depend on UTP.
    /// </summary>
    public sealed class UtpClientHost : IDisposable
    {
        private const int k_LocalWriteFailure = int.MinValue + 1;

        private NetworkDriver _driver;
        private ChannelSetup _channels;
        private NetworkConnection _connection;
        private bool _connected;
        private bool _disposed;

        public event Action Connected;
        public event Action Disconnected;
        public event Action PacketRejected;
        public event Action<int> SendError;

        public UtpClientHost()
        {
            _driver = NetworkDriver.Create(ChannelSetup.DefaultSettings());
            _channels = ChannelSetup.Create(ref _driver);
        }

        public bool IsCreated => !_disposed && _driver.IsCreated;
        public bool IsConnected => _connected && _connection.IsCreated;
        public NetworkEndpoint LocalEndpoint => IsCreated && _driver.Bound ? _driver.GetLocalEndpoint() : default;

        /// <summary>Begin an asynchronous UTP connection attempt.</summary>
        public bool Connect(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            if (_connection.IsCreated)
                return false;

            _connection = _driver.Connect(endpoint);
            _connected = false;
            return _connection.IsCreated;
        }

        /// <summary>
        /// Pump UTP once from the Unity frame loop. Connect/disconnect/data events are consumed here;
        /// decoded gameplay application remains behind IUtpClientPacketHandler.
        /// </summary>
        public void Pump(IUtpClientPacketHandler handler)
        {
            ThrowIfDisposed();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _driver.ScheduleUpdate().Complete();
            if (!_connection.IsCreated)
                return;

            // One reusable stack buffer per pump avoids byte[] allocations for EVENT/REPAIR/BULK.
            Span<byte> packetScratch = stackalloc byte[ChannelSetup.k_MaxBulkPacketBytes];

            NetworkEvent.Type eventType;
            while ((eventType = _connection.PopEvent(
                       _driver,
                       out var reader,
                       out var pipeline)) != NetworkEvent.Type.Empty)
            {
                switch (eventType)
                {
                    case NetworkEvent.Type.Connect:
                        _connected = true;
                        Connected?.Invoke();
                        break;

                    case NetworkEvent.Type.Data:
                    {
                        if (!TryResolveChannel(pipeline, out UtpChannel channel) ||
                            reader.Length > MaxPacketBytes(channel) ||
                            !UtpPacketIO.TryRead(ref reader, packetScratch, out int bytesRead) ||
                            !handler.HandlePacket(channel, packetScratch.Slice(0, bytesRead)))
                        {
                            PacketRejected?.Invoke();
                        }

                        break;
                    }

                    case NetworkEvent.Type.Disconnect:
                        _connected = false;
                        _connection = default;
                        Disconnected?.Invoke();
                        return;
                }
            }
        }

        /// <summary>Encode and queue a canonical 34-byte alteration request on EVENT.</summary>
        public bool TrySendAlterationRequest(in C_AlterationRequest request)
        {
            Span<byte> packet = stackalloc byte[AlterationRequestPacket.PacketSize];
            if (!AlterationRequestPacket.TryEncode(packet, in request))
                return false;

            return TrySend(UtpChannel.Event, packet);
        }

        public bool TrySend(UtpChannel channel, ReadOnlySpan<byte> packet)
        {
            ThrowIfDisposed();
            if (!_connection.IsCreated || packet.Length <= 0 || packet.Length > MaxPacketBytes(channel))
                return false;

            NetworkPipeline pipeline = PipelineFor(channel);
            int beginResult = _driver.BeginSend(pipeline, _connection, out var writer, packet.Length);
            if (beginResult < 0)
            {
                SendError?.Invoke(beginResult);
                return false;
            }

            if (!UtpPacketIO.TryWrite(ref writer, packet))
            {
                _driver.AbortSend(writer);
                SendError?.Invoke(k_LocalWriteFailure);
                return false;
            }

            int endResult = _driver.EndSend(writer);
            if (endResult < 0)
            {
                SendError?.Invoke(endResult);
                return false;
            }

            return true;
        }

        /// <summary>Flush all queued sends once after a command/input batch.</summary>
        public void FlushSends()
        {
            ThrowIfDisposed();
            _driver.ScheduleFlushSend().Complete();
        }

        public void Disconnect()
        {
            ThrowIfDisposed();
            if (!_connection.IsCreated)
                return;

            _driver.Disconnect(_connection);
            // UTP requires an update after Disconnect so the peer is notified immediately.
            _driver.ScheduleUpdate().Complete();
            _connection = default;
            _connected = false;
            Disconnected?.Invoke();
        }

        private bool TryResolveChannel(NetworkPipeline pipeline, out UtpChannel channel)
        {
            if (pipeline.Equals(_channels.Event))
            {
                channel = UtpChannel.Event;
                return true;
            }

            if (pipeline.Equals(_channels.Repair))
            {
                channel = UtpChannel.Repair;
                return true;
            }

            if (pipeline.Equals(_channels.Bulk))
            {
                channel = UtpChannel.Bulk;
                return true;
            }

            channel = default;
            return false;
        }

        private NetworkPipeline PipelineFor(UtpChannel channel)
        {
            return channel switch
            {
                UtpChannel.Event => _channels.Event,
                UtpChannel.Repair => _channels.Repair,
                UtpChannel.Bulk => _channels.Bulk,
                _ => throw new ArgumentOutOfRangeException(nameof(channel)),
            };
        }

        private static int MaxPacketBytes(UtpChannel channel)
        {
            return channel switch
            {
                UtpChannel.Event => ChannelSetup.k_MaxEventPacketBytes,
                UtpChannel.Repair => ChannelSetup.k_MaxRepairPacketBytes,
                UtpChannel.Bulk => ChannelSetup.k_MaxBulkPacketBytes,
                _ => 0,
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UtpClientHost));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_driver.IsCreated)
            {
                if (_connection.IsCreated)
                {
                    _driver.Disconnect(_connection);
                    _driver.ScheduleUpdate().Complete();
                }

                _driver.Dispose();
            }

            _connection = default;
            _connected = false;
            _disposed = true;
        }
    }
}
