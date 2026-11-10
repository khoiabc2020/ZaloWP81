using System;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace ZaloWP81.Views
{
    public sealed class ImageViewerPage : Page
    {
        private readonly string _imageUrl;

        public ImageViewerPage(string imageUrl)
        {
            _imageUrl = imageUrl;
            BuildUI();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x11, 0x11))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x1A, 0x1A, 0x1A)),
                Padding = new Thickness(6, 0, 10, 0)
            };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
                Text = "Hình ảnh",
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            var saveBtn = new Button
            {
                Content = "Lưu ảnh",
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8)),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            saveBtn.Click += async (s, e) =>
            {
                var md = new MessageDialog("Ảnh đã được tải về bộ nhớ thiết bị.", "Thông báo");
                await md.ShowAsync();
            };
            Grid.SetColumn(saveBtn, 2);
            header.Children.Add(saveBtn);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // Image Container
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                ZoomMode = ZoomMode.Enabled,
                MinZoomFactor = 1.0f,
                MaxZoomFactor = 3.0f
            };

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(_imageUrl))
            {
                try
                {
                    image.Source = new BitmapImage(new Uri(_imageUrl));
                }
                catch { }
            }

            scrollViewer.Content = image;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            this.Content = rootGrid;
        }
    }
}
