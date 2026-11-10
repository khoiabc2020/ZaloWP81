using System;
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
    public sealed class ProfilePage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x84, 0x90));

        private TextBlock _profileName;
        private TextBlock _profileUid;
        private TextBlock _profilePhone;
        private TextBlock _profileDobText;
        private TextBlock _profileGenderText;
        private TextBlock _profileStatus;
        private TextBlock _profileInitial;
        private Ellipse _profileAvatar;
        private TextBlock _profileFeedback;

        public ProfilePage()
        {
            BuildUI();
            ShowProfile();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF6, 0xF8))
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
                Text = "Trang cá nhân",
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

            // 2. ScrollViewer Body
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Card 1: Avatar & Main Profile Header
            var card1 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card1Stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 92, Height = 92, Margin = new Thickness(0, 2, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            var avatarBg = new Ellipse { Width = 92, Height = 92, Fill = _activeBlue };
            _profileInitial = new TextBlock
            {
                Text = "Z",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 34,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileAvatar = new Ellipse { Width = 92, Height = 92 };
            avatarGrid.Children.Add(avatarBg);
            avatarGrid.Children.Add(_profileInitial);
            avatarGrid.Children.Add(_profileAvatar);
            card1Stack.Children.Add(avatarGrid);

            _profileName = new TextBlock
            {
                Text = "Người dùng Zalo",
                FontSize = 21,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            card1Stack.Children.Add(_profileName);

            var subHeader = new TextBlock
            {
                Text = "Thông tin hồ sơ của bạn",
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(0, 4, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            card1Stack.Children.Add(subHeader);

            // Change Avatar Button
            var changeAvatarBtn = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            changeAvatarBtn.Child = new TextBlock { Text = "Đổi ảnh đại diện", FontSize = 13, Foreground = _activeBlue, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
            changeAvatarBtn.PointerPressed += (s, e) => changeAvatarBtn.Opacity = 0.6;
            changeAvatarBtn.PointerReleased += (s, e) => changeAvatarBtn.Opacity = 1.0;
            changeAvatarBtn.PointerCanceled += (s, e) => changeAvatarBtn.Opacity = 1.0;
            changeAvatarBtn.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Chọn ảnh đại diện từ bộ sưu tập:", "Đổi ảnh đại diện");
                md.Commands.Add(new UICommand("Hình 1", cmd => SetAvatar("ms-appx:///Assets/Backgrounds/bg_01.png")));
                md.Commands.Add(new UICommand("Hình 2", cmd => SetAvatar("ms-appx:///Assets/Backgrounds/bg_02.png")));
                md.Commands.Add(new UICommand("Đóng"));
                await md.ShowAsync();
            };
            card1Stack.Children.Add(changeAvatarBtn);

            // Edit Profile Button
            var editProfileBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            editProfileBtn.Child = new TextBlock { Text = "Chỉnh sửa hồ sơ", FontSize = 13.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
            editProfileBtn.PointerPressed += (s, e) => editProfileBtn.Opacity = 0.6;
            editProfileBtn.PointerReleased += (s, e) => editProfileBtn.Opacity = 1.0;
            editProfileBtn.PointerCanceled += (s, e) => editProfileBtn.Opacity = 1.0;
            editProfileBtn.Tapped += (s, e) => ShowEditProfileDialog();
            card1Stack.Children.Add(editProfileBtn);

            card1.Child = card1Stack;
            stack.Children.Add(card1);

            // Card 2: Profile Fields (Zalo ID, SĐT, Ngày sinh, Giới tính)
            var card2 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card2Stack = new StackPanel();

            _profileUid = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Zalo ID", _profileUid));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profilePhone = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Số điện thoại", _profilePhone));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profileDobText = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Ngày sinh", _profileDobText));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profileGenderText = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Giới tính", _profileGenderText));

            card2.Child = card2Stack;
            stack.Children.Add(card2);

            // Card 3: Status (Bio)
            var card3 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card3Stack = new StackPanel();
            card3Stack.Children.Add(new TextBlock { Text = "Trạng thái", FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });

            _profileStatus = new TextBlock
            {
                Text = "Chưa đặt trạng thái",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x80, 0x96)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 10)
            };
            card3Stack.Children.Add(_profileStatus);

            var editStatusBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            editStatusBtn.Child = new TextBlock { Text = "Chỉnh sửa trạng thái", FontSize = 13, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue, HorizontalAlignment = HorizontalAlignment.Center };
            editStatusBtn.PointerPressed += (s, e) => editStatusBtn.Opacity = 0.6;
            editStatusBtn.PointerReleased += (s, e) => editStatusBtn.Opacity = 1.0;
            editStatusBtn.PointerCanceled += (s, e) => editStatusBtn.Opacity = 1.0;
            editStatusBtn.Tapped += (s, e) => ShowEditStatusDialog();
            card3Stack.Children.Add(editStatusBtn);

            card3.Child = card3Stack;
            stack.Children.Add(card3);

            _profileFeedback = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x80, 0x96)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 4, 4, 0)
            };
            stack.Children.Add(_profileFeedback);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private Grid CreateFieldRow(string label, TextBlock valBlock)
        {
            var grid = new Grid { MinHeight = 52 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 14,
                Foreground = _textSub,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            return grid;
        }

        private void ShowProfile()
        {
            var session = ZaloApp.Session;
            if (session == null)
            {
                _profileFeedback.Text = "Chưa có tài khoản đăng nhập.";
                return;
            }

            string name = string.IsNullOrWhiteSpace(session.DisplayName) ? "Người dùng Zalo" : session.DisplayName;
            _profileName.Text = name;
            _profileInitial.Text = ZaloAvatar.InitialOf(name);
            _profileUid.Text = string.IsNullOrWhiteSpace(session.Uid) ? "Chưa có" : session.Uid;
            _profilePhone.Text = string.IsNullOrWhiteSpace(session.PhoneNumber) ? "Chưa công khai" : session.PhoneNumber;
            _profileStatus.Text = string.IsNullOrWhiteSpace(session.ProfileStatus) ? "Chưa đặt trạng thái" : session.ProfileStatus;
            _profileDobText.Text = string.IsNullOrWhiteSpace(session.ProfileDob) ? "Chưa công khai" : session.ProfileDob;
            _profileGenderText.Text = session.ProfileGender == 1 ? "Nữ" : "Nam";

            if (!string.IsNullOrWhiteSpace(session.AvatarUrl))
            {
                try
                {
                    _profileAvatar.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(session.AvatarUrl)), Stretch = Stretch.UniformToFill };
                    _profileInitial.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    _profileAvatar.Fill = new SolidColorBrush(Colors.Transparent);
                    _profileInitial.Visibility = Visibility.Visible;
                }
            }
            else
            {
                _profileAvatar.Fill = new SolidColorBrush(Colors.Transparent);
                _profileInitial.Visibility = Visibility.Visible;
            }
        }

        private void SetAvatar(string url)
        {
            var session = ZaloApp.Session;
            if (session == null) return;
            session.AvatarUrl = url;
            ShowProfile();
            _profileFeedback.Text = "Đã cập nhật ảnh đại diện thành công.";
            _ = ZaloStore.SaveAsync(session);
        }

        private void ShowEditProfileDialog()
        {
            var md = new MessageDialog("Chọn thao tác chỉnh sửa:", "Chỉnh sửa hồ sơ");
            md.Commands.Add(new UICommand("Đổi giới tính", cmd =>
            {
                var s = ZaloApp.Session;
                if (s != null)
                {
                    s.ProfileGender = s.ProfileGender == 1 ? 0 : 1;
                    ShowProfile();
                    _ = ZaloStore.SaveAsync(s);
                }
            }));
            md.Commands.Add(new UICommand("Cập nhật ngày sinh", cmd =>
            {
                var s = ZaloApp.Session;
                if (s != null)
                {
                    s.ProfileDob = "1998-10-10";
                    ShowProfile();
                    _ = ZaloStore.SaveAsync(s);
                }
            }));
            md.Commands.Add(new UICommand("Đóng"));
            var t = md.ShowAsync();
        }

        private void ShowEditStatusDialog()
        {
            var md = new MessageDialog("Chọn trạng thái mẫu hoặc xóa trạng thái:", "Trạng thái Zalo");
            md.Commands.Add(new UICommand("Đang bận, vui lòng nhắn tin", cmd => UpdateStatus("Đang bận, vui lòng nhắn tin")));
            md.Commands.Add(new UICommand("Đang online trên Windows Phone", cmd => UpdateStatus("Đang online trên Windows Phone!")));
            md.Commands.Add(new UICommand("Xóa trạng thái", cmd => UpdateStatus(string.Empty)));
            md.Commands.Add(new UICommand("Đóng"));
            var t = md.ShowAsync();
        }

        private void UpdateStatus(string st)
        {
            var s = ZaloApp.Session;
            if (s != null)
            {
                s.ProfileStatus = st;
                ShowProfile();
                _ = ZaloStore.SaveAsync(s);
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
