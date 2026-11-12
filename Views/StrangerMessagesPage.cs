using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class StrangerMessagesPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private StackPanel _itemsStack;
        private StackPanel _emptyPanel;

        public StrangerMessagesPage()
        {
            BuildUI();
            this.Loaded += (s, e) => RefreshList();
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.White)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(56 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Top Bar
            var headerBorder = new Border { Background = _activeBlue, Padding = new Thickness(6, 0, 12, 0) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var backBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(44 * scale));
            backBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_back_clean_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Tapped += (s, e) =>
            {
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var title = new TextBlock
            {
                Text = "Tin nhắn từ người lạ",
                FontSize = Math.Round(18.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Info Banner
            var noticeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF6, 0xF7)),
                Padding = new Thickness(Math.Round(16 * scale), Math.Round(12 * scale), Math.Round(16 * scale), Math.Round(12 * scale))
            };
            noticeBorder.Child = new TextBlock
            {
                Text = "Người chưa có trong danh bạ có thể nhắn tin cho bạn. Hãy kiểm tra kỹ trước khi chia sẻ thông tin cá nhân.",
                FontSize = Math.Round(14.5 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x70, 0x77, 0x7F)),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(noticeBorder, 1);
            rootGrid.Children.Add(noticeBorder);

            // 3. List & Empty View
            var contentGrid = new Grid();

            var scroll = new ScrollViewer();
            _itemsStack = new StackPanel();
            scroll.Content = _itemsStack;
            contentGrid.Children.Add(scroll);

            _emptyPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(Math.Round(28 * scale)),
                MaxWidth = Math.Round(340 * scale),
                Visibility = Visibility.Collapsed
            };
            var emptyIconBorder = new Border
            {
                Width = Math.Round(60 * scale),
                Height = Math.Round(60 * scale),
                CornerRadius = new CornerRadius(Math.Round(30 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF6, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, Math.Round(14 * scale))
            };
            emptyIconBorder.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_chat_clean_blue.png")),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _emptyPanel.Children.Add(emptyIconBorder);

            _emptyPanel.Children.Add(new TextBlock
            {
                Text = "Chưa có tin nhắn từ người lạ",
                FontSize = Math.Round(17.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });

            _emptyPanel.Children.Add(new TextBlock
            {
                Text = "Tin nhắn mới từ người chưa có trong danh bạ sẽ xuất hiện tại đây.",
                FontSize = Math.Round(14.5 * scale, 1),
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });
            contentGrid.Children.Add(_emptyPanel);

            Grid.SetRow(contentGrid, 2);
            rootGrid.Children.Add(contentGrid);

            this.Content = rootGrid;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ZaloApp.Conversations.CollectionChanged += OnConversationsChanged;
            ZaloApp.FriendsLoadChanged += OnFriendsLoadChanged;
            RefreshList();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ZaloApp.Conversations.CollectionChanged -= OnConversationsChanged;
            ZaloApp.FriendsLoadChanged -= OnFriendsLoadChanged;
            base.OnNavigatedFrom(e);
        }

        private void OnConversationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshList();
        }

        private async void OnFriendsLoadChanged()
        {
            if (Dispatcher == null) return;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, RefreshList);
        }

        private void RefreshList()
        {
            if (_itemsStack == null) return;
            _itemsStack.Children.Clear();

            var otherKeys = new HashSet<string>(
                ZaloSettingsService.Current.GetOtherConversationKeys(ZaloApp.Session?.Uid),
                StringComparer.Ordinal);

            var strangers = ZaloApp.FriendsLoaded
                ? ZaloApp.Conversations
                    .Where(c => c != null && !c.IsGroup && !ZaloApp.IsArchived(c)
                        && !otherKeys.Contains(c.Key) && !ZaloApp.IsFriend(c.ThreadId))
                    .OrderByDescending(c => c.LastTs)
                    .ToList()
                : new List<ZaloConversation>();

            if (strangers.Count == 0)
            {
                _emptyPanel.Visibility = Visibility.Visible;
                return;
            }

            _emptyPanel.Visibility = Visibility.Collapsed;

            foreach (var conv in strangers)
            {
                var row = CreateConversationRow(conv);
                _itemsStack.Children.Add(row);
            }
        }

        private FrameworkElement CreateConversationRow(ZaloConversation conv)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            double rowHeight = Math.Round(80 * scale);
            double avatarSize = Math.Round(52 * scale);
            double col0Width = Math.Round(60 * scale);

            var border = new Border
            {
                Height = rowHeight,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(Math.Round(14 * scale), 0, Math.Round(14 * scale), 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col0Width) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            var avatarGrid = new Grid { Width = avatarSize, Height = avatarSize, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = avatarSize, Height = avatarSize, Fill = conv.AvatarColor ?? _activeBlue };
            var initial = new TextBlock
            {
                Text = conv.Initial ?? "Z",
                FontSize = Math.Round(19 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);

            var brush = conv.AvatarImageBrush ?? ZaloAvatar.ImageFor(conv.AvatarUrl);
            if (brush != null)
            {
                ellipse.Fill = brush;
                initial.Text = string.Empty;
            }
            Grid.SetColumn(avatarGrid, 0);
            grid.Children.Add(avatarGrid);

            // Title & Snippet
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Math.Round(12 * scale), 0, Math.Round(8 * scale), 0) };
            textStack.Children.Add(new TextBlock
            {
                Text = conv.Name ?? "Người dùng Zalo",
                FontSize = Math.Round(18.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            textStack.Children.Add(new TextBlock
            {
                Text = conv.LastText ?? "Chưa có tin nhắn",
                FontSize = Math.Round(15 * scale, 1),
                Foreground = _textSub,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
                Margin = new Thickness(0, Math.Round(4 * scale), 0, 0)
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Time
            var timeText = new TextBlock
            {
                Text = conv.TimeText ?? string.Empty,
                FontSize = Math.Round(13 * scale, 1),
                Foreground = _textSub,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            Grid.SetColumn(timeText, 2);
            grid.Children.Add(timeText);

            border.Child = grid;

            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            border.Tapped += (s, e) => NavigationManager.NavigateTo(new ChatPage(conv.Key));

            return border;
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var b = new Border { Width = width, Height = height, Background = new SolidColorBrush(Colors.Transparent) };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }
    }
}