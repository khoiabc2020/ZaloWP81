using System;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloWP81.Core;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class CallPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _endCallRed = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
        private readonly SolidColorBrush _answerGreen = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));

        private Ellipse _avatarEllipse;
        private TextBlock _avatarInitial;
        private TextBlock _calleeNameText;
        private TextBlock _callStatusText;
        private TextBlock _debugStatusText;
        private TextBlock _callTypeHeaderText;
        private Border _micButtonBorder;
        private Border _speakerButtonBorder;
        private Border _videoButtonBorder;
        private Grid _incomingControlsGrid;
        private Grid _activeControlsGrid;
        private MediaElement _audioPlayer;

        private Grid _callResultOverlay;
        private TextBlock _overlayTitle;
        private TextBlock _overlaySubtitle;
        private TextBlock _overlayIcon;
        private Border _overlayIconCircle;
        private DispatcherTimer _overlayDismissTimer;

        private object _target;
        private bool _isVideo;
        private bool _isIncoming;
        private string _peerId;
        private string _peerName;
        private string _peerAvatar;
        private bool _sessionInitialized;

        public CallPage()
        {
            BuildUI();
        }

        public CallPage(object target, bool isVideo = false) : this()
        {
            _target = target;
            _isVideo = isVideo;
            PopulateTarget(target);
            InitCallSession();
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x0A, 0x11, 0x28), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x1C, 0x2A, 0x4A), Offset = 1 });

            var rootGrid = new Grid
            {
                Background = gradient
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(80 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Top Header with Safe Area
            var headerBorder = new Border
            {
                Padding = new Thickness(Math.Round(12 * scale), Math.Round(22 * scale), Math.Round(12 * scale), Math.Round(6 * scale))
            };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) });

            var minimizeBtn = CreateTouchBorder(Math.Round(44 * scale), Math.Round(44 * scale));
            var minIcon = new TextBlock
            {
                Text = "\uE112",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(22 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            minimizeBtn.Child = minIcon;
            minimizeBtn.Tapped += (s, e) =>
            {
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(minimizeBtn, 0);
            header.Children.Add(minimizeBtn);

            var titleStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _callTypeHeaderText = new TextBlock
            {
                Text = _isVideo ? "Cuộc gọi video Zalo" : "Cuộc gọi thoại Zalo",
                FontSize = Math.Round(16 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var encryptBadge = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var lockIcon = new TextBlock
            {
                Text = "\uE1F6",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(11 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x38, 0xBD, 0xF8)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var lockText = new TextBlock
            {
                Text = "Mã hóa đầu cuối",
                FontSize = Math.Round(11.5 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x38, 0xBD, 0xF8)),
                VerticalAlignment = VerticalAlignment.Center
            };
            encryptBadge.Children.Add(lockIcon);
            encryptBadge.Children.Add(lockText);

            titleStack.Children.Add(_callTypeHeaderText);
            titleStack.Children.Add(encryptBadge);
            Grid.SetColumn(titleStack, 1);
            header.Children.Add(titleStack);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Center Callee Card
            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(Math.Round(24 * scale), 0, Math.Round(24 * scale), 0)
            };

            double avatarSize = Math.Round(116 * scale);
            var avatarRing = new Border
            {
                Width = avatarSize + Math.Round(10 * scale),
                Height = avatarSize + Math.Round(10 * scale),
                CornerRadius = new CornerRadius((avatarSize + Math.Round(10 * scale)) / 2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x38, 0xBD, 0xF8)),
                BorderThickness = new Thickness(Math.Round(3 * scale)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, Math.Round(20 * scale))
            };

            var avatarGrid = new Grid
            {
                Width = avatarSize,
                Height = avatarSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _avatarEllipse = new Ellipse { Width = avatarSize, Height = avatarSize, Fill = _activeBlue };
            _avatarInitial = new TextBlock
            {
                Text = "Z",
                FontSize = Math.Round(44 * scale),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_avatarEllipse);
            avatarGrid.Children.Add(_avatarInitial);
            avatarRing.Child = avatarGrid;
            centerStack.Children.Add(avatarRing);

            _calleeNameText = new TextBlock
            {
                Text = "Người nhận",
                FontSize = Math.Round(24 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            centerStack.Children.Add(_calleeNameText);

            _callStatusText = new TextBlock
            {
                Text = "Đang gọi...",
                FontSize = Math.Round(16 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xEE, 0xEE, 0xF2, 0xF6)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, Math.Round(10 * scale), 0, 0)
            };
            centerStack.Children.Add(_callStatusText);

            _debugStatusText = new TextBlock
            {
                Text = "",
                FontSize = Math.Round(11 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = Math.Round(300 * scale),
                Margin = new Thickness(0, Math.Round(6 * scale), 0, 0)
            };
            centerStack.Children.Add(_debugStatusText);

            Grid.SetRow(centerStack, 1);
            rootGrid.Children.Add(centerStack);

            // 3. Bottom Controls Dock (Active controls + Incoming call controls)
            var controlsDock = new Border
            {
                Padding = new Thickness(Math.Round(20 * scale), Math.Round(20 * scale), Math.Round(20 * scale), Math.Round(36 * scale)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var controlsContainer = new Grid();

            // Active Controls Grid (Mute, Speaker, Video, End Call)
            _activeControlsGrid = new Grid();
            _activeControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _activeControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _activeControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _activeControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Button 1: Mute
            var muteStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _micButtonBorder = CreateRoundControlBorder(Math.Round(58 * scale), Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            var micIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_mic_clean_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _micButtonBorder.Child = micIcon;
            _micButtonBorder.Tapped += (s, e) =>
            {
                ZaloCallSignaling.ToggleMute();
                UpdateControlStates();
            };
            var micLabel = new TextBlock
            {
                Text = "Tắt tiếng",
                FontSize = Math.Round(12 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(6 * scale), 0, 0)
            };
            muteStack.Children.Add(_micButtonBorder);
            muteStack.Children.Add(micLabel);
            Grid.SetColumn(muteStack, 0);
            _activeControlsGrid.Children.Add(muteStack);

            // Button 2: Speaker
            var speakerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _speakerButtonBorder = CreateRoundControlBorder(Math.Round(58 * scale), Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            var speakerIcon = new TextBlock
            {
                Text = "\uE15D",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(22 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _speakerButtonBorder.Child = speakerIcon;
            _speakerButtonBorder.Tapped += (s, e) =>
            {
                ZaloCallSignaling.ToggleSpeaker();
                UpdateControlStates();
                var session = ZaloCallSignaling.CurrentSession;
                if (session != null)
                {
                    ZaloUdpRelayEngine.Current.SetSpeakerphone(session.IsSpeakerOn);
                }
            };
            var speakerLabel = new TextBlock
            {
                Text = "Loa ngoài",
                FontSize = Math.Round(12 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(6 * scale), 0, 0)
            };
            speakerStack.Children.Add(_speakerButtonBorder);
            speakerStack.Children.Add(speakerLabel);
            Grid.SetColumn(speakerStack, 1);
            _activeControlsGrid.Children.Add(speakerStack);

            // Button 3: Video Camera
            var videoStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _videoButtonBorder = CreateRoundControlBorder(Math.Round(58 * scale), Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            var videoIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_video_clean_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _videoButtonBorder.Child = videoIcon;
            _videoButtonBorder.Tapped += (s, e) =>
            {
                ZaloCallSignaling.ToggleVideo();
                UpdateControlStates();
            };
            var videoLabel = new TextBlock
            {
                Text = "Camera",
                FontSize = Math.Round(12 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(6 * scale), 0, 0)
            };
            videoStack.Children.Add(_videoButtonBorder);
            videoStack.Children.Add(videoLabel);
            Grid.SetColumn(videoStack, 2);
            _activeControlsGrid.Children.Add(videoStack);

            // Button 4: End Call
            var endStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var endButtonBorder = CreateRoundControlBorder(Math.Round(58 * scale), Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
            var endIcon = new TextBlock
            {
                Text = "\uE13A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(24 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            endButtonBorder.Child = endIcon;
            endButtonBorder.Tapped += async (s, e) =>
            {
                StopAudioPlayback();
                await ZaloCallSignaling.EndCallAsync();
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            var endLabel = new TextBlock
            {
                Text = "Kết thúc",
                FontSize = Math.Round(12 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(6 * scale), 0, 0)
            };
            endStack.Children.Add(endButtonBorder);
            endStack.Children.Add(endLabel);
            Grid.SetColumn(endStack, 3);
            _activeControlsGrid.Children.Add(endStack);

            // Incoming Controls Grid (Từ chối / Trả lời)
            _incomingControlsGrid = new Grid { Visibility = Visibility.Collapsed };
            _incomingControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _incomingControlsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Reject Button (Red)
            var rejectStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var rejectBtn = CreateRoundControlBorder(Math.Round(64 * scale), Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
            var rejectIcon = new TextBlock
            {
                Text = "\uE13A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(26 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rejectBtn.Child = rejectIcon;
            rejectBtn.Tapped += async (s, e) =>
            {
                StopAudioPlayback();
                await ZaloCallSignaling.RejectCallAsync();
            };
            var rejectLabel = new TextBlock
            {
                Text = "Từ chối",
                FontSize = Math.Round(13 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(8 * scale), 0, 0)
            };
            rejectStack.Children.Add(rejectBtn);
            rejectStack.Children.Add(rejectLabel);
            Grid.SetColumn(rejectStack, 0);
            _incomingControlsGrid.Children.Add(rejectStack);

            // Answer Button (Green)
            var answerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var answerBtn = CreateRoundControlBorder(Math.Round(64 * scale), Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));
            var answerIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_call_clean_white.png")),
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            answerBtn.Child = answerIcon;
            answerBtn.Tapped += async (s, e) =>
            {
                StopAudioPlayback();
                await ZaloCallSignaling.AnswerCallAsync();
                _incomingControlsGrid.Visibility = Visibility.Collapsed;
                _activeControlsGrid.Visibility = Visibility.Visible;
            };
            var answerLabel = new TextBlock
            {
                Text = "Trả lời",
                FontSize = Math.Round(13 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E)),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, Math.Round(8 * scale), 0, 0)
            };
            answerStack.Children.Add(answerBtn);
            answerStack.Children.Add(answerLabel);
            Grid.SetColumn(answerStack, 1);
            _incomingControlsGrid.Children.Add(answerStack);

            controlsContainer.Children.Add(_activeControlsGrid);
            controlsContainer.Children.Add(_incomingControlsGrid);
            controlsDock.Child = controlsContainer;
            Grid.SetRow(controlsDock, 2);
            rootGrid.Children.Add(controlsDock);

            // Audio Player for Ringtone / Ringback Tone (Must be visible with size > 0 for WP8.1 media pipeline)
            _audioPlayer = new MediaElement
            {
                AutoPlay = false,
                Width = 1,
                Height = 1,
                Opacity = 0.01,
                Visibility = Visibility.Visible,
                AudioCategory = AudioCategory.Communications
            };
            rootGrid.Children.Add(_audioPlayer);

            BuildCallResultOverlay(rootGrid, scale);

            this.Content = rootGrid;
        }

        private void BuildCallResultOverlay(Grid rootGrid, double scale)
        {
            _callResultOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x0A, 0x11, 0x28)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRowSpan(_callResultOverlay, 3);

            var cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x29, 0x3B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x41, 0x55)),
                BorderThickness = new Thickness(Math.Round(1.5 * scale)),
                CornerRadius = new CornerRadius(Math.Round(20 * scale)),
                Padding = new Thickness(Math.Round(24 * scale)),
                MaxWidth = Math.Round(320 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Icon Circle
            _overlayIconCircle = new Border
            {
                Width = Math.Round(68 * scale),
                Height = Math.Round(68 * scale),
                CornerRadius = new CornerRadius(Math.Round(34 * scale)),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, Math.Round(16 * scale))
            };
            _overlayIcon = new TextBlock
            {
                Text = "\uE13A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(30 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _overlayIconCircle.Child = _overlayIcon;
            stack.Children.Add(_overlayIconCircle);

            // Title
            _overlayTitle = new TextBlock
            {
                Text = "Cuộc gọi bị từ chối",
                FontSize = Math.Round(19 * scale),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, Math.Round(6 * scale))
            };
            stack.Children.Add(_overlayTitle);

            // Subtitle
            _overlaySubtitle = new TextBlock
            {
                Text = "Đối phương hiện đang bận hoặc đã từ chối cuộc gọi.",
                FontSize = Math.Round(13.5 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x94, 0xA3, 0xB8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, Math.Round(22 * scale))
            };
            stack.Children.Add(_overlaySubtitle);

            // Actions Row (Gọi lại | Đóng)
            var actionGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Button "Gọi lại"
            var redialBtn = new Border
            {
                Background = _activeBlue,
                CornerRadius = new CornerRadius(Math.Round(12 * scale)),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(10 * scale), Math.Round(14 * scale), Math.Round(10 * scale)),
                Margin = new Thickness(0, 0, Math.Round(6 * scale), 0)
            };
            var redialStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var redialIcon = new TextBlock
            {
                Text = "\uE13A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(16 * scale),
                Foreground = new SolidColorBrush(Colors.White),
                Margin = new Thickness(0, 0, Math.Round(6 * scale), 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var redialLabel = new TextBlock
            {
                Text = "Gọi lại",
                FontSize = Math.Round(13.5 * scale),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };
            redialStack.Children.Add(redialIcon);
            redialStack.Children.Add(redialLabel);
            redialBtn.Child = redialStack;

            redialBtn.PointerPressed += (s, e) => redialBtn.Opacity = 0.6;
            redialBtn.PointerReleased += (s, e) => redialBtn.Opacity = 1.0;
            redialBtn.PointerCanceled += (s, e) => redialBtn.Opacity = 1.0;
            redialBtn.Tapped += (s, e) =>
            {
                DismissOverlay();
                _sessionInitialized = false;
                InitCallSession();
            };
            Grid.SetColumn(redialBtn, 0);
            actionGrid.Children.Add(redialBtn);

            // Button "Đóng"
            var closeBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x41, 0x55)),
                CornerRadius = new CornerRadius(Math.Round(12 * scale)),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(10 * scale), Math.Round(14 * scale), Math.Round(10 * scale)),
                Margin = new Thickness(Math.Round(6 * scale), 0, 0, 0)
            };
            var closeLabel = new TextBlock
            {
                Text = "Đóng",
                FontSize = Math.Round(13.5 * scale),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Child = closeLabel;

            closeBtn.PointerPressed += (s, e) => closeBtn.Opacity = 0.6;
            closeBtn.PointerReleased += (s, e) => closeBtn.Opacity = 1.0;
            closeBtn.PointerCanceled += (s, e) => closeBtn.Opacity = 1.0;
            closeBtn.Tapped += (s, e) =>
            {
                DismissOverlay();
                if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(closeBtn, 1);
            actionGrid.Children.Add(closeBtn);

            stack.Children.Add(actionGrid);
            cardBorder.Child = stack;
            _callResultOverlay.Children.Add(cardBorder);
            rootGrid.Children.Add(_callResultOverlay);
        }

        private void DismissOverlay()
        {
            DismissOverlayTimer();
            if (_callResultOverlay != null)
            {
                _callResultOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void DismissOverlayTimer()
        {
            try
            {
                if (_overlayDismissTimer != null)
                {
                    _overlayDismissTimer.Stop();
                    _overlayDismissTimer = null;
                }
            }
            catch { }
        }

        private static Border CreateRoundControlBorder(double size, Color bgColor)
        {
            var b = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = new SolidColorBrush(bgColor)
            };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }

        private void InitCallSession()
        {
            if (_sessionInitialized) return;
            _sessionInitialized = true;

            ZaloCallSignaling.StateChanged -= OnCallStateChanged;
            ZaloCallSignaling.DurationTick -= OnDurationTick;
            ZaloCallSignaling.DiagnosticUpdated -= OnDiagnosticUpdated;
            ZaloCallSignaling.StateChanged += OnCallStateChanged;
            ZaloCallSignaling.DurationTick += OnDurationTick;
            ZaloCallSignaling.DiagnosticUpdated += OnDiagnosticUpdated;
            if (!string.IsNullOrEmpty(ZaloCallSignaling.LastDiagnostic))
            {
                _debugStatusText.Text = ZaloCallSignaling.LastDiagnostic;
            }

            if (_isIncoming)
            {
                _callStatusText.Text = "Cuộc gọi đến...";
                PlayIncomingRingtone();
                _incomingControlsGrid.Visibility = Visibility.Visible;
                _activeControlsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                _incomingControlsGrid.Visibility = Visibility.Collapsed;
                _activeControlsGrid.Visibility = Visibility.Visible;
                _ = ZaloCallSignaling.StartCallAsync(_peerId, _peerName, _peerAvatar, _isVideo);
                PlayRingbackTone();
            }
        }

        private void UpdateControlStates()
        {
            var session = ZaloCallSignaling.CurrentSession;
            if (session == null) return;

            if (_micButtonBorder != null)
            {
                _micButtonBorder.Background = session.IsMuted
                    ? new SolidColorBrush(Color.FromArgb(0xDD, 0xEF, 0x44, 0x44))
                    : new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            }

            if (_speakerButtonBorder != null)
            {
                _speakerButtonBorder.Background = session.IsSpeakerOn
                    ? new SolidColorBrush(Color.FromArgb(0xDD, 0x00, 0x88, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            }

            if (_videoButtonBorder != null)
            {
                _videoButtonBorder.Background = session.IsVideoEnabled
                    ? new SolidColorBrush(Color.FromArgb(0xDD, 0x00, 0x88, 0xFF))
                    : new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
            }
        }

        private void PlayRingbackTone()
        {
            try
            {
                ZaloAudioService.Current.PlayCallWaiting();
                if (_audioPlayer != null)
                {
                    _audioPlayer.IsLooping = true;
                    _audioPlayer.Source = new Uri("ms-appx:///Assets/Sounds/zalo_new_ringback.mp3");
                    _audioPlayer.Play();
                }
            }
            catch { }
        }

        private void PlayIncomingRingtone()
        {
            try
            {
                ZaloAudioService.Current.PlayIncomingRing();
                if (_audioPlayer != null)
                {
                    _audioPlayer.IsLooping = true;
                    _audioPlayer.Source = new Uri("ms-appx:///Assets/Sounds/zalo_ringtone.mp3");
                    _audioPlayer.Play();
                }
            }
            catch { }
        }

        private void StopAudioPlayback()
        {
            try
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.Stop();
                    _audioPlayer.IsLooping = false;
                }
                Windows.Media.Playback.BackgroundMediaPlayer.Current.Pause();
            }
            catch { }
        }

        private void PlayHangupTone()
        {
            try
            {
                ZaloAudioService.Current.PlayHangUp();
                if (_audioPlayer != null)
                {
                    _audioPlayer.IsLooping = false;
                    _audioPlayer.Source = new Uri("ms-appx:///Assets/Sounds/zalo_new_hang_up.mp3");
                    _audioPlayer.Play();
                }
            }
            catch { }
        }

        private void OnCallStateChanged(ZaloCallSession session, ZaloCallState state)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (state)
                {
                    case ZaloCallState.Calling:
                        _callStatusText.Text = _isIncoming ? "Cuộc gọi đến..." : "Đang gọi...";
                        break;

                    case ZaloCallState.Ringing:
                        _callStatusText.Text = _isIncoming ? "Cuộc gọi đến..." : "Đang đổ chuông...";
                        break;

                    case ZaloCallState.Connected:
                        StopAudioPlayback();
                        _incomingControlsGrid.Visibility = Visibility.Collapsed;
                        _activeControlsGrid.Visibility = Visibility.Visible;
                        _callStatusText.Text = "00:00";
                        _callStatusText.FontWeight = Windows.UI.Text.FontWeights.SemiBold;
                        _callStatusText.FontSize = 20;

                        // Attach live MediaStreamSource to _audioPlayer for real-time VoIP voice
                        try
                        {
                            var mss = ZaloUdpRelayEngine.Current.CreateMediaStreamSource();
                            _audioPlayer.SetMediaStreamSource(mss);
                            _audioPlayer.Play();
                            ZaloUdpRelayEngine.Current.SetSpeakerphone(session?.IsSpeakerOn ?? false);
                        }
                        catch (Exception ex)
                        {
                            ZaloLog.Add("CallPage attach audio stream error: " + ex.Message);
                        }
                        break;

                    case ZaloCallState.Ended:
                    case ZaloCallState.Rejected:
                        StopAudioPlayback();
                        PlayHangupTone();

                        bool isRej = state == ZaloCallState.Rejected;
                        int duration = session?.DurationSeconds ?? 0;

                        if (isRej)
                        {
                            _callStatusText.Text = "Người dùng bận";
                            _callStatusText.Foreground = _endCallRed;

                            if (_overlayIconCircle != null) _overlayIconCircle.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44));
                            if (_overlayIcon != null) _overlayIcon.Foreground = _endCallRed;
                            if (_overlayTitle != null) _overlayTitle.Text = "Cuộc gọi bị từ chối";
                            if (_overlaySubtitle != null) _overlaySubtitle.Text = "Người nhận hiện đang bận hoặc đã từ chối cuộc gọi.";
                        }
                        else if (duration > 0)
                        {
                            _callStatusText.Text = "Cuộc gọi đã kết thúc";
                            _callStatusText.Foreground = new SolidColorBrush(Colors.White);

                            if (_overlayIconCircle != null) _overlayIconCircle.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x88, 0xFF));
                            if (_overlayIcon != null) _overlayIcon.Foreground = _activeBlue;
                            if (_overlayTitle != null) _overlayTitle.Text = "Cuộc gọi đã kết thúc";
                            if (_overlaySubtitle != null) _overlaySubtitle.Text = string.Format("Thời gian đàm thoại: {0}", ZaloCallSignaling.FormatDuration(duration));
                        }
                        else
                        {
                            _callStatusText.Text = "Không có trả lời";
                            _callStatusText.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));

                            if (_overlayIconCircle != null) _overlayIconCircle.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0x9E, 0x0B));
                            if (_overlayIcon != null) _overlayIcon.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
                            if (_overlayTitle != null) _overlayTitle.Text = "Không có trả lời";
                            if (_overlaySubtitle != null) _overlaySubtitle.Text = "Người nhận hiện không thể nhận cuộc gọi của bạn.";
                        }

                        if (_callResultOverlay != null)
                        {
                            _callResultOverlay.Visibility = Visibility.Visible;
                        }

                        // Auto-return after 4 seconds if user doesn't tap "Gọi lại" or "Đóng"
                        DismissOverlayTimer();
                        _overlayDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.0) };
                        _overlayDismissTimer.Tick += (s, e) =>
                        {
                            DismissOverlay();
                            if (NavigationManager.CanGoBack) NavigationManager.GoBack();
                            else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                        };
                        _overlayDismissTimer.Start();
                        break;
                }
            });
        }

        private void OnDurationTick(ZaloCallSession session, int durationSeconds)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _callStatusText.Text = ZaloCallSignaling.FormatDuration(durationSeconds);
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                _target = e.Parameter;
                PopulateTarget(e.Parameter);
                InitCallSession();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            DismissOverlay();
            ZaloCallSignaling.StateChanged -= OnCallStateChanged;
            ZaloCallSignaling.DurationTick -= OnDurationTick;
            ZaloCallSignaling.DiagnosticUpdated -= OnDiagnosticUpdated;
            StopAudioPlayback();
            _sessionInitialized = false;
        }

        private void OnDiagnosticUpdated(string text)
        {
            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (_debugStatusText != null)
                {
                    _debugStatusText.Text = text ?? string.Empty;
                }
            });
        }

        private void PopulateTarget(object target)
        {
            if (target is ZaloCallSession session)
            {
                _peerId = session.PeerId;
                _peerName = session.PeerName ?? "Cuộc gọi Zalo";
                _peerAvatar = session.PeerAvatar;
                _isIncoming = !session.IsOutgoing;
                _isVideo = session.CallType == ZaloCallType.Video;

                _calleeNameText.Text = _peerName;
                _avatarInitial.Text = ZaloAvatar.InitialOf(_peerName);
                _avatarEllipse.Fill = _activeBlue;
                if (!string.IsNullOrEmpty(_peerAvatar))
                {
                    try
                    {
                        var brush = ZaloAvatar.ImageFor(_peerAvatar);
                        if (brush != null)
                        {
                            _avatarEllipse.Fill = brush;
                            _avatarInitial.Text = string.Empty;
                        }
                    }
                    catch { }
                }
            }
            else if (target is ZaloFriend friend)
            {
                _peerId = friend.UserId;
                _peerName = friend.Name ?? "Bạn bè Zalo";
                _peerAvatar = friend.Avatar;

                _calleeNameText.Text = _peerName;
                _avatarInitial.Text = ZaloAvatar.InitialOf(_peerName);
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
                _peerId = !string.IsNullOrEmpty(conv.ThreadId) ? conv.ThreadId : conv.Key;
                _peerName = conv.Name ?? "Trò chuyện";
                _peerAvatar = conv.AvatarUrl;

                _calleeNameText.Text = _peerName;
                _avatarInitial.Text = conv.Initial ?? "Z";
                _avatarEllipse.Fill = conv.AvatarColor ?? _activeBlue;
                var brush = conv.AvatarImageBrush ?? ZaloAvatar.ImageFor(conv.AvatarUrl);
                if (brush != null)
                {
                    _avatarEllipse.Fill = brush;
                    _avatarInitial.Text = string.Empty;
                }
            }

            // Ensure peerId is a clean numeric UID without "u:" or "g:" prefixes
            if (!string.IsNullOrEmpty(_peerId))
            {
                if (_peerId.StartsWith("u:") || _peerId.StartsWith("g:"))
                {
                    _peerId = _peerId.Substring(2);
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
