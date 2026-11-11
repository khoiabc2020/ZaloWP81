using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class CallPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x63, 0x72, 0x82));

        private Ellipse _avatarEllipse;
        private TextBlock _avatarInitial;
        private TextBlock _calleeNameText;

        public CallPage()
        {
            BuildUI();
        }

        public CallPage(object target) : this()
        {
            PopulateTarget(target);
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Top Bar
            var headerBorder = new Border { Background = _activeBlue, Padding = new Thickness(6, 0, 12, 0) };
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
                Text = "Cuộc gọi Zalo",
                FontSize = 18,
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

            // 2. Center Callee Card
            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 360,
                Margin = new Thickness(24, 0, 24, 0)
            };

            var avatarGrid = new Grid
            {
                Width = 104,
                Height = 104,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 18)
            };
            _avatarEllipse = new Ellipse { Width = 104, Height = 104, Fill = _activeBlue };
            _avatarInitial = new TextBlock
            {
                Text = "Z",
                FontSize = 42,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_avatarEllipse);
            avatarGrid.Children.Add(_avatarInitial);
            centerStack.Children.Add(avatarGrid);

            _calleeNameText = new TextBlock
            {
                Text = "Người nhận",
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            centerStack.Children.Add(_calleeNameText);

            var infoCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 24, 0, 0)
            };
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = "Chưa thể gọi tài khoản cá nhân",
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "Ứng dụng chưa có dịch vụ kết nối cuộc gọi Zalo trên Windows Phone 8.1.",
                FontSize = 14,
                Foreground = _textSub,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
            infoCard.Child = infoStack;
            centerStack.Children.Add(infoCard);

            var returnBtn = new Button
            {
                Content = "Quay lại cuộc trò chuyện",
                Background = _activeBlue,
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Thickness(16, 11, 16, 11),
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(0),
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            returnBtn.Click += (s, e) =>
            {
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            centerStack.Children.Add(returnBtn);

            Grid.SetRow(centerStack, 1);
            rootGrid.Children.Add(centerStack);

            this.Content = rootGrid;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                PopulateTarget(e.Parameter);
            }
        }

        private void PopulateTarget(object target)
        {
            if (target is ZaloFriend friend)
            {
                _calleeNameText.Text = friend.Name ?? "Bạn bè Zalo";
                _avatarInitial.Text = ZaloAvatar.InitialOf(friend.Name);
                _avatarEllipse.Fill = friend.AvatarColor ?? _activeBlue;
                if (!string.IsNullOrEmpty(friend.Avatar))
                {
                    try
                    {
                        var brush = friend.AvatarImageBrush ?? ZaloAvatar.ImageFor(friend.Avatar);
                        if (brush != null)
                        {
                            _avatarEllipse.Fill = brush;
                            _avatarInitial.Text = string.Empty;
                        }
                    }
                    catch { }
                }
            }
            else if (target is ZaloConversation conv)
            {
                _calleeNameText.Text = conv.Name ?? "Trò chuyện";
                _avatarInitial.Text = conv.Initial ?? "Z";
                _avatarEllipse.Fill = conv.AvatarColor ?? _activeBlue;
                var brush = conv.AvatarImageBrush ?? ZaloAvatar.ImageFor(conv.AvatarUrl);
                if (brush != null)
                {
                    _avatarEllipse.Fill = brush;
                    _avatarInitial.Text = string.Empty;
                }
            }
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
