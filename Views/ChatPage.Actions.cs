using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed partial class ChatPage : Page
    {
        private FrameworkElement CreateInputBar()
        {
            var container = new StackPanel { Background = new SolidColorBrush(Colors.White) };

            // Reply Preview Bar
            _replyBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(12, 6, 8, 6),
                Visibility = Visibility.Collapsed
            };
            var replyGrid = new Grid();
            replyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            replyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var replyTextStack = new StackPanel();
            _replySenderText = new TextBlock { FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
            _replyContentText = new TextBlock { FontSize = 12.5, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis };
            replyTextStack.Children.Add(_replySenderText);
            replyTextStack.Children.Add(_replyContentText);
            Grid.SetColumn(replyTextStack, 0);
            replyGrid.Children.Add(replyTextStack);

            var closeReplyBtn = CreateTouchBorder(32, 32);
            closeReplyBtn.Child = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_close_clean_gray.png"),
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeReplyBtn.Tapped += (s, e) => SetReplyTarget(null);
            Grid.SetColumn(closeReplyBtn, 1);
            replyGrid.Children.Add(closeReplyBtn);

            _replyBar.Child = replyGrid;
            container.Children.Add(_replyBar);

            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var bar = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(Math.Round(6 * scale), Math.Round(5 * scale), Math.Round(6 * scale), Math.Round(5 * scale))
            };

            var barRoot = new Grid();

            _inputBarGrid = new Grid();
            _inputBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) }); // 0. Emoji
            _inputBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });     // 1. Text input pill
            _inputBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(38 * scale)) }); // 2. More dots
            _inputBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) }); // 3. Mic
            _inputBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) }); // 4. Photo/Send

            // Emoji Button
            var emojiBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(46 * scale));
            emojiBtn.Child = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_emoji_gray.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emojiBtn.Tapped += (s, e) => ToggleDrawer();
            Grid.SetColumn(emojiBtn, 0);
            _inputBarGrid.Children.Add(emojiBtn);

            // Composer TextBox wrapped in spacious rounded pill
            var inputPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(Math.Round(21 * scale)),
                Height = Math.Round(42 * scale),
                Padding = new Thickness(Math.Round(12 * scale), 0, Math.Round(8 * scale), 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var inputContainer = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var inputPlaceholder = new TextBlock
            {
                Text = "Tin nhắn",
                FontSize = Math.Round(16 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0x64, 0x74, 0x8B)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                IsHitTestVisible = false
            };
            inputContainer.Children.Add(inputPlaceholder);

            _inputBox = new TextBox
            {
                FontSize = Math.Round(16 * scale, 1),
                Foreground = _textDark,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2, 0, 0, 0)
            };
            App.MakeTextBoxTransparent(_inputBox);
            inputContainer.Children.Add(_inputBox);

            _inputBox.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrWhiteSpace(_inputBox.Text);
                inputPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
                _sendButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                _micButton.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
                _photoButton.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
            };
            _inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    SendMessage();
                    e.Handled = true;
                }
            };
            inputPill.Child = inputContainer;
            Grid.SetColumn(inputPill, 1);
            _inputBarGrid.Children.Add(inputPill);

            // More Options Button (...)
            var moreDotsBtn = CreateTouchBorder(Math.Round(38 * scale), Math.Round(46 * scale));
            var dotsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            double dotSize = Math.Round(4 * scale);
            dotsStack.Children.Add(new Ellipse { Width = dotSize, Height = dotSize, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(2, 0, 2, 0) });
            dotsStack.Children.Add(new Ellipse { Width = dotSize, Height = dotSize, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(2, 0, 2, 0) });
            dotsStack.Children.Add(new Ellipse { Width = dotSize, Height = dotSize, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(2, 0, 2, 0) });
            moreDotsBtn.Child = dotsStack;
            moreDotsBtn.Tapped += (s, e) =>
            {
                var sheet = new Common.ZaloActionSheet("Tùy chọn đính kèm");
                sheet.AddAction("Gửi ảnh từ liên kết URL", async () => await ShowSendUrlPhotoDialog());
                sheet.AddAction("Chia sẻ danh thiếp", async () => await ShowShareContactDialog());
                sheet.Show();
            };
            Grid.SetColumn(moreDotsBtn, 2);
            _inputBarGrid.Children.Add(moreDotsBtn);

            // Microphone Button
            _micButton = CreateTouchBorder(Math.Round(44 * scale), Math.Round(46 * scale));
            var micImg = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/ic_fotohavevoice.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _micButton.Child = micImg;
            _micButton.Tapped += (s, e) => StartVoiceRecording();
            Grid.SetColumn(_micButton, 3);
            _inputBarGrid.Children.Add(_micButton);

            // Photo Button
            _photoButton = CreateTouchBorder(Math.Round(44 * scale), Math.Round(46 * scale));
            var photoImg = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icn_camera_gallery.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _photoButton.Child = photoImg;
            _photoButton.Tapped += (s, e) =>
            {
                var sheet = new Common.ZaloActionSheet("Gửi hình ảnh");
                sheet.AddAction("Chụp ảnh từ Máy ảnh", () =>
                {
                    if (_conversation != null)
                    {
                        NavigationManager.NavigateTo(new CameraCapturePage(_conversation.Key));
                    }
                });
                sheet.AddAction("Chọn ảnh từ Thư viện", () => PickPhoto());
                sheet.Show();
            };
            Grid.SetColumn(_photoButton, 4);
            _inputBarGrid.Children.Add(_photoButton);

            // Send Button
            _sendButton = CreateTouchBorder(Math.Round(44 * scale), Math.Round(46 * scale));
            _sendButton.Visibility = Visibility.Collapsed;
            double sendCircleSize = Math.Round(38 * scale);
            var sendCircle = new Border
            {
                Width = sendCircleSize,
                Height = sendCircleSize,
                CornerRadius = new CornerRadius(Math.Round(sendCircleSize / 2)),
                Background = _activeBlue
            };
            sendCircle.Child = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/btn_send_white.png"),
                Width = Math.Round(22 * scale),
                Height = Math.Round(22 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sendButton.Child = sendCircle;
            _sendButton.Tapped += (s, e) => SendMessage();
            Grid.SetColumn(_sendButton, 4);
            _inputBarGrid.Children.Add(_sendButton);

            barRoot.Children.Add(_inputBarGrid);

            // Real Voice Recording Bar
            _recordingBarGrid = new Grid { Visibility = Visibility.Collapsed };
            _recordingBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            _recordingBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _recordingBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _recordingBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var redDot = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x2D, 0x20)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(redDot, 0);
            _recordingBarGrid.Children.Add(redDot);

            _recordingTimeText = new TextBlock
            {
                Text = "00:00 · Đang ghi âm...",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x2D, 0x20)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(_recordingTimeText, 1);
            _recordingBarGrid.Children.Add(_recordingTimeText);

            var cancelRecBtn = CreateTouchBorder(60, 42);
            cancelRecBtn.Child = new TextBlock
            {
                Text = "Hủy",
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x75, 0x75, 0x75)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            cancelRecBtn.Tapped += (s, e) => CancelVoiceRecording();
            Grid.SetColumn(cancelRecBtn, 2);
            _recordingBarGrid.Children.Add(cancelRecBtn);

            var sendRecBtn = CreateTouchBorder(44, 42);
            var recCircle = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = _activeBlue
            };
            recCircle.Child = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/btn_send_white.png"),
                Width = 17,
                Height = 17,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sendRecBtn.Child = recCircle;
            sendRecBtn.Tapped += (s, e) => FinishAndSendVoiceRecording();
            Grid.SetColumn(sendRecBtn, 3);
            _recordingBarGrid.Children.Add(sendRecBtn);

            barRoot.Children.Add(_recordingBarGrid);

            // Group Posting Restricted Notice
            _groupPostingNotice = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Visibility = Visibility.Collapsed
            };
            var noticeGrid = new Grid();
            noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            noticeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _groupPostingNoticeText = new TextBlock
            {
                Text = "Chỉ trưởng và phó nhóm được gửi tin nhắn trong nhóm.",
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x47, 0x55, 0x69)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(_groupPostingNoticeText, 0);
            noticeGrid.Children.Add(_groupPostingNoticeText);

            _groupPostingNoticeBtn = CreateTouchBorder(74, 36);
            _groupPostingNoticeBtn.Background = _activeBlue;
            _groupPostingNoticeBtn.CornerRadius = new CornerRadius(6);
            var noticeBtnText = new TextBlock
            {
                Text = "Thông tin",
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _groupPostingNoticeBtn.Child = noticeBtnText;
            _groupPostingNoticeBtn.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    NavigationManager.NavigateTo(new GroupInfoPage(_conversation.Key));
                }
            };
            Grid.SetColumn(_groupPostingNoticeBtn, 1);
            noticeGrid.Children.Add(_groupPostingNoticeBtn);
            _groupPostingNotice.Child = noticeGrid;
            barRoot.Children.Add(_groupPostingNotice);

            bar.Child = barRoot;
            container.Children.Add(bar);
            return container;
        }

        private void SetReplyTarget(ZaloChatMessage msg)
        {
            _replyTarget = msg;
            if (_replyBar != null)
            {
                if (msg != null)
                {
                    _replySenderText.Text = "Trả lời " + (msg.FromMe ? "chính mình" : (msg.Sender ?? "Bạn"));
                    _replyContentText.Text = msg.Text ?? (msg.HasMedia ? "[Hình ảnh]" : "[Nội dung]");
                    _replyBar.Visibility = Visibility.Visible;
                    _inputBox.Focus(FocusState.Programmatic);
                }
                else
                {
                    _replyBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void SendMessage()
        {
            string text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _conversation == null || _isSendingText) return;

            _isSendingText = true;
            _inputBox.Text = string.Empty;
            if (_isDrawerOpen) ToggleDrawer();

            try
            {
                if (_replyTarget != null)
                {
                    var target = _replyTarget;
                    SetReplyTarget(null);
                    await ZaloApp.SendAsync(_conversation, text, target);
                }
                else
                {
                    await ZaloApp.SendAsync(_conversation, text);
                }
            }
            catch { }
            finally
            {
                _isSendingText = false;
            }
        }

        private async System.Threading.Tasks.Task SendQuickGreetingAsync(string text)
        {
            if (_conversation == null || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                await ZaloApp.SendAsync(_conversation, text);
            }
            catch { }
        }

        private void UpdateGroupPostingState()
        {
            if (_conversation == null || !_conversation.IsGroup)
            {
                if (_groupPostingNotice != null) _groupPostingNotice.Visibility = Visibility.Collapsed;
                if (_inputBarGrid != null) _inputBarGrid.Visibility = Visibility.Visible;
                if (_inputBox != null) _inputBox.IsEnabled = true;
                return;
            }

            bool restricted = _conversation.GroupSettingsLoaded && _conversation.GroupPostingRestricted;
            if (_groupPostingNotice != null)
            {
                _groupPostingNotice.Visibility = restricted ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_inputBarGrid != null)
            {
                _inputBarGrid.Visibility = restricted ? Visibility.Collapsed : Visibility.Visible;
            }
            if (_inputBox != null)
            {
                _inputBox.IsEnabled = !restricted;
            }
        }

        private async System.Threading.Tasks.Task ResolveSenderAvatarsAsync()
        {
            if (_senderAvatarResolveRunning)
            {
                _senderAvatarResolveRequested = true;
                return;
            }
            _senderAvatarResolveRunning = true;
            try
            {
                do
                {
                    _senderAvatarResolveRequested = false;
                    if (_conversation == null || !_conversation.IsGroup || ZaloApp.Session == null) return;

                    var incoming = _conversation.Messages.Where(m => !m.FromMe && !string.IsNullOrWhiteSpace(m.SenderUid)).ToList();
                    if (incoming.Count == 0) return;

                    var friendAvatars = (ZaloApp.Friends ?? new System.Collections.Generic.List<ZaloFriend>())
                        .Where(friend => !string.IsNullOrWhiteSpace(friend.UserId) && !string.IsNullOrWhiteSpace(friend.Avatar))
                        .GroupBy(friend => friend.UserId)
                        .ToDictionary(group => group.Key, group => group.First().Avatar, StringComparer.Ordinal);

                    foreach (var message in incoming)
                    {
                        if (!string.IsNullOrWhiteSpace(message.SenderAvatarUrl)) continue;
                        if (friendAvatars.TryGetValue(message.SenderUid, out string avatar))
                        {
                            message.SenderAvatarUrl = avatar;
                        }
                    }

                    var missingIds = incoming.Where(message => string.IsNullOrWhiteSpace(message.SenderAvatarUrl))
                        .Select(message => message.SenderUid)
                        .Where(id => !_senderAvatarLookupsAttempted.Contains(id)).Take(50).ToList();

                    if (missingIds.Count == 0) return;
                    foreach (string id in missingIds) _senderAvatarLookupsAttempted.Add(id);

                    var profiles = await ZaloAuth.GetGroupMemberProfilesAsync(ZaloApp.Session, missingIds, CancellationToken.None);
                    if (profiles == null || profiles.Count == 0) return;

                    foreach (var profile in profiles.Values)
                    {
                        if (string.IsNullOrWhiteSpace(profile.UserId)) continue;
                        foreach (var message in incoming.Where(m => m.SenderUid == profile.UserId))
                        {
                            if (!string.IsNullOrWhiteSpace(profile.Avatar) && string.IsNullOrWhiteSpace(message.SenderAvatarUrl))
                            {
                                message.SenderAvatarUrl = profile.Avatar;
                            }
                            if (!string.IsNullOrWhiteSpace(profile.DisplayName) && string.IsNullOrWhiteSpace(message.Sender))
                            {
                                message.Sender = profile.DisplayName;
                            }
                        }
                        if (_conversation != null)
                        {
                            _conversation.SetGroupMember(profile.UserId, profile.DisplayName, profile.Avatar, false, false);
                        }
                    }

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        RequestRenderMessages();
                    });
                }
                while (_senderAvatarResolveRequested);
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage ResolveSenderAvatars error: " + ex.Message);
            }
            finally
            {
                _senderAvatarResolveRunning = false;
                if (_senderAvatarResolveRequested) _ = ResolveSenderAvatarsAsync();
            }
        }
    }
}
