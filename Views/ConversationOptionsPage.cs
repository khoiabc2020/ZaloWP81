using System;
using System.Linq;
using Windows.UI;
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
    public sealed class ConversationOptionsPage : Page
    {
        private readonly ZaloConversation _conversation;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBlock _muteStatusText;
        private TextBlock _pinStatusText;

        public ConversationOptionsPage(ZaloConversation conversation)
        {
            _conversation = conversation;
            BuildUI();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Header (Gradient)
            var headerBorder = new Border { Padding = new Thickness(6, 0, 10, 0) };
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerBorder.Background = gradient;

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var backBtn = new Button
            {
                Content = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Width = 44,
                Height = 44,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };
            backBtn.Click += (s, e) => NavigationManager.GoBack();
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var title = new TextBlock
            {
                Text = "Tùy chọn cuộc trò chuyện",
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Scrollable Body
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Avatar & Info Card
            var infoCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 20, 16, 20),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var infoStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse
            {
                Width = 72,
                Height = 72,
                Fill = _conversation?.AvatarColor ?? _activeBlue
            };
            var initial = new TextBlock
            {
                Text = _conversation?.Initial ?? "?",
                FontSize = 26,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avatarEllipse);
            avatarGrid.Children.Add(initial);

            if (_conversation?.AvatarImageBrush != null)
            {
                var imgEllipse = new Ellipse { Width = 72, Height = 72, Fill = _conversation.AvatarImageBrush };
                avatarGrid.Children.Add(imgEllipse);
            }
            infoStack.Children.Add(avatarGrid);

            var nameBlock = new TextBlock
            {
                Text = _conversation?.Name ?? "Cuộc trò chuyện",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 2)
            };
            infoStack.Children.Add(nameBlock);

            var subBlock = new TextBlock
            {
                Text = _conversation != null && _conversation.IsGroup ? $"Nhóm chat ({_conversation.GroupMemberCount} thành viên)" : "Trò chuyện cá nhân",
                FontSize = 13,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            infoStack.Children.Add(subBlock);

            infoCard.Child = infoStack;
            stack.Children.Add(infoCard);

            // Action Group 1: Notifications & Pinning
            var group1 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var group1Stack = new StackPanel();

            // Mute Row
            var muteBorder = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var muteGrid = new Grid();
            muteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            muteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            muteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var muteIcon = new TextBlock { Text = "🔔", FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(muteIcon, 0);
            muteGrid.Children.Add(muteIcon);

            var muteTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            muteTextStack.Children.Add(new TextBlock { Text = "Thông báo cuộc trò chuyện", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            _muteStatusText = new TextBlock { Text = _conversation != null && _conversation.IsMuted ? "Đang tắt chuông" : "Đang bật", FontSize = 12, Foreground = _textSub };
            muteTextStack.Children.Add(_muteStatusText);
            Grid.SetColumn(muteTextStack, 1);
            muteGrid.Children.Add(muteTextStack);

            var muteAction = new TextBlock { Text = "Thay đổi", FontSize = 13, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(muteAction, 2);
            muteGrid.Children.Add(muteAction);

            muteBorder.Child = muteGrid;
            muteBorder.Tapped += (s, e) =>
            {
                if (_conversation == null) return;
                _conversation.IsMuted = !_conversation.IsMuted;
                _muteStatusText.Text = _conversation.IsMuted ? "Đang tắt chuông" : "Đang bật";
            };
            group1Stack.Children.Add(muteBorder);

            // Pin Row
            var pinBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var pinGrid = new Grid();
            pinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            pinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pinIcon = new TextBlock { Text = "📌", FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pinIcon, 0);
            pinGrid.Children.Add(pinIcon);

            var pinTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            pinTextStack.Children.Add(new TextBlock { Text = "Ghim lên đầu danh sách", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            _pinStatusText = new TextBlock { Text = _conversation != null && _conversation.IsPinned ? "Đã ghim" : "Chưa ghim", FontSize = 12, Foreground = _textSub };
            pinTextStack.Children.Add(_pinStatusText);
            Grid.SetColumn(pinTextStack, 1);
            pinGrid.Children.Add(pinTextStack);

            var pinAction = new TextBlock { Text = "Thay đổi", FontSize = 13, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pinAction, 2);
            pinGrid.Children.Add(pinAction);

            pinBorder.Child = pinGrid;
            pinBorder.Tapped += (s, e) =>
            {
                if (_conversation == null) return;
                _conversation.IsPinned = !_conversation.IsPinned;
                _pinStatusText.Text = _conversation.IsPinned ? "Đã ghim" : "Chưa ghim";
            };
            group1Stack.Children.Add(pinBorder);

            group1.Child = group1Stack;
            stack.Children.Add(group1);

            // Action Group 2: Wallpaper (zStyle)
            var wallpaperGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var wpStack = new StackPanel();
            wpStack.Children.Add(new TextBlock
            {
                Text = "Hình nền trò chuyện (zStyle)",
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var wpScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var wpRow = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 1; i <= 6; i++)
            {
                string wpUri = $"ms-appx:///Assets/Backgrounds/bg_{i:D2}.png";
                var wpBtn = new Border
                {
                    Width = 56,
                    Height = 80,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                    BorderThickness = new Thickness(1)
                };
                var wpImg = new Image
                {
                    Source = new BitmapImage(new Uri(wpUri)),
                    Stretch = Stretch.UniformToFill
                };
                wpBtn.Child = wpImg;
                wpBtn.Tapped += (s, e) =>
                {
                    if (_conversation != null)
                    {
                        ZaloSettingsService.Current.SetConversationWallpaper(ZaloApp.Session?.Uid, _conversation.Key, wpUri);
                        var md = new MessageDialog("Đã đổi hình nền cho cuộc trò chuyện này!", "Thành công");
                        var t = md.ShowAsync();
                    }
                };
                wpRow.Children.Add(wpBtn);
            }
            wpScroll.Content = wpRow;
            wpStack.Children.Add(wpScroll);

            wallpaperGroup.Child = wpStack;
            stack.Children.Add(wallpaperGroup);

            // Action Group 3: Delete & Clear History
            var dangerGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var dangerStack = new StackPanel();

            var clearBorder = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var clearText = new TextBlock
            {
                Text = "Xóa toàn bộ lịch sử tin nhắn",
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x4B, 0x4B))
            };
            clearBorder.Child = clearText;
            clearBorder.Tapped += async (s, e) =>
            {
                if (_conversation == null) return;
                var md = new MessageDialog("Bạn có chắc muốn xóa tất cả tin nhắn trong cuộc trò chuyện này?", "Xác nhận xóa");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    _conversation.Messages.Clear();
                    _conversation.LastText = string.Empty;
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(clearBorder);

            var deleteBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var deleteText = new TextBlock
            {
                Text = "Xóa cuộc trò chuyện",
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26))
            };
            deleteBorder.Child = deleteText;
            deleteBorder.Tapped += async (s, e) =>
            {
                if (_conversation == null) return;
                var md = new MessageDialog("Bạn có chắc muốn xóa cuộc trò chuyện này khỏi danh sách?", "Xác nhận xóa");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    ZaloApp.Conversations.Remove(_conversation);
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(deleteBorder);

            dangerGroup.Child = dangerStack;
            stack.Children.Add(dangerGroup);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }
    }
}
