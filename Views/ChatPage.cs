using System;
using System.Linq;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class ChatPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B));
        private readonly SolidColorBrush _bubbleMeBg = new SolidColorBrush(Color.FromArgb(0xFF, 0xD8, 0xEC, 0xFF));
        private readonly SolidColorBrush _bubbleMeBorder = new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD));
        private readonly SolidColorBrush _bubblePartnerBg = new SolidColorBrush(Colors.White);
        private readonly SolidColorBrush _bubblePartnerBorder = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF2, 0xF6));

        private ZaloConversation _conversation;
        private ListView _messageList;
        private TextBox _inputBox;
        private Button _sendButton;
        private TextBlock _titleBlock;
        private TextBlock _subtitleBlock;
        private Ellipse _recipientAvatar;
        private TextBlock _recipientInitial;

        private Grid _drawerContainer;
        private bool _isDrawerOpen = false;

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
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient)
            var headerGrid = CreateTopHeader();
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. MESSAGE LIST CANVAS
            _messageList = new ListView
            {
                Padding = new Thickness(12, 12, 12, 12),
                SelectionMode = ListViewSelectionMode.None,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEB, 0xF3))
            };
            Grid.SetRow(_messageList, 1);
            rootGrid.Children.Add(_messageList);

            // 3. INPUT BAR
            var inputBar = CreateInputBar();
            Grid.SetRow(inputBar, 2);
            rootGrid.Children.Add(inputBar);

            // 4. STICKER / EMOJI DRAWER
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
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Back Button
            var backBtn = new Button
            {
                Content = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Width = 42,
                Height = 44,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            backBtn.Click += (s, e) =>
            {
                if (NavigationManager.CanGoBack)
                    NavigationManager.GoBack();
                else
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            // Recipient Avatar
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

            // Recipient Info
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            _titleBlock = new TextBlock
            {
                Text = "Trò chuyện",
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _subtitleBlock = new TextBlock
            {
                Text = "Đang hoạt động",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD6, 0xE8, 0xFF))
            };
            titleStack.Children.Add(_titleBlock);
            titleStack.Children.Add(_subtitleBlock);
            Grid.SetColumn(titleStack, 2);
            header.Children.Add(titleStack);

            // Audio Call Icon Button
            var callBtn = new Button
            {
                Width = 38,
                Height = 44,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            var callIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_call_white.png")),
                Width = 20,
                Height = 20
            };
            callBtn.Content = callIcon;
            Grid.SetColumn(callBtn, 3);
            header.Children.Add(callBtn);

            // Video Call Icon Button
            var videoBtn = new Button
            {
                Width = 38,
                Height = 44,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            var videoIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_video_white.png")),
                Width = 20,
                Height = 20
            };
            videoBtn.Content = videoIcon;
            Grid.SetColumn(videoBtn, 4);
            header.Children.Add(videoBtn);

            headerBorder.Child = header;
            return headerBorder;
        }

        private Border CreateInputBar()
        {
            var bar = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(8, 6, 8, 6)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Emoji / Sticker Button
            var emojiBtn = new Button
            {
                Content = "😊",
                FontSize = 18,
                Width = 34,
                Height = 38,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            emojiBtn.Click += (s, e) => ToggleDrawer();
            Grid.SetColumn(emojiBtn, 0);
            grid.Children.Add(emojiBtn);

            // Text Input Box
            var inputBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xFA, 0xFC)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };
            _inputBox = new TextBox
            {
                PlaceholderText = "Tin nhắn",
                FontSize = 14.5,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center
            };
            _inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    SendMessage();
                    e.Handled = true;
                }
            };
            inputBorder.Child = _inputBox;
            Grid.SetColumn(inputBorder, 1);
            grid.Children.Add(inputBorder);

            // Photo Attachment Button
            var photoBtn = new Button
            {
                Width = 34,
                Height = 38,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            photoBtn.Content = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_photo_gray.png")),
                Width = 20,
                Height = 20
            };
            Grid.SetColumn(photoBtn, 2);
            grid.Children.Add(photoBtn);

            // Mic / Voice Button
            var micBtn = new Button
            {
                Width = 34,
                Height = 38,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            micBtn.Content = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_mic_gray.png")),
                Width = 20,
                Height = 20
            };
            Grid.SetColumn(micBtn, 3);
            grid.Children.Add(micBtn);

            // Send Button
            _sendButton = new Button
            {
                Content = "Gửi",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 13.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            _sendButton.Click += (s, e) => SendMessage();
            Grid.SetColumn(_sendButton, 4);
            grid.Children.Add(_sendButton);

            bar.Child = grid;
            return bar;
        }

        private Grid CreateStickerDrawer()
        {
            var grid = new Grid
            {
                Height = 190,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                Visibility = Visibility.Collapsed
            };

            var scroll = new ScrollViewer { Padding = new Thickness(10, 10, 10, 10) };
            var stack = new StackPanel();

            // Populate rows of emoji (6 per row, 5 rows = 30 official Zalo emojis)
            int count = 0;
            StackPanel currentRow = null;
            for (int i = 0; i < 30; i++)
            {
                if (count % 6 == 0)
                {
                    currentRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 4, 0, 4),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    stack.Children.Add(currentRow);
                }

                string emoIndex = i < 10 ? "0" + i : i.ToString();
                string uri = $"ms-appx:///Assets/Emoticons/Emoji/emo_{emoIndex}.png";

                var emoBtn = new Button
                {
                    Width = 46,
                    Height = 46,
                    Padding = new Thickness(4),
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(4, 0, 4, 0)
                };
                var emoImg = new Image
                {
                    Source = new BitmapImage(new Uri(uri)),
                    Width = 36,
                    Height = 36
                };
                emoBtn.Content = emoImg;

                int capturedIndex = i;
                emoBtn.Click += (s, e) =>
                {
                    _inputBox.Text += $" [emo_{emoIndex}]";
                };

                currentRow.Children.Add(emoBtn);
                count++;
            }

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
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
                _subtitleBlock.Text = _conversation.IsGroup ? (_conversation.GroupMemberCount > 0 ? $"{_conversation.GroupMemberCount} thành viên" : "Nhóm trò chuyện") : "Đang hoạt động";
                _recipientInitial.Text = _conversation.Initial ?? "Z";
                _recipientAvatar.Fill = _conversation.AvatarColor ?? _activeBlue;

                if (_conversation.AvatarImageBrush != null)
                {
                    _recipientAvatar.Fill = _conversation.AvatarImageBrush;
                    _recipientInitial.Text = string.Empty;
                }

                ZaloApp.CurrentChat = _conversation;

                RenderMessages();
                _conversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                _conversation.Messages.CollectionChanged += Messages_CollectionChanged;
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
            _messageList.Items.Clear();

            // Date Badge pill at top
            var dateBadgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAA, 0xB3, 0xBD)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            dateBadgeBorder.Child = new TextBlock
            {
                Text = "HÔM NAY",
                FontSize = 10.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            };
            _messageList.Items.Add(dateBadgeBorder);

            foreach (var m in _conversation.Messages)
            {
                var bubble = CreateMessageBubble(m);
                _messageList.Items.Add(bubble);
            }

            if (_messageList.Items.Count > 0)
            {
                _messageList.ScrollIntoView(_messageList.Items.Last());
            }
        }

        private FrameworkElement CreateMessageBubble(ZaloChatMessage msg)
        {
            bool isMe = msg.FromMe;

            var container = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = isMe ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var border = new Border
            {
                Background = isMe ? _bubbleMeBg : _bubblePartnerBg,
                BorderBrush = isMe ? _bubbleMeBorder : _bubblePartnerBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 290
            };

            var stack = new StackPanel();

            if (!isMe && !string.IsNullOrEmpty(msg.Sender) && _conversation.IsGroup)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = msg.Sender,
                    FontSize = 11.5,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _activeBlue,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = msg.Text ?? string.Empty,
                FontSize = 14.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _textDark
            });

            var timeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            timeStack.Children.Add(new TextBlock
            {
                Text = msg.TimeText ?? string.Empty,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x94, 0xA3, 0xB8))
            });

            if (isMe)
            {
                timeStack.Children.Add(new TextBlock
                {
                    Text = " ✓",
                    FontSize = 10,
                    Foreground = _activeBlue
                });
            }

            stack.Children.Add(timeStack);
            border.Child = stack;
            container.Children.Add(border);
            return container;
        }

        private void SendMessage()
        {
            string text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || _conversation == null) return;

            _inputBox.Text = string.Empty;
            if (_isDrawerOpen) ToggleDrawer();
            var task = ZaloApp.SendAsync(_conversation, text);
        }
    }
}
