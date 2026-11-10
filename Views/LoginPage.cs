using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZaloLite;
using ZaloUWP.Core;

namespace ZaloWP81.Views
{
    public sealed class LoginPage : Page
    {
        private CancellationTokenSource _cts;
        private Image _qrImage;
        private TextBlock _statusText;
        private ProgressRing _progressRing;
        private Button _retryButton;

        public LoginPage()
        {
            BuildUI();
            this.Loaded += (s, e) => CheckSessionOrLogin();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Header (Zalo Vibrant Gradient)
            var headerGrid = new Grid();
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerGrid.Background = gradient;

            var titleBlock = new TextBlock
            {
                Text = "Zalo",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.Children.Add(titleBlock);
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. Center Content Card
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(24, 20, 24, 20)
            };

            var mainTitle = new TextBlock
            {
                Text = "Đăng nhập Zalo",
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            contentStack.Children.Add(mainTitle);

            var instructions = new TextBlock
            {
                Text = "Mở Zalo trên điện thoại chính và quét mã QR bên dưới để đăng nhập.",
                FontSize = 13.5,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80)),
                Margin = new Thickness(0, 0, 0, 20),
                MaxWidth = 300
            };
            contentStack.Children.Add(instructions);

            var qrCard = new Border
            {
                Width = 250,
                Height = 250,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                BorderThickness = new Thickness(2.5),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var qrGrid = new Grid();
            _qrImage = new Image
            {
                Width = 230,
                Height = 230,
                Stretch = Stretch.Uniform
            };
            _progressRing = new ProgressRing
            {
                Width = 48,
                Height = 48,
                IsActive = true,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            qrGrid.Children.Add(_qrImage);
            qrGrid.Children.Add(_progressRing);
            qrCard.Child = qrGrid;
            contentStack.Children.Add(qrCard);

            _statusText = new TextBlock
            {
                Text = "Đang kết nối tới Zalo...",
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x4B, 0x55, 0x63)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            };
            contentStack.Children.Add(_statusText);

            _retryButton = new Button
            {
                Content = "Lấy mã mới",
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 14,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _retryButton.Click += (s, e) => StartQrLogin();
            contentStack.Children.Add(_retryButton);

            var guestButton = new Button
            {
                Content = "Dùng thử ngay (Khách)",
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 6, 16, 6),
                FontSize = 13
            };
            guestButton.Click += (s, e) => StartGuestMode();
            contentStack.Children.Add(guestButton);

            scroll.Content = contentStack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private async void CheckSessionOrLogin()
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var session = await ZaloStore.RestoreAsync(cts.Token);
                if (session != null)
                {
                    var task = ZaloApp.StartAsync(session);
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                    return;
                }
            }
            catch { }
            StartQrLogin();
        }

        private void StartGuestMode()
        {
            var guestSession = new ZaloLite.ZaloSession
            {
                Uid = "guest_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = "Khách thử nghiệm",
                PhoneNumber = "0988 888 888"
            };
            var task = ZaloApp.StartAsync(guestSession);
            NavigationManager.NavigateTo(new MainPage(), clearStack: true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _cts?.Cancel();
        }

        private void StartQrLogin()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _retryButton.Visibility = Visibility.Collapsed;
            _progressRing.IsActive = true;
            _progressRing.Visibility = Visibility.Visible;
            _statusText.Text = "Đang kết nối tới Zalo...";

            Task.Run(async () =>
            {
                try
                {
                    ZaloLite.ZaloSession session = await ZaloAuth.LoginWithQrAsync(
                        onQr: async bytes =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                try
                                {
                                    using (var stream = new InMemoryRandomAccessStream())
                                    {
                                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                                        {
                                            writer.WriteBytes(bytes);
                                            await writer.StoreAsync();
                                        }
                                        var bmp = new BitmapImage();
                                        await bmp.SetSourceAsync(stream);
                                        _qrImage.Source = bmp;
                                        _progressRing.IsActive = false;
                                        _progressRing.Visibility = Visibility.Collapsed;
                                    }
                                }
                                catch { }
                            });
                        },
                        onStatus: async status =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                _statusText.Text = status ?? string.Empty;
                            });
                        },
                        ct: _cts.Token
                    );

                    if (session != null)
                    {
                        await ZaloStore.SaveAsync(session);
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            _statusText.Text = "Đăng nhập thành công!";
                            var task = ZaloApp.StartAsync(session);
                            NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                        });
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        _statusText.Text = "Lỗi đăng nhập: " + ex.Message;
                        _progressRing.IsActive = false;
                        _progressRing.Visibility = Visibility.Collapsed;
                        _retryButton.Visibility = Visibility.Visible;
                    });
                }
            });
        }
    }
}
