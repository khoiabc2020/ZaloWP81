using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class CreateGroupPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBox _nameBox;
        private readonly HashSet<string> _selectedFriendIds = new HashSet<string>();
        private TextBlock _selectedCountText;

        public CreateGroupPage()
        {
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

            var backBtn = CreateTouchButton("←", 22, Colors.White, 44, 44);
            backBtn.Tapped += (s, e) => NavigationManager.GoBack();
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var title = new TextBlock
            {
                Text = "Tạo nhóm mới",
                FontSize = 18,
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

            // Name Card
            var nameCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var nameStack = new StackPanel();
            nameStack.Children.Add(new TextBlock
            {
                Text = "Đặt tên nhóm",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _nameBox = new TextBox
            {
                PlaceholderText = "Nhập tên nhóm trò chuyện...",
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 8)
            };
            nameStack.Children.Add(_nameBox);

            _selectedCountText = new TextBlock
            {
                Text = "Đã chọn: 0 thành viên",
                FontSize = 13,
                Foreground = _activeBlue,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            nameStack.Children.Add(_selectedCountText);

            nameCard.Child = nameStack;
            stack.Children.Add(nameCard);

            // Friends Selection
            var listCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var listStack = new StackPanel();
            listStack.Children.Add(new TextBlock
            {
                Text = "CHỌN THÀNH VIÊN TỪ DANH BẠ",
                FontSize = 11.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub,
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (var friend in ZaloApp.Friends)
            {
                var row = CreateFriendSelectRow(friend);
                listStack.Children.Add(row);
            }

            listCard.Child = listStack;
            stack.Children.Add(listCard);

            // Create Button
            var createBtn = new Button
            {
                Content = "Tạo nhóm trò chuyện",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            createBtn.Click += (s, e) => PerformCreateGroup();
            stack.Children.Add(createBtn);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private FrameworkElement CreateFriendSelectRow(ZaloFriend friend)
        {
            var border = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

            var avatarGrid = new Grid { Width = 38, Height = 38, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = 38, Height = 38, Fill = friend.AvatarColor ?? _activeBlue };
            var initial = new TextBlock
            {
                Text = friend.Initial ?? "?",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);
            Grid.SetColumn(avatarGrid, 0);
            grid.Children.Add(avatarGrid);

            var nameText = new TextBlock
            {
                Text = friend.Name,
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            var checkIndicator = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBD, 0xC3, 0xC7)),
                BorderThickness = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var checkIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_check_clean_white.png")),
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            checkIndicator.Child = checkIcon;
            Grid.SetColumn(checkIndicator, 2);
            grid.Children.Add(checkIndicator);

            border.Child = grid;
            border.Tapped += (s, e) =>
            {
                if (_selectedFriendIds.Contains(friend.UserId))
                {
                    _selectedFriendIds.Remove(friend.UserId);
                    checkIndicator.Background = new SolidColorBrush(Colors.Transparent);
                    checkIndicator.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBD, 0xC3, 0xC7));
                    checkIcon.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _selectedFriendIds.Add(friend.UserId);
                    checkIndicator.Background = _activeBlue;
                    checkIndicator.BorderBrush = _activeBlue;
                    checkIcon.Visibility = Visibility.Visible;
                }
                _selectedCountText.Text = $"Đã chọn: {_selectedFriendIds.Count} thành viên";
            };

            return border;
        }

        private async void PerformCreateGroup()
        {
            string name = _nameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                var md = new MessageDialog("Vui lòng nhập tên cho nhóm trò chuyện!", "Chưa đặt tên");
                await md.ShowAsync();
                return;
            }

            if (_selectedFriendIds.Count == 0)
            {
                var md = new MessageDialog("Vui lòng chọn ít nhất 1 thành viên từ danh bạ để tạo nhóm!", "Chưa chọn thành viên");
                await md.ShowAsync();
                return;
            }

            long nowMs = DateTime.UtcNow.Ticks / 10000;
            string groupKey = "group_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var newGroup = new ZaloConversation
            {
                Key = groupKey,
                Name = name,
                IsGroup = true,
                GroupMemberCount = _selectedFriendIds.Count + 1,
                LastText = "Đã tạo nhóm trò chuyện",
                LastTs = nowMs
            };
            ZaloApp.Conversations.Insert(0, newGroup);

            NavigationManager.NavigateTo(new ChatPage(newGroup.Key));
        }

        private static Border CreateTouchButton(string text, double fontSize, Color fg, double width, double height)
        {
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Colors.Transparent)
            };
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = new SolidColorBrush(fg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = tb;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }
    }
}

