using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Concentus.Enums;
using Concentus.Structs;
using ZaloLite;

namespace ZaloWP81.Core
{
    /// <summary>
    /// Zalo UDP Media Relay Transport and Real-Time Audio Engine.
    /// Handles UDP communication with Zalo Media Relay Servers,
    /// Opus encoding/decoding via Concentus, RTP packetization (RFC 3550),
    /// real-time microphone capture via MediaCapture, and low-latency audio playback via WinRT MediaStreamSource.
    /// </summary>
    public sealed class ZaloUdpRelayEngine
    {
        private static readonly ZaloUdpRelayEngine _current = new ZaloUdpRelayEngine();
        public static ZaloUdpRelayEngine Current => _current;

        private DatagramSocket _socket;
        private DataWriter _writer;
        private ZaloCallSession _session;
        private OpusEncoder _encoder;
        private OpusDecoder _decoder;

        private MediaCapture _audioCapture;
        private InMemoryRandomAccessStream _captureStream;
        private ulong _captureReadPosition;

        private ushort _sequenceNumber;
        private uint _rtpTimestamp;
        private TimeSpan _currentPlaybackTime = TimeSpan.Zero;

        private readonly ConcurrentQueue<short[]> _playbackQueue = new ConcurrentQueue<short[]>();
        private readonly byte[] _silenceBufferBytes = new byte[960 * 2]; // 20ms silence @ 48kHz mono 16-bit
        private IBuffer _silenceBuffer;

        private Timer _keepAliveTimer;
        private CancellationTokenSource _captureCts;
        private MediaStreamSource _mediaStreamSource;

        private readonly object _lock = new object();
        private bool _isRunning;
        private bool _isStreaming;

        public bool IsRunning => _isRunning;
        public bool IsStreaming => _isStreaming;
        public long PacketsReceived { get; private set; }
        public long PacketsSent { get; private set; }
        public string LastError { get; private set; }

        public event Action<long, long> StatsUpdated;

        private ZaloUdpRelayEngine()
        {
            _silenceBuffer = CryptographicBuffer.CreateFromByteArray(_silenceBufferBytes);
        }

        public async Task StartSessionAsync(ZaloCallSession session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.RelayHost) || session.RelayPort <= 0)
            {
                ZaloLog.Add("ZRTC Relay: Missing host or port in session config.");
                return;
            }

            StopSession();

            lock (_lock)
            {
                _session = session;
                _isRunning = true;
                _sequenceNumber = (ushort)new Random().Next(1, 10000);
                _rtpTimestamp = (uint)new Random().Next(100000, 500000);
                _currentPlaybackTime = TimeSpan.Zero;
                PacketsReceived = 0;
                PacketsSent = 0;
            }

            try
            {
                ZaloLog.Add(string.Format("ZRTC UDP Relay: Connecting to {0}:{1}...", session.RelayHost, session.RelayPort));

                // 1. Initialize Concentus Opus Codec (48kHz mono VoIP)
                try
                {
                    _encoder = OpusEncoder.Create(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
                    _encoder.Bitrate = 32000; // 32 kbps voice
                    _decoder = OpusDecoder.Create(48000, 1);
                    ZaloLog.Add("ZRTC Opus encoder & decoder initialized successfully.");
                }
                catch (Exception ex)
                {
                    ZaloLog.Add("ZRTC Opus init error: " + ex.Message);
                }

                // 2. Setup UDP DatagramSocket
                _socket = new DatagramSocket();
                _socket.MessageReceived += OnDatagramReceived;

                var host = new HostName(session.RelayHost);
                await _socket.ConnectAsync(host, session.RelayPort.ToString());
                _writer = new DataWriter(_socket.OutputStream);

                ZaloLog.Add(string.Format("ZRTC UDP Relay: Connected to {0}:{1}", session.RelayHost, session.RelayPort));

                // 3. Send initial relay authentication packet & keepalive
                await SendRelayAuthPingAsync();
                StartKeepAliveTimer();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                ZaloLog.Add("ZRTC UDP Relay Connect failed: " + ex.Message);
            }
        }

        public async void StartStreaming()
        {
            if (!_isRunning || _session == null) return;
            if (_isStreaming) return;

            _isStreaming = true;
            ZaloLog.Add("ZRTC UDP Relay: Audio streaming started.");

            // Start hardware microphone capture
            await InitAudioCaptureAsync();

            // Start 20ms audio transmission loop
            _captureCts = new CancellationTokenSource();
            _ = Task.Run(() => CaptureAndTransmitLoop(_captureCts.Token));
        }

