using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class CameraCapturePage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x68, 0xFF));
        private MediaCapture _capture;
        private CaptureElement _captureElement;
        private Image _previewImage;
        private Border _previewContainer;
        private Border _shutterBtn;
        private Grid _reviewDock;
        private TextBlock _statusText;

        private string _conversationKey;
        private StorageFile _capturedFile;
        private bool _isFrontCamera = false;
        private bool _isPreviewing = false;

        public CameraCapturePage()
        {
            BuildUI();
        }

        public CameraCapturePage(string conversationKey) : this()
        {
            _conversationKey = conversationKey;
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;

            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.Black)
            };

            // 1. Live Viewfinder Element
            _captureElement = new CaptureElement
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            rootGrid.Children.Add(_captureElement);

            // 2. Photo Review Container (Visible after capture)
            _previewContainer = new Border
            {
                Background = new SolidColorBrush(Colors.Black),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            _previewImage = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _previewContainer.Child = _previewImage;
            rootGrid.Children.Add(_previewContainer);

            // 3. Top Controls Bar
            var topBar = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(Math.Round(16 * scale), Math.Round(26 * scale), Math.Round(16 * scale), Math.Round(10 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x00, 0x00))
            };
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(48 * scale)) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(48 * scale)) });

            var backBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(44 * scale));
            var backIcon = new TextBlock
            {
                Text = "\uE112",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(22 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Child = backIcon;
            backBtn.Tapped += (s, e) =>
            {
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new ChatPage(_conversationKey));
            };
            Grid.SetColumn(backBtn, 0);
            topGrid.Children.Add(backBtn);

            _statusText = new TextBlock
            {
                Text = "Chụp ảnh gửi Zalo",
                FontSize = Math.Round(16 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_statusText, 1);
            topGrid.Children.Add(_statusText);

            var switchCamBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(44 * scale));
            var switchCamIcon = new TextBlock
            {
                Text = "\uE114",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(22 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            switchCamBtn.Child = switchCamIcon;
            switchCamBtn.Tapped += async (s, e) => await SwitchCameraAsync();
            Grid.SetColumn(switchCamBtn, 2);
            topGrid.Children.Add(switchCamBtn);

            topBar.Child = topGrid;
            rootGrid.Children.Add(topBar);

            // 4. Bottom Controls (Shutter Button OR Review Dock)
            var bottomDock = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(Math.Round(24 * scale), Math.Round(16 * scale), Math.Round(24 * scale), Math.Round(32 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x77, 0x00, 0x00, 0x00))
            };

            var bottomGrid = new Grid();

            // Shutter Button (Big circular trigger)
            _shutterBtn = new Border
            {
                Width = Math.Round(72 * scale),
                Height = Math.Round(72 * scale),
                CornerRadius = new CornerRadius(Math.Round(36 * scale)),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(Math.Round(4 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var innerShutter = new Ellipse
            {
                Width = Math.Round(54 * scale),
                Height = Math.Round(54 * scale),
                Fill = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _shutterBtn.Child = innerShutter;
            _shutterBtn.PointerPressed += (s, e) => _shutterBtn.Opacity = 0.6;
            _shutterBtn.PointerReleased += (s, e) => _shutterBtn.Opacity = 1.0;
            _shutterBtn.PointerCanceled += (s, e) => _shutterBtn.Opacity = 1.0;
            _shutterBtn.Tapped += async (s, e) => await TakePhotoAsync();
            bottomGrid.Children.Add(_shutterBtn);

            // Review Dock (Chụp lại & Gửi ảnh)
            _reviewDock = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _reviewDock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _reviewDock.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Retake Button
            var retakeBtn = new Border
            {
                Height = Math.Round(46 * scale),
                CornerRadius = new CornerRadius(Math.Round(23 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 0, Math.Round(8 * scale), 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var retakeText = new TextBlock
            {
                Text = "Chụp lại",
                FontSize = Math.Round(15 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            retakeBtn.Child = retakeText;
            retakeBtn.PointerPressed += (s, e) => retakeBtn.Opacity = 0.6;
            retakeBtn.PointerReleased += (s, e) => retakeBtn.Opacity = 1.0;
            retakeBtn.PointerCanceled += (s, e) => retakeBtn.Opacity = 1.0;
            retakeBtn.Tapped += async (s, e) => await ResumePreviewAsync();
            Grid.SetColumn(retakeBtn, 0);
            _reviewDock.Children.Add(retakeBtn);

            // Send Button
            var sendBtn = new Border
            {
                Height = Math.Round(46 * scale),
                CornerRadius = new CornerRadius(Math.Round(23 * scale)),
                Background = _activeBlue,
                Margin = new Thickness(Math.Round(8 * scale), 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var sendText = new TextBlock
            {
                Text = "Gửi ảnh",
                FontSize = Math.Round(15 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sendBtn.Child = sendText;
            sendBtn.PointerPressed += (s, e) => sendBtn.Opacity = 0.6;
            sendBtn.PointerReleased += (s, e) => sendBtn.Opacity = 1.0;
            sendBtn.PointerCanceled += (s, e) => sendBtn.Opacity = 1.0;
            sendBtn.Tapped += async (s, e) => await SendCapturedPhotoAsync();
            Grid.SetColumn(sendBtn, 1);
            _reviewDock.Children.Add(sendBtn);

            bottomGrid.Children.Add(_reviewDock);
            bottomDock.Child = bottomGrid;
            rootGrid.Children.Add(bottomDock);

            this.Content = rootGrid;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string convKey && !string.IsNullOrEmpty(convKey))
            {
                _conversationKey = convKey;
            }
            await InitializeCameraAsync();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            await CleanupCameraAsync();
        }

        private async Task InitializeCameraAsync()
        {
            try
            {
                _statusText.Text = "Đang mở máy ảnh...";
                _capture = new MediaCapture();

                DeviceInformation targetDevice = null;
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                foreach (var d in devices)
                {
                    if (_isFrontCamera && d.EnclosureLocation != null && d.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front)
                    {
                        targetDevice = d;
                        break;
                    }
                    else if (!_isFrontCamera && d.EnclosureLocation != null && d.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back)
                    {
                        targetDevice = d;
                        break;
                    }
                }

                var settings = new MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    PhotoCaptureSource = PhotoCaptureSource.Photo
                };
                if (targetDevice != null)
                {
                    settings.VideoDeviceId = targetDevice.Id;
                }

                await _capture.InitializeAsync(settings);
                _captureElement.Source = _capture;
                await _capture.StartPreviewAsync();
                _isPreviewing = true;
                _statusText.Text = "Chụp ảnh gửi Zalo";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Lỗi máy ảnh";
                var md = new MessageDialog("Không thể mở máy ảnh: " + ex.Message + "\nVui lòng kiểm tra quyền truy cập Camera trong Cài đặt của máy.", "Lỗi máy ảnh");
                await md.ShowAsync();
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new ChatPage(_conversationKey));
            }
        }

        private async Task SwitchCameraAsync()
        {
            _isFrontCamera = !_isFrontCamera;
            await CleanupCameraAsync();
            await InitializeCameraAsync();
        }

        private async Task TakePhotoAsync()
        {
            if (_capture == null || !_isPreviewing) return;
            try
            {
                _statusText.Text = "Đang chụp...";
                var tempFolder = ApplicationData.Current.TemporaryFolder;
                _capturedFile = await tempFolder.CreateFileAsync("zalo_cam_" + DateTime.Now.Ticks + ".jpg", CreationCollisionOption.GenerateUniqueName);
                var encProps = ImageEncodingProperties.CreateJpeg();
                await _capture.CapturePhotoToStorageFileAsync(encProps, _capturedFile);

                // Show preview of captured image
                using (var stream = await _capturedFile.OpenReadAsync())
                {
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(stream);
                    _previewImage.Source = bmp;
                }

                _previewContainer.Visibility = Visibility.Visible;
                _shutterBtn.Visibility = Visibility.Collapsed;
                _reviewDock.Visibility = Visibility.Visible;
                _statusText.Text = "Xem lại ảnh chụp";
            }
            catch (Exception ex)
            {
                _statusText.Text = "Lỗi chụp ảnh";
                var md = new MessageDialog("Lỗi chụp ảnh: " + ex.Message, "Thông báo");
                await md.ShowAsync();
            }
        }

        private async Task ResumePreviewAsync()
        {
            _previewContainer.Visibility = Visibility.Collapsed;
            _reviewDock.Visibility = Visibility.Collapsed;
            _shutterBtn.Visibility = Visibility.Visible;
            _statusText.Text = "Chụp ảnh gửi Zalo";
            await Task.Yield();
        }

        private async Task SendCapturedPhotoAsync()
        {
            if (_capturedFile == null) return;
            try
            {
                var conv = ZaloApp.Conversations.FirstOrDefault(c => c.Key == _conversationKey);
                if (conv != null)
                {
                    var props = await _capturedFile.GetBasicPropertiesAsync();
                    using (var imageStream = await _capturedFile.OpenReadAsync())
                    {
                        uint width = 800;
                        uint height = 600;
                        try
                        {
                            var decoder = await BitmapDecoder.CreateAsync(imageStream);
                            width = decoder.PixelWidth;
                            height = decoder.PixelHeight;
                            imageStream.Seek(0);
                        }
                        catch { }

                        await ZaloApp.SendImageAsync(conv, _capturedFile.Name, imageStream, (long)props.Size, width, height, "");
                    }
                }

                await CleanupCameraAsync();
                NavigationManager.NavigateTo(new ChatPage(_conversationKey));
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi ảnh: " + ex.Message, "Gửi thất bại");
                await md.ShowAsync();
            }
        }

        private async Task CleanupCameraAsync()
        {
            try
            {
                if (_capture != null)
                {
                    if (_isPreviewing)
                    {
                        await _capture.StopPreviewAsync();
                        _isPreviewing = false;
                    }
                    _captureElement.Source = null;
                    _capture.Dispose();
                    _capture = null;
                }
            }
            catch { }
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var b = new Border { Width = width, Height = height, Background = new SolidColorBrush(Colors.Transparent), VerticalAlignment = VerticalAlignment.Center };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }
    }
}
