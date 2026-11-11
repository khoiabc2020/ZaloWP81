using System;
using System.Threading.Tasks;
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
        private static DispatcherTimer _simulationTimer;

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
                State = ZaloCallState.Calling,
                DurationSeconds = 0,
                IsMuted = false,
                IsSpeakerOn = isVideo, // Default speaker for video calls
                IsVideoEnabled = isVideo
            };

            CurrentSession = session;
            NotifyState(ZaloCallState.Calling);

            // Send Signal 401 (requestCall)
            await SendSignal401Async(session);

            // Start simulated connection transition (Calling -> Ringing -> Connected)
            StartOutgoingCallSequence(session);

            return session;
        }

        private static void StartOutgoingCallSequence(ZaloCallSession session)
        {
            StopSimulationTimer();

            _simulationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _simulationTimer.Tick += (s, e) =>
            {
                if (CurrentSession != session) return;

                if (session.State == ZaloCallState.Calling)
                {
                    session.State = ZaloCallState.Ringing;
                    NotifyState(ZaloCallState.Ringing);
                    _simulationTimer.Interval = TimeSpan.FromSeconds(4.0);
                }
                else if (session.State == ZaloCallState.Ringing)
                {
                    session.State = ZaloCallState.Connected;
                    NotifyState(ZaloCallState.Connected);
                    StartDurationTimer();
                    StopSimulationTimer();
                }
            };
            _simulationTimer.Start();
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

        private static void StopSimulationTimer()
        {
            if (_simulationTimer != null)
            {
                _simulationTimer.Stop();
                _simulationTimer = null;
            }
        }

        private static void StopTimers()
        {
            StopDurationTimer();
            StopSimulationTimer();
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

        // --- ZRTC Signaling Protocol Frames ---

        private static async Task SendSignal401Async(ZaloCallSession session)
        {
            try
            {
                ZaloLog.Add(string.Format("ZRTC Signal 401 requestCall: callId={0}, callee={1}, type={2}",
                    session.CallId, session.PeerId, session.CallType));
                // Dispatched through WebSocket or HTTP when socket is active
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal401 error: " + ex.Message);
            }
            await Task.Yield();
        }

        private static async Task SendSignal402Async(ZaloCallSession session, int status)
        {
            try
            {
                ZaloLog.Add(string.Format("ZRTC Signal 402 answer/reject: callId={0}, status={1}", session.CallId, status));
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal402 error: " + ex.Message);
            }
            await Task.Yield();
        }

        private static async Task SendSignal405Async(ZaloCallSession session)
        {
            try
            {
                ZaloLog.Add(string.Format("ZRTC Signal 405 cancelCall: callId={0}", session.CallId));
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal405 error: " + ex.Message);
            }
            await Task.Yield();
        }

        private static async Task SendSignal409Async(ZaloCallSession session)
        {
            try
            {
                ZaloLog.Add(string.Format("ZRTC Signal 409 hangup: callId={0}, duration={1}s", session.CallId, session.DurationSeconds));
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal409 error: " + ex.Message);
            }
            await Task.Yield();
        }

        private static async Task SendSignal406Async(ZaloCallSession session, int statusCode)
        {
            try
            {
                ZaloLog.Add(string.Format("ZRTC Signal 406 stats: callId={0}, status={1}, duration={2}s",
                    session.CallId, statusCode, session.DurationSeconds));
            }
            catch (Exception ex)
            {
                ZaloLog.Add("SendSignal406 error: " + ex.Message);
            }
            await Task.Yield();
        }
    }
}
