using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using ZaloUWP.Services;

namespace ZaloWP81.Common
{
    public sealed class ZaloActionItem
    {
        public string Title { get; set; }
        public Action Action { get; set; }
        public bool IsDestructive { get; set; }
    }

    public sealed class ZaloActionSheet
    {
        private static Popup _activePopup;
        private static Action _activeDismiss;

        public static bool IsAnyOpen => _activePopup != null && _activePopup.IsOpen;

        public static bool TryDismissActive()
        {
            if (_activePopup != null && _activePopup.IsOpen)
            {
                _activeDismiss?.Invoke();
                return true;
            }
            return false;
        }

        private readonly string _title;
        private readonly List<ZaloActionItem> _items = new List<ZaloActionItem>();

        public ZaloActionSheet(string title = null)
        {
            _title = title;
        }

        public ZaloActionSheet AddAction(string title, Action action, bool isDestructive = false)
        {
            if (!string.IsNullOrEmpty(title))
            {
                _items.Add(new ZaloActionItem { Title = title, Action = action, IsDestructive = isDestructive });
            }
            return this;
        }

        public void Show()
        {
            TryDismissActive();

            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var windowBounds = Window.Current.Bounds;

            var popup = new Popup
            {
                Width = windowBounds.Width,
                Height = windowBounds.Height
            };

            var overlay = new Grid
            {
                Width = windowBounds.Width,
                Height = windowBounds.Height,
                Background = new SolidColorBrush(Color.FromArgb(0x7F, 0x00, 0x00, 0x00))
            };

            void Close()
            {
                if (popup.IsOpen)
                {
                    popup.IsOpen = false;
                    if (_activePopup == popup)
                    {
                        _activePopup = null;
                        _activeDismiss = null;
                    }
                }
            }

            overlay.Tapped += (s, e) =>
            {
                if (e.OriginalSource == overlay)
                {
                    Close();
                }
            };

            var sheetCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(16, 16, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(16, 14, 16, 16),
                MaxWidth = 480
            };

            var cardStack = new StackPanel();

            // Top drag handle indicator
            var handle = new Border
            {
                Width = Math.Round(38 * scale),
                Height = Math.Round(4 * scale),
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            cardStack.Children.Add(handle);

            // Title if provided
            if (!string.IsNullOrWhiteSpace(_title))
            {
                var titleBlock = new TextBlock
                {
                    Text = _title,
                    FontSize = Math.Round(16 * scale, 1),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12),
                    TextWrapping = TextWrapping.Wrap
                };
                cardStack.Children.Add(titleBlock);

                cardStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }

            // Items
            foreach (var item in _items)
            {
                var rowBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    Padding = new Thickness(12, Math.Round(12 * scale), 12, Math.Round(12 * scale)),
                    CornerRadius = new CornerRadius(8)
                };

                var rowText = new TextBlock
                {
                    Text = item.Title,
                    FontSize = Math.Round(15.5 * scale, 1),
                    FontWeight = item.IsDestructive ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                    Foreground = item.IsDestructive
                        ? new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26))
                        : new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                rowBorder.Child = rowText;

                rowBorder.PointerPressed += (s, e) => rowBorder.Background = new SolidColorBrush(Color.FromArgb(0x1F, 0x00, 0x78, 0xD7));
                rowBorder.PointerReleased += (s, e) => rowBorder.Background = new SolidColorBrush(Colors.Transparent);
                rowBorder.PointerCanceled += (s, e) => rowBorder.Background = new SolidColorBrush(Colors.Transparent);

                var capturedItem = item;
                rowBorder.Tapped += (s, e) =>
                {
                    e.Handled = true;
                    Close();
                    try { capturedItem.Action?.Invoke(); } catch { }
                };

                cardStack.Children.Add(rowBorder);
            }

            // Cancel Button
            var cancelBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(Math.Round(10 * scale)),
                Padding = new Thickness(12, Math.Round(11 * scale), 12, Math.Round(11 * scale)),
                Margin = new Thickness(0, 10, 0, 4)
            };
            cancelBtn.Child = new TextBlock
            {
                Text = "Hủy",
                FontSize = Math.Round(15 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            cancelBtn.PointerPressed += (s, e) => cancelBtn.Opacity = 0.7;
            cancelBtn.PointerReleased += (s, e) => cancelBtn.Opacity = 1.0;
            cancelBtn.PointerCanceled += (s, e) => cancelBtn.Opacity = 1.0;
            cancelBtn.Tapped += (s, e) =>
            {
                e.Handled = true;
                Close();
            };
            cardStack.Children.Add(cancelBtn);

            sheetCard.Child = cardStack;
            overlay.Children.Add(sheetCard);

            popup.Child = overlay;
            _activePopup = popup;
            _activeDismiss = Close;
            popup.IsOpen = true;
        }
    }
}

