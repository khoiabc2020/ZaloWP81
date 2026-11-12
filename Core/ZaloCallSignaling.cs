using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using ZaloLite;

namespace ZaloWP81.Core
{
    public enum ZaloCallState
    {
        Idle,
        Calling,
        Ringing,
        Connected,
        Ended,
        Rejected
    }

    public enum ZaloCallType
    {
        Voice = 0,
        Video = 1
    }

    public sealed class ZaloCallSession
    {
        public long CallId { get; set; }
        public string PeerId { get; set; }
        public string PeerName { get; set; }
        public string PeerAvatar { get; set; }
        public ZaloCallType CallType { get; set; }
        public bool IsOutgoing { get; set; }
        public ZaloCallState State { get; set; }
        public int DurationSeconds { get; set; }
        public bool IsMuted { get; set; }
        public bool IsSpeakerOn { get; set; }
        public bool IsVideoEnabled { get; set; }

        // ZRTC Relay & Media parameters
        public string RelayHost { get; set; }
        public int RelayPort { get; set; }
        public string RelayToken { get; set; }
        public uint LocalSsrc { get; set; }
        public uint RemoteSsrc { get; set; }
        public string NegotiatedCodec { get; set; } = "opus";
    }

    /// <summary>
    /// Zalo Call Signaling and Call State Management Engine.
    /// Implements ZRTC signaling protocol (Signals 401, 402, 405, 406, 407, 409).
    /// </summary>
    public static class ZaloCallSignaling
    {
        private static readonly Random _rng = new Random();
        private static DispatcherTimer _durationTimer;
        private static DispatcherTimer _timeoutTimer;

        public static ZaloCallSession CurrentSession { get; private set; }

        public static event Action<ZaloCallSession, ZaloCallState> StateChanged;
        public static event Action<ZaloCallSession, int> DurationTick;

        public static long GenerateCallId()
        {
            // Call ID fits a positive 32-bit integer as required by Zalo call servers
            return 1000000000L + _rng.Next(0, 1147483647);
        }

        public static async Task<ZaloCallSession> StartCallAsync(string peerId, string peerName, string peerAvatar, bool isVideo)
        {
            if (CurrentSession != null && CurrentSession.State != ZaloCallState.Ended && CurrentSession.State != ZaloCallState.Rejected)
            {
                await EndCallAsync();
            }

            if (!string.IsNullOrEmpty(peerId))
            {
                if (peerId.StartsWith("u:") || peerId.StartsWith("g:"))
                    peerId = peerId.Substring(2);
            }

            var session = new ZaloCallSession
            {
                CallId = GenerateCallId(),
                PeerId = peerId,
                PeerName = peerName ?? "Người dùng Zalo",
                PeerAvatar = peerAvatar,
                CallType = isVideo ? ZaloCallType.Video : ZaloCallType.Voice,
                IsOutgoing = true,
                State = ZaloCallState.Ringing,
                DurationSeconds = 0,
                IsMuted = false,
                IsSpeakerOn = isVideo, // Default speaker for video calls
                IsVideoEnabled = isVideo,
                LocalSsrc = (uint)_rng.Next(1000000, 9999999)
            };

            CurrentSession = session;
            NotifyState(ZaloCallState.Ringing);

            // Send real ZRTC Signal 401 (requestCall) through WebSocket to server
            await SendSignal401Async(session);

            // Wait up to 60s for the other party to answer; if no answer, automatically cancel
            StartTimeoutTimer(session);

            return session;
        }

