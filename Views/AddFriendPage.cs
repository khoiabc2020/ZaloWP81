using System;
using System.Linq;
using System.Threading;
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
    public sealed class AddFriendPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBox _phoneBox;
        private Border _resultCard;
        private TextBlock _foundName;
        private TextBlock _foundPhone;
        private Ellipse _foundAvatar;
        private TextBlock _foundInitial;
        private TextBlock _statusText;
        private ZaloFriend _foundFriend;

        public AddFriendPage()
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

            var backBtn = CreateTouchBorder(44, 44);
            backBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_back_clean_white.png")),
                Width = 24,
                Height = 24,
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
                Text = "Thêm bạn mới",
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

            var searchCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var searchStack = new StackPanel();
            searchStack.Children.Add(new TextBlock
            {
                Text = "Tìm bạn bằng số điện thoại",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _phoneBox = new TextBox
            {
                PlaceholderText = "Nhập số điện thoại (VD: 0912345678)",
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 12),
                InputScope = new Windows.UI.Xaml.Input.InputScope
                {
                    Names = { new Windows.UI.Xaml.Input.InputScopeName(Windows.UI.Xaml.Input.InputScopeNameValue.TelephoneNumber) }
                }
            };
            searchStack.Children.Add(_phoneBox);

            var searchBtn = new Button
            {
                Content = "Tìm kiếm",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(16, 9, 16, 9),
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            searchBtn.Click += (s, e) => PerformSearch();
            searchStack.Children.Add(searchBtn);

            _statusText = new TextBlock
            {
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            searchStack.Children.Add(_statusText);

            searchCard.Child = searchStack;
            stack.Children.Add(searchCard);

            // Result Card
            _resultCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 18, 16, 18),
                Margin = new Thickness(0, 0, 0, 16),
                Visibility = Visibility.Collapsed
            };
            var resultStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center };
            _foundAvatar = new Ellipse { Width = 64, Height = 64, Fill = _activeBlue };
            _foundInitial = new TextBlock
            {
                Text = "?",
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_foundAvatar);
            avatarGrid.Children.Add(_foundInitial);
            resultStack.Children.Add(avatarGrid);

            _foundName = new TextBlock
            {
                Text = "Tên người dùng",
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 2)
            };
            resultStack.Children.Add(_foundName);

            _foundPhone = new TextBlock
            {
                Text = "09xx xxx xxx",
                FontSize = 13,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            resultStack.Children.Add(_foundPhone);

            var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var addBtn = new Button
            {
                Content = "Kết bạn",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 7, 18, 7),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0)
            };
            addBtn.Click += async (s, e) =>
            {
                if (_foundFriend == null) return;
                addBtn.IsEnabled = false;
                try
                {
                    if (ZaloApp.Session != null)
                    {
                        await ZaloAuth.SendFriendRequestAsync(ZaloApp.Session, _foundFriend.UserId,
                            "Xin chào, mình muốn kết bạn với bạn trên Zalo.", CancellationToken.None);
                    }
                    if (!ZaloApp.Friends.Any(f => f.UserId == _foundFriend.UserId))
                    {
                        ZaloApp.Friends.Add(_foundFriend);
                    }
                    var md = new MessageDialog("Đã gửi lời mời kết bạn thành công!", "Thông báo");
                    await md.ShowAsync();
                    addBtn.Content = "Đã gửi";
                }
                catch (Exception ex)
                {
                    var md = new MessageDialog("Không thể gửi lời mời: " + ex.Message, "Thông báo");
                    await md.ShowAsync();
                    addBtn.IsEnabled = true;
                }
            };
            actionRow.Children.Add(addBtn);

            var chatBtn = new Button
            {
                Content = "Nhắn tin",
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 7, 18, 7),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            chatBtn.Click += (s, e) =>
            {
                if (_foundFriend != null)
                {
                    var conv = ZaloApp.GetOrCreate(_foundFriend.UserId, false);
                    NavigationManager.NavigateTo(new ChatPage(conv.Key));
                }
            };
            actionRow.Children.Add(chatBtn);

            resultStack.Children.Add(actionRow);
            _resultCard.Child = resultStack;
            stack.Children.Add(_resultCard);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private async void PerformSearch()
        {
            string phone = _phoneBox.Text?.Trim();
            if (string.IsNullOrEmpty(phone) || phone.Length < 9)
            {
                _statusText.Text = "Vui lòng nhập số điện thoại hợp lệ (từ 9 số trở lên).";
                _statusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26));
                _statusText.Visibility = Visibility.Visible;
                _resultCard.Visibility = Visibility.Collapsed;
                return;
            }

            _statusText.Text = "Đang tìm kiếm tài khoản...";
            _statusText.Foreground = _activeBlue;
            _statusText.Visibility = Visibility.Visible;
            _resultCard.Visibility = Visibility.Collapsed;

            try
            {
                if (ZaloApp.Session != null)
                {
                    _foundFriend = await ZaloAuth.FindFriendByPhoneAsync(ZaloApp.Session, phone, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _foundFriend = null;
                _statusText.Text = ex.Message;
                _statusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x43, 0x43));
                return;
            }

            if (_foundFriend == null)
            {
                _statusText.Text = "Không tìm thấy tài khoản Zalo với số điện thoại này.";
                _statusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x43, 0x43));
                return;
            }

            _foundName.Text = _foundFriend.Name;
            _foundPhone.Text = phone;
            _foundInitial.Text = ZaloAvatar.InitialOf(_foundFriend.Name);
            _statusText.Text = "Đã tìm thấy tài khoản Zalo.";
            _statusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            _resultCard.Visibility = Visibility.Visible;
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var b = new Border { Width = width, Height = height, Background = new SolidColorBrush(Colors.Transparent) };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
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
