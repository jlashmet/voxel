using System;
using Unity.Networking.Transport;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Net.Transport
{
    public interface IUtpClientPacketHandler
    {
        bool HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet);
    }

    /// <summary>
    /// Concrete Unity Transport 6.5 client lifecycle. Transport is pumped independently from the
    /// simulation clock; protocol/gameplay application remains behind IUtpClientPacketHandler.
    /// </summary>
    public sealed class UtpClientHost : IDisposable
    {
        private const int k_LocalWriteFailure = int.MinValue + 1;

        private NetworkDriver _driver;
        private ChannelSetup _channels;
        private NetworkConnection _connection;
        private bool _connected;
        private bool _disposed;

        // Last three input samples for loss-tolerant redundancy. Stored as fields to avoid an
        // allocation/ring-buffer object in the high-frequency client input path.
        private C_PlayerInput _inputNewest;
        private C_PlayerInput _inputPrevious1;
        private C_PlayerInput _inputPrevious2;
        private int _inputHistoryCount;

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

        public bool Connect(NetworkEndpoint endpoint)
        {
            ThrowIfDisposed();
            if (_connection.IsCreated)
                return false;

            ResetInputHistory();
            _connection = _driver.Connect(endpoint);
            _connected = false;
            return _connection.IsCreated;
        }

        public void Pump(IUtpClientPacketHandler handler)
        {
            ThrowIfDisposed();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _driver.ScheduleUpdate().Complete();
            if (!_connection.IsCreated)
                return;

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
                        ResetInputHistory();
                        Disconnected?.Invoke();
                        return;
                }
            }
        }

        public bool TrySendAlterationRequest(in C_AlterationRequest request)
        {
            Span<byte> packet = stackalloc byte[AlterationRequestPacket.PacketSize];
            if (!AlterationRequestPacket.TryEncode(packet, in request))
                return false;

            return TrySend(UtpChannel.Event, packet);
        }

        /// <summary>
        /// Queue current input on EPHEMERAL together with up to two previous samples. At steady
        /// state this is a 51-byte packet carrying three 16-byte command frames plus framing.
        /// </summary>
        public bool TrySendPlayerInput(in C_PlayerInput input)
        {
            _inputPrevious2 = _inputPrevious1;
            _inputPrevious1 = _inputNewest;
            _inputNewest = input;
            if (_inputHistoryCount < PlayerInputBundlePacket.MaxSamples)
                _inputHistoryCount++;

            Span<C_PlayerInput> history = stackalloc C_PlayerInput[PlayerInputBundlePacket.MaxSamples];
            switch (_inputHistoryCount)
            {
                case 1:
                    history[0] = _inputNewest;
                    break;
                case 2:
                    history[0] = _inputPrevious1;
                    history[1] = _inputNewest;
                    break;
                default:
                    history[0] = _inputPrevious2;
                    history[1] = _inputPrevious1;
                    history[2] = _inputNewest;
                    break;
            }

            Span<byte> packet = stackalloc byte[PlayerInputBundlePacket.MaxPacketSize];
            if (!PlayerInputBundlePacket.TryEncode(
                    packet,
                    history.Slice(0, _inputHistoryCount),
                    out int bytesWritten))
            {
                return false;
            }

            return TrySend(UtpChannel.Ephemeral, packet.Slice(0, bytesWritten));
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
            _driver.ScheduleUpdate().Complete();
            _connection = default;
            _connected = false;
            ResetInputHistory();
            Disconnected?.Invoke();
        }

        private bool TryResolveChannel(NetworkPipeline pipeline, out UtpChannel channel)
        {
            if (pipeline.Equals(_channels.Event))
            {
                channel = UtpChannel.Event;
                return true;
            }

            if (pipeline.Equals(_channels.Ephemeral))
            {
                channel = UtpChannel.Ephemeral;
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
                UtpChannel.Ephemeral => _channels.Ephemeral,
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
                UtpChannel.Ephemeral => ChannelSetup.k_MaxEphemeralPacketBytes,
                UtpChannel.Repair => ChannelSetup.k_MaxRepairPacketBytes,
                UtpChannel.Bulk => ChannelSetup.k_MaxBulkPacketBytes,
                _ => 0,
            };
        }

        private void ResetInputHistory()
        {
            _inputNewest = default;
            _inputPrevious1 = default;
            _inputPrevious2 = default;
            _inputHistoryCount = 0;
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
            ResetInputHistory();
            _disposed = true;
        }
    }
}
