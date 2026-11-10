using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class ImageViewerPage : Page
    {
        private readonly string _imageUrl;
        private byte[] _imageBytes;
        private Image _mainImage;
        private ProgressRing _loadingRing;
        private Border _retryBtn;

        public ImageViewerPage(string imageUrl)
        {
            _imageUrl = imageUrl;
            BuildUI();
            this.Loaded += (s, e) => LoadImage();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Black)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Top Header Bar
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x14, 0x14, 0x14)),
                Padding = new Thickness(8, 0, 14, 0)
            };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var backBtn = new Border
            {
                Width = 44,
                Height = 44,
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Child = new TextBlock
            {
                Text = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.PointerPressed += (s, e) => backBtn.Opacity = 0.6;
            backBtn.PointerReleased += (s, e) => backBtn.Opacity = 1.0;
            backBtn.PointerCanceled += (s, e) => backBtn.Opacity = 1.0;
            backBtn.Tapped += (s, e) => NavigationManager.GoBack();
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

            var saveBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x08, 0x7F, 0xF5)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 6, 14, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            saveBtn.Child = new TextBlock
            {
                Text = "Lưu ảnh",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            saveBtn.PointerPressed += (s, e) => saveBtn.Opacity = 0.6;
            saveBtn.PointerReleased += (s, e) => saveBtn.Opacity = 1.0;
            saveBtn.PointerCanceled += (s, e) => saveBtn.Opacity = 1.0;
            saveBtn.Tapped += async (s, e) => await SaveImageAsync();
            Grid.SetColumn(saveBtn, 2);
            header.Children.Add(saveBtn);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Viewport with Zoom
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                ZoomMode = ZoomMode.Enabled,
                MinZoomFactor = 1.0f,
                MaxZoomFactor = 4.0f
            };

            var imageContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _mainImage = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            imageContainer.Children.Add(_mainImage);

            _loadingRing = new ProgressRing
            {
                Width = 48,
                Height = 48,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x08, 0x7F, 0xF5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsActive = true
            };
            imageContainer.Children.Add(_loadingRing);

            _retryBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x22, 0x22, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var retryStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            retryStack.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_photo_clean_white.png")), Width = 18, Height = 18, Margin = new Thickness(0, 0, 8, 0) });
            retryStack.Children.Add(new TextBlock { Text = "Không tải được ảnh. Chạm để thử lại", Foreground = new SolidColorBrush(Colors.White), FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
            _retryBtn.Child = retryStack;
            _retryBtn.Tapped += (s, e) => LoadImage();
            imageContainer.Children.Add(_retryBtn);

            scrollViewer.Content = imageContainer;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            this.Content = rootGrid;
        }

        private async void LoadImage()
        {
            if (string.IsNullOrWhiteSpace(_imageUrl))
            {
                _loadingRing.IsActive = false;
                _loadingRing.Visibility = Visibility.Collapsed;
                _retryBtn.Visibility = Visibility.Visible;
                return;
            }

            _loadingRing.IsActive = true;
            _loadingRing.Visibility = Visibility.Visible;
            _retryBtn.Visibility = Visibility.Collapsed;

            try
            {
                if (_imageUrl.StartsWith("ms-appx:///"))
                {
                    _mainImage.Source = new BitmapImage(new Uri(_imageUrl));
                    _loadingRing.IsActive = false;
                    _loadingRing.Visibility = Visibility.Collapsed;
                    return;
                }

                byte[] bytes = null;
                if (ZaloApp.Session != null)
                {
                    try
                    {
                        bytes = await ZaloApp.Session.Http.DownloadBytesAsync(_imageUrl, CancellationToken.None);
                    }
                    catch { }
                }

                if (bytes == null)
                {
                    using (var hc = new System.Net.Http.HttpClient())
                    {
                        bytes = await hc.GetByteArrayAsync(_imageUrl);
                    }
                }

                if (bytes != null && bytes.Length > 0)
                {
                    _imageBytes = bytes;
                    using (var stream = new InMemoryRandomAccessStream())
                    using (var writer = new DataWriter(stream))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream();
                        stream.Seek(0);
                        var bitmap = new BitmapImage { DecodePixelWidth = 1280 };
                        await bitmap.SetSourceAsync(stream);
                        _mainImage.Source = bitmap;
                    }
                }
                else
                {
                    throw new Exception("Empty image data received");
                }
            }
            catch
            {
                _retryBtn.Visibility = Visibility.Visible;
            }
            finally
            {
                _loadingRing.IsActive = false;
                _loadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private async Task SaveImageAsync()
        {
            if (_imageBytes == null || _imageBytes.Length == 0)
            {
                var md = new MessageDialog("Ảnh chưa được tải xong, vui lòng đợi giây lát.", "Thông báo");
                await md.ShowAsync();
                return;
            }

            try
            {
                var folder = KnownFolders.SavedPictures;
                string fileName = "Zalo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteBytesAsync(file, _imageBytes);
                var md = new MessageDialog("Đã lưu ảnh thành công vào Thư viện ảnh (Saved Pictures).", "Thành công");
                await md.ShowAsync();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể lưu ảnh: " + ex.Message, "Lỗi");
                await md.ShowAsync();
            }
        }
    }
}
