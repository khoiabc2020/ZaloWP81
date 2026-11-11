using System;
using System.IO;
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
    public sealed class SettingsPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBlock _soundStatusText;
        private TextBlock _enterToSendStatusText;
        private Border[] _wallpaperBorders = new Border[20];

        public SettingsPage()
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
                Text = "Cài đặt",
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

            // Section 0: Thu phóng giao diện (UI Scale)
            stack.Children.Add(CreateSectionHeader("THU PHÓNG GIAO DIỆN (UI SCALE)"));

            var uiScaleGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 14, 14, 14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var uiScaleStack = new StackPanel();

            uiScaleStack.Children.Add(new TextBlock
            {
                Text = "Tỷ lệ kích thước toàn bộ ứng dụng:",
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark
            });
            uiScaleStack.Children.Add(new TextBlock
            {
                Text = "Tùy chỉnh tỷ lệ (100%, 125%, 150%, 175%) phù hợp với màn hình lớn như Lumia 640 XL. Hiển thị phóng to rõ nét mượt mà 60 FPS, không méo hay giật lag.",
                FontSize = 12,
                Foreground = _textSub,
                Margin = new Thickness(0, 2, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            // 4 UI Scale pills: 100%, 125%, 150%, 175%
            var uiScaleGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int k = 0; k < 4; k++)
                uiScaleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            double[] scaleFactors = new double[] { 1.0, 1.25, 1.50, 1.75 };
            string[] scaleLabels = new string[] { "100%", "125%", "150%", "175%" };
            var scaleBorders = new Border[4];
            var scaleTexts = new TextBlock[4];

            double currentUiScale = ZaloSettingsService.Current.UiScaleFactor;

            var scaleStatusText = new TextBlock
            {
                Text = $"Đang chọn: {(int)(currentUiScale * 100)}% · Áp dụng ngay lập tức cho các danh sách và trang trò chuyện.",
                FontSize = 12,
                Foreground = _activeBlue,
                Margin = new Thickness(2, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            for (int k = 0; k < 4; k++)
            {
                int pillIdx = k;
                double factor = scaleFactors[k];
                bool isSelected = Math.Abs(currentUiScale - factor) < 0.05;

                var pBorder = new Border
                {
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(pillIdx == 0 ? 0 : 3, 0, pillIdx == 3 ? 0 : 3, 0),
                    Background = isSelected ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9))
                };
                var pText = new TextBlock
                {
                    Text = scaleLabels[k],
                    FontSize = 13,
                    FontWeight = isSelected ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                    Foreground = isSelected ? new SolidColorBrush(Colors.White) : _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pBorder.Child = pText;
                scaleBorders[k] = pBorder;
                scaleTexts[k] = pText;

                pBorder.Tapped += (s, e) =>
                {
                    ZaloSettingsService.Current.UiScaleFactor = factor;
                    scaleStatusText.Text = $"Đang chọn: {(int)(factor * 100)}% · Đã lưu! Các trang tiếp theo sẽ áp dụng kích thước này.";
                    for (int m = 0; m < 4; m++)
                    {
                        bool sel = m == pillIdx;
                        scaleBorders[m].Background = sel ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
                        scaleTexts[m].Foreground = sel ? new SolidColorBrush(Colors.White) : _textDark;
                        scaleTexts[m].FontWeight = sel ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
                    }
                };

                Grid.SetColumn(pBorder, k);
                uiScaleGrid.Children.Add(pBorder);
            }

            uiScaleStack.Children.Add(uiScaleGrid);
            uiScaleStack.Children.Add(scaleStatusText);
            uiScaleGroup.Child = uiScaleStack;
            stack.Children.Add(uiScaleGroup);

            // Section 1: Cỡ chữ trò chuyện (Chat Font Size)
            stack.Children.Add(CreateSectionHeader("CỠ CHỮ TRÒ CHUYỆN"));

            var scaleGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 14, 14, 14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var scaleStack = new StackPanel();

            scaleStack.Children.Add(new TextBlock
            {
                Text = "Kích thước chữ tin nhắn:",
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark
            });
            scaleStack.Children.Add(new TextBlock
            {
                Text = "Tăng giảm cỡ chữ tin nhắn để dễ nhìn hơn trên màn hình của bạn",
                FontSize = 12,
                Foreground = _textSub,
                Margin = new Thickness(0, 2, 0, 10)
            });

            // 4 Font size pills
            var fontPillGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            for (int k = 0; k < 4; k++)
                fontPillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            double[] fontSizes = new double[] { 13.5, 15.5, 17.5, 19.5 };
            string[] fontLabels = new string[] { "Nhỏ", "Vừa", "Lớn", "Rất lớn" };
            var fontBorders = new Border[4];
            var fontTexts = new TextBlock[4];

            double currentFontSize = ZaloSettingsService.Current.MessageFontSize;

            // Preview bubble definition
            var previewBubble = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0xF2, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xBA, 0xE6, 0xFD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var previewText = new TextBlock
            {
                Text = "Chào bạn! Cỡ chữ tin nhắn sẽ hiển thị như thế này.",
                FontSize = currentFontSize,
                Foreground = _textDark,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 260
            };
            var previewTime = new TextBlock
            {
                Text = "10:30  ✓✓ Đã xem",
                FontSize = 11,
                Foreground = _activeBlue,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };
            var previewStack = new StackPanel();
            previewStack.Children.Add(previewText);
            previewStack.Children.Add(previewTime);
            previewBubble.Child = previewStack;

            for (int k = 0; k < 4; k++)
            {
                int pillIdx = k;
                double fs = fontSizes[k];
                bool isSelected = Math.Abs(currentFontSize - fs) < 0.5;

                var pBorder = new Border
                {
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(pillIdx == 0 ? 0 : 3, 0, pillIdx == 3 ? 0 : 3, 0),
                    Background = isSelected ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9))
                };
                var pText = new TextBlock
                {
                    Text = fontLabels[k],
                    FontSize = 13,
                    FontWeight = isSelected ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                    Foreground = isSelected ? new SolidColorBrush(Colors.White) : _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pBorder.Child = pText;
                fontBorders[k] = pBorder;
                fontTexts[k] = pText;

                pBorder.Tapped += (s, e) =>
                {
                    ZaloSettingsService.Current.MessageFontSize = fs;
                    previewText.FontSize = fs;
                    for (int m = 0; m < 4; m++)
                    {
                        bool sel = m == pillIdx;
                        fontBorders[m].Background = sel ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
                        fontTexts[m].Foreground = sel ? new SolidColorBrush(Colors.White) : _textDark;
                        fontTexts[m].FontWeight = sel ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
                    }
                };

                Grid.SetColumn(pBorder, k);
                fontPillGrid.Children.Add(pBorder);
            }

            scaleStack.Children.Add(fontPillGrid);
            scaleStack.Children.Add(previewBubble);

            scaleGroup.Child = scaleStack;
            stack.Children.Add(scaleGroup);

            // Section 1: Giao diện & Hình nền trò chuyện (zStyle)
            stack.Children.Add(CreateSectionHeader("GIAO DIỆN & HÌNH NỀN (ZSTYLE)"));

            var wpGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 14, 14, 14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var wpStack = new StackPanel();
            wpStack.Children.Add(new TextBlock
            {
                Text = "Chọn hình nền mặc định cho cuộc trò chuyện:",
                FontSize = 13.5,
                Foreground = _textSub,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var wpScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var wpRow = new StackPanel { Orientation = Orientation.Horizontal };

            string currentWp = ZaloSettingsService.Current.SelectedWallpaper;
            for (int i = 1; i <= 20; i++)
            {
                int wpIdx = i;
                string wpUri = $"ms-appx:///Assets/Backgrounds/bg_{i:D2}.png";
                bool isSelected = string.Equals(currentWp, wpUri, StringComparison.OrdinalIgnoreCase);

                var border = new Border
                {
                    Width = 60,
                    Height = 88,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderThickness = new Thickness(isSelected ? 3 : 1),
                    BorderBrush = isSelected ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xE0, 0xE5))
                };
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(wpUri)),
                    Stretch = Stretch.UniformToFill
                };
                border.Child = img;
                _wallpaperBorders[i - 1] = border;

                border.Tapped += (s, e) =>
                {
                    ZaloSettingsService.Current.SelectedWallpaper = wpUri;
                    for (int j = 0; j < 20; j++)
                    {
                        if (_wallpaperBorders[j] != null)
                        {
                            bool sel = (j + 1) == wpIdx;
                            _wallpaperBorders[j].BorderThickness = new Thickness(sel ? 3 : 1);
                            _wallpaperBorders[j].BorderBrush = sel ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xE0, 0xE5));
                        }
                    }
                };

                wpRow.Children.Add(border);
            }
            wpScroll.Content = wpRow;
            wpStack.Children.Add(wpScroll);
            wpGroup.Child = wpStack;
            stack.Children.Add(wpGroup);

            // Section 2: Thông báo & Âm thanh
            stack.Children.Add(CreateSectionHeader("THÔNG BÁO & ÂM THANH"));

            var notifGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var notifStack = new StackPanel();

            var soundRow = CreateSettingRow("Âm thanh thông báo", "Phát âm thanh khi có tin nhắn mới",
                ZaloSettingsService.Current.IsSoundEnabled ? "Đang bật" : "Đang tắt", out _soundStatusText);
            soundRow.Tapped += (s, e) =>
            {
                ZaloSettingsService.Current.IsSoundEnabled = !ZaloSettingsService.Current.IsSoundEnabled;
                _soundStatusText.Text = ZaloSettingsService.Current.IsSoundEnabled ? "Đang bật" : "Đang tắt";
            };
            notifStack.Children.Add(soundRow);

            var enterRow = CreateSettingRow("Phím Enter gửi tin nhắn", "Bấm Enter trên bàn phím để gửi nhanh",
                ZaloSettingsService.Current.IsEnterToSend ? "Đang bật" : "Đang tắt", out _enterToSendStatusText);
            enterRow.Tapped += (s, e) =>
            {
                ZaloSettingsService.Current.IsEnterToSend = !ZaloSettingsService.Current.IsEnterToSend;
                _enterToSendStatusText.Text = ZaloSettingsService.Current.IsEnterToSend ? "Đang bật" : "Đang tắt";
            };
            notifStack.Children.Add(enterRow);

            notifGroup.Child = notifStack;
            stack.Children.Add(notifGroup);

            // Section 3: Dữ liệu trên máy & Bộ nhớ đệm
            stack.Children.Add(CreateSectionHeader("DỮ LIỆU & BỘ NHỚ"));

            var storageGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var storageStack = new StackPanel();

            var clearCacheRow = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var clearGrid = new Grid();
            clearGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            clearGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            clearGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var storageIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_storage_blue.png")),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(storageIcon, 0);
            clearGrid.Children.Add(storageIcon);

            var storageText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            storageText.Children.Add(new TextBlock { Text = "Xóa dữ liệu bộ nhớ đệm", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            storageText.Children.Add(new TextBlock { Text = "Giải phóng dung lượng tin nhắn và hình ảnh tạm", FontSize = 12, Foreground = _textSub });
            Grid.SetColumn(storageText, 1);
            clearGrid.Children.Add(storageText);

            var clearBtn = new TextBlock { Text = "Dọn dẹp", FontSize = 13, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(clearBtn, 2);
            clearGrid.Children.Add(clearBtn);

            clearCacheRow.Child = clearGrid;
            clearCacheRow.Tapped += async (s, e) =>
            {
                await ZaloApp.ClearLocalCacheAsync();
                var md = new MessageDialog("Đã dọn dẹp sạch bộ nhớ đệm thành công!", "Thông báo");
                await md.ShowAsync();
            };
            storageStack.Children.Add(clearCacheRow);

            storageGroup.Child = storageStack;
            stack.Children.Add(storageGroup);

            // Section 4: Tài khoản & Bảo mật
            stack.Children.Add(CreateSectionHeader("TÀI KHOẢN & BẢO MẬT"));

            var accountGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var accStack = new StackPanel();

            var accRow = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var accGrid = new Grid();
            accGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            accGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var secIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_security_blue.png")),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(secIcon, 0);
            accGrid.Children.Add(secIcon);

            var accText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            accText.Children.Add(new TextBlock { Text = "Zalo ID: " + (ZaloApp.Session?.Uid ?? "Chưa có"), FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            accText.Children.Add(new TextBlock { Text = "Số điện thoại: " + (ZaloApp.Session?.PhoneNumber ?? "Chưa công khai"), FontSize = 12.5, Foreground = _textSub });
            Grid.SetColumn(accText, 1);
            accGrid.Children.Add(accText);

            accRow.Child = accGrid;
            accStack.Children.Add(accRow);
            accountGroup.Child = accStack;
            stack.Children.Add(accountGroup);

            // Section 5: Giới thiệu ứng dụng
            var aboutGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 16, 16, 16),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var aboutStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            aboutStack.Children.Add(new TextBlock
            {
                Text = "Zalo cho Windows Phone 8.1",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            aboutStack.Children.Add(new TextBlock
            {
                Text = "Phiên bản 1.0.0.0 (ARM Release) • Native C# WinRT",
                FontSize = 12,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            });
            aboutGroup.Child = aboutStack;
            stack.Children.Add(aboutGroup);

            // Section 6: Đăng xuất
            var logoutBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xE2, 0xE2)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xCA, 0xCA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var logoutText = new TextBlock
            {
                Text = "Đăng xuất tài khoản",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logoutBorder.Child = logoutText;
            logoutBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Bạn có chắc muốn đăng xuất khỏi tài khoản Zalo này?", "Xác nhận đăng xuất");
                md.Commands.Add(new UICommand("Đăng xuất", async cmd =>
                {
                    ZaloApp.Reset();
                    await ZaloStore.ClearAsync();
                    NavigationManager.NavigateTo(new LoginPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            stack.Children.Add(logoutBorder);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub,
                Margin = new Thickness(6, 4, 0, 8)
            };
        }

        private Border CreateSettingRow(string title, string sub, string status, out TextBlock statusBlock)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = title, FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            stack.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(stack, 0);
            grid.Children.Add(stack);

            statusBlock = new TextBlock
            {
                Text = status,
                FontSize = 13,
                Foreground = _activeBlue,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusBlock, 1);
            grid.Children.Add(statusBlock);

            border.Child = grid;
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

