using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Core;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class FriendRequestsPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x68, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
        private readonly SolidColorBrush _cardBg = new SolidColorBrush(Colors.White);

        private int _currentTab = 0; // 0 = Đã nhận, 1 = Đã gửi
        private Border _tab0Indicator;
        private Border _tab1Indicator;
        private TextBlock _tab0Title;
        private TextBlock _tab1Title;
        private StackPanel _receivedListStack;
        private StackPanel _sentListStack;
        private ProgressRing _loadingRing;
        private ScrollViewer _scrollViewer;

        private List<ZaloFriendRequest> _receivedRequests = new List<ZaloFriendRequest>();
        private List<ZaloFriendRequest> _sentRequests = new List<ZaloFriendRequest>();
        private double _appliedScale = -1;

        public FriendRequestsPage(int initialTab = 0)
        {
            _currentTab = initialTab;
            BuildUI();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            double currentScale = ZaloSettingsService.Current.UiScaleFactor;
            if (_appliedScale > 0 && Math.Abs(_appliedScale - currentScale) > 0.01)
            {
                BuildUI();
                RenderReceivedList();
                RenderSentList();
            }
            _appliedScale = currentScale;
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;

            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF6, 0xF8))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(56 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(48 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient)
            var headerBorder = new Border { Padding = new Thickness(Math.Round(6 * scale), 0, Math.Round(12 * scale), 0) };
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerBorder.Background = gradient;

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var backBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(44 * scale));
            backBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_back_clean_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
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
                Text = "Lời mời kết bạn",
                FontSize = Math.Round(18 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Math.Round(6 * scale), 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. SUBTAB HEADER (Đã nhận | Đã gửi)
            var subTabBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE2, 0xE8, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var subTabGrid = new Grid();
            subTabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            subTabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Tab 0: Đã nhận
            var tab0Border = new Border { Background = new SolidColorBrush(Colors.Transparent), IsTapEnabled = true };
            var tab0Stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            _tab0Title = new TextBlock
            {
                Text = "Đã nhận",
                FontSize = Math.Round(15 * scale, 1),
                FontWeight = _currentTab == 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = _currentTab == 0 ? _activeBlue : _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(10 * scale), 0, Math.Round(8 * scale))
            };
            _tab0Indicator = new Border
            {
                Height = Math.Round(3 * scale),
                Background = _currentTab == 0 ? _activeBlue : new SolidColorBrush(Colors.Transparent),
                Width = Math.Round(80 * scale)
            };
            tab0Stack.Children.Add(_tab0Title);
            tab0Stack.Children.Add(_tab0Indicator);
            tab0Border.Child = tab0Stack;
            tab0Border.Tapped += (s, e) => SwitchTab(0);
            Grid.SetColumn(tab0Border, 0);
            subTabGrid.Children.Add(tab0Border);

            // Tab 1: Đã gửi
            var tab1Border = new Border { Background = new SolidColorBrush(Colors.Transparent), IsTapEnabled = true };
            var tab1Stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            _tab1Title = new TextBlock
            {
                Text = "Đã gửi",
                FontSize = Math.Round(15 * scale, 1),
                FontWeight = _currentTab == 1 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = _currentTab == 1 ? _activeBlue : _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(10 * scale), 0, Math.Round(8 * scale))
            };
            _tab1Indicator = new Border
            {
                Height = Math.Round(3 * scale),
                Background = _currentTab == 1 ? _activeBlue : new SolidColorBrush(Colors.Transparent),
                Width = Math.Round(80 * scale)
            };
            tab1Stack.Children.Add(_tab1Title);
            tab1Stack.Children.Add(_tab1Indicator);
            tab1Border.Child = tab1Stack;
            tab1Border.Tapped += (s, e) => SwitchTab(1);
            Grid.SetColumn(tab1Border, 1);
            subTabGrid.Children.Add(tab1Border);

            subTabBorder.Child = subTabGrid;
            Grid.SetRow(subTabBorder, 1);
            rootGrid.Children.Add(subTabBorder);

            // 3. BODY CONTENT (ScrollViewer with Stacks)
            _scrollViewer = new ScrollViewer { Padding = new Thickness(0, Math.Round(8 * scale), 0, Math.Round(20 * scale)) };
            var contentContainer = new Grid();

            _receivedListStack = new StackPanel { Visibility = _currentTab == 0 ? Visibility.Visible : Visibility.Collapsed };
            _sentListStack = new StackPanel { Visibility = _currentTab == 1 ? Visibility.Visible : Visibility.Collapsed };

            _loadingRing = new ProgressRing
            {
                Width = Math.Round(40 * scale),
                Height = Math.Round(40 * scale),
                IsActive = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, Math.Round(60 * scale), 0, 0)
            };

            contentContainer.Children.Add(_receivedListStack);
            contentContainer.Children.Add(_sentListStack);
            contentContainer.Children.Add(_loadingRing);

            _scrollViewer.Content = contentContainer;
            Grid.SetRow(_scrollViewer, 2);
            rootGrid.Children.Add(_scrollViewer);

            Content = rootGrid;
        }

        private void SwitchTab(int tab)
        {
            _currentTab = tab;
            double scale = ZaloSettingsService.Current.UiScaleFactor;

            _tab0Title.Foreground = _currentTab == 0 ? _activeBlue : _textSub;
            _tab0Title.FontWeight = _currentTab == 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _tab0Indicator.Background = _currentTab == 0 ? _activeBlue : new SolidColorBrush(Colors.Transparent);

            _tab1Title.Foreground = _currentTab == 1 ? _activeBlue : _textSub;
            _tab1Title.FontWeight = _currentTab == 1 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _tab1Indicator.Background = _currentTab == 1 ? _activeBlue : new SolidColorBrush(Colors.Transparent);

            _receivedListStack.Visibility = _currentTab == 0 ? Visibility.Visible : Visibility.Collapsed;
            _sentListStack.Visibility = _currentTab == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task LoadDataAsync()
        {
            if (ZaloApp.Session == null) return;
            _loadingRing.IsActive = true;
            _loadingRing.Visibility = Visibility.Visible;

            try
            {
                var recTask = ZaloAuth.GetFriendRequestsAsync(ZaloApp.Session, CancellationToken.None);
                var sentTask = ZaloAuth.GetSentFriendRequestsAsync(ZaloApp.Session, CancellationToken.None);
                await Task.WhenAll(recTask, sentTask);

                _receivedRequests = recTask.Result ?? new List<ZaloFriendRequest>();
                _sentRequests = sentTask.Result ?? new List<ZaloFriendRequest>();
            }
            catch (Exception ex)
            {
                ZaloLog.Add("LoadDataAsync FriendRequests failed: " + ex.Message);
            }
            finally
            {
                _loadingRing.IsActive = false;
                _loadingRing.Visibility = Visibility.Collapsed;
                RenderReceivedList();
                RenderSentList();
            }
        }

        private void RenderReceivedList()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            _receivedListStack.Children.Clear();

            _tab0Title.Text = _receivedRequests.Count > 0 ? $"Đã nhận ({_receivedRequests.Count})" : "Đã nhận";

            if (_receivedRequests.Count == 0)
            {
                var emptyCard = new Border
                {
                    Padding = new Thickness(Math.Round(24 * scale), Math.Round(50 * scale), Math.Round(24 * scale), Math.Round(50 * scale)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Không có lời mời kết bạn nào",
                    FontSize = Math.Round(16 * scale, 1),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Khi ai đó gửi lời mời kết bạn, bạn sẽ thấy xuất hiện tại đây.",
                    FontSize = Math.Round(13 * scale, 1),
                    Foreground = _textSub,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, Math.Round(6 * scale), 0, 0),
                    MaxWidth = Math.Round(260 * scale),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                _receivedListStack.Children.Add(emptyCard);
                return;
            }

            foreach (var req in _receivedRequests)
            {
                _receivedListStack.Children.Add(CreateReceivedRequestCard(req));
            }
        }

        private FrameworkElement CreateReceivedRequestCard(ZaloFriendRequest req)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var card = new Border
            {
                Background = _cardBg,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF2, 0xF6)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(12 * scale), Math.Round(14 * scale), Math.Round(12 * scale))
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(52 * scale)) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Avatar
            double avaSize = Math.Round(44 * scale);
            var avatarGrid = new Grid { Width = avaSize, Height = avaSize, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, Math.Round(2 * scale), 0, 0) };
            var avaBg = new Ellipse { Width = avaSize, Height = avaSize, Fill = ZaloAvatar.ColorFor(req.Name ?? "Z") };
            var avaInitial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(req.Name),
                FontSize = Math.Round(16 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avaBg);
            avatarGrid.Children.Add(avaInitial);

            if (!string.IsNullOrEmpty(req.Avatar))
            {
                var brush = ZaloAvatar.ImageFor(req.Avatar);
                if (brush != null)
                {
                    avatarGrid.Children.Add(new Ellipse { Width = avaSize, Height = avaSize, Fill = brush });
                    avaInitial.Text = string.Empty;
                }
            }
            Grid.SetColumn(avatarGrid, 0);
            mainGrid.Children.Add(avatarGrid);

            // Details & Actions
            var infoStack = new StackPanel { Margin = new Thickness(Math.Round(8 * scale), 0, 0, 0) };
            infoStack.Children.Add(new TextBlock
            {
                Text = req.Name ?? "Người dùng Zalo",
                FontSize = Math.Round(15.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            string msg = !string.IsNullOrWhiteSpace(req.Message) ? req.Message : "Muốn kết bạn với bạn";
            infoStack.Children.Add(new TextBlock
            {
                Text = msg,
                FontSize = Math.Round(13 * scale, 1),
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, Math.Round(2 * scale), 0, Math.Round(8 * scale))
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var statusText = new TextBlock
            {
                FontSize = Math.Round(13 * scale, 1),
                Foreground = _activeBlue,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            // Từ chối
            var rejectBtn = CreateTouchBorder(Math.Round(86 * scale), Math.Round(34 * scale));
            rejectBtn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
            rejectBtn.CornerRadius = new CornerRadius(Math.Round(17 * scale));
            rejectBtn.Margin = new Thickness(0, 0, Math.Round(10 * scale), 0);
            var rejectText = new TextBlock
            {
                Text = "Từ chối",
                FontSize = Math.Round(13 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x47, 0x55, 0x69)),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rejectBtn.Child = rejectText;

            // Đồng ý
            var acceptBtn = CreateTouchBorder(Math.Round(86 * scale), Math.Round(34 * scale));
            acceptBtn.Background = _activeBlue;
            acceptBtn.CornerRadius = new CornerRadius(Math.Round(17 * scale));
            var acceptText = new TextBlock
            {
                Text = "Đồng ý",
                FontSize = Math.Round(13 * scale, 1),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            acceptBtn.Child = acceptText;

            acceptBtn.Tapped += async (s, e) =>
            {
                acceptBtn.IsHitTestVisible = false;
                rejectBtn.IsHitTestVisible = false;
                acceptBtn.Opacity = 0.5;
                rejectBtn.Opacity = 0.5;
                try
                {
                    await ZaloAuth.AcceptFriendRequestAsync(ZaloApp.Session, req.UserId, CancellationToken.None);
                    btnRow.Visibility = Visibility.Collapsed;
                    statusText.Text = "Đã trở thành bạn bè";
                    statusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
                    statusText.Visibility = Visibility.Visible;

                    // Add to friends cache
                    if (!ZaloApp.Friends.Any(f => f.UserId == req.UserId))
                    {
                        ZaloApp.Friends.Add(new ZaloFriend
                        {
                            UserId = req.UserId,
                            Name = req.Name,
                            Avatar = req.Avatar
                        });
                        _ = ZaloApp.SaveCachedFriendsAsync();
                    }
                }
                catch (Exception ex)
                {
                    acceptBtn.IsHitTestVisible = true;
                    rejectBtn.IsHitTestVisible = true;
                    acceptBtn.Opacity = 1.0;
                    rejectBtn.Opacity = 1.0;
                    statusText.Text = "Lỗi: " + ex.Message;
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                    statusText.Visibility = Visibility.Visible;
                }
            };

            rejectBtn.Tapped += async (s, e) =>
            {
                acceptBtn.IsHitTestVisible = false;
                rejectBtn.IsHitTestVisible = false;
                acceptBtn.Opacity = 0.5;
                rejectBtn.Opacity = 0.5;
                try
                {
                    await ZaloAuth.RejectFriendRequestAsync(ZaloApp.Session, req.UserId, CancellationToken.None);
                    btnRow.Visibility = Visibility.Collapsed;
                    statusText.Text = "Đã từ chối lời mời";
                    statusText.Foreground = _textSub;
                    statusText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    acceptBtn.IsHitTestVisible = true;
                    rejectBtn.IsHitTestVisible = true;
                    acceptBtn.Opacity = 1.0;
                    rejectBtn.Opacity = 1.0;
                    statusText.Text = "Lỗi: " + ex.Message;
                    statusText.Foreground = new SolidColorBrush(Colors.Red);
                    statusText.Visibility = Visibility.Visible;
                }
            };

            btnRow.Children.Add(rejectBtn);
            btnRow.Children.Add(acceptBtn);
            infoStack.Children.Add(btnRow);
            infoStack.Children.Add(statusText);

            Grid.SetColumn(infoStack, 1);
            mainGrid.Children.Add(infoStack);

            card.Child = mainGrid;
            return card;
        }

        private void RenderSentList()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            _sentListStack.Children.Clear();

            _tab1Title.Text = _sentRequests.Count > 0 ? $"Đã gửi ({_sentRequests.Count})" : "Đã gửi";

            if (_sentRequests.Count == 0)
            {
                var emptyCard = new Border
                {
                    Padding = new Thickness(Math.Round(24 * scale), Math.Round(50 * scale), Math.Round(24 * scale), Math.Round(50 * scale)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Không có lời mời đang chờ",
                    FontSize = Math.Round(16 * scale, 1),
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Những lời mời kết bạn bạn đã gửi đi sẽ được hiển thị tại đây.",
                    FontSize = Math.Round(13 * scale, 1),
                    Foreground = _textSub,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, Math.Round(6 * scale), 0, 0),
                    MaxWidth = Math.Round(260 * scale),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                _sentListStack.Children.Add(emptyCard);
                return;
            }

            foreach (var req in _sentRequests)
            {
                _sentListStack.Children.Add(CreateSentRequestCard(req));
            }
        }

        private FrameworkElement CreateSentRequestCard(ZaloFriendRequest req)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var card = new Border
            {
                Background = _cardBg,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF2, 0xF6)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(12 * scale), Math.Round(14 * scale), Math.Round(12 * scale))
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(52 * scale)) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Avatar
            double avaSize = Math.Round(44 * scale);
            var avatarGrid = new Grid { Width = avaSize, Height = avaSize, VerticalAlignment = VerticalAlignment.Center };
            var avaBg = new Ellipse { Width = avaSize, Height = avaSize, Fill = ZaloAvatar.ColorFor(req.Name ?? "Z") };
            var avaInitial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(req.Name),
                FontSize = Math.Round(16 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avaBg);
            avatarGrid.Children.Add(avaInitial);

            if (!string.IsNullOrEmpty(req.Avatar))
            {
                var brush = ZaloAvatar.ImageFor(req.Avatar);
                if (brush != null)
                {
                    avatarGrid.Children.Add(new Ellipse { Width = avaSize, Height = avaSize, Fill = brush });
                    avaInitial.Text = string.Empty;
                }
            }
            Grid.SetColumn(avatarGrid, 0);
            mainGrid.Children.Add(avatarGrid);

            // Details
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Math.Round(8 * scale), 0, Math.Round(8 * scale), 0) };
            infoStack.Children.Add(new TextBlock
            {
                Text = req.Name ?? "Người dùng Zalo",
                FontSize = Math.Round(15.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "Đang chờ phản hồi...",
                FontSize = Math.Round(12.5 * scale, 1),
                Foreground = _textSub,
                Margin = new Thickness(0, Math.Round(2 * scale), 0, 0)
            });
            Grid.SetColumn(infoStack, 1);
            mainGrid.Children.Add(infoStack);

            // Undo Button
            var undoBtn = CreateTouchBorder(Math.Round(80 * scale), Math.Round(34 * scale));
            undoBtn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF5, 0xF9));
            undoBtn.CornerRadius = new CornerRadius(Math.Round(17 * scale));
            var undoText = new TextBlock
            {
                Text = "Thu hồi",
                FontSize = Math.Round(13 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)), // Red
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            undoBtn.Child = undoText;

            undoBtn.Tapped += async (s, e) =>
            {
                undoBtn.IsHitTestVisible = false;
                undoBtn.Opacity = 0.5;
                try
                {
                    await ZaloAuth.UndoFriendRequestAsync(ZaloApp.Session, req.UserId, CancellationToken.None);
                    _sentRequests.Remove(req);
                    RenderSentList();
                }
                catch (Exception ex)
                {
                    undoBtn.IsHitTestVisible = true;
                    undoBtn.Opacity = 1.0;
                    undoText.Text = "Thử lại";
                    ZaloLog.Add("Undo friend request failed: " + ex.Message);
                }
            };
            Grid.SetColumn(undoBtn, 2);
            mainGrid.Children.Add(undoBtn);

            card.Child = mainGrid;
            return card;
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var b = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Colors.Transparent),
                IsTapEnabled = true
            };
            b.PointerPressed += (s, e) => b.Opacity = 0.65;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }
    }
}
