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
                IsVideoEnabled = isVideo
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
        }

        public static async Task RejectCallAsync()
        {
            if (CurrentSession == null) return;
            CurrentSession.State = ZaloCallState.Rejected;
            NotifyState(ZaloCallState.Rejected);
            StopTimers();

            // Send Signal 402 status 3 (rejected)
            await SendSignal402Async(CurrentSession, 3);
        }

        public static async Task EndCallAsync()
        {
            if (CurrentSession == null) return;
            var session = CurrentSession;
            bool wasConnected = session.State == ZaloCallState.Connected;
            session.State = ZaloCallState.Ended;
            StopTimers();
            NotifyState(ZaloCallState.Ended);

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

        public static void ProcessIncomingSignal(int cmd, JsonObject parsed)
        {
            if (parsed == null) return;
            try
            {
                long callId = parsed.ContainsKey("callId") ? (long)parsed.GetNamedNumber("callId") : 0;
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
                        string callerId = parsed.ContainsKey("callerId") ? parsed.GetNamedString("callerId") : string.Empty;
                        int type = parsed.ContainsKey("type") ? (int)parsed.GetNamedNumber("type") : 0;
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
                            State = ZaloCallState.Calling
                        };
                        CurrentSession = session;
                        NotifyState(ZaloCallState.Calling);

                        var ignored = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            NavigationManager.NavigateTo(new Views.CallPage(session, type == 1));
                        });
                    }
                }
                else if (cmd == 402)
                {
                    int status = parsed.ContainsKey("status") ? (int)parsed.GetNamedNumber("status") : -1;
                    if (CurrentSession != null)
                    {
                        if (status == 0)
                        {
                            CurrentSession.State = ZaloCallState.Connected;
                            NotifyState(ZaloCallState.Connected);
                            StartDurationTimer();
                            StopTimeoutTimer();
                        }
                        else if (status == 1 || status == 3)
                        {
                            CurrentSession.State = ZaloCallState.Rejected;
                            NotifyState(ZaloCallState.Rejected);
                            StopTimers();
                        }
                    }
                }
                else if (cmd == 405 || cmd == 409)
                {
                    if (CurrentSession != null)
                    {
                        CurrentSession.State = ZaloCallState.Ended;
                        NotifyState(ZaloCallState.Ended);
                        StopTimers();
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ProcessIncomingSignal error: " + ex.Message);
            }
        }

        // --- ZRTC Signaling Protocol Frames ---

        private static async Task SendSignal401Async(ZaloCallSession session)
        {
            try
            {
                string callerId = ZaloApp.Session?.Uid ?? string.Empty;
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"callerId\":\"{2}\",\"codec\":\"[]\\n\",\"type\":{3},\"time\":{4},\"req_id\":\"req_{5}\"}}",
                    session.CallId, session.PeerId, callerId, (int)session.CallType, nowMs, _rng.Next(1000, 9999));
                ZaloLog.Add("ZRTC Signal 401 payload: " + json);
                if (ZaloApp.Listener != null)
                {
                    await ZaloApp.Listener.SendCallSignalAsync(401, json);
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
                string json = string.Format("{{\"callId\":{0},\"status\":{1},\"callerId\":\"{2}\",\"req_id\":\"req_{3}\"}}",
                    session.CallId, status, session.PeerId, _rng.Next(1000, 9999));
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
                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"status\":103,\"req_id\":\"req_{2}\"}}",
                    session.CallId, session.PeerId, _rng.Next(1000, 9999));
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
                string json = string.Format("{{\"callId\":{0},\"calleeId\":\"{1}\",\"status\":50,\"duration\":{2},\"req_id\":\"req_{3}\"}}",
                    session.CallId, session.PeerId, session.DurationSeconds, _rng.Next(1000, 9999));
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
    }
}

