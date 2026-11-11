using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
    public sealed class ChatPage : Page, IFileOpenPickerContinuable
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
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(72 * scale)) });
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

            // 3. INPUT BAR (MATCHING media_1794285355410_9cc5344d.png)
            var inputBar = CreateInputBar();
            Grid.SetRow(inputBar, 3);
            _rootGrid.Children.Add(inputBar);

            // 4. EMOJI & STICKER DRAWER (MATCHING media_1794285161826_53038b6c.jpg)
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

        private void ResetActiveVoicePlayer()
        {
            if (_activeVoicePlayIcon != null)
            {
                try
                {
                    _activeVoicePlayIcon.Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_play_clean_white.png");
                }
                catch { }
            }
            if (_activeVoiceStatusText != null && _activeVoiceStatusText.Text == "Đang phát...")
            {
                _activeVoiceStatusText.Text = "Chạm để nghe";
            }
            _activeVoiceMsg = null;
            _activeVoicePlayIcon = null;
            _activeVoiceStatusText = null;
        }

        private FrameworkElement CreateTopHeader()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var headerBorder = new Border
            {
                Padding = new Thickness(Math.Round(4 * scale), Math.Round(22 * scale), Math.Round(6 * scale), Math.Round(6 * scale))
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
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(40 * scale)) }); // 0: Back
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(46 * scale)) }); // 1: Avatar
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });     // 2: Title
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(38 * scale)) }); // 3: Audio Call
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(38 * scale)) }); // 4: Video Call
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(38 * scale)) }); // 5: More Options

            // Back Button (Pure Touch Border with clean white vector back icon)
            var backBtn = CreateTouchBorder(Math.Round(40 * scale), Math.Round(44 * scale));
            var backIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_back_clean_white.png"),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
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
            var audioCallBtn = CreateTouchBorder(Math.Round(38 * scale), Math.Round(44 * scale));
            var audioIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icn_header_voicecall_white.png"),
                Width = Math.Round(22 * scale),
                Height = Math.Round(22 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            audioCallBtn.Child = audioIcon;
            audioCallBtn.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    NavigationManager.NavigateTo(new CallPage(_conversation, false));
                }
            };
            Grid.SetColumn(audioCallBtn, 3);
            header.Children.Add(audioCallBtn);

            // Video Call Button
            var videoCallBtn = CreateTouchBorder(Math.Round(38 * scale), Math.Round(44 * scale));
            var videoIcon = new Image
            {
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/btn_videocall_white.png"),
                Width = Math.Round(22 * scale),
                Height = Math.Round(22 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            videoCallBtn.Child = videoIcon;
            videoCallBtn.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    NavigationManager.NavigateTo(new CallPage(_conversation, true));
                }
            };
            Grid.SetColumn(videoCallBtn, 4);
            header.Children.Add(videoCallBtn);

            // More Options Button (Three vertical dots ⋮ - Opens ConversationOptionsPage or GroupInfoPage)
            var moreBtn = CreateTouchBorder(Math.Round(38 * scale), Math.Round(44 * scale));
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

        // EXACT INPUT BAR MATCHING media_1794285355410_9cc5344d.png - ZERO SYSTEM BUTTON HIGHLIGHT
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

            // 1. Normal Composer Input Grid
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
                Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icon_viewfull_kb_emoticon.png"),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emojiBtn.Tapped += (s, e) => ToggleDrawer();
            Grid.SetColumn(emojiBtn, 0);
            _inputBarGrid.Children.Add(emojiBtn);

            // Composer TextBox wrapped in spacious modern rounded pill
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

            // 4. Microphone Button (Real Voice Notes)
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

            // 5. Photo Button (Direct Native Gallery Picker)
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
            _photoButton.Tapped += (s, e) => PickPhoto();
            Grid.SetColumn(_photoButton, 4);
            _inputBarGrid.Children.Add(_photoButton);

            // 6. Send Button (Blue Circle #0786F9 with btn_send_white.png)
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

            // 2. Real Voice Recording Bar (Appears when recording)
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

            // 3. Group Posting Restricted Notice matching WP10
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

        private async void StartVoiceRecording()
        {
            if (_isRecordingVoice || _conversation == null) return;
            try
            {
                bool started = await ZaloVoiceService.Current.StartRecordingAsync();
                if (!started)
                {
                    var md = new MessageDialog("Không thể truy cập microphone. Vui lòng vào Cài đặt -> Quyền riêng tư -> Micrô (Settings -> Privacy -> Microphone) và cho phép Zalo.\n\nChi tiết: " + ZaloVoiceService.Current.LastError, "Lỗi Micro");
                    await md.ShowAsync();
                    return;
                }

                _isRecordingVoice = true;
                _recordingSeconds = 0;
                _recordingTimeText.Text = "00:00 · Đang ghi âm...";
                _recordingTimer.Start();
                _inputBarGrid.Visibility = Visibility.Collapsed;
                _recordingBarGrid.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi mở micro: " + ex.Message, "Lỗi");
                await md.ShowAsync();
            }
        }

        private async void CancelVoiceRecording()
        {
            if (!_isRecordingVoice) return;
            _recordingTimer.Stop();
            _isRecordingVoice = false;
            _recordingBarGrid.Visibility = Visibility.Collapsed;
            _inputBarGrid.Visibility = Visibility.Visible;
            try
            {
                var file = await ZaloVoiceService.Current.StopRecordingAsync();
                if (file != null) await file.DeleteAsync();
            }
            catch { }
        }

        private async void FinishAndSendVoiceRecording()
        {
            if (!_isRecordingVoice || _conversation == null) return;
            _recordingTimer.Stop();
            _isRecordingVoice = false;
            _recordingBarGrid.Visibility = Visibility.Collapsed;
            _inputBarGrid.Visibility = Visibility.Visible;

            try
            {
                var file = await ZaloVoiceService.Current.StopRecordingAsync();
                if (file != null)
                {
                    var props = await file.GetBasicPropertiesAsync();
                    if (props.Size > 400)
                    {
                        using (var stream = await file.OpenReadAsync())
                        {
                            await ZaloApp.SendVoiceAsync(_conversation, file.Name, stream, (long)props.Size);
                        }
                        RenderMessages();
                        ScrollToBottom();
                    }
                    try { await file.DeleteAsync(); } catch { }
                }
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi tin nhắn thoại: " + ex.Message, "Lỗi");
                await md.ShowAsync();
            }
        }

        private void PickPhoto()
        {
            if (_conversation == null) return;
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                    ViewMode = PickerViewMode.Thumbnail
                };
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".bmp");
                picker.ContinuationData["ConversationKey"] = _conversation.Key;
                picker.ContinuationData["Action"] = "SendPhoto";
                picker.PickSingleFileAndContinue();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể mở bộ chọn ảnh: " + ex.Message + "\nHãy kiểm tra quyền Thư viện ảnh trong Cài đặt.", "Lỗi ảnh");
                var ignore = md.ShowAsync();
            }
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

        public async void ContinueWithFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            if (args == null || args.Files == null || args.Files.Count == 0) return;
            if (_conversation == null)
            {
                string convKey = (args.ContinuationData != null && args.ContinuationData.ContainsKey("ConversationKey"))
                    ? args.ContinuationData["ConversationKey"] as string : null;
                if (!string.IsNullOrEmpty(convKey))
                {
                    _conversation = ZaloApp.Conversations.FirstOrDefault(c => c.Key == convKey);
                }
            }
            if (_conversation == null) return;

            var file = args.Files[0];
            try
            {
                var props = await file.GetBasicPropertiesAsync();
                using (var imageStream = await file.OpenReadAsync())
                {
                    uint width = 800;
                    uint height = 600;
                    try
                    {
                        var decoder = await BitmapDecoder.CreateAsync(imageStream);
                        width = decoder.PixelWidth;
                        height = decoder.PixelHeight;
                        imageStream.Seek(0);
                    }
                    catch
                    {
                        try { imageStream.Seek(0); } catch { }
                    }

                    string caption = _inputBox.Text?.Trim() ?? string.Empty;
                    _inputBox.Text = string.Empty;
                    await ZaloApp.SendImageAsync(_conversation, file.Name, imageStream, (long)props.Size, width, height, caption);
                }
                RenderMessages();
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi ảnh: " + ex.Message, "Gửi ảnh thất bại");
                await md.ShowAsync();
            }
        }

        private async Task ShowSendUrlPhotoDialog()
        {
            if (_conversation == null) return;
            var input = new TextBox
            {
                PlaceholderText = "Nhập URL ảnh (https://... hoặc http://...)",
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 44
            };
            var cd = new ContentDialog
            {
                Title = "Gửi ảnh từ liên kết URL",
                Content = input,
                PrimaryButtonText = "Gửi",
                SecondaryButtonText = "Hủy"
            };
            var res = await cd.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                string caption = _inputBox.Text?.Trim() ?? string.Empty;
                _inputBox.Text = string.Empty;
                await SendPhotoFromUrlAsync(input.Text.Trim(), caption);
            }
        }

        private async Task SendPhotoFromUrlAsync(string url, string caption)
        {
            if (string.IsNullOrWhiteSpace(url) || _conversation == null) return;
            try
            {
                byte[] bytes = null;
                if (ZaloApp.Session != null)
                {
                    try { bytes = await ZaloApp.Session.Http.DownloadBytesAsync(url, CancellationToken.None); }
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
                    using (var stream = new InMemoryRandomAccessStream())
                    using (var writer = new DataWriter(stream))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream();
                        stream.Seek(0);
                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        uint width = decoder.PixelWidth;
                        uint height = decoder.PixelHeight;
                        stream.Seek(0);
                        string fileName = "photo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                        await ZaloApp.SendImageAsync(_conversation, fileName, stream, (long)bytes.Length, width, height, caption ?? string.Empty);
                    }
                    RenderMessages();
                    ScrollToBottom();
                }
                else
                {
                    throw new Exception("Không tải được dữ liệu ảnh từ URL này.");
                }
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi ảnh từ liên kết: " + ex.Message, "Thất bại");
                await md.ShowAsync();
            }
        }

        private async Task ShowShareContactDialog()
        {
            if (_conversation == null) return;
            if (ZaloApp.Friends == null || ZaloApp.Friends.Count == 0)
            {
                var md = new MessageDialog("Danh bạ chưa có bạn bè để chia sẻ danh thiếp.", "Chia sẻ danh thiếp");
                await md.ShowAsync();
                return;
            }

            var scroll = new ScrollViewer { MaxHeight = 350 };
            var listStack = new StackPanel();
            var cd = new ContentDialog
            {
                Title = "Chọn liên hệ cần chia sẻ",
                PrimaryButtonText = "Đóng",
                Content = scroll
            };

            foreach (var fr in ZaloApp.Friends.Take(30))
            {
                var item = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var ava = new Ellipse { Width = 32, Height = 32, Fill = fr.AvatarImageBrush ?? ZaloAvatar.ColorFor(fr.DisplayName ?? "Z") };
                Grid.SetColumn(ava, 0);
                grid.Children.Add(ava);

                var nameBlock = new TextBlock
                {
                    Text = fr.DisplayName ?? "Bạn bè",
                    FontSize = 14.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    Foreground = _textDark
                };
                Grid.SetColumn(nameBlock, 1);
                grid.Children.Add(nameBlock);

                item.Child = grid;
                var currentFr = fr;
                item.Tapped += async (s, e) =>
                {
                    cd.Hide();
                    await ZaloApp.SendContactCardAsync(_conversation, currentFr);
                };
                listStack.Children.Add(item);
            }
            scroll.Content = listStack;
            await cd.ShowAsync();
        }

        // EXACT EMOJI DRAWER MATCHING media_1794285161826_53038b6c.jpg
        private FrameworkElement CreateStickerDrawer()
        {
            var drawerBorder = new Border
            {
                Height = 210,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xEC, 0xEF)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Drawer Subtabs: Emoji | Nhãn dán | GIF | Zalo
            var tabsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var tabEmoji = CreateDrawerTab("Emoji", true);
            var tabSticker = CreateDrawerTab("Nhãn dán", false);
            var tabGif = CreateDrawerTab("GIF", false);
            var tabZalo = CreateDrawerTab("Zalo", false);

            tabsStack.Children.Add(tabEmoji);
            tabsStack.Children.Add(tabSticker);
            tabsStack.Children.Add(tabGif);
            tabsStack.Children.Add(tabZalo);

            Grid.SetRow(tabsStack, 0);
            grid.Children.Add(tabsStack);

            // 7 Emojis per row (matching media_1794285161826_53038b6c.jpg)
            var scroll = new ScrollViewer { Padding = new Thickness(10, 6, 10, 10) };
            var stack = new StackPanel();

            int count = 0;
            StackPanel currentRow = null;
            for (int i = 0; i < 35; i++)
            {
                if (count % 7 == 0)
                {
                    currentRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 3, 0, 3),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    stack.Children.Add(currentRow);
                }

                string emoIndex = i < 10 ? "0" + i : i.ToString();
                string uri = $"ms-appx:///Assets/Emoticons/Emoji/emo_{emoIndex}.png";

                var emoBtn = CreateTouchBorder(44, 44);
                emoBtn.Margin = new Thickness(2, 0, 2, 0);

                var emoImg = new Image
                {
                    Source = SafeAppxImage(uri),
                    Width = 34,
                    Height = 34,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                emoBtn.Child = emoImg;

                emoBtn.Tapped += (s, e) =>
                {
                    if (_conversation != null)
                    {
                        long nowMs = DateTime.UtcNow.Ticks / 10000;
                        var stickerMsg = new ZaloChatMessage
                        {
                            FromMe = true,
                            Text = "[Sticker]",
                            MediaUrl = uri,
                            MsgType = "chat.sticker",
                            Ts = nowMs
                        };
                        _conversation.Messages.Add(stickerMsg);
                        _conversation.LastText = "[Sticker]";
                        _conversation.LastTs = nowMs;
                        ToggleDrawer();
                    }
                };

                currentRow.Children.Add(emoBtn);
                count++;
            }

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            drawerBorder.Child = grid;
            return drawerBorder;
        }

        private FrameworkElement CreateDrawerTab(string text, bool active)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = active ? new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF4, 0xFA)) : new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(14)
            };
            var txt = new TextBlock
            {
                Text = text,
                FontSize = 13.5,
                FontWeight = active ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = active ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B))
            };
            border.Child = txt;
            return border;
        }

        private void ToggleDrawer()
        {
            _isDrawerOpen = !_isDrawerOpen;
            _drawerContainer.Visibility = _isDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
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
                _titleBlock.Text = _conversation.Name ?? "Trò chuyện";
                _recipientInitial.Text = _conversation.Initial ?? "Z";
                _recipientAvatar.Fill = _conversation.AvatarColor ?? _activeBlue;

                if (_conversation.AvatarImageBrush != null)
                {
                    _recipientAvatar.Fill = _conversation.AvatarImageBrush;
                    _recipientInitial.Text = string.Empty;
                }

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
                RequestRenderMessages();
                if (_conversation.IsGroup)
                {
                    _ = EnsureGroupMembersLoadedAsync();
                    _ = ResolveSenderAvatarsAsync();
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
            TimeSpan age = DateTimeOffset.Now - timestamp.ToLocalTime();
            if (age < TimeSpan.Zero) return "Vừa mới truy cập";
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
                    var incoming = _conversation.Messages
                        .Where(message => !message.FromMe && !string.IsNullOrWhiteSpace(message.SenderUid))
                        .ToList();
                    if (incoming.Count == 0) return;

                    var friendAvatars = (ZaloApp.Friends ?? new System.Collections.Generic.List<ZaloFriend>())
                        .Where(friend => !string.IsNullOrWhiteSpace(friend.UserId) && !string.IsNullOrWhiteSpace(friend.Avatar))
                        .GroupBy(friend => friend.UserId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First().Avatar, StringComparer.Ordinal);
                    foreach (ZaloChatMessage message in incoming)
                    {
                        if (!string.IsNullOrWhiteSpace(message.SenderAvatarUrl)) continue;
                        if (friendAvatars.TryGetValue(message.SenderUid, out string avatar))
                        {
                            message.SenderAvatarUrl = avatar;
                        }
                    }

                    var missingIds = incoming.Where(message => string.IsNullOrWhiteSpace(message.SenderAvatarUrl))
                        .Select(message => message.SenderUid).Distinct(StringComparer.Ordinal)
                        .Where(id => !_senderAvatarLookupsAttempted.Contains(id)).Take(50).ToList();
                    if (missingIds.Count == 0) continue;

                    foreach (string id in missingIds) _senderAvatarLookupsAttempted.Add(id);
                    var profiles = await ZaloAuth.GetGroupMemberProfilesAsync(
                        ZaloApp.Session, missingIds, System.Threading.CancellationToken.None);
                    bool anyUpdated = false;
                    foreach (ZaloChatMessage message in incoming)
                    {
                        if (profiles.TryGetValue(message.SenderUid, out ZaloFriend profile))
                        {
                            if (!string.IsNullOrWhiteSpace(profile.Avatar) && string.IsNullOrWhiteSpace(message.SenderAvatarUrl))
                            {
                                message.SenderAvatarUrl = profile.Avatar;
                                anyUpdated = true;
                            }
                            if (!string.IsNullOrWhiteSpace(profile.DisplayName) && (string.IsNullOrWhiteSpace(message.Sender) || message.Sender == "Thành viên" || message.Sender == message.SenderUid))
                            {
                                message.Sender = profile.DisplayName;
                                anyUpdated = true;
                            }
                            _conversation.SetGroupMember(profile.UserId, profile.DisplayName, profile.Avatar, false, false);
                        }
                    }
                    if (anyUpdated)
                    {
                        RequestRenderMessages();
                        _ = ZaloApp.SaveCachedConversationsAsync();
                    }
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

        private bool _isRenderingSubscribed = false;

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

        private void OnAvatarUpdated(string url)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
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

        private void ZaloApp_StatusChanged()
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RequestRenderMessages();
            });
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

        private void RenderMessages()
        {
            if (_conversation == null) return;
            try
            {
                _messageStack.Children.Clear();

                if (_conversation.HasMoreHistory)
                {
                    var loadOlderBtn = new Border
                    {
                        Background = new SolidColorBrush(Colors.White),
                        CornerRadius = new CornerRadius(14),
                        Padding = new Thickness(14, 6, 14, 6),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 8)
                    };
                    loadOlderBtn.Child = new TextBlock
                    {
                        Text = "Tải tin nhắn cũ hơn",
                        FontSize = 13,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _activeBlue
                    };
                    loadOlderBtn.PointerPressed += (s, e) => loadOlderBtn.Opacity = 0.6;
                    loadOlderBtn.PointerReleased += (s, e) => loadOlderBtn.Opacity = 1.0;
                    loadOlderBtn.PointerCanceled += (s, e) => loadOlderBtn.Opacity = 1.0;
                    loadOlderBtn.Tapped += (s, e) => { var ignored = LoadOlderHistoryCoreAsync(); };
                    _messageStack.Children.Add(loadOlderBtn);
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

        private void ScrollToBottom()
        {
            if (_messageScroll == null) return;
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                try
                {
                    double offset = _messageScroll.ScrollableHeight;
                    if (!double.IsNaN(offset) && !double.IsInfinity(offset) && offset > 0)
                    {
                        _messageScroll.ChangeView(null, offset, null, true);
                    }
                }
                catch { }
            });
        }

        // EXACT MESSAGE BUBBLE MATCHING media_1794285355410_9cc5344d.png
        private FrameworkElement CreateMessageBubble(ZaloChatMessage msg)
        {
            bool isMe = msg.FromMe;

            if (_conversation != null && _conversation.IsGroup)
            {
                _conversation.ResolveMemberForMessage(msg);
            }

            bool isGroupOther = !isMe && _conversation != null && _conversation.IsGroup;

            var container = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
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

            // Render Quoted Reply Box if message is a reply
            if (!string.IsNullOrEmpty(msg.QuotedText))
            {
                var quoteBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, 0x00, 0x78, 0xD7)),
                    CornerRadius = new CornerRadius(8),
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

            bool isSticker = msg.IsSticker || msg.MsgType == "chat.sticker" || msg.MsgType == "5"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.Contains("emo_") || msg.MediaUrl.Contains("sticker_")));

            bool isVoice = msg.HasVoice || msg.MsgType == "chat.voice" || msg.MsgType == "31" || msg.MsgType == "chat.audio"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.Contains("voice")));

            bool isVideo = !isVoice && (msg.HasVideo || msg.MsgType == "chat.video.msg" || msg.MsgType == "chat.video" || msg.MsgType == "44" || msg.MsgType == "32"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".3gp", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.Contains("video"))));

            bool isFile = !isVoice && !isVideo && (msg.HasFile || msg.MsgType == "share.file" || msg.MsgType == "46" || !string.IsNullOrWhiteSpace(msg.FileName));

            bool isPhoto = !isSticker && !isVoice && !isVideo && !isFile && (msg.HasMedia || msg.MsgType == "chat.photo"
                || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.StartsWith("http") || msg.MediaUrl.Contains("res-zalo") || msg.MediaUrl.Contains("photo")
                    || msg.MediaUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    || msg.MediaUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || msg.MediaUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))));

            if (isSticker)
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

                // Play Overlay Icon (Official 56x56 Zalo Video Play Icon)
                var playOverlay = new Image
                {
                    Source = SafeAppxImage("ms-appx:///Assets/Icons/Android/Chat/icn_csc_filevideo_play.png"),
                    Width = 56,
                    Height = 56,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                videoGrid.Children.Add(playOverlay);

                // Video Badge
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

                if (!string.IsNullOrWhiteSpace(msg.Text) && msg.Text != "[Hình ảnh]")
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
            else
            {
                // Regular Text Bubble
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

                // Tap message bubble shows options (Trả lời, Sao chép, Thu hồi, Xóa) via ZaloActionSheet
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
                Margin = new Thickness(0, 4, 0, 0)
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

        private bool _isSendingText = false;
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
