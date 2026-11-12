using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed partial class ChatPage : Page, IFileOpenPickerContinuable
    {
        private Grid _rootGrid;
        private FrameworkElement _headerGrid;
        private double _keyboardHeight = 0;
        private InputPane _inputPane;
        private Border _groupPostingNotice;
        private TextBlock _groupPostingNoticeText;
        private Border _groupPostingNoticeBtn;
        private DispatcherTimer _renderDebounceTimer;
        private readonly System.Collections.Generic.HashSet<string> _senderAvatarLookupsAttempted = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _senderAvatarResolveRunning = false;
        private bool _senderAvatarResolveRequested = false;

        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0x86, 0xF9));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));
        private readonly SolidColorBrush _bubbleMeBg = new SolidColorBrush(Color.FromArgb(0xFF, 0xD8, 0xEC, 0xFF));
        private readonly SolidColorBrush _bubblePartnerBg = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush _bubblePartnerBorder = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF2, 0xF6));
        private readonly SolidColorBrush _timeColor = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x92, 0x9A));

        private ZaloConversation _conversation;
        private ScrollViewer _messageScroll;
        private StackPanel _messageStack;
        private TextBox _inputBox;
        private Border _sendButton;
        private Border _micButton;
        private Border _photoButton;
        private TextBlock _titleBlock;
        private TextBlock _subtitleBlock;
        private Ellipse _recipientAvatar;
        private TextBlock _recipientInitial;

        private Grid _inputBarGrid;
        private Grid _recordingBarGrid;
        private TextBlock _recordingTimeText;
        private DispatcherTimer _recordingTimer;
        private int _recordingSeconds = 0;
        private bool _isRecordingVoice = false;

        private FrameworkElement _drawerContainer;
        private bool _isDrawerOpen = false;

        private Border _replyBar;
        private TextBlock _replySenderText;
        private TextBlock _replyContentText;
        private ZaloChatMessage _replyTarget;

        private MediaElement _voicePlayer;
        private ZaloChatMessage _activeVoiceMsg;
        private Image _activeVoicePlayIcon;
        private TextBlock _activeVoiceStatusText;

        private Border _strangerBanner;
        private Border _audioCallBtn;
        private Border _videoCallBtn;
        private ColumnDefinition _audioCallCol;
        private ColumnDefinition _videoCallCol;
        private bool _isSendingText = false;
        private bool _isRenderingSubscribed = false;

        public ChatPage(string conversationKey = null)
        {
            BuildUI();
            LayoutUpdated += (s, e) => PinHeaderToTop();
            _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _recordingTimer.Tick += (s, e) =>
            {
                _recordingSeconds++;
                int m = _recordingSeconds / 60;
                int sec = _recordingSeconds % 60;
                if (_recordingTimeText != null)
                {
                    _recordingTimeText.Text = string.Format("{0:D2}:{1:D2} · Đang ghi âm...", m, sec);
                }
            };
            if (!string.IsNullOrEmpty(conversationKey))
            {
                InitializeChat(conversationKey);
            }
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            _rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEB, 0xF3))
            };
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(76 * scale)) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient: [Back] [Avatar] [Name] [Audio] [Video] [More])
            _headerGrid = CreateTopHeader();
            Canvas.SetZIndex(_headerGrid, 9999);
            Grid.SetRow(_headerGrid, 0);
            _rootGrid.Children.Add(_headerGrid);

            // 1b. STRANGER BANNER
            _strangerBanner = CreateStrangerBanner();
            Grid.SetRow(_strangerBanner, 1);
            _rootGrid.Children.Add(_strangerBanner);

            // 2. MESSAGE LIST CANVAS (ScrollViewer with StackPanel for Silky Touch & Zero Highlight)
            _messageScroll = new ScrollViewer
            {
                Padding = new Thickness(12, 12, 12, 12),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEB, 0xF3))
            };
            _messageStack = new StackPanel();
            _messageScroll.Content = _messageStack;
            Grid.SetRow(_messageScroll, 2);
            _rootGrid.Children.Add(_messageScroll);

            // 3. INPUT BAR
            var inputBar = CreateInputBar();
            Grid.SetRow(inputBar, 3);
            _rootGrid.Children.Add(inputBar);

            // 4. EMOJI & STICKER DRAWER
            _drawerContainer = CreateStickerDrawer();
            Grid.SetRow(_drawerContainer, 4);
            _rootGrid.Children.Add(_drawerContainer);

            // Global Voice Audio Player
            _voicePlayer = new MediaElement { AutoPlay = true, Width = 0, Height = 0, Visibility = Visibility.Collapsed };
            _voicePlayer.MediaEnded += (s, e) => ResetActiveVoicePlayer();
            _voicePlayer.MediaFailed += (s, e) =>
            {
                if (_activeVoiceStatusText != null) _activeVoiceStatusText.Text = "Không thể phát âm thanh";
                ResetActiveVoicePlayer();
            };
            _voicePlayer.CurrentStateChanged += (s, e) =>
            {
                if (_voicePlayer.CurrentState == MediaElementState.Paused || _voicePlayer.CurrentState == MediaElementState.Stopped)
                {
                    ResetActiveVoicePlayer();
                }
            };
            _rootGrid.Children.Add(_voicePlayer);

            this.Content = _rootGrid;
        }

        private FrameworkElement CreateTopHeader()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var headerBorder = new Border
            {
                Padding = new Thickness(Math.Round(4 * scale), Math.Round(24 * scale), Math.Round(4 * scale), Math.Round(6 * scale))
            };
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerBorder.Background = gradient;

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(42 * scale)) }); // 0: Back
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) }); // 1: Avatar
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });     // 2: Title
            _audioCallCol = new ColumnDefinition { Width = new GridLength(Math.Round(42 * scale)) };
            _videoCallCol = new ColumnDefinition { Width = new GridLength(Math.Round(42 * scale)) };
            header.ColumnDefinitions.Add(_audioCallCol); // 3: Audio Call
            header.ColumnDefinitions.Add(_videoCallCol); // 4: Video Call
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(40 * scale)) }); // 5: More Options

            // Back Button
            var backBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(46 * scale));
            var backIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_back_clean_white.png"),
                Width = Math.Round(26 * scale),
                Height = Math.Round(26 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Child = backIcon;
            backBtn.Tapped += (s, e) =>
            {
                if (NavigationManager.CanGoBack)
                    NavigationManager.GoBack();
                else
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            // Recipient Avatar
            double avaSize = Math.Round(40 * scale);
            var avatarGrid = new Grid { Width = avaSize, Height = avaSize, VerticalAlignment = VerticalAlignment.Center };
            _recipientAvatar = new Ellipse { Width = avaSize, Height = avaSize, Fill = _activeBlue };
            _recipientInitial = new TextBlock
            {
                Text = "Z",
                FontSize = Math.Round(17 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_recipientAvatar);
            avatarGrid.Children.Add(_recipientInitial);
            avatarGrid.Tapped += (s, e) => OpenTargetProfile();
            Grid.SetColumn(avatarGrid, 1);
            header.Children.Add(avatarGrid);

            // Recipient Title & Subtitle Stack
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Math.Round(6 * scale), 0, 0, 0) };
            _titleBlock = new TextBlock
            {
                Text = "Trò chuyện",
                FontSize = Math.Round(17 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _subtitleBlock = new TextBlock
            {
                Text = string.Empty,
                FontSize = Math.Round(12 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Collapsed
            };
            titleStack.Children.Add(_titleBlock);
            titleStack.Children.Add(_subtitleBlock);
            titleStack.Tapped += (s, e) => OpenTargetProfile();
            Grid.SetColumn(titleStack, 2);
            header.Children.Add(titleStack);

            // Audio Call Button
            _audioCallBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(46 * scale));
            var audioIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_call_clean_white.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _audioCallBtn.Child = audioIcon;
            _audioCallBtn.Tapped += async (s, e) => await PromptCallOptionAsync(false);
            Grid.SetColumn(_audioCallBtn, 3);
            header.Children.Add(_audioCallBtn);

            // Video Call Button
            _videoCallBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(46 * scale));
            var videoIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_video_clean_white.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _videoCallBtn.Child = videoIcon;
            _videoCallBtn.Tapped += async (s, e) => await PromptCallOptionAsync(true);
            Grid.SetColumn(_videoCallBtn, 4);
            header.Children.Add(_videoCallBtn);

            // More Options Button
            var moreBtn = CreateTouchBorder(Math.Round(40 * scale), Math.Round(46 * scale));
            var moreIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icn_header_menu_more_white.png"),
                Width = Math.Round(26 * scale),
                Height = Math.Round(26 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            moreBtn.Child = moreIcon;
            moreBtn.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    if (_conversation.IsGroup)
                        NavigationManager.NavigateTo(new GroupInfoPage(_conversation.Key));
                    else
                        NavigationManager.NavigateTo(new ConversationOptionsPage(_conversation));
                }
            };
            Grid.SetColumn(moreBtn, 5);
            header.Children.Add(moreBtn);

            headerBorder.Child = header;
            return headerBorder;
        }

        private Border CreateStrangerBanner()
        {
            var banner = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xED, 0xF2)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Height = 50,
                Padding = new Thickness(14, 0, 14, 0),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_add_user_clean_blue.png"),
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Người lạ muốn trò chuyện · Chạm để kết bạn",
                FontSize = 13.5,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            var addBtn = new Button
            {
                Content = "Kết bạn",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Padding = new Thickness(12, 4, 12, 4),
                BorderThickness = new Thickness(0)
            };
            addBtn.Click += async (s, e) =>
            {
                if (_conversation == null) return;
                addBtn.IsEnabled = false;
                try
                {
                    if (ZaloApp.Session != null)
                    {
                        await ZaloAuth.SendFriendRequestAsync(ZaloApp.Session, _conversation.ThreadId,
                            "Xin chào, mình muốn kết bạn với bạn trên Zalo.", CancellationToken.None);
                    }
                    await new MessageDialog("Đã gửi yêu cầu kết bạn thành công.", "Thông báo").ShowAsync();
                    banner.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    await new MessageDialog("Không thể kết bạn: " + ex.Message, "Thông báo").ShowAsync();
                    addBtn.IsEnabled = true;
                }
            };
            Grid.SetColumn(addBtn, 1);
            grid.Children.Add(addBtn);

            banner.Child = grid;
            return banner;
        }

        private void OpenTargetProfile()
        {
            if (_conversation == null) return;
            if (_conversation.IsGroup)
            {
                NavigationManager.NavigateTo(new GroupInfoPage(_conversation.Key));
            }
            else
            {
                var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == _conversation.ThreadId)
                    ?? new ZaloFriend { UserId = _conversation.ThreadId, Name = _conversation.Name, Avatar = _conversation.AvatarUrl };
                NavigationManager.NavigateTo(new FriendProfilePage(fr));
            }
        }

        private void InitializeChat(string conversationKey)
        {
            if (string.IsNullOrEmpty(conversationKey) && ZaloApp.Conversations.Count > 0)
            {
                _conversation = ZaloApp.Conversations[0];
            }
            else
            {
                _conversation = ZaloApp.Conversations.FirstOrDefault(c => c.Key == conversationKey);
                if (_conversation == null && !string.IsNullOrEmpty(conversationKey))
                {
                    _conversation = ZaloApp.GetOrCreate(conversationKey, false, true);
                    var fr = ZaloApp.Friends.FirstOrDefault(f => f.UserId == conversationKey);
                    if (fr != null)
                    {
                        if (fr.Name.Length > 0) _conversation.Name = fr.Name;
                        if (!string.IsNullOrEmpty(fr.Avatar)) _conversation.AvatarUrl = fr.Avatar;
                    }
                }
            }

            if (_conversation != null)
            {
                UpdateHeaderAvatar();

                if (_subtitleBlock != null)
                {
                    if (_conversation.IsGroup)
                    {
                        int count = _conversation.GroupMemberCount > 0 ? _conversation.GroupMemberCount : _conversation.GroupMembers.Count;
                        _subtitleBlock.Text = count > 0 ? $"{count} thành viên" : "Nhóm trò chuyện";
                        _subtitleBlock.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        _ = RefreshFriendSubtitleAsync(_conversation.ThreadId);
                    }
                }

                if (_strangerBanner != null)
                {
                    bool isStranger = !_conversation.IsGroup && ZaloApp.FriendsLoaded && !ZaloApp.IsFriend(_conversation.ThreadId);
                    _strangerBanner.Visibility = isStranger ? Visibility.Visible : Visibility.Collapsed;
                }

                ZaloApp.CurrentChat = _conversation;

                string wp = ZaloSettingsService.Current.GetConversationWallpaper(ZaloApp.Session?.Uid, _conversation.Key);
                if (!string.IsNullOrEmpty(wp))
                {
                    try
                    {
                        _messageScroll.Background = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(wp)),
                            Stretch = Stretch.UniformToFill
                        };
                    }
                    catch { }
                }

                UpdateGroupPostingState();

                bool isCloud = !string.IsNullOrEmpty(conversationKey) && conversationKey == ZaloApp.Session?.Uid;
                if (isCloud)
                {
                    _titleBlock.Text = "Cloud của tôi";
                    _recipientInitial.Text = string.Empty;
                    _recipientAvatar.Fill = new ImageBrush { ImageSource = SafeAppxImage("ms-appx:///Assets/Icons/ic_cloud_blue.png") };
                    if (_subtitleBlock != null)
                    {
                        _subtitleBlock.Text = "Lưu trữ cá nhân";
                        _subtitleBlock.Visibility = Visibility.Visible;
                    }
                    if (_audioCallBtn != null) _audioCallBtn.Visibility = Visibility.Collapsed;
                    if (_videoCallBtn != null) _videoCallBtn.Visibility = Visibility.Collapsed;
                    if (_audioCallCol != null) _audioCallCol.Width = new GridLength(0);
                    if (_videoCallCol != null) _videoCallCol.Width = new GridLength(0);
                }

                RenderMessages();
                ScrollToBottom(instant: true);

                if (_conversation.IsGroup)
                {
                    _ = EnsureGroupMembersLoadedAsync();
                }
                else
                {
                    _ = RefreshFriendSubtitleAsync(_conversation.ThreadId);
                }
                _ = LoadHistoryCoreAsync();
                _ = ZaloApp.MarkConversationSeenAsync(_conversation);

                _conversation.PropertyChanged -= Conversation_PropertyChanged;
                _conversation.PropertyChanged += Conversation_PropertyChanged;
                _conversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                _conversation.Messages.CollectionChanged += Messages_CollectionChanged;
                ZaloApp.StatusChanged -= ZaloApp_StatusChanged;
                ZaloApp.StatusChanged += ZaloApp_StatusChanged;
            }
        }

        private void UpdateHeaderAvatar()
        {
            if (_conversation == null) return;

            bool isCloud = !string.IsNullOrEmpty(_conversation.Key) && _conversation.Key == ZaloApp.Session?.Uid;
            if (isCloud)
            {
                _titleBlock.Text = "Cloud của tôi";
                _recipientInitial.Text = string.Empty;
                _recipientAvatar.Fill = new ImageBrush { ImageSource = SafeAppxImage("ms-appx:///Assets/Icons/ic_cloud_blue.png") };
                return;
            }

            if (!_conversation.IsGroup && !string.IsNullOrEmpty(_conversation.ThreadId))
            {
                var fr = ZaloApp.Friends?.FirstOrDefault(f => f.UserId == _conversation.ThreadId);
                if (fr != null)
                {
                    if (string.IsNullOrEmpty(_conversation.Name) || _conversation.Name == _conversation.ThreadId)
                    {
                        _conversation.Name = fr.DisplayName ?? fr.Name;
                    }
                    if (string.IsNullOrEmpty(_conversation.AvatarUrl) && !string.IsNullOrEmpty(fr.Avatar))
                    {
                        _conversation.AvatarUrl = fr.Avatar;
                    }
                }
                else if (string.IsNullOrEmpty(_conversation.AvatarUrl) && ZaloApp.Session != null)
                {
                    _ = LoadMissingPartnerProfileAsync(_conversation.ThreadId);
                }
            }

            _titleBlock.Text = _conversation.Name ?? "Trò chuyện";
            _recipientInitial.Text = _conversation.Initial ?? "Z";
            _recipientAvatar.Fill = _conversation.AvatarColor ?? _activeBlue;

            var brush = _conversation.AvatarImageBrush ?? ZaloAvatar.ImageFor(_conversation.AvatarUrl);
            if (brush != null)
            {
                _recipientAvatar.Fill = brush;
                _recipientInitial.Text = string.Empty;
            }
        }

        private async System.Threading.Tasks.Task LoadMissingPartnerProfileAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId) || ZaloApp.Session == null) return;
            try
            {
                var profile = await ZaloAuth.GetFriendProfileAsync(ZaloApp.Session, userId, System.Threading.CancellationToken.None);
                if (profile != null)
                {
                    string name = ZaloAuth.Str(profile, "display_name");
                    if (string.IsNullOrEmpty(name)) name = ZaloAuth.Str(profile, "displayName");
                    if (string.IsNullOrEmpty(name)) name = ZaloAuth.Str(profile, "zalo_name");

                    string avatar = ZaloAuth.Str(profile, "avatar");
                    if (string.IsNullOrEmpty(avatar)) avatar = ZaloAuth.Str(profile, "avatar_120");
                    if (string.IsNullOrEmpty(avatar)) avatar = ZaloAuth.Str(profile, "avatar_240");
                    if (string.IsNullOrEmpty(avatar)) avatar = ZaloAuth.Str(profile, "avatar_standard");
                    if (string.IsNullOrEmpty(avatar)) avatar = ZaloAuth.Str(profile, "thumb_avatar");
                    if (string.IsNullOrEmpty(avatar)) avatar = ZaloAuth.Str(profile, "global_avatar");

                    if (!string.IsNullOrEmpty(avatar))
                    {
                        avatar = avatar.Trim();
                        if (avatar.StartsWith("//", StringComparison.Ordinal)) avatar = "https:" + avatar;
                    }

                    bool changed = false;
                    if (!string.IsNullOrEmpty(name) && (_conversation.Name == _conversation.ThreadId || string.IsNullOrEmpty(_conversation.Name)))
                    {
                        _conversation.Name = name;
                        changed = true;
                    }
                    if (!string.IsNullOrEmpty(avatar) && string.IsNullOrEmpty(_conversation.AvatarUrl))
                    {
                        _conversation.AvatarUrl = avatar;
                        changed = true;
                    }

                    if (changed)
                    {
                        var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            UpdateHeaderAvatar();
                        });
                    }
                }
            }
            catch { }
        }

        private async System.Threading.Tasks.Task RefreshFriendSubtitleAsync(string userId)
        {
            if (_conversation == null || _conversation.IsGroup || string.IsNullOrEmpty(userId)) return;
            try
            {
                if (ZaloApp.IsFriendOnline(userId))
                {
                    if (_subtitleBlock != null)
                    {
                        _subtitleBlock.Text = "Đang hoạt động";
                        _subtitleBlock.Visibility = Visibility.Visible;
                    }
                    return;
                }

                var fr = ZaloApp.Friends?.FirstOrDefault(f => f.UserId == userId);
                if (fr != null && fr.LastActionTime > 0)
                {
                    if (_subtitleBlock != null)
                    {
                        _subtitleBlock.Text = FormatLastOnline(UnixTime.FromMilliseconds(fr.LastActionTime));
                        _subtitleBlock.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    if (_subtitleBlock != null)
                    {
                        _subtitleBlock.Text = "Vừa mới truy cập";
                        _subtitleBlock.Visibility = Visibility.Visible;
                    }
                }

                if (ZaloApp.Session != null)
                {
                    DateTimeOffset? lastOnline = await ZaloAuth.GetFriendLastOnlineAsync(
                        ZaloApp.Session, userId, System.Threading.CancellationToken.None);
                    if (_conversation == null || _conversation.IsGroup || _conversation.ThreadId != userId) return;

                    if (ZaloApp.IsFriendOnline(userId))
                    {
                        if (_subtitleBlock != null)
                        {
                            _subtitleBlock.Text = "Đang hoạt động";
                            _subtitleBlock.Visibility = Visibility.Visible;
                        }
                    }
                    else if (lastOnline.HasValue)
                    {
                        if (_subtitleBlock != null)
                        {
                            _subtitleBlock.Text = FormatLastOnline(lastOnline.Value);
                            _subtitleBlock.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage RefreshFriendSubtitle error: " + ex.Message);
            }
        }

        private static string FormatLastOnline(DateTimeOffset timestamp)
        {
            TimeSpan age = DateTimeOffset.Now - timestamp;
            if (age.TotalMinutes < 3) return "Vừa mới truy cập";
            if (age.TotalHours < 1) return "Truy cập " + Math.Max(1, (int)age.TotalMinutes) + " phút trước";
            if (age.TotalDays < 1) return "Truy cập " + Math.Max(1, (int)age.TotalHours) + " giờ trước";
            if (age.TotalDays < 2) return "Truy cập hôm qua lúc " + timestamp.ToLocalTime().ToString("HH:mm");
            return "Truy cập " + timestamp.ToLocalTime().ToString("dd/MM lúc HH:mm");
        }

        private void Conversation_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "GroupSettingsLoaded" || e.PropertyName == "GroupPostingRestricted")
            {
                var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateGroupPostingState();
                });
            }
            else if (e.PropertyName == "AvatarImageBrush" || e.PropertyName == "AvatarUrl" || e.PropertyName == "Name")
            {
                var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    UpdateHeaderAvatar();
                });
            }
        }

        private async System.Threading.Tasks.Task EnsureGroupMembersLoadedAsync()
        {
            if (_conversation == null || !_conversation.IsGroup) return;
            try
            {
                await ZaloApp.LoadGroupMembersAsync(_conversation, System.Threading.CancellationToken.None);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (_subtitleBlock != null && _conversation != null && _conversation.IsGroup)
                    {
                        int count = _conversation.GroupMemberCount > 0 ? _conversation.GroupMemberCount : _conversation.GroupMembers.Count;
                        _subtitleBlock.Text = count > 0 ? $"{count} thành viên" : "Nhóm trò chuyện";
                    }
                    UpdateGroupPostingState();
                    RequestRenderMessages();
                });
                _ = ResolveSenderAvatarsAsync();
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage EnsureGroupMembersLoaded error: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadHistoryCoreAsync()
        {
            if (_conversation == null) return;
            try
            {
                await ZaloApp.LoadHistoryAsync(_conversation, System.Threading.CancellationToken.None);
                if (_conversation.IsGroup)
                {
                    foreach (var m in _conversation.Messages)
                    {
                        _conversation.ResolveMemberForMessage(m);
                    }
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (_subtitleBlock != null && _conversation != null && _conversation.IsGroup)
                    {
                        int count = _conversation.GroupMemberCount > 0 ? _conversation.GroupMemberCount : _conversation.GroupMembers.Count;
                        _subtitleBlock.Text = count > 0 ? $"{count} thành viên" : "Nhóm trò chuyện";
                    }
                    UpdateGroupPostingState();
                    RequestRenderMessages();
                });
                if (_conversation.IsGroup)
                {
                    _ = ResolveSenderAvatarsAsync();
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage LoadHistory error: " + ex.Message);
            }
        }

        private async System.Threading.Tasks.Task LoadOlderHistoryCoreAsync()
        {
            if (_conversation == null) return;
            try
            {
                await ZaloApp.LoadOlderGroupHistoryAsync(_conversation, System.Threading.CancellationToken.None);
                if (_conversation.IsGroup)
                {
                    foreach (var m in _conversation.Messages)
                    {
                        _conversation.ResolveMemberForMessage(m);
                    }
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (_subtitleBlock != null && _conversation != null && _conversation.IsGroup)
                    {
                        int count = _conversation.GroupMemberCount > 0 ? _conversation.GroupMemberCount : _conversation.GroupMembers.Count;
                        _subtitleBlock.Text = count > 0 ? $"{count} thành viên" : "Nhóm trò chuyện";
                    }
                    UpdateGroupPostingState();
                    RequestRenderMessages();
                });
                if (_conversation.IsGroup)
                {
                    _ = ResolveSenderAvatarsAsync();
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ChatPage LoadOlderHistory error: " + ex.Message);
            }
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RequestRenderMessages();
                if (_conversation != null && _conversation.IsGroup)
                {
                    _ = ResolveSenderAvatarsAsync();
                }
            });
        }

        private void ZaloApp_StatusChanged()
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RequestRenderMessages();
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            try
            {
                _inputPane = InputPane.GetForCurrentView();
                _inputPane.Showing -= InputPane_Showing;
                _inputPane.Hiding -= InputPane_Hiding;
                _inputPane.Showing += InputPane_Showing;
                _inputPane.Hiding += InputPane_Hiding;
            }
            catch { }

            ZaloAvatar.AvatarUpdated -= OnAvatarUpdated;
            ZaloAvatar.AvatarUpdated += OnAvatarUpdated;

            if (e.Parameter is Tuple<string, string> hit)
            {
                InitializeChat(hit.Item1);
            }
            else
            {
                InitializeChat(e.Parameter as string);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_inputPane != null)
            {
                _inputPane.Showing -= InputPane_Showing;
                _inputPane.Hiding -= InputPane_Hiding;
                _inputPane = null;
            }
            ZaloAvatar.AvatarUpdated -= OnAvatarUpdated;
            ZaloApp.StatusChanged -= ZaloApp_StatusChanged;
            if (_isRecordingVoice)
            {
                CancelVoiceRecording();
            }
            if (_isRenderingSubscribed)
            {
                _isRenderingSubscribed = false;
                CompositionTarget.Rendering -= OnHeaderPinRendering;
            }
            if (_conversation != null)
            {
                _conversation.PropertyChanged -= Conversation_PropertyChanged;
                _conversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                ZaloApp.CurrentChat = null;
            }
        }

        private void PinHeaderToTop()
        {
            if (_headerGrid == null || _rootGrid == null) return;
            try
            {
                var transform = _rootGrid.TransformToVisual(null);
                var pt = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                if (pt.Y < 0)
                {
                    double targetY = -pt.Y;
                    if (_headerGrid.RenderTransform is TranslateTransform tt)
                    {
                        if (Math.Abs(tt.Y - targetY) > 0.5) tt.Y = targetY;
                    }
                    else
                    {
                        _headerGrid.RenderTransform = new TranslateTransform { Y = targetY };
                    }
                }
                else if (_keyboardHeight == 0)
                {
                    if (_headerGrid.RenderTransform != null) _headerGrid.RenderTransform = null;
                }
            }
            catch { }
        }

        private void OnHeaderPinRendering(object sender, object e)
        {
            PinHeaderToTop();
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = Math.Max(0, args.OccludedRect.Height);
            if (_rootGrid != null)
            {
                _rootGrid.Margin = new Thickness(0, 0, 0, _keyboardHeight);
            }
            if (_isDrawerOpen && _drawerContainer != null)
            {
                _isDrawerOpen = false;
                _drawerContainer.Visibility = Visibility.Collapsed;
            }
            args.EnsuredFocusedElementInView = true;
            if (!_isRenderingSubscribed)
            {
                _isRenderingSubscribed = true;
                CompositionTarget.Rendering += OnHeaderPinRendering;
            }
            PinHeaderToTop();
            ScrollToBottom();
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = 0;
            if (_isRenderingSubscribed)
            {
                _isRenderingSubscribed = false;
                CompositionTarget.Rendering -= OnHeaderPinRendering;
            }
            if (_rootGrid != null)
            {
                _rootGrid.Margin = new Thickness(0);
            }
            if (_headerGrid != null)
            {
                _headerGrid.RenderTransform = null;
            }
            args.EnsuredFocusedElementInView = true;
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center
            };
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private async System.Threading.Tasks.Task PromptCallOptionAsync(bool isVideo)
        {
            if (_conversation == null) return;
            string partner = _conversation.Name ?? "Bạn bè";
            var dialog = new Windows.UI.Popups.MessageDialog(
                "Tính năng gọi VoIP thoại/video đang được tối ưu hóa theo giao thức mới của Zalo.\n\nBạn có thể gửi tin nhắn thoại tức thì để kết nối trò chuyện nhanh chóng với " + partner + "!",
                isVideo ? "Cuộc gọi video Zalo" : "Cuộc gọi thoại Zalo");
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Gửi tin nhắn thoại", c =>
            {
                StartVoiceRecording();
            }));
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Vẫn thử gọi", c =>
            {
                NavigationManager.NavigateTo(new CallPage(_conversation, isVideo));
            }));
            dialog.Commands.Add(new Windows.UI.Popups.UICommand("Đóng"));
            await dialog.ShowAsync();
        }

        private static BitmapImage SafeAppxImage(string uriStr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(uriStr)) return null;
                return new BitmapImage(new Uri(uriStr));
            }
            catch
            {
                return null;
            }
        }
    }
}
