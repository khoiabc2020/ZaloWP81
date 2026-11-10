using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
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
    public sealed class ChatPage : Page
    {
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
        private Ellipse _recipientAvatar;
        private TextBlock _recipientInitial;

        private FrameworkElement _drawerContainer;
        private bool _isDrawerOpen = false;

        private Border _replyBar;
        private TextBlock _replySenderText;
        private TextBlock _replyContentText;
        private ZaloChatMessage _replyTarget;

        public ChatPage(string conversationKey = null)
        {
            BuildUI();
            InitializeChat(conversationKey);
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEB, 0xF3))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient: ← [Avatar] [Name]        ⋮)
            var headerGrid = CreateTopHeader();
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. MESSAGE LIST CANVAS (ScrollViewer with StackPanel for Silky Touch & Zero Highlight)
            _messageScroll = new ScrollViewer
            {
                Padding = new Thickness(12, 12, 12, 12),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEB, 0xF3))
            };
            _messageStack = new StackPanel();
            _messageScroll.Content = _messageStack;
            Grid.SetRow(_messageScroll, 1);
            rootGrid.Children.Add(_messageScroll);

            // 3. INPUT BAR (MATCHING media_1794285355410_9cc5344d.png)
            var inputBar = CreateInputBar();
            Grid.SetRow(inputBar, 2);
            rootGrid.Children.Add(inputBar);

            // 4. EMOJI & STICKER DRAWER (MATCHING media_1794285161826_53038b6c.jpg)
            _drawerContainer = CreateStickerDrawer();
            Grid.SetRow(_drawerContainer, 3);
            rootGrid.Children.Add(_drawerContainer);

            this.Content = rootGrid;
        }

        private FrameworkElement CreateTopHeader()
        {
            var headerBorder = new Border
            {
                Padding = new Thickness(4, 0, 10, 0)
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
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Back Button (Pure Touch Border)
            var backBtn = CreateTouchBorder(40, 44);
            var backArrow = new TextBlock
            {
                Text = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Child = backArrow;
            backBtn.Tapped += (s, e) =>
            {
                if (NavigationManager.CanGoBack)
                    NavigationManager.GoBack();
                else
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            // Recipient Avatar (36x36 Circle)
            var avatarGrid = new Grid { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
            _recipientAvatar = new Ellipse { Width = 36, Height = 36, Fill = _activeBlue };
            _recipientInitial = new TextBlock
            {
                Text = "Z",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_recipientAvatar);
            avatarGrid.Children.Add(_recipientInitial);
            Grid.SetColumn(avatarGrid, 1);
            header.Children.Add(avatarGrid);

            // Recipient Title (White Bold 16.5)
            _titleBlock = new TextBlock
            {
                Text = "Trò chuyện",
                FontSize = 16.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_titleBlock, 2);
            header.Children.Add(_titleBlock);

            // More Options Button (Three vertical dots ⋮ - Opens ConversationOptionsPage)
            var moreBtn = CreateTouchBorder(42, 44);
            var moreIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/Chat/icn_header_menu_more_white.png")),
                Width = 24,
                Height = 24,
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
            Grid.SetColumn(moreBtn, 3);
            header.Children.Add(moreBtn);

            headerBorder.Child = header;
            return headerBorder;
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
            closeReplyBtn.Child = new TextBlock { Text = "✕", FontSize = 14, Foreground = _timeColor, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            closeReplyBtn.Tapped += (s, e) => SetReplyTarget(null);
            Grid.SetColumn(closeReplyBtn, 1);
            replyGrid.Children.Add(closeReplyBtn);

            _replyBar.Child = replyGrid;
            container.Children.Add(_replyBar);

            var bar = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(6, 4, 6, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            // 1. Smiley Icon Button
            var emojiBtn = CreateTouchBorder(38, 42);
            var emojiImg = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/Chat/icon_viewfull_kb_emoticon.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            emojiBtn.Child = emojiImg;
            emojiBtn.Tapped += (s, e) => ToggleDrawer();
            Grid.SetColumn(emojiBtn, 0);
            grid.Children.Add(emojiBtn);

            // 2. Open Composer TextBox (No outer border, matching Android/W10M style)
            _inputBox = new TextBox
            {
                PlaceholderText = "Tin nhắn",
                FontSize = 17,
                Foreground = _textDark,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            _inputBox.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrWhiteSpace(_inputBox.Text);
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
            Grid.SetColumn(_inputBox, 1);
            grid.Children.Add(_inputBox);

            // 3. More Button (...)
            var moreDotsBtn = CreateTouchBorder(34, 42);
            var dotsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            dotsStack.Children.Add(new Ellipse { Width = 3.5, Height = 3.5, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(1.5, 0, 1.5, 0) });
            dotsStack.Children.Add(new Ellipse { Width = 3.5, Height = 3.5, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(1.5, 0, 1.5, 0) });
            dotsStack.Children.Add(new Ellipse { Width = 3.5, Height = 3.5, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x73, 0x73)), Margin = new Thickness(1.5, 0, 1.5, 0) });
            moreDotsBtn.Child = dotsStack;
            moreDotsBtn.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Chọn tệp đính kèm:", "Gửi tệp");
                md.Commands.Add(new UICommand("Gửi danh thiếp", cmd =>
                {
                    if (_conversation != null)
                    {
                        var task = ZaloApp.SendAsync(_conversation, "[Danh thiếp] Nguyễn Văn Đức - 0977 889 900");
                    }
                }));
                md.Commands.Add(new UICommand("Gửi vị trí", cmd =>
                {
                    if (_conversation != null)
                    {
                        var task = ZaloApp.SendAsync(_conversation, "[Vị trí] Tòa nhà VNG Campus, TP.HCM");
                    }
                }));
                md.Commands.Add(new UICommand("Đóng"));
                await md.ShowAsync();
            };
            Grid.SetColumn(moreDotsBtn, 2);
            grid.Children.Add(moreDotsBtn);

            // 4. Microphone Button (Voice Notes)
            _micButton = CreateTouchBorder(36, 42);
            var micImg = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/Chat/ic_fotohavevoice.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _micButton.Child = micImg;
            _micButton.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    long nowMs = DateTime.UtcNow.Ticks / 10000;
                    var voiceMsg = new ZaloChatMessage
                    {
                        FromMe = true,
                        Text = "🎙️ Tin nhắn thoại (0:05)",
                        Ts = nowMs,
                        MsgType = "chat.voice"
                    };
                    _conversation.Messages.Add(voiceMsg);
                    _conversation.LastText = "[Tin nhắn thoại]";
                    _conversation.LastTs = nowMs;
                }
            };
            Grid.SetColumn(_micButton, 3);
            grid.Children.Add(_micButton);

            // 5. Photo Gallery Button
            _photoButton = CreateTouchBorder(36, 42);
            var photoImg = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/Chat/icn_camera_gallery.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _photoButton.Child = photoImg;
            _photoButton.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Chọn ảnh muốn gửi:", "Thư viện ảnh");
                md.Commands.Add(new UICommand("Ảnh thiên nhiên", cmd => SendPhoto("ms-appx:///Assets/Backgrounds/bg_01.png")));
                md.Commands.Add(new UICommand("Ảnh phong cảnh", cmd => SendPhoto("ms-appx:///Assets/Backgrounds/bg_02.png")));
                md.Commands.Add(new UICommand("Ảnh nghệ thuật", cmd => SendPhoto("ms-appx:///Assets/Backgrounds/bg_03.png")));
                md.Commands.Add(new UICommand("Đóng"));
                await md.ShowAsync();
            };
            Grid.SetColumn(_photoButton, 4);
            grid.Children.Add(_photoButton);

            // 6. Send Button (Blue Circle #0786F9 with btn_send_white.png)
            _sendButton = CreateTouchBorder(36, 42);
            _sendButton.Visibility = Visibility.Collapsed;

            var sendCircle = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = _activeBlue
            };
            sendCircle.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/Chat/btn_send_white.png")),
                Width = 17,
                Height = 17,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sendButton.Child = sendCircle;
            _sendButton.Tapped += (s, e) => SendMessage();
            Grid.SetColumn(_sendButton, 4);
            grid.Children.Add(_sendButton);

            bar.Child = grid;
            container.Children.Add(bar);
            return container;
        }

        private void SendPhoto(string url)
        {
            if (_conversation == null) return;
            long nowMs = DateTime.UtcNow.Ticks / 10000;
            var photoMsg = new ZaloChatMessage
            {
                FromMe = true,
                Text = "[Hình ảnh]",
                MediaUrl = url,
                Ts = nowMs,
                MsgType = "chat.photo"
            };
            _conversation.Messages.Add(photoMsg);
            _conversation.LastText = "[Hình ảnh]";
            _conversation.LastTs = nowMs;
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
                    Source = new BitmapImage(new Uri(uri)),
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

                RenderMessages();
                _ = LoadHistoryCoreAsync();
                _ = ZaloApp.MarkConversationSeenAsync(_conversation);

                _conversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                _conversation.Messages.CollectionChanged += Messages_CollectionChanged;
            }
        }

        private async System.Threading.Tasks.Task LoadHistoryCoreAsync()
        {
            if (_conversation == null) return;
            try
            {
                await ZaloApp.LoadHistoryAsync(_conversation, System.Threading.CancellationToken.None);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    RenderMessages();
                });
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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    RenderMessages();
                });
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
            InitializeChat(e.Parameter as string);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (_conversation != null)
            {
                _conversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                ZaloApp.CurrentChat = null;
            }
        }

        private async void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                RenderMessages();
            });
        }

        private void RenderMessages()
        {
            if (_conversation == null) return;
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
                    FontSize = 12.5,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _activeBlue
                };
                loadOlderBtn.PointerPressed += (s, e) => loadOlderBtn.Opacity = 0.6;
                loadOlderBtn.PointerReleased += (s, e) => loadOlderBtn.Opacity = 1.0;
                loadOlderBtn.PointerCanceled += (s, e) => loadOlderBtn.Opacity = 1.0;
                loadOlderBtn.Tapped += (s, e) => { var ignored = LoadOlderHistoryCoreAsync(); };
                _messageStack.Children.Add(loadOlderBtn);
            }

            foreach (var m in _conversation.Messages)
            {
                var bubble = CreateMessageBubble(m);
                _messageStack.Children.Add(bubble);
            }

            try
            {
                _messageScroll.ChangeView(0, _messageScroll.ScrollableHeight, null, false);
            }
            catch { }
        }

        // EXACT MESSAGE BUBBLE MATCHING media_1794285355410_9cc5344d.png
        private FrameworkElement CreateMessageBubble(ZaloChatMessage msg)
        {
            bool isMe = msg.FromMe;

            var container = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var stack = new StackPanel();

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

            if (msg.IsSticker || (!string.IsNullOrEmpty(msg.MediaUrl) && (msg.MediaUrl.Contains("emo_") || msg.MediaUrl.Contains("bg_"))))
            {
                // Sticker card (White rounded card with light blue border, matching media_1794285355410_9cc5344d.png)
                var stickerCard = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(10),
                    MaxWidth = 260
                };
                var stickerImg = new Image
                {
                    Source = new BitmapImage(new Uri(msg.MediaUrl ?? "ms-appx:///Assets/Emoticons/Emoji/emo_05.png")),
                    Width = 96,
                    Height = 96,
                    Stretch = Stretch.Uniform
                };
                stickerCard.Child = stickerImg;

                // Tap sticker/photo opens ImageViewerPage
                stickerCard.PointerPressed += (s, e) => stickerCard.Opacity = 0.7;
                stickerCard.PointerReleased += (s, e) => stickerCard.Opacity = 1.0;
                stickerCard.PointerCanceled += (s, e) => stickerCard.Opacity = 1.0;
                stickerCard.Tapped += (s, e) =>
                {
                    NavigationManager.NavigateTo(new ImageViewerPage(msg.MediaUrl));
                };

                stack.Children.Add(stickerCard);
            }
            else
            {
                // Regular Text Bubble
                var bubbleBorder = new Border
                {
                    Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                    BorderBrush = isMe ? new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)) : _bubblePartnerBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(15),
                    Padding = new Thickness(14, 9, 14, 9),
                    MaxWidth = 290
                };

                var textBlock = new TextBlock
                {
                    Text = msg.Text ?? string.Empty,
                    FontSize = 15,
                    Foreground = _textDark,
                    TextWrapping = TextWrapping.Wrap
                };
                bubbleBorder.Child = textBlock;

                // Tap message bubble shows options (Trả lời, Sao chép, Thu hồi, Xóa)
                bubbleBorder.PointerPressed += (s, e) => bubbleBorder.Opacity = 0.7;
                bubbleBorder.PointerReleased += (s, e) => bubbleBorder.Opacity = 1.0;
                bubbleBorder.PointerCanceled += (s, e) => bubbleBorder.Opacity = 1.0;
                bubbleBorder.Tapped += async (s, e) =>
                {
                    var md = new MessageDialog("Chọn thao tác cho tin nhắn:", "Tin nhắn");
                    md.Commands.Add(new UICommand("Trả lời", cmd => SetReplyTarget(msg)));
                    md.Commands.Add(new UICommand("Sao chép vào ô soạn", cmd =>
                    {
                        _inputBox.Text = msg.Text ?? string.Empty;
                    }));
                    if (isMe)
                    {
                        md.Commands.Add(new UICommand("Thu hồi tin nhắn", async cmd =>
                        {
                            await ZaloApp.RecallMessageAsync(_conversation, msg);
                            RenderMessages();
                        }));
                    }
                    md.Commands.Add(new UICommand("Xóa phía tôi", async cmd =>
                    {
                        await ZaloApp.DeleteMessageForMeAsync(_conversation, msg);
                        RenderMessages();
                    }));
                    md.Commands.Add(new UICommand("Đóng"));
                    await md.ShowAsync();
                };

                stack.Children.Add(bubbleBorder);
            }

            // Timestamp and Status (✓ Đã gửi)
            var timeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(0, 3, 0, 0)
            };

            timeStack.Children.Add(new TextBlock
            {
                Text = msg.TimeText ?? string.Empty,
                FontSize = 11,
                Foreground = _timeColor
            });

            if (isMe)
            {
                timeStack.Children.Add(new TextBlock
                {
                    Text = "  ✓ Đã gửi",
                    FontSize = 11,
                    Foreground = _timeColor
                });
            }

            stack.Children.Add(timeStack);
            container.Children.Add(stack);
            return container;
        }

        private void SendMessage()
        {
            string text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _conversation == null) return;

            _inputBox.Text = string.Empty;
            if (_isDrawerOpen) ToggleDrawer();

            if (_replyTarget != null)
            {
                var target = _replyTarget;
                SetReplyTarget(null);
                _ = ZaloApp.SendAsync(_conversation, text, target);
            }
            else
            {
                _ = ZaloApp.SendAsync(_conversation, text);
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
    }
}
