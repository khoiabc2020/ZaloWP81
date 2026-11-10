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
    public sealed class FriendProfilePage : Page
    {
        private readonly ZaloFriend _friend;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        public FriendProfilePage(ZaloFriend friend)
        {
            _friend = friend;
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

            var backBtn = CreateTouchBorder(44, 44);
            backBtn.Child = new TextBlock
            {
                Text = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Tapped += (s, e) => NavigationManager.GoBack();
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var title = new TextBlock
            {
                Text = "Thông tin bạn bè",
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

            // 2. Body
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Profile Card
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 24, 16, 24),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var cardStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse
            {
                Width = 84,
                Height = 84,
                Fill = _friend?.AvatarColor ?? _activeBlue
            };
            var initial = new TextBlock
            {
                Text = _friend?.Initial ?? "?",
                FontSize = 30,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avatarEllipse);
            avatarGrid.Children.Add(initial);
            cardStack.Children.Add(avatarGrid);

            var nameBlock = new TextBlock
            {
                Text = _friend?.Name ?? "Bạn bè",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            };
            cardStack.Children.Add(nameBlock);

            var phoneBlock = new TextBlock
            {
                Text = _friend?.Subtitle ?? "Bạn bè Zalo",
                FontSize = 13.5,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            };
            cardStack.Children.Add(phoneBlock);

            // Action Buttons (Nhắn tin, Gọi điện)
            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var chatBtn = new Button
            {
                Content = "💬  Nhắn tin",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 10, 0)
            };
            chatBtn.Click += (s, e) =>
            {
                if (_friend == null) return;
                var conv = ZaloApp.GetOrCreate(_friend.UserId, false);
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };
            actionRow.Children.Add(chatBtn);

            var callBtn = new Button
            {
                Content = "📞  Gọi thoại",
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            callBtn.Click += async (s, e) =>
            {
                var md = new MessageDialog("Tính năng cuộc gọi thoại đang kết nối tới máy chủ.", "Zalo Call");
                await md.ShowAsync();
            };
            actionRow.Children.Add(callBtn);

            cardStack.Children.Add(actionRow);
            card.Child = cardStack;
            stack.Children.Add(card);

            // Options Group
            var optionsGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var optionsStack = new StackPanel();

            var blockBorder = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var blockText = new TextBlock
            {
                Text = "Chặn người này",
                FontSize = 14.5,
                Foreground = _textDark
            };
            blockBorder.Child = blockText;
            blockBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog($"Bạn có chắc muốn chặn {_friend?.Name}? Bạn sẽ không nhận được tin nhắn từ người này.", "Chặn bạn bè");
                md.Commands.Add(new UICommand("Chặn", cmd =>
                {
                    if (_friend != null)
                    {
                        var blocked = ZaloSettingsService.Current.GetBlockedUserIds(ZaloApp.Session?.Uid);
                        if (!blocked.Contains(_friend.UserId)) blocked.Add(_friend.UserId);
                        ZaloSettingsService.Current.SetBlockedUserIds(ZaloApp.Session?.Uid, blocked);
                    }
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            optionsStack.Children.Add(blockBorder);

            var deleteFriendBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var deleteFriendText = new TextBlock
            {
                Text = "Xóa khỏi danh bạ",
                FontSize = 14.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26))
            };
            deleteFriendBorder.Child = deleteFriendText;
            deleteFriendBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog($"Bạn có chắc muốn xóa {_friend?.Name} khỏi danh bạ bạn bè?", "Xóa bạn bè");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    if (_friend != null) ZaloApp.Friends.Remove(_friend);
                    NavigationManager.GoBack();
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            optionsStack.Children.Add(deleteFriendBorder);

            optionsGroup.Child = optionsStack;
            stack.Children.Add(optionsGroup);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
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