        private static void StartTimeoutTimer(ZaloCallSession session)
        {
            StopTimeoutTimer();
            _timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60.0) };
            _timeoutTimer.Tick += (s, e) =>
            {
                StopTimeoutTimer();
                if (CurrentSession == session && session.State == ZaloCallState.Ringing)
                {
                    session.State = ZaloCallState.Ended;
                    NotifyState(ZaloCallState.Ended);
                    StopTimers();
                    _ = SendSignal405Async(session);
                    RecordCallHistory(session, false, isMissed: true);
                }
            };
            _timeoutTimer.Start();
        }

        public static async Task AnswerCallAsync()
        {
            if (CurrentSession == null) return;
            CurrentSession.State = ZaloCallState.Connected;
            NotifyState(ZaloCallState.Connected);
            StartDurationTimer();

            // Send Signal 402 status 0
            await SendSignal402Async(CurrentSession, 0);

            // If relay server was already received via Signal 407, start UDP streaming
            if (!string.IsNullOrEmpty(CurrentSession.RelayHost))
            {
                _ = ZaloUdpRelayEngine.Current.StartSessionAsync(CurrentSession);
                ZaloUdpRelayEngine.Current.StartStreaming();
            }
        }

        public static async Task RejectCallAsync()
        {
            if (CurrentSession == null) return;
            var session = CurrentSession;
            session.State = ZaloCallState.Rejected;
            NotifyState(ZaloCallState.Rejected);
            StopTimers();
            ZaloUdpRelayEngine.Current.StopSession();

            // Send Signal 402 status 3 (rejected)
            await SendSignal402Async(session, 3);
            RecordCallHistory(session, false, isRejected: true);
        }

        public static async Task EndCallAsync()
        {
            if (CurrentSession == null) return;
            var session = CurrentSession;
            bool wasConnected = session.State == ZaloCallState.Connected;
            session.State = ZaloCallState.Ended;
            StopTimers();
            NotifyState(ZaloCallState.Ended);
            ZaloUdpRelayEngine.Current.StopSession();

            // Send Signal 405 (cancel) or 409 (hangup)
            if (wasConnected)
            {
                await SendSignal409Async(session);
                await SendSignal406Async(session, 50); // status 50 = connected call
            }
            else
            {
                await SendSignal405Async(session);
                await SendSignal406Async(session, 103); // status 103 = caller cancelled
            }

            RecordCallHistory(session, wasConnected);
        }

        public static void ToggleMute()
        {
            if (CurrentSession == null) return;
            CurrentSession.IsMuted = !CurrentSession.IsMuted;
        }

        public static void ToggleSpeaker()
        {
            if (CurrentSession == null) return;
            CurrentSession.IsSpeakerOn = !CurrentSession.IsSpeakerOn;
        }

        public static void ToggleVideo()
        {
            if (CurrentSession == null) return;
            CurrentSession.IsVideoEnabled = !CurrentSession.IsVideoEnabled;
        }

        private static void StartDurationTimer()
        {
            StopDurationTimer();
            _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _durationTimer.Tick += (s, e) =>
            {
                if (CurrentSession != null && CurrentSession.State == ZaloCallState.Connected)
                {
                    CurrentSession.DurationSeconds++;
                    var handler = DurationTick;
                    if (handler != null) handler(CurrentSession, CurrentSession.DurationSeconds);
                }
            };
            _durationTimer.Start();
        }

        private static void StopDurationTimer()
        {
            if (_durationTimer != null)
            {
                _durationTimer.Stop();
                _durationTimer = null;
            }
        }

        private static void StopTimeoutTimer()
        {
            if (_timeoutTimer != null)
            {
                _timeoutTimer.Stop();
                _timeoutTimer = null;
            }
        }

        private static void StopTimers()
        {
            StopDurationTimer();
            StopTimeoutTimer();
        }

        private static void NotifyState(ZaloCallState state)
        {
            var handler = StateChanged;
            if (handler != null && CurrentSession != null)
            {
                handler(CurrentSession, state);
            }
        }

        public static string FormatDuration(int totalSeconds)
        {
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return string.Format("{0:D2}:{1:D2}", m, s);
        }

        public static long GetSafeNumber(JsonObject obj, string key, long fallback = 0)
        {
            if (obj == null || !obj.ContainsKey(key)) return fallback;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.Number) return (long)val.GetNumber();
                if (val.ValueType == JsonValueType.String)
                {
                    long n;
                    if (long.TryParse(val.GetString(), out n)) return n;
                }
            }
            catch { }
            return fallback;
        }

        public static string GetSafeString(JsonObject obj, string key, string fallback = "")
        {
            if (obj == null || !obj.ContainsKey(key)) return fallback;
            try
            {
                var val = obj.GetNamedValue(key);
                if (val.ValueType == JsonValueType.String) return val.GetString();
                if (val.ValueType == JsonValueType.Number) return val.GetNumber().ToString();
                if (val.ValueType == JsonValueType.Boolean) return val.GetBoolean().ToString();
            }
            catch { }
            return fallback;
        }

        public static void ProcessIncomingSignal(int cmd, JsonObject parsed)
        {
            if (parsed == null) return;
            try
            {
                long callId = GetSafeNumber(parsed, "callId", 0);
                ZaloLog.Add(string.Format("ZRTC received signal {0}: callId={1}", cmd, callId));

                if (cmd == 401)
                {
                    if (CurrentSession != null && CurrentSession.IsOutgoing && CurrentSession.CallId == callId)
                    {
                        CurrentSession.State = ZaloCallState.Ringing;
                        NotifyState(ZaloCallState.Ringing);
                    }
                    else if (CurrentSession == null || CurrentSession.State == ZaloCallState.Ended || CurrentSession.State == ZaloCallState.Rejected)
                    {
                        string callerId = GetSafeString(parsed, "callerId", string.Empty);
                        if (callerId.StartsWith("u:") || callerId.StartsWith("g:")) callerId = callerId.Substring(2);

                        int type = (int)GetSafeNumber(parsed, "type", 0);
                        var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == callerId);
                        string callerName = fr?.Name ?? "Cuộc gọi Zalo";
                        string callerAvatar = fr?.Avatar;

                        var session = new ZaloCallSession
                        {
                            CallId = callId,
                            PeerId = callerId,
                            PeerName = callerName,
                            PeerAvatar = callerAvatar,
                            CallType = type == 1 ? ZaloCallType.Video : ZaloCallType.Voice,
                            IsOutgoing = false,
                            State = ZaloCallState.Calling,
                            LocalSsrc = (uint)_rng.Next(1000000, 9999999)
                        };
                        CurrentSession = session;
                        NotifyState(ZaloCallState.Calling);

                        var ignored = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            NavigationManager.NavigateTo(new Views.CallPage(session, type == 1));
                        });
                    }
                }
                else if (cmd == 416)
                {
                    // Server ACK for call request
                    if (CurrentSession != null && CurrentSession.IsOutgoing)
                    {
                        CurrentSession.State = ZaloCallState.Ringing;
                        NotifyState(ZaloCallState.Ringing);
                    }
                }
                else if (cmd == 407)
                {
                    // Server assigns media relay server
                    if (CurrentSession != null)
                    {
                        ParseRelayConfig(parsed, CurrentSession);
                        _ = ZaloUdpRelayEngine.Current.StartSessionAsync(CurrentSession);
                    }
                }
                else if (cmd == 408)
                {
                    // Call bridged / connected
                    if (CurrentSession != null)
                    {
                        CurrentSession.State = ZaloCallState.Connected;
                        NotifyState(ZaloCallState.Connected);
                        StartDurationTimer();
                        StopTimeoutTimer();
                        ZaloUdpRelayEngine.Current.StartStreaming();
                    }
                }
                else if (cmd == 402)
                {
                    int status = (int)GetSafeNumber(parsed, "status", -1);
                    if (CurrentSession != null)
                    {
                        if (status == 0)
                        {
                            CurrentSession.State = ZaloCallState.Connected;
                            NotifyState(ZaloCallState.Connected);
                            StartDurationTimer();
                            StopTimeoutTimer();
                            ZaloUdpRelayEngine.Current.StartStreaming();
                        }
                        else if (status == 1 || status == 3)
                        {
                            var session = CurrentSession;
                            session.State = ZaloCallState.Rejected;
                            NotifyState(ZaloCallState.Rejected);
                            StopTimers();
                            ZaloUdpRelayEngine.Current.StopSession();
                            RecordCallHistory(session, false, isRejected: true);
                        }
                    }
                }
                else if (cmd == 405 || cmd == 409)
                {
                    if (CurrentSession != null)
                    {
                        var session = CurrentSession;
                        bool wasConnected = session.State == ZaloCallState.Connected;
                        session.State = ZaloCallState.Ended;
                        NotifyState(ZaloCallState.Ended);
                        StopTimers();
                        ZaloUdpRelayEngine.Current.StopSession();
                        RecordCallHistory(session, wasConnected);
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ProcessIncomingSignal error: " + ex.Message);
            }
        }

        private static void ParseRelayConfig(JsonObject parsed, ZaloCallSession session)
        {
            try
            {
                session.RelayToken = GetSafeString(parsed, "token", session.RelayToken ?? string.Empty);

                uint ssrc = (uint)GetSafeNumber(parsed, "ssrc", (long)GetSafeNumber(parsed, "local_ssrc", session.LocalSsrc));
                if (ssrc != 0) session.LocalSsrc = ssrc;

                uint rSsrc = (uint)GetSafeNumber(parsed, "remote_ssrc", session.RemoteSsrc);
                if (rSsrc != 0) session.RemoteSsrc = rSsrc;

                string host = null;
                int port = 0;

                if (parsed.ContainsKey("servers"))
                {
                    var serversVal = parsed.GetNamedValue("servers");
                    if (serversVal.ValueType == JsonValueType.Array)
                    {
                        var arr = serversVal.GetArray();
                        foreach (var item in arr)
                        {
                            if (item.ValueType == JsonValueType.Object)
                            {
                                var obj = item.GetObject();
                                host = GetSafeString(obj, "ip", GetSafeString(obj, "host"));
                                port = (int)GetSafeNumber(obj, "port", 0);
                                if (!string.IsNullOrEmpty(host) && port > 0) break;
                            }
                            else if (item.ValueType == JsonValueType.String)
                            {
                                string str = item.GetString();
                                string[] parts = str.Split(':');
                                if (parts.Length == 2 && int.TryParse(parts[1], out port))
                                {
                                    host = parts[0];
                                    break;
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(host))
                {
                    host = GetSafeString(parsed, "ip", GetSafeString(parsed, "host", GetSafeString(parsed, "server")));
                    port = (int)GetSafeNumber(parsed, "port", 0);
                }

                if (!string.IsNullOrEmpty(host) && port > 0)
                {
                    session.RelayHost = host;
                    session.RelayPort = port;
                    ZaloLog.Add(string.Format("ZRTC Parsed Relay Config: Host={0}, Port={1}, Token={2}, SSRC={3}",
                        host, port, session.RelayToken, session.LocalSsrc));
                }
                else
                {
                    ZaloLog.Add("ZRTC Signal 407 parsed but no valid host/port found.");
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ParseRelayConfig error: " + ex.Message);
            }
        }

        // --- ZRTC Signaling Protocol Frames ---

        private static async Task SendSignal401Async(ZaloCallSession session)
        {
            try
            {
                string callerId = ZaloApp.Session?.Uid ?? string.Empty;
                if (callerId.StartsWith("u:") || callerId.StartsWith("g:")) callerId = callerId.Substring(2);

                string calleeId = session.PeerId ?? string.Empty;
                if (calleeId.StartsWith("u:") || calleeId.StartsWith("g:")) calleeId = calleeId.Substring(2);

                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                uint localSsrc = session.LocalSsrc != 0 ? session.LocalSsrc : (uint)_rng.Next(1000000, 9999999);
                session.LocalSsrc = localSsrc;

                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"callerId\":\"{2}\",\"codec\":\"[\\\"opus/48000/2\\\"]\\n\",\"sP2P\":0,\"ssrc\":{3},\"type\":{4},\"time\":{5},\"req_id\":\"req_{6}\"}}",
                    session.CallId, calleeId, callerId, localSsrc, (int)session.CallType, nowMs, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 401 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(401, json);
                }
                else
                {
                    ZaloLog.Add("ZRTC SendSignal401 warning: ZaloApp.Listener is null!");
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal401 error: " + ex.Message);
            }
        }

        private static async Task SendSignal402Async(ZaloCallSession session, int status)
        {
            try
            {
                string callerId = session.PeerId ?? string.Empty;
                if (callerId.StartsWith("u:") || callerId.StartsWith("g:")) callerId = callerId.Substring(2);

                uint localSsrc = session.LocalSsrc != 0 ? session.LocalSsrc : (uint)_rng.Next(1000000, 9999999);
                session.LocalSsrc = localSsrc;

                string json = string.Format("{{\"callId\":{0},\"status\":{1},\"callerId\":\"{2}\",\"ssrc\":{3},\"sP2P\":0,\"req_id\":\"req_{4}\"}}",
                    session.CallId, status, callerId, localSsrc, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 402 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(402, json);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal402 error: " + ex.Message);
            }
        }

        private static async Task SendSignal405Async(ZaloCallSession session)
        {
            try
            {
                string calleeId = session.PeerId ?? string.Empty;
                if (calleeId.StartsWith("u:") || calleeId.StartsWith("g:")) calleeId = calleeId.Substring(2);

                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"status\":103,\"req_id\":\"req_{2}\"}}",
                    session.CallId, calleeId, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 405 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(405, json);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal405 error: " + ex.Message);
            }
        }

        private static async Task SendSignal409Async(ZaloCallSession session)
        {
            try
            {
                string calleeId = session.PeerId ?? string.Empty;
                if (calleeId.StartsWith("u:") || calleeId.StartsWith("g:")) calleeId = calleeId.Substring(2);

                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"status\":50,\"duration\":{2},\"req_id\":\"req_{3}\"}}",
                    session.CallId, calleeId, session.DurationSeconds, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 409 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(409, json);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal409 error: " + ex.Message);
            }
        }

        private static async Task SendSignal406Async(ZaloCallSession session, int statusCode)
        {
            try
            {
                string json = string.Format("{{\"callId\":{0},\"status\":{1},\"duration\":{2},\"req_id\":\"req_{3}\"}}",
                    session.CallId, statusCode, session.DurationSeconds, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 406 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(406, json);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal406 error: " + ex.Message);
            }
        }

        public static void RecordCallHistory(ZaloCallSession session, bool wasConnected, bool isRejected = false, bool isMissed = false)
        {
            if (session == null || string.IsNullOrEmpty(session.PeerId)) return;

            try
            {
                string peerId = session.PeerId;
                if (peerId.StartsWith("u:") || peerId.StartsWith("g:")) peerId = peerId.Substring(2);

                string convKey = "u:" + peerId;
                var conv = ZaloApp.Conversations.FirstOrDefault(c => c.Key == convKey || c.ThreadId == peerId);
                if (conv == null)
                {
                    conv = new ZaloConversation
                    {
                        Key = convKey,
                        ThreadId = peerId,
                        Name = session.PeerName ?? "Người dùng Zalo",
                        AvatarUrl = session.PeerAvatar,
                        IsGroup = false
                    };
                    ZaloApp.Conversations.Insert(0, conv);
                }

                bool isVideo = session.CallType == ZaloCallType.Video;
                string callPrefix = isVideo ? "Cuộc gọi video" : "Cuộc gọi thoại";
                string callText;

                if (session.IsOutgoing)
                {
                    if (wasConnected && session.DurationSeconds > 0)
                    {
                        callText = string.Format("{0} đi ({1})", callPrefix, FormatDuration(session.DurationSeconds));
                    }
                    else if (isRejected)
                    {
                        callText = string.Format("{0} đi bị từ chối", callPrefix);
                    }
                    else
                    {
                        callText = string.Format("{0} đi không trả lời", callPrefix);
                    }
                }
                else
                {
                    if (wasConnected && session.DurationSeconds > 0)
                    {
                        callText = string.Format("{0} đến ({1})", callPrefix, FormatDuration(session.DurationSeconds));
                    }
                    else if (isRejected)
                    {
                        callText = string.Format("{0} đến đã từ chối", callPrefix);
                    }
                    else
                    {
                        callText = string.Format("{0} nhỡ", callPrefix);
                    }
                }

                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var chatMsg = new ZaloChatMessage
                {
                    Id = "call_" + session.CallId + "_" + nowMs,
                    GlobalMessageId = session.CallId.ToString(),
                    ClientMessageId = nowMs.ToString(),
                    SenderUid = session.IsOutgoing ? (ZaloApp.Session?.Uid ?? "") : peerId,
                    RecipientUid = session.IsOutgoing ? peerId : (ZaloApp.Session?.Uid ?? ""),
                    Sender = session.IsOutgoing ? "Tôi" : (session.PeerName ?? "Người dùng Zalo"),
                    Text = callText,
                    MsgType = "chat.call",
                    Ts = nowMs,
                    FromMe = session.IsOutgoing,
                    Status = wasConnected ? 1 : 0
                };

                var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
                if (dispatcher != null)
                {
                    var ignored = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (!conv.Messages.Any(m => m.GlobalMessageId == session.CallId.ToString() && m.MsgType == "chat.call"))
                        {
                            conv.Messages.Add(chatMsg);
                            conv.LastText = callText;
                            conv.LastTs = nowMs;

                            int idx = ZaloApp.Conversations.IndexOf(conv);
                            if (idx > 0)
                            {
                                ZaloApp.Conversations.Move(idx, 0);
                            }
                            _ = ZaloApp.SaveCachedConversationsAsync();
                            ZaloLog.Add(string.Format("ZRTC Call history recorded for {0}: {1}", peerId, callText));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("RecordCallHistory error: " + ex.Message);
            }
        }
    }
}

