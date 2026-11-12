using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.Web.Http;
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
        public string SessionId { get; set; }
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

        public static string GenerateCallSession()
        {
            byte[] bytes = new byte[115];
            _rng.NextBytes(bytes);
            string b64 = Convert.ToBase64String(bytes);
            return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
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

            string callSessionToken = GenerateCallSession();
            session.SessionId = callSessionToken;

            CurrentSession = session;
            NotifyState(ZaloCallState.Ringing);

            // Step 1: Initiate call session via Zalo HTTP VoIP API (/api/voicecall/requestcall)
            try
            {
                string rtpIp = "171.244.128.61:4200";
                string rtcpIp = "171.244.128.61:4200";
                string codecStr = "[{\"dynamicFptime\":0,\"frmPtime\":20,\"name\":\"opus/16000/1\",\"payload\":112}]\n";
                string extendDataStr = BuildExtendData(null, isVideo, rtpIp, rtcpIp);

                var req401 = new JsonObject();
                req401["calleeId"] = JsonValue.CreateStringValue(peerId);
                req401["callId"] = JsonValue.CreateNumberValue(session.CallId);
                req401["codec"] = JsonValue.CreateStringValue(codecStr);
                req401["extendData"] = JsonValue.CreateStringValue(extendDataStr);
                req401["rtcpAddress"] = JsonValue.CreateStringValue(rtcpIp);
                req401["rtpAddress"] = JsonValue.CreateStringValue(rtpIp);
                req401["session"] = JsonValue.CreateStringValue(callSessionToken);
                req401["typeRequest"] = JsonValue.CreateNumberValue(isVideo ? 3 : 1);
                req401["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);

                var resp401 = await SendVoiceCallApiAsync("/api/voicecall/requestcall", req401, CancellationToken.None);
                if (resp401 != null)
                {
                    long status = GetSafeNumber(resp401, "status", 0);
                    if (status != 0)
                    {
                        string msg = GetSafeString(resp401, "msg", "Không thể khởi tạo cuộc gọi (mã " + status + ")");
                        ZaloLog.Add("VoiceCall requestcall rejected: " + msg);
                        session.State = ZaloCallState.Ended;
                        NotifyState(ZaloCallState.Ended);
                        RecordCallHistory(session, false, isMissed: false);
                        return session;
                    }

                    long realCallId = GetSafeNumber(resp401, "id", session.CallId);
                    session.CallId = realCallId;
                    string sessId = GetSafeString(resp401, "sessId", GetSafeString(resp401, "session", string.Empty));
                    if (!string.IsNullOrEmpty(sessId)) session.SessionId = sessId;

                    // Parse relay servers from resp401
                    ParseRelayConfig(resp401, session);

                    if (!string.IsNullOrEmpty(session.RelayHost) && session.RelayPort > 0)
                    {
                        rtpIp = session.RelayHost + ":" + session.RelayPort;
                        rtcpIp = rtpIp;
                    }
                    else
                    {
                        string srvRtp = GetSafeString(resp401, "rtpIP", GetSafeString(resp401, "rtpSerIp", ""));
                        if (!string.IsNullOrEmpty(srvRtp))
                        {
                            rtpIp = srvRtp;
                            rtcpIp = GetSafeString(resp401, "rtcpIP", rtpIp);
                        }
                    }

                    extendDataStr = BuildExtendData(resp401, isVideo, rtpIp, rtcpIp);

                    // Step 2: Send media invite via /api/voicecall/request (Signal 416)
                    // subCommand = 3 is strictly required by Zalo official protocol when extendData is present!
                    var req416 = new JsonObject();
                    req416["callId"] = JsonValue.CreateNumberValue(realCallId);
                    req416["calleeId"] = JsonValue.CreateStringValue(peerId);
                    req416["codec"] = JsonValue.CreateStringValue(codecStr);
                    req416["extendData"] = JsonValue.CreateStringValue(extendDataStr);
                    req416["rtcpAddress"] = JsonValue.CreateStringValue(rtcpIp);
                    req416["rtpAddress"] = JsonValue.CreateStringValue(rtpIp);
                    req416["session"] = JsonValue.CreateStringValue(session.SessionId);
                    req416["subCommand"] = JsonValue.CreateNumberValue(3);
                    req416["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);

                    var resp416 = await SendVoiceCallApiAsync("/api/voicecall/request", req416, CancellationToken.None);
                    ZaloLog.Add("VoiceCall /api/voicecall/request completed, recipient is now ringing!");

                    if (resp416 != null)
                    {
                        ParseRelayConfig(resp416, session);
                    }

                    // If relay server was obtained, initialize UDP session immediately
                    if (!string.IsNullOrEmpty(session.RelayHost) && session.RelayPort > 0)
                    {
                        _ = ZaloUdpRelayEngine.Current.StartSessionAsync(session);
                    }
                }
                else
                {
                    // HTTP initiation failed, fall back to WebSocket Signal 401
                    await SendSignal401Async(session);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("StartCallAsync HTTP initiation error: " + ex.Message);
                await SendSignal401Async(session);
            }

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
            StopTimeoutTimer();

            string peerId = CurrentSession.PeerId ?? string.Empty;
            if (peerId.StartsWith("u:") || peerId.StartsWith("g:")) peerId = peerId.Substring(2);

            try
            {
                // HTTP /api/voicecall/answer (Signal 402)
                var reqAnswer = new JsonObject();
                reqAnswer["callerId"] = JsonValue.CreateStringValue(peerId);
                reqAnswer["callId"] = JsonValue.CreateNumberValue(CurrentSession.CallId);
                reqAnswer["status"] = JsonValue.CreateNumberValue(0);
                reqAnswer["codec"] = JsonValue.CreateStringValue("[{\"dynamicFptime\":0,\"frmPtime\":20,\"name\":\"opus/16000/1\",\"payload\":112}]\n");
                reqAnswer["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);
                var respAnswer = await SendVoiceCallApiAsync("/api/voicecall/answer", reqAnswer, CancellationToken.None);
                if (respAnswer != null)
                {
                    ParseRelayConfig(respAnswer, CurrentSession);
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("AnswerCallAsync HTTP error: " + ex.Message);
            }

            // WebSocket Signal 402 status 0
            await SendSignal402Async(CurrentSession, 0);

            // If relay server was already received via Signal 407 / resp401, start UDP streaming
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

            string peerId = session.PeerId ?? string.Empty;
            if (peerId.StartsWith("u:") || peerId.StartsWith("g:")) peerId = peerId.Substring(2);

            try
            {
                // HTTP /api/voicecall/cancel
                var reqCancel = new JsonObject();
                reqCancel["callerId"] = JsonValue.CreateStringValue(peerId);
                reqCancel["callId"] = JsonValue.CreateNumberValue(session.CallId);
                reqCancel["callType"] = JsonValue.CreateNumberValue(session.CallType == ZaloCallType.Video ? 3 : 1);
                reqCancel["status"] = JsonValue.CreateNumberValue(3); // 3 = rejected
                reqCancel["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);
                _ = SendVoiceCallApiAsync("/api/voicecall/cancel", reqCancel, CancellationToken.None);
            }
            catch (Exception ex)
            {
                ZaloLog.Add("RejectCallAsync HTTP error: " + ex.Message);
            }

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

            string peerId = session.PeerId ?? string.Empty;
            if (peerId.StartsWith("u:") || peerId.StartsWith("g:")) peerId = peerId.Substring(2);

            try
            {
                if (wasConnected)
                {
                    // HTTP /api/voicecall/endcall
                    var reqEnd = new JsonObject();
                    reqEnd["uidTo"] = JsonValue.CreateStringValue(peerId);
                    reqEnd["callId"] = JsonValue.CreateNumberValue(session.CallId);
                    reqEnd["status"] = JsonValue.CreateNumberValue(3);
                    reqEnd["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);
                    _ = SendVoiceCallApiAsync("/api/voicecall/endcall", reqEnd, CancellationToken.None);

                    await SendSignal409Async(session);
                    await SendSignal406Async(session, 50); // status 50 = connected call
                }
                else
                {
                    // HTTP /api/voicecall/cancel
                    var reqCancel = new JsonObject();
                    reqCancel["callerId"] = JsonValue.CreateStringValue(peerId);
                    reqCancel["callId"] = JsonValue.CreateNumberValue(session.CallId);
                    reqCancel["callType"] = JsonValue.CreateNumberValue(session.CallType == ZaloCallType.Video ? 3 : 1);
                    reqCancel["status"] = JsonValue.CreateNumberValue(0);
                    reqCancel["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);
                    _ = SendVoiceCallApiAsync("/api/voicecall/cancel", reqCancel, CancellationToken.None);

                    await SendSignal405Async(session);
                    await SendSignal406Async(session, 103); // status 103 = caller cancelled
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("EndCallAsync error: " + ex.Message);
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

        public static bool TryExtractCallerInfo(JsonObject parsed, out string callerId, out long callId, out bool isVideo)
        {
            callerId = string.Empty;
            callId = 0;
            isVideo = false;
            if (parsed == null) return false;

            callId = GetSafeNumber(parsed, "callId", 0);
            if (callId == 0) callId = GetSafeNumber(parsed, "id", 0);
            if (callId == 0) callId = GetSafeNumber(parsed, "call_id", 0);
            if (callId == 0) callId = GetSafeNumber(parsed, "zaloCallId", 0);

            string[] callerKeys = { "callerId", "caller_id", "peerZaloId", "senderID", "senderId", "senderUid", "fromUid", "uidFrom", "partnerId", "caller", "uid", "userId" };
            string myUid = ZaloApp.Session?.Uid ?? string.Empty;
            if (myUid.StartsWith("u:") || myUid.StartsWith("g:")) myUid = myUid.Substring(2);

            foreach (var key in callerKeys)
            {
                string val = GetSafeString(parsed, key, string.Empty);
                if (!string.IsNullOrEmpty(val))
                {
                    if (val.StartsWith("u:") || val.StartsWith("g:")) val = val.Substring(2);
                    if (!string.IsNullOrEmpty(myUid) && val == myUid) continue;
                    callerId = val;
                    break;
                }
            }

            int type = (int)GetSafeNumber(parsed, "type", 0);
            if (type == 0) type = (int)GetSafeNumber(parsed, "typeCall", 0);
            if (type == 0) type = (int)GetSafeNumber(parsed, "typeRequest", 0);
            bool isVid = type == 1 || type == 3;
            if (!isVid)
            {
                string vidStr = GetSafeString(parsed, "videocall", GetSafeString(parsed, "call_video_call", ""));
                isVid = vidStr.Equals("true", StringComparison.OrdinalIgnoreCase) || vidStr == "1";
            }
            isVideo = isVid;

            return !string.IsNullOrEmpty(callerId);
        }

        private static void SendIncomingRingingAck(ZaloCallSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.PeerId)) return;
            try
            {
                string myUid = ZaloApp.Session?.Uid ?? string.Empty;
                if (myUid.StartsWith("u:") || myUid.StartsWith("g:")) myUid = myUid.Substring(2);

                // 1. WebSocket Signal 416 ringing ACK
                var ringingAck = new JsonObject();
                ringingAck["callId"] = JsonValue.CreateNumberValue(session.CallId);
                ringingAck["calleeId"] = JsonValue.CreateStringValue(myUid);
                ringingAck["callerId"] = JsonValue.CreateStringValue(session.PeerId);
                ringingAck["receiverID"] = JsonValue.CreateStringValue(session.PeerId);
                ringingAck["senderID"] = JsonValue.CreateStringValue(myUid);
                ringingAck["status"] = JsonValue.CreateNumberValue(0);
                ringingAck["time"] = JsonValue.CreateNumberValue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                ringingAck["req_id"] = JsonValue.CreateStringValue("req_" + _rng.Next(1000, 9999));
                _ = ZaloApp.Listener?.SendCallSignalAsync(416, ringingAck.Stringify());

                // 2. HTTP /api/voicecall/ringing
                var reqRinging = new JsonObject();
                reqRinging["callId"] = JsonValue.CreateNumberValue(session.CallId);
                reqRinging["callerId"] = JsonValue.CreateStringValue(session.PeerId);
                reqRinging["status"] = JsonValue.CreateNumberValue(0);
                reqRinging["imei"] = JsonValue.CreateStringValue(ZaloApp.Session?.Imei ?? string.Empty);
                _ = SendVoiceCallApiAsync("/api/voicecall/ringing", reqRinging, CancellationToken.None);

                ZaloLog.Add("ZRTC Sent incoming ringing ACK for call " + session.CallId);
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendIncomingRingingAck error: " + ex.Message);
            }
        }

        public static void ProcessIncomingSignal(int cmd, JsonObject parsed)
        {
            if (parsed == null) return;
            try
            {
                long callId = GetSafeNumber(parsed, "callId", 0);
                if (callId == 0) callId = GetSafeNumber(parsed, "id", 0);
                if (callId == 0) callId = GetSafeNumber(parsed, "call_id", 0);
                if (callId == 0) callId = GetSafeNumber(parsed, "zaloCallId", 0);

                ZaloLog.Add(string.Format("ZRTC received signal {0}: callId={1}, payload={2}", cmd, callId, parsed.Stringify()));

                // Case 1: Lumia is currently IDLE (handle incoming call invitation)
                if (CurrentSession == null || CurrentSession.State == ZaloCallState.Ended || CurrentSession.State == ZaloCallState.Rejected)
                {
                    if (cmd == 401 || cmd == 402 || cmd == 416 || cmd == 400 || cmd == 426 || cmd == 428)
                    {
                        int status = (int)GetSafeNumber(parsed, "status", 0);
                        if (status == 1 || status == 3 || status == 50 || status == 103)
                        {
                            // Status indicates call already ended/cancelled/rejected
                            return;
                        }

                        if (TryExtractCallerInfo(parsed, out string callerId, out long extractedCallId, out bool isVideo))
                        {
                            if (extractedCallId != 0) callId = extractedCallId;
                            if (callId == 0) callId = GenerateCallId();

                            var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == callerId);
                            string callerName = fr?.Name;
                            string callerAvatar = fr?.Avatar;
                            if (string.IsNullOrEmpty(callerName))
                            {
                                var conv = ZaloApp.Conversations.FirstOrDefault(c => c.ThreadId == callerId || c.Key == "u:" + callerId);
                                callerName = conv?.Name ?? GetSafeString(parsed, "displayName", GetSafeString(parsed, "dName", "Cuộc gọi Zalo"));
                                callerAvatar = conv?.AvatarUrl;
                            }

                            var session = new ZaloCallSession
                            {
                                CallId = callId,
                                PeerId = callerId,
                                PeerName = callerName ?? "Người dùng Zalo",
                                PeerAvatar = callerAvatar,
                                CallType = isVideo ? ZaloCallType.Video : ZaloCallType.Voice,
                                IsOutgoing = false,
                                State = ZaloCallState.Calling,
                                DurationSeconds = 0,
                                LocalSsrc = (uint)_rng.Next(1000000, 9999999)
                            };

                            ParseRelayConfig(parsed, session);
                            CurrentSession = session;
                            NotifyState(ZaloCallState.Calling);

                            // Send ringing ACK back to caller
                            SendIncomingRingingAck(session);

                            // Start timeout timer (60s)
                            StartTimeoutTimer(session);

                            // Dispatch navigation to CallPage on UI thread
                            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
                            if (dispatcher != null)
                            {
                                var ignored = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    NavigationManager.NavigateTo(new Views.CallPage(session, isVideo));
                                });
                            }
                            return;
                        }
                    }
                }

                // Case 2: Lumia is currently in an active / ongoing session
                if (CurrentSession != null)
                {
                    if (cmd == 401 || cmd == 416)
                    {
                        if (CurrentSession.IsOutgoing)
                        {
                            CurrentSession.State = ZaloCallState.Ringing;
                            NotifyState(ZaloCallState.Ringing);
                            ParseRelayConfig(parsed, CurrentSession);
                        }
                    }
                    else if (cmd == 402)
                    {
                        int status = (int)GetSafeNumber(parsed, "status", -1);
                        if (CurrentSession.IsOutgoing)
                        {
                            if (status == 0 || (status == -1 && !parsed.ContainsKey("status") && !parsed.ContainsKey("reason")))
                            {
                                CurrentSession.State = ZaloCallState.Connected;
                                NotifyState(ZaloCallState.Connected);
                                StartDurationTimer();
                                StopTimeoutTimer();
                                ParseRelayConfig(parsed, CurrentSession);
                                _ = ZaloUdpRelayEngine.Current.StartSessionAsync(CurrentSession);
                                ZaloUdpRelayEngine.Current.StartStreaming();
                            }
                            else if (status == 1 || status == 3 || status == 103 || parsed.ContainsKey("reason"))
                            {
                                var session = CurrentSession;
                                session.State = ZaloCallState.Rejected;
                                NotifyState(ZaloCallState.Rejected);
                                StopTimers();
                                ZaloUdpRelayEngine.Current.StopSession();
                                RecordCallHistory(session, false, isRejected: true);
                            }
                        }
                        else
                        {
                            // In incoming call: if caller cancels
                            if (status == 103 || status == 1 || status == 3)
                            {
                                var session = CurrentSession;
                                session.State = ZaloCallState.Ended;
                                NotifyState(ZaloCallState.Ended);
                                StopTimers();
                                ZaloUdpRelayEngine.Current.StopSession();
                                RecordCallHistory(session, false, isMissed: true);
                            }
                        }
                    }
                    else if (cmd == 407)
                    {
                        // Server media relay config
                        ParseRelayConfig(parsed, CurrentSession);
                        _ = ZaloUdpRelayEngine.Current.StartSessionAsync(CurrentSession);
                    }
                    else if (cmd == 408)
                    {
                        // Call bridged / connected
                        CurrentSession.State = ZaloCallState.Connected;
                        NotifyState(ZaloCallState.Connected);
                        StartDurationTimer();
                        StopTimeoutTimer();
                        ParseRelayConfig(parsed, CurrentSession);
                        _ = ZaloUdpRelayEngine.Current.StartSessionAsync(CurrentSession);
                        ZaloUdpRelayEngine.Current.StartStreaming();
                    }
                    else if (cmd == 405 || cmd == 409 || cmd == 406)
                    {
                        var session = CurrentSession;
                        bool wasConnected = session.State == ZaloCallState.Connected;
                        int status = (int)GetSafeNumber(parsed, "status", 0);
                        session.State = ZaloCallState.Ended;
                        NotifyState(ZaloCallState.Ended);
                        StopTimers();
                        ZaloUdpRelayEngine.Current.StopSession();
                        RecordCallHistory(session, wasConnected, isRejected: status == 3 || status == 1);
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ProcessIncomingSignal error: " + ex.Message);
            }
        }

        public static async Task<JsonObject> SendVoiceCallApiAsync(string endpointPath, JsonObject payload, CancellationToken ct)
        {
            var s = ZaloApp.Session;
            if (s == null || string.IsNullOrEmpty(s.SecretKey))
            {
                ZaloLog.Add("VoiceCall API error: Chưa đăng nhập hoặc thiếu SecretKey");
                return null;
            }

            string domain = "https://voicecall-wpa.chat.zalo.me";
            if (s.ServiceMap != null && s.ServiceMap.ContainsKey("voice_call"))
            {
                try
                {
                    var arr = s.ServiceMap.GetNamedArray("voice_call");
                    if (arr.Count > 0 && !string.IsNullOrEmpty(arr.GetStringAt(0)))
                        domain = arr.GetStringAt(0).TrimEnd('/');
                }
                catch { }
            }

            try
            {
                string json = payload.Stringify();
                string encrypted = ZaloCrypto.Encode(s.SecretKey, json);
                string url = ZaloAuth.ApiUrl(domain + endpointPath, new KeyValuePair<string, string>("params", encrypted));

                ZaloLog.Add("VoiceCall API > " + endpointPath);
                var resp = await s.Http.SendAsync(HttpMethod.Get, url, null, null, ct);
                if (resp == null || string.IsNullOrEmpty(resp.Body))
                {
                    ZaloLog.Add("VoiceCall API < " + endpointPath + " empty response");
                    return null;
                }

                JsonObject outer;
                if (!JsonObject.TryParse(resp.Body, out outer))
                {
                    ZaloLog.Add("VoiceCall API < " + endpointPath + " parse error: " + resp.Body);
                    return null;
                }

                double errCode = ZaloAuth.Num(outer, "error_code");
                if (errCode != 0)
                {
                    ZaloLog.Add(string.Format("VoiceCall API < {0} error {1}: {2}", endpointPath, errCode, ZaloAuth.Str(outer, "error_message")));
                    return outer;
                }

                IJsonValue dataVal;
                if (outer.TryGetValue("data", out dataVal))
                {
                    if (dataVal.ValueType == JsonValueType.String)
                    {
                        string b64 = dataVal.GetString();
                        if (!string.IsNullOrEmpty(b64))
                        {
                            string decrypted = ZaloCrypto.Decode(s.SecretKey, b64);
                            JsonObject inner;
                            if (JsonObject.TryParse(decrypted, out inner))
                            {
                                ZaloLog.Add(string.Format("VoiceCall API < {0} decrypted {1} bytes", endpointPath, decrypted.Length));
                                if (inner.ContainsKey("data") && inner.GetNamedValue("data").ValueType == JsonValueType.Object)
                                    return inner.GetNamedObject("data");
                                return inner;
                            }
                        }
                    }
                    else if (dataVal.ValueType == JsonValueType.Object)
                    {
                        return dataVal.GetObject();
                    }
                }
                return outer;
            }
            catch (Exception ex)
            {
                ZaloLog.Add(string.Format("VoiceCall API < {0} exception: {1}", endpointPath, ex.Message));
                return null;
            }
        }

        private static string BuildExtendData(JsonObject resp401, bool isVideo, string rtpIp, string rtcpIp)
        {
            var serverAddr = new JsonArray();
            var serverResult = new JsonArray();

            if (resp401 != null && resp401.ContainsKey("servers") && resp401.GetNamedValue("servers").ValueType == JsonValueType.Array)
            {
                var srvArr = resp401.GetNamedArray("servers");
                foreach (var s in srvArr)
                {
                    if (s.ValueType == JsonValueType.Object)
                    {
                        var sObj = s.GetObject();
                        string rtp = GetSafeString(sObj, "rtpaddr", GetSafeString(sObj, "rtp", ""));
                        string rtcp = GetSafeString(sObj, "rtcpaddr", GetSafeString(sObj, "rtcp", ""));
                        string rtpV6 = GetSafeString(sObj, "rtpIPv6", "");
                        string rtcpV6 = GetSafeString(sObj, "rtcpIPv6", "");
                        int tpType = (int)GetSafeNumber(sObj, "tpType", 0);

                        var a = new JsonObject();
                        a["rtp"] = JsonValue.CreateStringValue(rtp);
                        a["rtcp"] = JsonValue.CreateStringValue(rtcp);
                        a["rtpIPv6"] = JsonValue.CreateStringValue(rtpV6);
                        a["rtcpIPv6"] = JsonValue.CreateStringValue(rtcpV6);
                        a["tpType"] = JsonValue.CreateNumberValue(tpType);
                        serverAddr.Add(a);

                        var r = new JsonObject();
                        r["rtp"] = JsonValue.CreateStringValue(rtp);
                        r["rtcp"] = JsonValue.CreateStringValue(rtcp);
                        r["rtpIPv6"] = JsonValue.CreateStringValue(rtpV6);
                        r["rtcpIPv6"] = JsonValue.CreateStringValue(rtcpV6);
                        r["rtt"] = JsonValue.CreateNumberValue(25);
                        r["recv"] = JsonValue.CreateNumberValue(14);
                        r["spTcp"] = JsonValue.CreateNumberValue(1);
                        r["tpType"] = JsonValue.CreateNumberValue(tpType);
                        serverResult.Add(r);
                    }
                }
            }

            if (serverAddr.Count == 0)
            {
                string fallbackRtp = !string.IsNullOrEmpty(rtpIp) ? rtpIp : "171.244.128.61:4200";
                string fallbackRtcp = !string.IsNullOrEmpty(rtcpIp) ? rtcpIp : fallbackRtp;

                var a = new JsonObject();
                a["rtp"] = JsonValue.CreateStringValue(fallbackRtp);
                a["rtcp"] = JsonValue.CreateStringValue(fallbackRtcp);
                a["rtpIPv6"] = JsonValue.CreateStringValue("");
                a["rtcpIPv6"] = JsonValue.CreateStringValue("");
                a["tpType"] = JsonValue.CreateNumberValue(0);
                serverAddr.Add(a);

                var r = new JsonObject();
                r["rtp"] = JsonValue.CreateStringValue(fallbackRtp);
                r["rtcp"] = JsonValue.CreateStringValue(fallbackRtcp);
                r["rtpIPv6"] = JsonValue.CreateStringValue("");
                r["rtcpIPv6"] = JsonValue.CreateStringValue("");
                r["rtt"] = JsonValue.CreateNumberValue(25);
                r["recv"] = JsonValue.CreateNumberValue(14);
                r["spTcp"] = JsonValue.CreateNumberValue(1);
                r["tpType"] = JsonValue.CreateNumberValue(0);
                serverResult.Add(r);
            }

            var p2pArr = new JsonArray();
            var p0 = new JsonObject();
            p0["ip"] = JsonValue.CreateStringValue("0.0.0.0");
            p0["port"] = JsonValue.CreateNumberValue(0);
            p0["type"] = JsonValue.CreateNumberValue(0);
            p2pArr.Add(p0);

            var ext = new JsonObject();
            ext["callType"] = JsonValue.CreateNumberValue(isVideo ? 1 : 0);
            ext["fecTP"] = JsonValue.CreateNumberValue(0);
            ext["gccAudio"] = JsonValue.CreateNumberValue(1);
            ext["gccEarlyCall"] = JsonValue.CreateNumberValue(0);
            ext["gccMode"] = JsonValue.CreateNumberValue(1);
            ext["gccSVLR"] = JsonValue.CreateNumberValue(1);
            ext["maxFT"] = JsonValue.CreateNumberValue(60);
            ext["newZrtc"] = JsonValue.CreateNumberValue(1);
            ext["numServers"] = JsonValue.CreateNumberValue(serverResult.Count);
            ext["p2p"] = p2pArr;
            ext["packetMode"] = JsonValue.CreateNumberValue(2);
            ext["platform"] = JsonValue.CreateNumberValue(2);
            ext["sP2P"] = JsonValue.CreateNumberValue(0);
            ext["serverAddr"] = serverAddr;
            ext["serverResult"] = serverResult;
            ext["spTcp"] = JsonValue.CreateNumberValue(1);
            ext["srtcp"] = JsonValue.CreateNumberValue(0);
            ext["srtpMode"] = JsonValue.CreateNumberValue(0);
            ext["supportCallBusy"] = JsonValue.CreateNumberValue(1);
            ext["supportHevcDecode"] = JsonValue.CreateNumberValue(0);
            ext["tpType"] = JsonValue.CreateNumberValue(0);

            var video = new JsonObject();
            var vCodecs = new JsonArray();
            var h264 = new JsonObject();
            h264["name"] = JsonValue.CreateStringValue("h264");
            h264["payload"] = JsonValue.CreateNumberValue(97);
            vCodecs.Add(h264);
            video["codec"] = vCodecs;
            ext["video"] = video;

            return ext.Stringify() + "\n";
        }

        private static void ParseRelayConfig(JsonObject parsed, ZaloCallSession session)
        {
            try
            {
                session.RelayToken = GetSafeString(parsed, "token", session.RelayToken ?? string.Empty);
                string sessId = GetSafeString(parsed, "sessId", GetSafeString(parsed, "session", string.Empty));
                if (!string.IsNullOrEmpty(sessId)) session.SessionId = sessId;

                uint ssrc = (uint)GetSafeNumber(parsed, "ssrc", (long)GetSafeNumber(parsed, "local_ssrc", session.LocalSsrc));
                if (ssrc != 0) session.LocalSsrc = ssrc;

                uint rSsrc = (uint)GetSafeNumber(parsed, "remote_ssrc", session.RemoteSsrc);
                if (rSsrc != 0) session.RemoteSsrc = rSsrc;

                string host = null;
                int port = 0;

                // 1. Check direct fields (rtpIP, rtpSerIp, rtpAddress)
                string rtpDirect = GetSafeString(parsed, "rtpIP", GetSafeString(parsed, "rtpSerIp", GetSafeString(parsed, "rtpAddress", "")));
                if (!string.IsNullOrEmpty(rtpDirect))
                {
                    string[] parts = rtpDirect.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out port))
                    {
                        host = parts[0];
                    }
                }

                // 2. Check servers array
                if (string.IsNullOrEmpty(host) && parsed.ContainsKey("servers"))
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
                                string rtpStr = GetSafeString(obj, "rtpaddr", GetSafeString(obj, "rtp", ""));
                                if (!string.IsNullOrEmpty(rtpStr))
                                {
                                    string[] parts = rtpStr.Split(':');
                                    if (parts.Length == 2 && int.TryParse(parts[1], out port))
                                    {
                                        host = parts[0];
                                        break;
                                    }
                                }
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

                var s401 = new JsonObject();
                s401["callId"] = JsonValue.CreateNumberValue(session.CallId);
                s401["calleeId"] = JsonValue.CreateStringValue(calleeId);
                s401["callerId"] = JsonValue.CreateStringValue(callerId);
                s401["receiverID"] = JsonValue.CreateStringValue(calleeId);
                s401["senderID"] = JsonValue.CreateStringValue(callerId);
                s401["session"] = JsonValue.CreateStringValue(session.SessionId ?? string.Empty);
                s401["codec"] = JsonValue.CreateStringValue("[\"opus/48000/2\"]\n");
                s401["sP2P"] = JsonValue.CreateNumberValue(0);
                s401["ssrc"] = JsonValue.CreateNumberValue(localSsrc);
                s401["type"] = JsonValue.CreateNumberValue((int)session.CallType);
                s401["time"] = JsonValue.CreateNumberValue(nowMs);
                s401["req_id"] = JsonValue.CreateStringValue("req_" + _rng.Next(1000, 9999));
                string json = s401.Stringify();
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

