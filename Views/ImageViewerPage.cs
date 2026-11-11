using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class ImageViewerPage : Page
    {
        private string _imageUrl;
        private byte[] _imageBytes;
        private Image _mainImage;
        private ProgressRing _loadingRing;
        private Border _retryBottomBtn;
        private StorageFile _shareImageFile;

        public ImageViewerPage()
        {
            BuildUI();
        }

        public ImageViewerPage(string imageUrl) : this()
        {
            _imageUrl = imageUrl;
            this.Loaded += (s, e) => LoadImage();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Black)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            // 1. Top Bar (#99000000, 48px)
            var topBarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
                Padding = new Thickness(6, 0, 12, 0)
            };
            var topBar = new Grid();
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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
            topBar.Children.Add(backBtn);

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
            topBar.Children.Add(title);

            topBarBorder.Child = topBar;
            Grid.SetRow(topBarBorder, 0);
            rootGrid.Children.Add(topBarBorder);

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
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsActive = true
            };
            imageContainer.Children.Add(_loadingRing);

            scrollViewer.Content = imageContainer;
            Grid.SetRow(scrollViewer, 1);
            rootGrid.Children.Add(scrollViewer);

            // 3. Bottom Actions Bar (#99000000, 52px)
            var bottomBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
                Padding = new Thickness(12, 0, 12, 0)
            };
            var actionStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Retry Button
            _retryBottomBtn = CreateTouchBorder(48, 48);
            _retryBottomBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_refresh_clean_white.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _retryBottomBtn.Visibility = Visibility.Collapsed;
            _retryBottomBtn.Tapped += (s, e) => LoadImage();
            actionStack.Children.Add(_retryBottomBtn);

            // Share Button
            var shareBtn = CreateTouchBorder(48, 48);
            shareBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_share_clean_white.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            shareBtn.Tapped += (s, e) => ShareImage();
            actionStack.Children.Add(shareBtn);

            // Save Button
            var saveBtn = CreateTouchBorder(48, 48);
            saveBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_download_clean_white.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            saveBtn.Tapped += async (s, e) => await SaveImageAsync();
            actionStack.Children.Add(saveBtn);

            bottomBar.Child = actionStack;
            Grid.SetRow(bottomBar, 2);
            rootGrid.Children.Add(bottomBar);

            this.Content = rootGrid;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string url && !string.IsNullOrEmpty(url))
            {
                _imageUrl = url;
                HookShare();
                LoadImage();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            UnhookShare();
            if (_shareImageFile != null)
            {
                var f = _shareImageFile;
                _shareImageFile = null;
                _ = f.DeleteAsync();
            }
            base.OnNavigatedFrom(e);
        }

        private void HookShare()
        {
            try
            {
                var transferManager = DataTransferManager.GetForCurrentView();
                transferManager.DataRequested -= ShareDataRequested;
                transferManager.DataRequested += ShareDataRequested;
            }
            catch { }
        }

        private void UnhookShare()
        {
            try
            {
                DataTransferManager.GetForCurrentView().DataRequested -= ShareDataRequested;
            }
            catch { }
        }

        private void ShareImage()
        {
            if (string.IsNullOrWhiteSpace(_imageUrl)) return;
            try
            {
                DataTransferManager.ShowShareUI();
            }
            catch (Exception ex)
            {
                ZaloLog.Add("Image share UI error: " + ex.Message);
                _ = new MessageDialog("Không mở được bảng chia sẻ trên thiết bị này.", "Chia sẻ ảnh").ShowAsync();
            }
        }

        private async void ShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
        {
            DataRequestDeferral deferral = args.Request.GetDeferral();
            try
            {
                if (_imageBytes != null && _imageBytes.Length > 0)
                {
                    string ext = ImageFileExtension();
                    string fname = "zalo_share_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ext;
                    _shareImageFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fname, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBytesAsync(_shareImageFile, _imageBytes);

                    args.Request.Data.Properties.Title = "Ảnh Zalo";
                    args.Request.Data.Properties.Description = "Ảnh được chia sẻ từ Zalo.";
                    args.Request.Data.SetStorageItems(new IStorageItem[] { _shareImageFile });
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("ShareDataRequested failed: " + ex.Message);
                args.Request.FailWithDisplayText("Không tải được ảnh để chia sẻ. Hãy thử lại.");
            }
            finally
            {
                deferral.Complete();
            }
        }

        private string ImageFileExtension()
        {
            Uri imageUri;
            string extension = Uri.TryCreate(_imageUrl, UriKind.Absolute, out imageUri)
                ? Path.GetExtension(imageUri.AbsolutePath).ToLowerInvariant() : string.Empty;
            return extension == ".png" || extension == ".webp" || extension == ".jpeg" ? extension : ".jpg";
        }

        private async void LoadImage()
        {
            if (string.IsNullOrWhiteSpace(_imageUrl))
            {
                _loadingRing.IsActive = false;
                _loadingRing.Visibility = Visibility.Collapsed;
                if (_retryBottomBtn != null) _retryBottomBtn.Visibility = Visibility.Visible;
                return;
            }

            _loadingRing.IsActive = true;
            _loadingRing.Visibility = Visibility.Visible;
            if (_retryBottomBtn != null) _retryBottomBtn.Visibility = Visibility.Collapsed;

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
                if (_retryBottomBtn != null) _retryBottomBtn.Visibility = Visibility.Visible;
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
                string ext = ImageFileExtension();
                string fileName = "Zalo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteBytesAsync(file, _imageBytes);
                var md = new MessageDialog("Đã lưu hình ảnh vào Bộ sưu tập (Saved Pictures)!", "Thành công");
                await md.ShowAsync();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể lưu ảnh: " + ex.Message, "Lỗi");
                await md.ShowAsync();
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