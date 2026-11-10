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
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBox _nameBox;
        private TextBox _statusBox;

        public ProfilePage()
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

            // 2. Body
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            var session = ZaloApp.Session;

            // Avatar Card
            var avatarCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 20, 16, 20),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var avatarStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse
            {
                Width = 84,
                Height = 84,
                Fill = _activeBlue
            };
            var initial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(session?.DisplayName ?? "Zalo"),
                FontSize = 32,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avatarEllipse);
            avatarGrid.Children.Add(initial);

            if (!string.IsNullOrEmpty(session?.AvatarUrl))
            {
                try
                {
                    var img = new Ellipse
                    {
                        Width = 84,
                        Height = 84,
                        Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(session.AvatarUrl)), Stretch = Stretch.UniformToFill }
                    };
                    avatarGrid.Children.Add(img);
                }
                catch { }
            }

            avatarStack.Children.Add(avatarGrid);

            var nameHeader = new TextBlock
            {
                Text = session?.DisplayName ?? "Người dùng Zalo",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 2)
            };
            avatarStack.Children.Add(nameHeader);

            var phoneHeader = new TextBlock
            {
                Text = session?.PhoneNumber ?? "Chưa công khai",
                FontSize = 13.5,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            avatarStack.Children.Add(phoneHeader);

            avatarCard.Child = avatarStack;
            stack.Children.Add(avatarCard);

            // Information Card
            var infoCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 16, 16, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var infoStack = new StackPanel();

            infoStack.Children.Add(new TextBlock { Text = "Tên hiển thị:", FontSize = 12.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textSub });
            _nameBox = new TextBox
            {
                Text = session?.DisplayName ?? string.Empty,
                FontSize = 15,
                Margin = new Thickness(0, 4, 0, 12)
            };
            infoStack.Children.Add(_nameBox);

            infoStack.Children.Add(new TextBlock { Text = "Trạng thái (Bio):", FontSize = 12.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textSub });
            _statusBox = new TextBox
            {
                Text = session?.ProfileStatus ?? "Đang dùng Zalo trên Windows Phone!",
                FontSize = 15,
                Margin = new Thickness(0, 4, 0, 12)
            };
            infoStack.Children.Add(_statusBox);

            infoStack.Children.Add(CreateReadOnlyField("Mã tài khoản (UID):", session?.Uid ?? "Chưa có"));
            infoStack.Children.Add(CreateReadOnlyField("Số điện thoại:", session?.PhoneNumber ?? "Chưa công khai"));
            infoStack.Children.Add(CreateReadOnlyField("Giới tính:", session?.ProfileGender == 1 ? "Nữ" : "Nam"));

            // Save Button
            var saveBtn = new Button
            {
                Content = "Lưu thay đổi",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 10, 16, 10),
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 0)
            };
            saveBtn.Click += async (s, e) =>
            {
                if (session != null)
                {
                    string newName = _nameBox.Text?.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        session.DisplayName = newName;
                    }
                    session.ProfileStatus = _statusBox.Text?.Trim();
                    await ZaloStore.SaveAsync(session);

                    var md = new MessageDialog("Đã cập nhật thông tin hồ sơ cá nhân thành công!", "Thông báo");
                    await md.ShowAsync();
                }
            };
            infoStack.Children.Add(saveBtn);

            infoCard.Child = infoStack;
            stack.Children.Add(infoCard);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private StackPanel CreateReadOnlyField(string label, string val)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textSub });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 14.5, Foreground = _textDark, Margin = new Thickness(0, 2, 0, 0) });
            return sp;
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