        private async Task InitAudioCaptureAsync()
        {
            try
            {
                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Audio
                };
                _audioCapture = new MediaCapture();
                await _audioCapture.InitializeAsync(settings);

                _captureStream = new InMemoryRandomAccessStream();
                _captureReadPosition = 44; // Skip 44-byte WAV header

                var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Auto);
                await _audioCapture.StartRecordToStreamAsync(profile, _captureStream);
                ZaloLog.Add("ZRTC Real-time microphone capture started via MediaCapture WAV stream.");
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ZRTC Microphone capture init warning: " + ex.Message);
                _audioCapture = null;
                _captureStream = null;
            }
        }

        private async Task SendRelayAuthPingAsync()
        {
            if (_writer == null || _session == null) return;
            try
            {
                uint fromUid = 0;
                uint partnerUid = 0;
                uint.TryParse(ZaloApp.Session?.Uid ?? "0", out fromUid);
                uint.TryParse(_session.PeerId ?? "0", out partnerUid);
                uint callId = (uint)_session.CallId;
                string sessId = _session.SessionId ?? string.Empty;
                byte[] sessBytes = Encoding.UTF8.GetBytes(sessId);

                // 1. Official Zalo ZRTP Init Packet (cmd 0x000B if caller, 0x000C if callee, sub_cmd 2)
                ushort cmd = (ushort)(_session.IsOutgoing ? 0x000B : 0x000C);
                byte subCmd = 2;

                byte[] zrtpInit = new byte[21 + 10 + sessBytes.Length];
                zrtpInit[0] = 0x01; // magic
                zrtpInit[1] = 0x7E; // payload_type InitZRTP
                Array.Copy(BitConverter.GetBytes(fromUid), 0, zrtpInit, 10, 4);
                Array.Copy(BitConverter.GetBytes(cmd), 0, zrtpInit, 18, 2);
                zrtpInit[20] = subCmd;
                Array.Copy(BitConverter.GetBytes(callId), 0, zrtpInit, 21, 4);
                Array.Copy(BitConverter.GetBytes(partnerUid), 0, zrtpInit, 25, 4);
                Array.Copy(BitConverter.GetBytes((ushort)sessBytes.Length), 0, zrtpInit, 29, 2);
                if (sessBytes.Length > 0)
                {
                    Array.Copy(sessBytes, 0, zrtpInit, 31, sessBytes.Length);
                }

                _writer.WriteBytes(zrtpInit);

                // 2. ZRTP Echo Packet (payload_type 0x01, cmd 0x0005)
                byte[] echoPkt = new byte[21 + 2 + 10];
                echoPkt[0] = 0x01;
                echoPkt[1] = 0x01;
                Array.Copy(BitConverter.GetBytes(fromUid), 0, echoPkt, 10, 4);
                Array.Copy(BitConverter.GetBytes((ushort)0x0005), 0, echoPkt, 18, 2);
                echoPkt[20] = 0x00;
                string tsNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                byte[] tsBytes = Encoding.UTF8.GetBytes(tsNow);
                Array.Copy(BitConverter.GetBytes((ushort)Math.Min(10, tsBytes.Length)), 0, echoPkt, 21, 2);
                Array.Copy(tsBytes, 0, echoPkt, 23, Math.Min(10, tsBytes.Length));
                _writer.WriteBytes(echoPkt);

                // 3. Format ZRTC Relay Auth Frame if token exists
                string token = _session.RelayToken ?? string.Empty;
                if (!string.IsNullOrEmpty(token))
                {
                    byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                    byte[] authPacket = new byte[2 + 8 + 4 + 2 + tokenBytes.Length];
                    authPacket[0] = 0x00;
                    authPacket[1] = 0x01;
                    long cid = _session.CallId;
                    for (int i = 0; i < 8; i++) authPacket[2 + i] = (byte)((cid >> ((7 - i) * 8)) & 0xFF);
                    uint ssrc = _session.LocalSsrc;
                    authPacket[10] = (byte)((ssrc >> 24) & 0xFF);
                    authPacket[11] = (byte)((ssrc >> 16) & 0xFF);
                    authPacket[12] = (byte)((ssrc >> 8) & 0xFF);
                    authPacket[13] = (byte)(ssrc & 0xFF);
                    authPacket[14] = (byte)((tokenBytes.Length >> 8) & 0xFF);
                    authPacket[15] = (byte)(tokenBytes.Length & 0xFF);
                    Array.Copy(tokenBytes, 0, authPacket, 16, tokenBytes.Length);
                    _writer.WriteBytes(authPacket);
                }

                await _writer.StoreAsync();

                // Send initial RTP dummy packet to establish bidirectional relay mapping
                await SendRtpPacketAsync(new byte[0]);
                ZaloLog.Add("ZRTC UDP Relay ZRTP Init & Echo ping sent.");
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ZRTC SendRelayAuthPing error: " + ex.Message);
            }
        }

        private void StartKeepAliveTimer()
        {
            StopKeepAliveTimer();
            _keepAliveTimer = new Timer(async state =>
            {
                if (!_isRunning || _writer == null) return;
                try
                {
                    await SendRelayAuthPingAsync();
                }
                catch { }
            }, null, 2000, 2000);
        }

        private void StopKeepAliveTimer()
        {
            if (_keepAliveTimer != null)
            {
                _keepAliveTimer.Dispose();
                _keepAliveTimer = null;
            }
        }

        private void OnDatagramReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (var reader = args.GetDataReader())
                {
                    uint len = reader.UnconsumedBufferLength;
                    if (len == 0) return;

                    byte[] packet = new byte[len];
                    reader.ReadBytes(packet);

                    PacketsReceived++;

                    // Verify RTP header (RFC 3550): Version must be 2 (0x80)
                    if (packet.Length >= 12 && (packet[0] & 0xC0) == 0x80)
                    {
                        int csrcCount = packet[0] & 0x0F;
                        bool hasExtension = (packet[0] & 0x10) != 0;
                        int headerLen = 12 + (csrcCount * 4);

                        if (hasExtension && packet.Length >= headerLen + 4)
                        {
                            int extLenWords = (packet[headerLen + 2] << 8) | packet[headerLen + 3];
                            headerLen += 4 + (extLenWords * 4);
                        }

                        int payloadOffset = headerLen;
                        int payloadLength = packet.Length - payloadOffset;

                        if (payloadLength > 0 && _decoder != null)
                        {
                            short[] pcm = new short[960]; // 20ms @ 48kHz
                            int decodedSamples = _decoder.Decode(packet, payloadOffset, payloadLength, pcm, 0, 960, false);
                            if (decodedSamples > 0)
                            {
                                // Cap queue at 8 frames (160ms) to prevent latency buffer bloat
                                if (_playbackQueue.Count > 8)
                                {
                                    short[] discarded;
                                    _playbackQueue.TryDequeue(out discarded);
                                }
                                _playbackQueue.Enqueue(pcm);
                            }
                        }
                    }

                    if (PacketsReceived % 100 == 0)
                    {
                        var handler = StatsUpdated;
                        if (handler != null) handler(PacketsReceived, PacketsSent);
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("OnDatagramReceived error: " + ex.Message);
            }
        }

        private async Task SendRtpPacketAsync(byte[] opusPayload)
        {
            if (_writer == null || _session == null) return;

            try
            {
                int payloadLen = opusPayload != null ? opusPayload.Length : 0;
                byte[] rtp = new byte[12 + payloadLen];

                // RFC 3550 RTP Header
                rtp[0] = 0x80; // V=2, P=0, X=0, CC=0
                rtp[1] = 97;   // PT=97 (Opus)

                // Sequence Number
                rtp[2] = (byte)((_sequenceNumber >> 8) & 0xFF);
                rtp[3] = (byte)(_sequenceNumber & 0xFF);
                _sequenceNumber++;

                // Timestamp (48kHz, +960 per 20ms frame)
                rtp[4] = (byte)((_rtpTimestamp >> 24) & 0xFF);
                rtp[5] = (byte)((_rtpTimestamp >> 16) & 0xFF);
                rtp[6] = (byte)((_rtpTimestamp >> 8) & 0xFF);
                rtp[7] = (byte)(_rtpTimestamp & 0xFF);
                _rtpTimestamp += 960;

                // SSRC
                uint ssrc = _session.LocalSsrc;
                rtp[8] = (byte)((ssrc >> 24) & 0xFF);
                rtp[9] = (byte)((ssrc >> 16) & 0xFF);
                rtp[10] = (byte)((ssrc >> 8) & 0xFF);
                rtp[11] = (byte)(ssrc & 0xFF);

                if (payloadLen > 0)
                {
                    Array.Copy(opusPayload, 0, rtp, 12, payloadLen);
                }

                _writer.WriteBytes(rtp);
                await _writer.StoreAsync();
                PacketsSent++;
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendRtpPacket error: " + ex.Message);
            }
        }

        private async void CaptureAndTransmitLoop(CancellationToken token)
        {
            byte[] encodedOpus = new byte[1275];
            short[] micPcm = new short[960]; // 20ms PCM frame

            while (!token.IsCancellationRequested && _isStreaming)
            {
                try
                {
                    if (_session != null && !_session.IsMuted && _encoder != null)
                    {
                        // Check if live audio bytes are available in capture stream
                        if (_captureStream != null && _captureStream.Size > _captureReadPosition + 1920)
                        {
                            try
                            {
                                using (var reader = new DataReader(_captureStream.GetInputStreamAt(_captureReadPosition)))
                                {
                                    await reader.LoadAsync(1920);
                                    byte[] raw = new byte[1920];
                                    reader.ReadBytes(raw);
                                    _captureReadPosition += 1920;

                                    for (int i = 0; i < 960; i++)
                                    {
                                        micPcm[i] = (short)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                                    }
                                }
                            }
                            catch
                            {
                                Array.Clear(micPcm, 0, 960);
                            }
                        }

                        // Encode mic frame into Opus
                        int opusLen = _encoder.Encode(micPcm, 0, 960, encodedOpus, 0, encodedOpus.Length);
                        if (opusLen > 0)
                        {
                            byte[] payload = new byte[opusLen];
                            Array.Copy(encodedOpus, 0, payload, 0, opusLen);
                            await SendRtpPacketAsync(payload);
                        }
                    }

                    await Task.Delay(20, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ZaloLog.Add("CaptureAndTransmitLoop error: " + ex.Message);
                    await Task.Delay(50);
                }
            }
        }

        public MediaStreamSource CreateMediaStreamSource()
        {
            if (_mediaStreamSource != null) return _mediaStreamSource;

            var audioProps = AudioEncodingProperties.CreatePcm(48000, 1, 16);
            var descriptor = new AudioStreamDescriptor(audioProps);

            _mediaStreamSource = new MediaStreamSource(descriptor);
            _mediaStreamSource.BufferTime = TimeSpan.FromMilliseconds(50);

            _mediaStreamSource.Starting += (s, e) =>
            {
                e.Request.SetActualStartPosition(TimeSpan.Zero);
            };

            _mediaStreamSource.SampleRequested += (s, e) =>
            {
                if (!_isStreaming) return;

                short[] pcm;
                IBuffer buf;

                if (_playbackQueue.TryDequeue(out pcm) && pcm != null && pcm.Length > 0)
                {
                    byte[] bytes = new byte[pcm.Length * 2];
                    System.Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
                    buf = CryptographicBuffer.CreateFromByteArray(bytes);
                }
                else
                {
                    buf = _silenceBuffer;
                }

                var sample = MediaStreamSample.CreateFromBuffer(buf, _currentPlaybackTime);
                sample.Duration = TimeSpan.FromMilliseconds(20);
                _currentPlaybackTime += TimeSpan.FromMilliseconds(20);

                e.Request.Sample = sample;
            };

            return _mediaStreamSource;
        }

        public void SetSpeakerphone(bool enableSpeaker)
        {
            try
            {
                var routing = Windows.Phone.Media.Devices.AudioRoutingManager.GetDefault();
                if (routing != null)
                {
                    if (enableSpeaker)
                    {
                        routing.SetAudioEndpoint(Windows.Phone.Media.Devices.AudioRoutingEndpoint.Speakerphone);
                    }
                    else
                    {
                        if (routing.AvailableAudioEndpoints.HasFlag(Windows.Phone.Media.Devices.AvailableAudioRoutingEndpoints.Earpiece))
                        {
                            routing.SetAudioEndpoint(Windows.Phone.Media.Devices.AudioRoutingEndpoint.Earpiece);
                        }
                        else
                        {
                            routing.SetAudioEndpoint(Windows.Phone.Media.Devices.AudioRoutingEndpoint.Default);
                        }
                    }
                    ZaloLog.Add("ZRTC Audio routing set to: " + routing.GetAudioEndpoint());
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SetSpeakerphone error: " + ex.Message);
            }
        }

        public void StopSession()
        {
            lock (_lock)
            {
                _isRunning = false;
                _isStreaming = false;

                if (_captureCts != null)
                {
                    _captureCts.Cancel();
                    _captureCts.Dispose();
                    _captureCts = null;
                }

                StopKeepAliveTimer();

                if (_audioCapture != null)
                {
                    try { var ignored = _audioCapture.StopRecordAsync(); } catch { }
                    try { _audioCapture.Dispose(); } catch { }
                    _audioCapture = null;
                }

                if (_captureStream != null)
                {
                    try { _captureStream.Dispose(); } catch { }
                    _captureStream = null;
                }

                if (_writer != null)
                {
                    try { _writer.DetachStream(); _writer.Dispose(); } catch { }
                    _writer = null;
                }

                if (_socket != null)
                {
                    try { _socket.Dispose(); } catch { }
                    _socket = null;
                }

                _encoder = null;
                _decoder = null;
                _mediaStreamSource = null;

                while (_playbackQueue.TryDequeue(out _)) { }

                ZaloLog.Add("ZRTC UDP Relay Session stopped.");
            }
        }
    }
}

