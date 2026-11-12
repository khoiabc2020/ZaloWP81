using System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Common
{
    /// <summary>
    /// In-app floating toast banner for missed calls, rejected calls, and system alerts.
    /// Uses Windows.UI.Xaml.Controls.Primitives.Popup to display over any active page.
    /// </summary>
    public static class ZaloToast
    {
        private static Popup _activeToastPopup;
        private static DispatcherTimer _autoDismissTimer;

        public static void Dismiss()
        {
            try
            {
                if (_autoDismissTimer != null)
                {
                    _autoDismissTimer.Stop();
                    _autoDismissTimer = null;
                }
                if (_activeToastPopup != null)
                {
                    _activeToastPopup.IsOpen = false;
                    _activeToastPopup = null;
                }
            }
            catch { }
        }

        public static void ShowMissedCall(string callerName, string callerId, string callerAvatar, bool isVideo)
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
            if (dispatcher == null) return;

            var _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ShowMissedCallInternal(callerName, callerId, callerAvatar, isVideo);
            });
        }

        private static void ShowMissedCallInternal(string callerName, string callerId, string callerAvatar, bool isVideo)
        {
            Dismiss();

            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var windowBounds = Window.Current.Bounds;
            double width = Math.Min(windowBounds.Width - Math.Round(20 * scale), Math.Round(380 * scale));

            var container = new Border
            {
                Width = width,
                Background = new SolidColorBrush(Color.FromArgb(0xF8, 0x1E, 0x29, 0x3B)), // Slate dark
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)), // Red accent border
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(Math.Round(16 * scale)),
                Padding = new Thickness(Math.Round(12 * scale), Math.Round(10 * scale), Math.Round(12 * scale), Math.Round(10 * scale)),
                Margin = new Thickness(Math.Round((windowBounds.Width - width) / 2), Math.Round(16 * scale), 0, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 1. Icon Badge (Red Missed Call Icon)
            var iconCircle = new Border
            {
                Width = Math.Round(42 * scale),
                Height = Math.Round(42 * scale),
                CornerRadius = new CornerRadius(Math.Round(21 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x2E, 0xEF, 0x44, 0x44)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, Math.Round(10 * scale), 0)
            };
            var iconText = new TextBlock
            {
                Text = isVideo ? "\uE116" : "\uE13A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(20 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = iconText;
            Grid.SetColumn(iconCircle, 0);
            grid.Children.Add(iconCircle);

            // 2. Text Info
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, Math.Round(8 * scale), 0)
            };

            var titleBlock = new TextBlock
            {
                Text = isVideo ? "Cuộc gọi video nhỡ" : "Cuộc gọi nhỡ",
                FontSize = Math.Round(14 * scale),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44))
            };
            textStack.Children.Add(titleBlock);

            var descBlock = new TextBlock
            {
                Text = callerName ?? "Người dùng Zalo",
                FontSize = Math.Round(13 * scale),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textStack.Children.Add(descBlock);

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // 3. Action Buttons (Gọi lại | Đóng)
            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var callBtn = new Button
            {
                Content = "Gọi lại",
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(Math.Round(10 * scale), Math.Round(4 * scale), Math.Round(10 * scale), Math.Round(4 * scale)),
                FontSize = Math.Round(12 * scale),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 0, Math.Round(4 * scale), 0)
            };
            callBtn.Click += (s, e) =>
            {
                Dismiss();
                var conv = ZaloApp.GetOrCreate(callerId, false);
                NavigationManager.NavigateTo(new Views.CallPage(conv, isVideo));
            };
            btnStack.Children.Add(callBtn);

            var closeBtn = new Border
            {
                Width = Math.Round(26 * scale),
                Height = Math.Round(26 * scale),
                CornerRadius = new CornerRadius(Math.Round(13 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var closeIcon = new TextBlock
            {
                Text = "\uE106",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(12 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeIcon;
            closeBtn.Tapped += (s, e) => Dismiss();
            btnStack.Children.Add(closeBtn);

            Grid.SetColumn(btnStack, 2);
            grid.Children.Add(btnStack);

            container.Child = grid;

            // Tap banner body opens chat
            textStack.Tapped += (s, e) =>
            {
                Dismiss();
                var conv = ZaloApp.GetOrCreate(callerId, false);
                NavigationManager.NavigateTo(new Views.ChatPage(conv.Key));
            };

            var popup = new Popup
            {
                Child = container,
                IsOpen = true
            };
            _activeToastPopup = popup;

            // Auto dismiss after 7 seconds
            _autoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            _autoDismissTimer.Tick += (s, e) => Dismiss();
            _autoDismissTimer.Start();
        }
    }
}

