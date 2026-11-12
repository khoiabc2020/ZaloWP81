using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed partial class ChatPage : Page
    {
        private void OnAvatarUpdated(string url)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (_conversation != null && (string.Equals(_conversation.AvatarUrl, url, StringComparison.OrdinalIgnoreCase)
                    || (_recipientInitial != null && _recipientInitial.Text.Length > 0)))
                {
                    _conversation.AvatarImageBrush = null;
                    UpdateHeaderAvatar();
                }
                RequestRenderMessages();
            });
        }

        private void RequestRenderMessages()
        {
            if (_renderDebounceTimer == null)
            {
                _renderDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                _renderDebounceTimer.Tick += (s, e) =>
                {
                    _renderDebounceTimer.Stop();
                    RenderMessages();
                };
            }
            _renderDebounceTimer.Stop();
            _renderDebounceTimer.Start();
        }

        private void RenderMessages()
        {
            if (_conversation == null) return;
            try
            {
                _messageStack.Children.Clear();

                if (_conversation.HasMoreHistory)
                {
                    var loadMoreBtn = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14, 6, 14, 6),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 10)
                    };
                    var loadMoreText = new TextBlock
                    {
                        Text = "Tải tin nhắn cũ hơn",
                        FontSize = 12.5,
                        Foreground = _activeBlue,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold
                    };
                    loadMoreBtn.Child = loadMoreText;
                    loadMoreBtn.PointerPressed += (s, e) => loadMoreBtn.Opacity = 0.6;
                    loadMoreBtn.PointerReleased += (s, e) => loadMoreBtn.Opacity = 1.0;
                    loadMoreBtn.PointerCanceled += (s, e) => loadMoreBtn.Opacity = 1.0;
                    loadMoreBtn.Tapped += async (s, e) =>
                    {
                        loadMoreText.Text = "Đang tải…";
                        loadMoreBtn.IsHitTestVisible = false;
                        await LoadOlderHistoryCoreAsync();
                    };
                    _messageStack.Children.Add(loadMoreBtn);
                }

                var msgList = _conversation.Messages.ToList();
                foreach (var m in msgList)
                {
                    try
                    {
                        var bubble = CreateMessageBubble(m);
                        if (bubble != null) _messageStack.Children.Add(bubble);
                    }
                    catch (Exception ex)
                    {
                        ZaloLog.Add("ChatPage bubble error: " + ex.Message);
                    }
                }

                ScrollToBottom();
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage RenderMessages error: " + ex.Message);
            }
        }

        private void ScrollToBottom(bool instant = false)
        {
            if (_messageScroll == null) return;
            try
            {
                _messageScroll.UpdateLayout();
                _messageScroll.ChangeView(null, 9999999, null, disableAnimation: instant);
            }
            catch { }

            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    _messageScroll.UpdateLayout();
                    _messageScroll.ChangeView(null, 9999999, null, disableAnimation: instant);
                }
                catch { }
            });
        }

        private FrameworkElement CreateMessageBubble(ZaloChatMessage msg)
        {
            bool isMe = msg.FromMe;

            if (_conversation != null && _conversation.IsGroup)
            {
                _conversation.ResolveMemberForMessage(msg);
            }

            bool isGroupOther = !isMe && _conversation != null && _conversation.IsGroup;

            bool isNextSameSender = false;
            if (_conversation != null && _conversation.Messages != null)
            {
                int idx = _conversation.Messages.IndexOf(msg);
                if (idx >= 0 && idx < _conversation.Messages.Count - 1)
                {
                    var nextMsg = _conversation.Messages[idx + 1];
                    if (nextMsg != null && nextMsg.FromMe == msg.FromMe && (!isGroupOther || nextMsg.SenderUid == msg.SenderUid))
                    {
                        if (string.Equals(nextMsg.TimeText, msg.TimeText, StringComparison.Ordinal))
                        {
                            isNextSameSender = true;
                        }
                    }
                }
            }

            bool isFriendshipCard = (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.Contains("artboard") || msg.MediaUrl.Contains("74009")))
                || (!string.IsNullOrEmpty(msg.Text) && (msg.Text.Contains("artboard") || msg.Text.Contains("74009") || msg.Text.Contains("trở thành bạn bè") || msg.Text.Contains("thành bạn bè")));

            var container = new Grid
            {
                Margin = new Thickness(0, isNextSameSender ? 1 : 2, 0, isNextSameSender ? 1 : 6),
                HorizontalAlignment = isFriendshipCard ? HorizontalAlignment.Center : (isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left)
            };

            var stack = new StackPanel();

            if (isGroupOther)
            {
                double scale = ZaloSettingsService.Current.UiScaleFactor;
                var senderNameBlock = new TextBlock
                {
                    Text = msg.Sender ?? "Thành viên",
                    FontSize = Math.Round(13.5 * scale, 1),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = ZaloAvatar.ColorFor(msg.Sender ?? "Z"),
                    Margin = new Thickness(2, 0, 0, 3)
                };
                senderNameBlock.Tapped += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(msg.SenderUid))
                    {
                        var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == msg.SenderUid)
                            ?? new ZaloFriend { UserId = msg.SenderUid, Name = msg.Sender, Avatar = msg.SenderAvatarUrl };
                        NavigationManager.NavigateTo(new FriendProfilePage(fr));
                    }
                };
                stack.Children.Add(senderNameBlock);
            }

            // Quoted message preview if any
            if (!string.IsNullOrWhiteSpace(msg.QuotedText))
            {
                var quoteBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x00, 0x88, 0xFF)),
                    BorderBrush = _activeBlue,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 0, 4),
                    MaxWidth = 280
                };
                var quoteStack = new StackPanel();
                quoteStack.Children.Add(new TextBlock
                {
                    Text = msg.QuotedSender ?? "Trả lời",
                    FontSize = 11,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _activeBlue
                });
                quoteStack.Children.Add(new TextBlock
                {
                    Text = msg.QuotedText,
                    FontSize = 12,
                    Foreground = _textDark,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 2
                });
                quoteBorder.Child = quoteStack;
                stack.Children.Add(quoteBorder);
            }

            bool isSticker = !isFriendshipCard && (msg.IsSticker || msg.MsgType == "chat.sticker" || msg.MsgType == "5"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.Contains("emo_") || msg.MediaUrl.Contains("sticker_"))));

            bool isVoice = !isFriendshipCard && (msg.HasVoice || msg.MsgType == "chat.voice" || msg.MsgType == "31" || msg.MsgType == "chat.audio"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.Contains("voice"))));

            bool isVideo = !isFriendshipCard && !isVoice && (msg.HasVideo || msg.MsgType == "chat.video.msg" || msg.MsgType == "chat.video" || msg.MsgType == "44" || msg.MsgType == "32"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".3gp", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.Contains("video"))));

            bool isFile = !isFriendshipCard && !isVoice && !isVideo && (msg.HasFile || msg.MsgType == "share.file" || msg.MsgType == "46" || !string.IsNullOrWhiteSpace(msg.FileName));

            bool isPhoto = !isFriendshipCard && !isSticker && !isVoice && !isVideo && !isFile && (msg.HasMedia || msg.MsgType == "chat.photo"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.StartsWith("http") || msg.MediaUrl.Contains("res-zalo") || msg.MediaUrl.Contains("photo")
                    || msg.MediaUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || msg.MediaUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))));

            if (isFriendshipCard)
            {
                var friendshipCard = CreateFriendshipGreetingCard(msg);
                stack.Children.Add(friendshipCard);
            }
            else if (isSticker)
            {
                var stickerCard = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(10),
                    MaxWidth = 280
                };
                var stickerImg = new Image
                {
                    Width = 104,
                    Height = 104,
                    Stretch = Stretch.Uniform
                };
                stickerCard.Child = stickerImg;

                var ring = new ProgressRing { Width = 32, Height = 32, Foreground = _activeBlue, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                BindMessageImage(stickerImg, ring, null, msg);

                stickerCard.PointerPressed += (s, e) => stickerCard.Opacity = 0.7;
                stickerCard.PointerReleased += (s, e) => stickerCard.Opacity = 1.0;
                stickerCard.PointerCanceled += (s, e) => stickerCard.Opacity = 1.0;
                stickerCard.Tapped += (s, e) =>
                {
                    NavigationManager.NavigateTo(new ImageViewerPage(msg.ViewerUrl ?? msg.MediaUrl));
                };
                stack.Children.Add(stickerCard);
            }
            else if (isVideo)
            {
                var videoCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B)),
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(4),
                    MaxWidth = 300
                };
                var videoGrid = new Grid { MinWidth = 160, MinHeight = 140, MaxWidth = 292, MaxHeight = 300 };

                var videoThumb = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    MaxWidth = 292,
                    MaxHeight = 300,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                videoGrid.Children.Add(videoThumb);

                var ring = new ProgressRing
                {
                    Width = 36,
                    Height = 36,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsActive = false,
                    Visibility = Visibility.Collapsed
                };
                videoGrid.Children.Add(ring);

                var playOverlay = new Image
                {
                    Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icn_csc_filevideo_play.png"),
                    Width = 56,
                    Height = 56,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                videoGrid.Children.Add(playOverlay);

                var videoBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xB3, 0x00, 0x00, 0x00)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(8)
                };
                videoBadge.Child = new TextBlock { Text = "Video", Foreground = new SolidColorBrush(Colors.White), FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
                videoGrid.Children.Add(videoBadge);

                videoCard.Child = videoGrid;

                BindMessageImage(videoThumb, ring, null, msg);

                videoCard.PointerPressed += (s, e) => videoCard.Opacity = 0.7;
                videoCard.PointerReleased += (s, e) => videoCard.Opacity = 1.0;
                videoCard.PointerCanceled += (s, e) => videoCard.Opacity = 1.0;
                videoCard.Tapped += async (s, e) =>
                {
                    try
                    {
                        string vUrl = msg.ViewerUrl ?? msg.MediaUrl;
                        if (string.IsNullOrEmpty(vUrl)) return;
                        if (Uri.TryCreate(vUrl, UriKind.Absolute, out Uri vUri))
                        {
                            await Windows.System.Launcher.LaunchUriAsync(vUri);
                        }
                    }
                    catch (Exception ex)
                    {
                        await new MessageDialog("Không thể phát video: " + ex.Message, "Video").ShowAsync();
                    }
                };
                stack.Children.Add(videoCard);
            }
            else if (isPhoto)
            {
                var photoCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xED, 0xF2, 0xF7)),
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(4),
                    MaxWidth = 300
                };
                var photoStack = new StackPanel();

                var imgGrid = new Grid { MinWidth = 140, MinHeight = 140, MaxWidth = 292, MaxHeight = 320 };
                var photoImg = new Image
                {
                    Stretch = Stretch.Uniform,
                    MaxWidth = 292,
                    MaxHeight = 320,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                imgGrid.Children.Add(photoImg);

                var ring = new ProgressRing
                {
                    Width = 36,
                    Height = 36,
                    Foreground = _activeBlue,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsActive = true
                };
                imgGrid.Children.Add(ring);

                var retryBtn = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xD0, 0x22, 0x22, 0x22)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6, 10, 6),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                var retryStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                retryStack.Children.Add(new Image { Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_photo_clean_white.png"), Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
                retryStack.Children.Add(new TextBlock { Text = "Tải lại ảnh", FontSize = 12.5, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center });
                retryBtn.Child = retryStack;
                imgGrid.Children.Add(retryBtn);

                if (msg.HasHdPhoto)
                {
                    var hdBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0xB3, 0x00, 0x00, 0x00)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(6)
                    };
                    hdBadge.Child = new TextBlock { Text = "HD", Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
                    imgGrid.Children.Add(hdBadge);
                }

                photoStack.Children.Add(imgGrid);

                bool isTextUrl = !string.IsNullOrWhiteSpace(msg.Text) &&
                    (msg.Text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || msg.Text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                    (msg.Text.Contains("res-zalo") || msg.Text.Contains("zadn.vn") || msg.Text.Contains("artboard") || msg.Text.Contains("74009") || msg.Text == msg.MediaUrl || msg.Text == msg.ViewerUrl
                     || msg.Text.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || msg.Text.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || msg.Text.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || msg.Text.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(msg.Text) && msg.Text != "[Hình ảnh]" && !isTextUrl)
                {
                    var captionBlock = new TextBlock
                    {
                        Text = msg.Text,
                        FontSize = 15,
                        Foreground = _textDark,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(6, 4, 6, 4)
                    };
                    photoStack.Children.Add(captionBlock);
                }

                photoCard.Child = photoStack;

                BindMessageImage(photoImg, ring, retryBtn, msg);
                retryBtn.Tapped += (s, e) => BindMessageImage(photoImg, ring, retryBtn, msg);

                photoCard.PointerPressed += (s, e) => photoCard.Opacity = 0.7;
                photoCard.PointerReleased += (s, e) => photoCard.Opacity = 1.0;
                photoCard.PointerCanceled += (s, e) => photoCard.Opacity = 1.0;
                photoCard.Tapped += (s, e) =>
                {
                    NavigationManager.NavigateTo(new ImageViewerPage(msg.ViewerUrl ?? msg.MediaUrl));
                };
                stack.Children.Add(photoCard);
            }
            else if (isVoice)
            {
                var voiceCard = new Border
                {
                    Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 12, 14, 12),
                    MaxWidth = 310
                };
                var voiceStack = new StackPanel { Orientation = Orientation.Horizontal };
                var playCircle = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(21),
                    Background = _activeBlue,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var playIcon = new Image
                {
                    Source = SafeAppxImage((_activeVoiceMsg == msg && _voicePlayer.CurrentState == MediaElementState.Playing)
                        ? "ms-appx:///Assets/Icons/ic_pause_clean_white.png"
                        : "ms-appx:///Assets/Icons/ic_play_clean_white.png"),
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                playCircle.Child = playIcon;
                voiceStack.Children.Add(playCircle);

                var voiceInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                voiceInfo.Children.Add(new TextBlock { Text = "Tin nhắn thoại", FontSize = 16, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
                var statusText = new TextBlock
                {
                    Text = (_activeVoiceMsg == msg && _voicePlayer.CurrentState == MediaElementState.Playing) ? "Đang phát..." : "Chạm để nghe",
                    FontSize = 13,
                    Foreground = _timeColor,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                voiceInfo.Children.Add(statusText);
                voiceStack.Children.Add(voiceInfo);
                voiceCard.Child = voiceStack;

                voiceCard.PointerPressed += (s, e) => voiceCard.Opacity = 0.7;
                voiceCard.PointerReleased += (s, e) => voiceCard.Opacity = 1.0;
                voiceCard.PointerCanceled += (s, e) => voiceCard.Opacity = 1.0;
                voiceCard.Tapped += async (s, e) =>
                {
                    if (_activeVoiceMsg == msg && _voicePlayer.CurrentState == MediaElementState.Playing)
                    {
                        _voicePlayer.Pause();
                        ResetActiveVoicePlayer();
                        return;
                    }

                    ResetActiveVoicePlayer();
                    _activeVoiceMsg = msg;
                    _activeVoicePlayIcon = playIcon;
                    _activeVoiceStatusText = statusText;
                    playIcon.Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_pause_clean_white.png");
                    statusText.Text = "Đang tải...";

                    try
                    {
                        string audioUrl = msg.MediaUrl ?? msg.ViewerUrl;
                        if (string.IsNullOrEmpty(audioUrl))
                        {
                            statusText.Text = "Không có dữ liệu âm thanh";
                            ResetActiveVoicePlayer();
                            return;
                        }

                        byte[] bytes = null;
                        if (ZaloApp.Session != null)
                        {
                            try { bytes = await ZaloApp.Session.Http.DownloadBytesAsync(audioUrl, CancellationToken.None); }
                            catch { }
                        }
                        if (bytes == null)
                        {
                            using (var hc = new System.Net.Http.HttpClient())
                            {
                                bytes = await hc.GetByteArrayAsync(audioUrl);
                            }
                        }

                        if (bytes != null && bytes.Length > 0)
                        {
                            var stream = new InMemoryRandomAccessStream();
                            using (var writer = new DataWriter(stream))
                            {
                                writer.WriteBytes(bytes);
                                await writer.StoreAsync();
                                await writer.FlushAsync();
                                writer.DetachStream();
                            }
                            stream.Seek(0);
                            string mime = "audio/mp4";
                            if (audioUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) mime = "audio/wav";
                            else if (audioUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) mime = "audio/mp3";
                            else if (audioUrl.EndsWith(".aac", StringComparison.OrdinalIgnoreCase)) mime = "audio/aac";
                            else if (audioUrl.EndsWith(".amr", StringComparison.OrdinalIgnoreCase)) mime = "audio/amr";

                            _voicePlayer.SetSource(stream, mime);
                            _voicePlayer.Play();
                            statusText.Text = "Đang phát...";
                        }
                        else
                        {
                            _voicePlayer.Source = new Uri(audioUrl);
                            _voicePlayer.Play();
                            statusText.Text = "Đang phát...";
                        }
                    }
                    catch (Exception ex)
                    {
                        statusText.Text = "Lỗi: " + ex.Message;
                        ResetActiveVoicePlayer();
                    }
                };
                stack.Children.Add(voiceCard);
            }
            else if (isFile)
            {
                var fileCard = new Border
                {
                    Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 12, 14, 12),
                    MaxWidth = 310
                };
                var fileStack = new StackPanel { Orientation = Orientation.Horizontal };
                var fileIconBox = new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF4, 0xFA)),
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                fileIconBox.Child = new Image
                {
                    Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/im_downloadfile.png"),
                    Width = 26,
                    Height = 26,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                fileStack.Children.Add(fileIconBox);

                var fileTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 210 };
                string dispName = !string.IsNullOrWhiteSpace(msg.FileName) ? msg.FileName : (!string.IsNullOrWhiteSpace(msg.Text) && msg.Text != "[Tập tin]" ? msg.Text : "Tập tin đính kèm");
                fileTextStack.Children.Add(new TextBlock { Text = dispName, FontSize = 15.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis });
                var fileStatus = new TextBlock { Text = "Chạm để tải về", FontSize = 13, Foreground = _activeBlue, Margin = new Thickness(0, 2, 0, 0) };
                fileTextStack.Children.Add(fileStatus);
                fileStack.Children.Add(fileTextStack);
                fileCard.Child = fileStack;

                fileCard.PointerPressed += (s, e) => fileCard.Opacity = 0.7;
                fileCard.PointerReleased += (s, e) => fileCard.Opacity = 1.0;
                fileCard.PointerCanceled += (s, e) => fileCard.Opacity = 1.0;
                fileCard.Tapped += async (s, e) =>
                {
                    string fUrl = msg.ViewerUrl ?? msg.MediaUrl;
                    if (string.IsNullOrEmpty(fUrl)) return;
                    fileStatus.Text = "Đang tải về…";

                    try
                    {
                        byte[] bytes = null;
                        if (ZaloApp.Session != null)
                        {
                            try { bytes = await ZaloApp.Session.Http.DownloadBytesAsync(fUrl, CancellationToken.None); }
                            catch { }
                        }
                        if (bytes == null)
                        {
                            using (var hc = new System.Net.Http.HttpClient())
                            {
                                bytes = await hc.GetByteArrayAsync(fUrl);
                            }
                        }

                        if (bytes != null && bytes.Length > 0)
                        {
                            string safeName = string.IsNullOrWhiteSpace(msg.FileName) ? "Zalo_File" : msg.FileName;
                            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                            {
                                safeName = safeName.Replace(c, '_');
                            }

                            StorageFolder folder = null;
                            try
                            {
                                folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Downloads", CreationCollisionOption.OpenIfExists);
                            }
                            catch
                            {
                                folder = ApplicationData.Current.LocalFolder;
                            }

                            StorageFile file = await folder.CreateFileAsync(safeName, CreationCollisionOption.GenerateUniqueName);
                            using (var outStream = await file.OpenStreamForWriteAsync())
                            {
                                await outStream.WriteAsync(bytes, 0, bytes.Length);
                            }

                            fileStatus.Text = "Đã tải về máy";
                            fileStatus.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0xA3, 0x4A));

                            var md = new MessageDialog($"Đã tải tệp về thiết bị:\n{file.Name}", "Tải tệp thành công");
                            md.Commands.Add(new UICommand("Mở tệp", async cmd =>
                            {
                                try { await Windows.System.Launcher.LaunchFileAsync(file); }
                                catch (Exception exLaunch) { await new MessageDialog("Không thể mở tệp: " + exLaunch.Message).ShowAsync(); }
                            }));
                            md.Commands.Add(new UICommand("Đóng"));
                            await md.ShowAsync();
                        }
                        else
                        {
                            fileStatus.Text = "Tải thất bại";
                        }
                    }
                    catch (Exception ex)
                    {
                        fileStatus.Text = "Lỗi: " + ex.Message;
                    }
                };
                stack.Children.Add(fileCard);
            }
            else if (msg.MsgType == "chat.call" || msg.MsgType == "chat.call.notify" || msg.MsgType == "chat.call_notify" || msg.MsgType == "100" || msg.MsgType == "chat.call_history"
                || (!string.IsNullOrEmpty(msg.Text) && (msg.Text.StartsWith("Cuộc gọi") || msg.Text.Contains("cuộc gọi nhỡ"))))
            {
                double scale = ZaloSettingsService.Current.UiScaleFactor;
                var callCard = new Border
                {
                    Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12, 10, 14, 10),
                    MaxWidth = Math.Round(290 * scale)
                };

                var callGrid = new Grid();
                callGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                callGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                bool isMissed = msg.Text != null && (msg.Text.Contains("nhỡ") || msg.Text.Contains("từ chối") || msg.Text.Contains("không trả lời"));
                bool isVideoCall = msg.Text != null && msg.Text.ToLower().Contains("video");

                var iconCircle = new Border
                {
                    Width = Math.Round(38 * scale),
                    Height = Math.Round(38 * scale),
                    CornerRadius = new CornerRadius(Math.Round(19 * scale)),
                    Background = isMissed
                        ? new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44))
                        : new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x88, 0xFF)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, Math.Round(10 * scale), 0)
                };

                var callIcon = new TextBlock
                {
                    Text = isVideoCall ? "\uE116" : "\uE13A",
                    FontFamily = new FontFamily("Segoe UI Symbol"),
                    FontSize = Math.Round(18 * scale),
                    Foreground = isMissed
                        ? new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44))
                        : new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconCircle.Child = callIcon;
                Grid.SetColumn(iconCircle, 0);
                callGrid.Children.Add(iconCircle);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var titleText = new TextBlock
                {
                    Text = msg.Text ?? "Cuộc gọi",
                    FontSize = Math.Round(14 * scale),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = isMissed
                        ? new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44))
                        : _textDark,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                var subtitleText = new TextBlock
                {
                    Text = "Gọi lại",
                    FontSize = Math.Round(12 * scale),
                    Foreground = _activeBlue,
                    FontWeight = Windows.UI.Text.FontWeights.Normal,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textStack.Children.Add(titleText);
                textStack.Children.Add(subtitleText);
                Grid.SetColumn(textStack, 1);
                callGrid.Children.Add(textStack);

                callCard.Child = callGrid;

                callCard.PointerPressed += (s, ev) => callCard.Opacity = 0.7;
                callCard.PointerReleased += (s, ev) => callCard.Opacity = 1.0;
                callCard.PointerCanceled += (s, ev) => callCard.Opacity = 1.0;
                callCard.Tapped += (s, ev) =>
                {
                    if (_conversation != null)
                    {
                        NavigationManager.NavigateTo(new CallPage(_conversation, isVideoCall));
                    }
                };

                stack.Children.Add(callCard);
            }
            else
            {
                double scale = ZaloSettingsService.Current.UiScaleFactor;
                var bubbleBorder = new Border
                {
                    Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 10, 14, 10),
                    MaxWidth = Math.Round(310 * scale)
                };

                double msgFontSize = Math.Round(ZaloSettingsService.Current.MessageFontSize * scale, 1);
                var textBlock = new TextBlock
                {
                    Text = msg.Text ?? string.Empty,
                    FontSize = msgFontSize,
                    Foreground = _textDark,
                    TextWrapping = TextWrapping.Wrap
                };
                bubbleBorder.Child = textBlock;

                bubbleBorder.PointerPressed += (s, e) => bubbleBorder.Opacity = 0.7;
                bubbleBorder.PointerReleased += (s, e) => bubbleBorder.Opacity = 1.0;
                bubbleBorder.PointerCanceled += (s, e) => bubbleBorder.Opacity = 1.0;
                bubbleBorder.Tapped += (s, e) =>
                {
                    var sheet = new Common.ZaloActionSheet("Tùy chọn tin nhắn");
                    sheet.AddAction("Trả lời", () => SetReplyTarget(msg));
                    sheet.AddAction("Sao chép vào ô soạn", () =>
                    {
                        _inputBox.Text = msg.Text ?? string.Empty;
                    });
                    if (isMe)
                    {
                        sheet.AddAction("Thu hồi tin nhắn", async () =>
                        {
                            await ZaloApp.RecallMessageAsync(_conversation, msg);
                            RenderMessages();
                        }, isDestructive: true);
                    }
                    sheet.AddAction("Xóa phía tôi", async () =>
                    {
                        await ZaloApp.DeleteMessageForMeAsync(_conversation, msg);
                        RenderMessages();
                    }, isDestructive: true);
                    sheet.Show();
                };

                stack.Children.Add(bubbleBorder);
            }

            // Timestamp and Status
            double uiScale = ZaloSettingsService.Current.UiScaleFactor;
            var timeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = (isFriendshipCard || isNextSameSender) ? Visibility.Collapsed : Visibility.Visible
            };

            timeStack.Children.Add(new TextBlock
            {
                Text = msg.TimeText ?? string.Empty,
                FontSize = Math.Round(11.5 * uiScale, 1),
                Foreground = _timeColor
            });

            if (isMe)
            {
                string statusText;
                SolidColorBrush statusColor;
                if (msg.ReceiptState >= 2)
                {
                    statusText = "  ✓✓ Đã xem";
                    statusColor = _activeBlue;
                }
                else if (msg.ReceiptState == 1)
                {
                    statusText = "  ✓✓ Đã nhận";
                    statusColor = _timeColor;
                }
                else
                {
                    statusText = "  ✓ Đã gửi";
                    statusColor = _timeColor;
                }

                timeStack.Children.Add(new TextBlock
                {
                    Text = statusText,
                    FontSize = Math.Round(11.5 * uiScale, 1),
                    FontWeight = msg.ReceiptState >= 2 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                    Foreground = statusColor
                });

                if (msg.ReceiptState >= 2 && !string.IsNullOrEmpty(msg.DeliveryAvatarUrl))
                {
                    Brush deliveryBrush = null;
                    if (msg.DeliveryAvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        deliveryBrush = ZaloAvatar.ImageFor(msg.DeliveryAvatarUrl);
                    }
                    double miniAvaSize = Math.Round(14 * uiScale);
                    var miniAvatar = new Ellipse
                    {
                        Width = miniAvaSize,
                        Height = miniAvaSize,
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Fill = deliveryBrush ?? _activeBlue
                    };
                    timeStack.Children.Add(miniAvatar);
                }
            }

            stack.Children.Add(timeStack);

            if (isGroupOther)
            {
                container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                double memberAvaSize = Math.Round(40 * uiScale);
                var avatarGrid = new Grid
                {
                    Width = memberAvaSize,
                    Height = memberAvaSize,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, Math.Round(8 * uiScale), 0)
                };
                var avatarEllipse = new Ellipse
                {
                    Width = memberAvaSize,
                    Height = memberAvaSize,
                    Fill = ZaloAvatar.ColorFor(msg.Sender ?? "Z")
                };
                var avatarInitial = new TextBlock
                {
                    Text = msg.SenderInitial ?? "Z",
                    FontSize = Math.Round(15 * uiScale, 1),
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                avatarGrid.Children.Add(avatarEllipse);
                avatarGrid.Children.Add(avatarInitial);

                if (!string.IsNullOrEmpty(msg.SenderAvatarUrl) && msg.SenderAvatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var brush = ZaloAvatar.ImageFor(msg.SenderAvatarUrl);
                        if (brush != null)
                        {
                            var imgEllipse = new Ellipse
                            {
                                Width = memberAvaSize,
                                Height = memberAvaSize,
                                Fill = brush
                            };
                            avatarGrid.Children.Add(imgEllipse);
                            avatarInitial.Text = string.Empty;
                        }
                    }
                    catch { }
                }

                avatarGrid.Tapped += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(msg.SenderUid))
                    {
                        var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == msg.SenderUid)
                            ?? new ZaloFriend { UserId = msg.SenderUid, Name = msg.Sender, Avatar = msg.SenderAvatarUrl };
                        NavigationManager.NavigateTo(new FriendProfilePage(fr));
                    }
                };

                Grid.SetColumn(avatarGrid, 0);
                container.Children.Add(avatarGrid);

                Grid.SetColumn(stack, 1);
                container.Children.Add(stack);
            }
            else
            {
                container.Children.Add(stack);
            }

            return container;
        }

        private FrameworkElement CreateFriendshipGreetingCard(ZaloChatMessage msg)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 14, 12, 14),
                MaxWidth = 310,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 8)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            string bannerUrl = msg.MediaUrl;
            if (string.IsNullOrEmpty(bannerUrl) && !string.IsNullOrEmpty(msg.Text) && msg.Text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                bannerUrl = msg.Text;
            }
            if (string.IsNullOrEmpty(bannerUrl))
            {
                bannerUrl = "https://res-zalo.zadn.vn/upload/media/2019/10/11/artboard_5_4_x_2x_1570759790847_74009.png";
            }

            var bannerGrid = new Grid { MaxWidth = 286, Height = 96, HorizontalAlignment = HorizontalAlignment.Center };
            var bannerImg = new Image
            {
                Stretch = Stretch.Uniform,
                MaxWidth = 286,
                MaxHeight = 96,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var ring = new ProgressRing { Width = 28, Height = 28, Foreground = _activeBlue, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            bannerGrid.Children.Add(bannerImg);
            bannerGrid.Children.Add(ring);
            stack.Children.Add(bannerGrid);

            var tempMsg = new ZaloChatMessage { MediaUrl = bannerUrl };
            BindMessageImage(bannerImg, ring, null, tempMsg);

            string partnerName = _conversation?.Name ?? "bạn bè";

            // Linked Avatars row (User avatar + Cheers + Partner avatar)
            var avatarsRow = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            };
            avatarsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            avatarsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            avatarsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // User Avatar
            var myAvatarGrid = new Grid { Width = 52, Height = 52, VerticalAlignment = VerticalAlignment.Center };
            var myEllipse = new Ellipse { Width = 52, Height = 52, Fill = _activeBlue };
            var myInitial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(ZaloApp.Session?.DisplayName ?? "Tôi"),
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            myAvatarGrid.Children.Add(myEllipse);
            myAvatarGrid.Children.Add(myInitial);
            if (!string.IsNullOrEmpty(ZaloApp.Session?.AvatarUrl))
            {
                var brush = ZaloAvatar.ImageFor(ZaloApp.Session.AvatarUrl, 80);
                if (brush != null) { myEllipse.Fill = brush; myInitial.Text = string.Empty; }
            }
            Grid.SetColumn(myAvatarGrid, 0);
            avatarsRow.Children.Add(myAvatarGrid);

            // Toast / Celebration Icon in center
            var centerToast = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xF3, 0xC7)),
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            centerToast.Child = new TextBlock
            {
                Text = "🍻",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(centerToast, 1);
            avatarsRow.Children.Add(centerToast);

            // Partner Avatar
            var partnerAvatarGrid = new Grid { Width = 52, Height = 52, VerticalAlignment = VerticalAlignment.Center };
            var partnerEllipse = new Ellipse { Width = 52, Height = 52, Fill = _conversation?.AvatarColor ?? _activeBlue };
            var partnerInitial = new TextBlock
            {
                Text = _conversation?.Initial ?? ZaloAvatar.InitialOf(partnerName),
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            partnerAvatarGrid.Children.Add(partnerEllipse);
            partnerAvatarGrid.Children.Add(partnerInitial);
            if (_conversation != null && !string.IsNullOrEmpty(_conversation.AvatarUrl))
            {
                var brush = _conversation.AvatarImageBrush ?? ZaloAvatar.ImageFor(_conversation.AvatarUrl, 80);
                if (brush != null) { partnerEllipse.Fill = brush; partnerInitial.Text = string.Empty; }
            }
            Grid.SetColumn(partnerAvatarGrid, 2);
            avatarsRow.Children.Add(partnerAvatarGrid);

            stack.Children.Add(avatarsRow);
            var titleBlock = new TextBlock
            {
                Text = $"Bạn và {partnerName} đã trở thành bạn bè",
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 10, 4, 3)
            };
            stack.Children.Add(titleBlock);

            var subBlock = new TextBlock
            {
                Text = "Chọn một sticker dưới đây để gửi lời chào",
                FontSize = 12,
                Foreground = _timeColor,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(4, 0, 4, 12)
            };
            stack.Children.Add(subBlock);

            var quickRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            string[] greetings = { "👋 Chào bạn", "🐱 Xin chào", "🐰 HI!", "🍻 Chúc mừng" };
            foreach (var g in greetings)
            {
                string textToSend = g;
                var btn = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(8, 5, 8, 5),
                    Margin = new Thickness(3, 0, 3, 0)
                };
                btn.Child = new TextBlock
                {
                    Text = textToSend,
                    FontSize = 12,
                    Foreground = _textDark,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold
                };
                btn.PointerPressed += (s, e) => btn.Opacity = 0.6;
                btn.PointerReleased += (s, e) => btn.Opacity = 1.0;
                btn.PointerCanceled += (s, e) => btn.Opacity = 1.0;
                btn.Tapped += async (s, e) =>
                {
                    await SendQuickGreetingAsync(textToSend);
                };
                quickRow.Children.Add(btn);
            }
            stack.Children.Add(quickRow);

            card.Child = stack;
            return card;
        }

        private async void BindMessageImage(Image img, ProgressRing ring, Border retryBtn, ZaloChatMessage msg)
        {
            if (msg.MediaImage != null)
            {
                img.Source = msg.MediaImage;
                if (ring != null) { ring.IsActive = false; ring.Visibility = Visibility.Collapsed; }
                return;
            }

            string url = msg.ViewerUrl ?? msg.MediaUrl;
            if (string.IsNullOrEmpty(url)) return;

            if (url.StartsWith("ms-appx:///"))
            {
                try
                {
                    var bmp = new BitmapImage(new Uri(url));
                    img.Source = bmp;
                    msg.SetDownloadedMediaImage(bmp);
                    if (ring != null) { ring.IsActive = false; ring.Visibility = Visibility.Collapsed; }
                }
                catch { }
                return;
            }

            uint hash = 5381;
            foreach (char c in url) hash = ((hash << 5) + hash) + c;
            string fileName = "img_" + hash.ToString("x8") + ".jpg";

            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                var cacheFolder = await folder.CreateFolderAsync("cache", CreationCollisionOption.OpenIfExists);
                var existingItem = await cacheFolder.TryGetItemAsync(fileName);
                if (existingItem != null)
                {
                    var cachedBmp = new BitmapImage(new Uri("ms-appdata:///local/cache/" + fileName)) { DecodePixelWidth = 520 };
                    img.Source = cachedBmp;
                    msg.SetDownloadedMediaImage(cachedBmp);
                    if (ring != null) { ring.IsActive = false; ring.Visibility = Visibility.Collapsed; }
                    return;
                }

                if (ring != null) { ring.IsActive = true; ring.Visibility = Visibility.Visible; }
                if (retryBtn != null) retryBtn.Visibility = Visibility.Collapsed;

                byte[] bytes = null;
                if (ZaloApp.Session != null)
                {
                    try
                    {
                        bytes = await ZaloApp.Session.Http.DownloadBytesAsync(url, CancellationToken.None);
                    }
                    catch { }
                }

                if (bytes == null)
                {
                    using (var hc = new System.Net.Http.HttpClient())
                    {
                        bytes = await hc.GetByteArrayAsync(url);
                    }
                }

                if (bytes != null && bytes.Length > 0)
                {
                    var file = await cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBytesAsync(file, bytes);
                    var newBmp = new BitmapImage(new Uri("ms-appdata:///local/cache/" + fileName)) { DecodePixelWidth = 520 };
                    img.Source = newBmp;
                    msg.SetDownloadedMediaImage(newBmp);
                }
                else
                {
                    throw new Exception("Empty data");
                }
            }
            catch (Exception)
            {
                if (retryBtn != null) retryBtn.Visibility = Visibility.Visible;
            }
            finally
            {
                if (ring != null) { ring.IsActive = false; ring.Visibility = Visibility.Collapsed; }
            }
        }
    }
}
