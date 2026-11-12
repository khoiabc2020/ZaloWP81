using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.System;
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
    public sealed class ConversationOptionsPage : Page
    {
        private readonly ZaloConversation _conversation;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x19, 0x1C));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0x87, 0x91));
        private readonly SolidColorBrush _cardBg = new SolidColorBrush(Colors.White);

        private TextBlock _nameText;
        private TextBlock _subtitleText;
        private Image _muteIconImage;
        private TextBlock _muteLabelText;
        private TextBlock _aliasStatusText;
        private TextBlock _mediaCountText;
        private TextBlock _mediaSummaryText;
        private TextBlock _commonGroupsStatusText;
        private TextBlock _hiddenStatusText;
        private ZaloModernSwitch _hiddenToggle;
        private bool _loadingHidden;
        private bool _updatingHidden;

        private List<ConversationMediaItem> _mediaItems = new List<ConversationMediaItem>();

        public ConversationOptionsPage(ZaloConversation conversation)
        {
            _conversation = conversation;
            BuildUI();
            Loaded += ConversationOptionsPage_Loaded;
        }

        private async void ConversationOptionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMuteAction();
            UpdateAliasText();
            BuildMediaItems();
            await LoadHiddenStateAsync();
        }

        private ZaloFriend FindFriend()
        {
            return ZaloApp.Friends == null ? null
                : ZaloApp.Friends.FirstOrDefault(item => item.UserId == _conversation?.ThreadId);
        }

        private void UpdateMuteAction()
        {
            if (_conversation == null) return;
            if (_muteLabelText != null)
                _muteLabelText.Text = _conversation.IsMuted ? "Bật thông báo" : "Tắt thông báo";
            if (_muteIconImage != null)
                _muteIconImage.Source = new BitmapImage(new Uri(_conversation.IsMuted ? "ms-appx:///Assets/Icons/ic_bell_mute_clean_gray.png" : "ms-appx:///Assets/Icons/ic_bell_clean_gray.png"));
        }

        private void UpdateAliasText()
        {
            ZaloFriend friend = FindFriend();
            if (_aliasStatusText != null)
            {
                _aliasStatusText.Text = friend != null && !string.IsNullOrWhiteSpace(friend.Alias)
                    ? friend.Alias : "Chưa đặt tên gợi nhớ";
            }
        }

        private void BuildMediaItems()
        {
            if (_conversation?.Messages == null) return;

            _mediaItems = _conversation.Messages
                .Where(m => m != null && !m.HasVoice && !m.IsSticker && (m.HasMedia || m.HasLinkPreview || m.HasFile))
                .OrderByDescending(m => m.Ts)
                .Take(40)
                .Select(m =>
                {
                    bool link = m.HasLinkPreview;
                    string url = link ? m.LinkPreviewUrl : m.MediaFullUrl;
                    if (string.IsNullOrWhiteSpace(url)) url = m.MediaUrl;
                    string kind = link ? "Liên kết" : m.HasVideo ? "Video"
                        : m.HasFile ? "Tệp" : "Ảnh";
                    string title = link ? (m.LinkPreviewTitle.Length > 0 ? m.LinkPreviewTitle : url)
                        : m.HasFile ? (m.FileName ?? "Tệp tin") : kind;
                    return new ConversationMediaItem
                    {
                        Title = title,
                        Subtitle = kind + " · " + ZaloFormat.Time(m.Ts),
                        Url = url,
                        IsImage = !link && !m.HasVideo && !m.HasFile && !m.IsSticker
                    };
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Url))
                .ToList();

            if (_mediaCountText != null)
                _mediaCountText.Text = _mediaItems.Count == 0 ? "" : _mediaItems.Count + " mục";
            if (_mediaSummaryText != null)
                _mediaSummaryText.Text = _mediaItems.Count == 0
                    ? "Chưa có ảnh, tệp hay liên kết nào trong cuộc trò chuyện này."
                    : $"{_mediaItems.Count} mục gần đây · chạm để xem chi tiết";
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF6, 0xF8))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Header (Zalo Blue Gradient)
            var headerBorder = new Border { Padding = new Thickness(6, 0, 14, 0) };
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
                Text = "Tùy chọn",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Scrollable Body
            var scroll = new ScrollViewer { Padding = new Thickness(0, 0, 0, 20) };
            var stack = new StackPanel();

            // Profile & 4 Quick Actions Card (Exact Win10 Dimensions)
            var profileCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(18, 22, 18, 18),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var profileStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            // Avatar 108x108
            var avatarGrid = new Grid { Width = 108, Height = 108, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 14) };
            var avatarBg = new Ellipse { Width = 108, Height = 108, Fill = _conversation?.AvatarColor ?? _activeBlue };
            var initial = new TextBlock
            {
                Text = _conversation?.Initial ?? "?",
                FontSize = 36,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avatarBg);
            avatarGrid.Children.Add(initial);

            if (_conversation?.AvatarImageBrush != null)
            {
                var imgEllipse = new Ellipse { Width = 108, Height = 108, Fill = _conversation.AvatarImageBrush };
                avatarGrid.Children.Add(imgEllipse);
            }
            profileStack.Children.Add(avatarGrid);

            _nameText = new TextBlock
            {
                Text = _conversation?.Name ?? "Trò chuyện",
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            profileStack.Children.Add(_nameText);

            _subtitleText = new TextBlock
            {
                Text = ZaloApp.IsFriend(_conversation?.ThreadId) ? "" : "Người lạ",
                FontSize = 13.5,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            profileStack.Children.Add(_subtitleText);

            // 4 Quick Action Buttons
            var actionsGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            for (int i = 0; i < 4; i++) actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Button 0: Tìm tin nhắn
            var btnSearch = CreateQuickActionButton("ms-appx:///Assets/Icons/ic_search_clean_gray.png", "Tìm tin nhắn");
            btnSearch.Tapped += (s, e) => ShowSearchDialog();
            Grid.SetColumn(btnSearch, 0);
            actionsGrid.Children.Add(btnSearch);

            // Button 1: Trang cá nhân
            var btnProfile = CreateQuickActionButton("ms-appx:///Assets/Icons/ic_user_clean_gray.png", "Trang cá nhân");
            btnProfile.Tapped += (s, e) => OpenFriendProfile();
            Grid.SetColumn(btnProfile, 1);
            actionsGrid.Children.Add(btnProfile);

            // Button 2: Đổi hình nền
            var btnWp = CreateQuickActionButton("ms-appx:///Assets/Icons/ic_wallpaper_clean_gray.png", "Đổi hình nền");
            btnWp.Tapped += (s, e) => ShowWallpaperDialog();
            Grid.SetColumn(btnWp, 2);
            actionsGrid.Children.Add(btnWp);

            // Button 3: Tắt / Bật thông báo
            string muteIconUri = _conversation?.IsMuted == true ? "ms-appx:///Assets/Icons/ic_bell_mute_clean_gray.png" : "ms-appx:///Assets/Icons/ic_bell_clean_gray.png";
            string muteLabel = _conversation?.IsMuted == true ? "Bật thông báo" : "Tắt thông báo";
            var btnMute = CreateQuickActionButton(muteIconUri, muteLabel, out _muteIconImage, out _muteLabelText);
            btnMute.Tapped += async (s, e) =>
            {
                if (_conversation == null) return;
                try
                {
                    await ZaloApp.SetMutedAsync(_conversation, !_conversation.IsMuted);
                    UpdateMuteAction();
                }
                catch (Exception ex)
                {
                    await new MessageDialog("Lỗi đổi thông báo: " + ex.Message).ShowAsync();
                }
            };
            Grid.SetColumn(btnMute, 3);
            actionsGrid.Children.Add(btnMute);

            profileStack.Children.Add(actionsGrid);
            profileCard.Child = profileStack;
            stack.Children.Add(profileCard);

            // Card 2: Đổi tên gợi nhớ (Alias)
            var aliasCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(22, 16, 22, 16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var aliasGrid = new Grid();
            aliasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            aliasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            aliasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var aliasIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_edit_clean_gray.png")),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(aliasIcon, 0);
            aliasGrid.Children.Add(aliasIcon);

            var aliasTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            aliasTextStack.Children.Add(new TextBlock { Text = "Đổi tên gợi nhớ", FontSize = 16.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            _aliasStatusText = new TextBlock { Text = "Đang tải...", FontSize = 13, Foreground = _textSub, Margin = new Thickness(0, 3, 0, 0) };
            aliasTextStack.Children.Add(_aliasStatusText);
            Grid.SetColumn(aliasTextStack, 1);
            aliasGrid.Children.Add(aliasTextStack);

            var aliasChevron = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_chevron_right_clean_gray.png")),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(aliasChevron, 2);
            aliasGrid.Children.Add(aliasChevron);

            aliasCard.Child = aliasGrid;
            aliasCard.PointerPressed += (s, e) => aliasCard.Opacity = 0.6;
            aliasCard.PointerReleased += (s, e) => aliasCard.Opacity = 1.0;
            aliasCard.PointerCanceled += (s, e) => aliasCard.Opacity = 1.0;
            aliasCard.Tapped += (s, e) => ShowAliasDialog();
            stack.Children.Add(aliasCard);

            // Card 3: Ảnh, file, link
            var mediaCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(22, 16, 22, 16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var mediaStack = new StackPanel();

            var mediaHeaderGrid = new Grid();
            mediaHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            mediaHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mediaHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var mediaIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_photo_clean_gray.png")),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(mediaIcon, 0);
            mediaHeaderGrid.Children.Add(mediaIcon);

            var mediaTitle = new TextBlock { Text = "Ảnh, file, link", FontSize = 16.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(mediaTitle, 1);
            mediaHeaderGrid.Children.Add(mediaTitle);

            _mediaCountText = new TextBlock { Text = "", FontSize = 13, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_mediaCountText, 2);
            mediaHeaderGrid.Children.Add(_mediaCountText);

            mediaStack.Children.Add(mediaHeaderGrid);

            var mediaSummaryBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF7, 0xF8, 0xFA)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(38, 12, 0, 0)
            };
            _mediaSummaryText = new TextBlock
            {
                Text = "Nội dung đã tải trong cuộc trò chuyện sẽ xuất hiện tại đây.",
                FontSize = 13.5,
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap
            };
            mediaSummaryBox.Child = _mediaSummaryText;
            mediaStack.Children.Add(mediaSummaryBox);

            mediaCard.Child = mediaStack;
            mediaCard.PointerPressed += (s, e) => mediaCard.Opacity = 0.6;
            mediaCard.PointerReleased += (s, e) => mediaCard.Opacity = 1.0;
            mediaCard.PointerCanceled += (s, e) => mediaCard.Opacity = 1.0;
            mediaCard.Tapped += (s, e) => ShowMediaDialog();
            stack.Children.Add(mediaCard);

            // Card 4: Xem nhóm chung
            var commonGroupsCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(22, 16, 22, 16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var commonStack = new StackPanel();

            var commonGrid = new Grid();
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            commonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cgIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_clean_gray.png")),
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(cgIcon, 0);
            commonGrid.Children.Add(cgIcon);

            var cgTitle = new TextBlock { Text = "Xem nhóm chung", FontSize = 16.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(cgTitle, 1);
            commonGrid.Children.Add(cgTitle);

            var cgChevron = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_chevron_right_clean_gray.png")),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(cgChevron, 2);
            commonGrid.Children.Add(cgChevron);

            commonStack.Children.Add(commonGrid);

            _commonGroupsStatusText = new TextBlock
            {
                Text = "Chạm để tìm các nhóm bạn và người này cùng tham gia.",
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(38, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            commonStack.Children.Add(_commonGroupsStatusText);

            commonGroupsCard.Child = commonStack;
            commonGroupsCard.PointerPressed += (s, e) => commonGroupsCard.Opacity = 0.6;
            commonGroupsCard.PointerReleased += (s, e) => commonGroupsCard.Opacity = 1.0;
            commonGroupsCard.PointerCanceled += (s, e) => commonGroupsCard.Opacity = 1.0;
            commonGroupsCard.Tapped += (s, e) => ShowCommonGroupsDialog();
            stack.Children.Add(commonGroupsCard);

            // Card 5: Ẩn trò chuyện
            var hiddenCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(22, 14, 22, 14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var hiddenStack = new StackPanel();

            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hTitle = new TextBlock { Text = "Ẩn trò chuyện", FontSize = 16.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(hTitle, 0);
            hGrid.Children.Add(hTitle);

            _hiddenToggle = new ZaloModernSwitch { HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = false };
            _hiddenToggle.Toggled += HiddenToggle_Toggled;
            Grid.SetColumn(_hiddenToggle, 1);
            hGrid.Children.Add(_hiddenToggle);

            hiddenStack.Children.Add(hGrid);

            _hiddenStatusText = new TextBlock
            {
                Text = "Đang tải trạng thái ẩn...",
                FontSize = 12.5,
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            hiddenStack.Children.Add(_hiddenStatusText);

            hiddenCard.Child = hiddenStack;
            stack.Children.Add(hiddenCard);

            // Card 6: Danger zone (Xóa lịch sử & Xóa hội thoại)
            var dangerCard = new Border
            {
                Background = _cardBg,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var dangerStack = new StackPanel();

            var clearRow = CreateDangerRow("Xóa toàn bộ lịch sử trò chuyện", Color.FromArgb(0xFF, 0xDC, 0x26, 0x26));
            clearRow.Tapped += async (s, e) =>
            {
                if (_conversation == null) return;
                var md = new MessageDialog("Bạn có chắc chắn muốn xóa toàn bộ lịch sử tin nhắn của cuộc trò chuyện này không?", "Xác nhận xóa");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    _conversation.Messages.Clear();
                    _conversation.LastText = string.Empty;
                    BuildMediaItems();
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(clearRow);

            dangerStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            var deleteRow = CreateDangerRow("Xóa cuộc trò chuyện", Color.FromArgb(0xFF, 0xB9, 0x1C, 0x1C));
            deleteRow.Tapped += async (s, e) =>
            {
                if (_conversation == null) return;
                var md = new MessageDialog("Bạn có chắc chắn muốn xóa cuộc trò chuyện này khỏi danh sách không?", "Xóa cuộc trò chuyện");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    ZaloApp.Conversations.Remove(_conversation);
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(deleteRow);

            dangerCard.Child = dangerStack;
            stack.Children.Add(dangerCard);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private Border CreateQuickActionButton(string iconUri, string label)
        {
            Image dummyIcon;
            TextBlock dummyLabel;
            return CreateQuickActionButton(iconUri, label, out dummyIcon, out dummyLabel);
        }

        private Border CreateQuickActionButton(string iconUri, string label, out Image iconImage, out TextBlock labelBlock)
        {
            var btnBorder = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(2)
            };
            var btnStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var circle = new Border
            {
                Width = 54,
                Height = 54,
                CornerRadius = new CornerRadius(27),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF6, 0xF8)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconImage = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 26,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            circle.Child = iconImage;
            btnStack.Children.Add(circle);

            labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                Foreground = _textDark,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            btnStack.Children.Add(labelBlock);

            btnBorder.Child = btnStack;
            btnBorder.PointerPressed += (s, e) => btnBorder.Opacity = 0.6;
            btnBorder.PointerReleased += (s, e) => btnBorder.Opacity = 1.0;
            btnBorder.PointerCanceled += (s, e) => btnBorder.Opacity = 1.0;
            return btnBorder;
        }

        private Border CreateDangerRow(string title, Color color)
        {
            var border = new Border
            {
                Padding = new Thickness(22, 14, 22, 14),
                Background = new SolidColorBrush(Colors.Transparent)
            };
            var text = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color)
            };
            border.Child = text;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private Border CreateTouchBorder(double width, double height)
        {
            var b = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Colors.Transparent)
            };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }

        private void OpenFriendProfile()
        {
            if (_conversation == null) return;
            ZaloFriend friend = FindFriend() ?? new ZaloFriend
            {
                UserId = _conversation.ThreadId,
                Name = _conversation.Name,
                OriginalName = _conversation.Name,
                Avatar = _conversation.AvatarUrl
            };
            NavigationManager.NavigateTo(new FriendProfilePage(friend));
        }

        private async void ShowSearchDialog()
        {
            if (_conversation == null) return;
            var box = new TextBox { PlaceholderText = "Nhập từ khóa cần tìm kiếm...", MinHeight = 44 };
            var cd = new ContentDialog
            {
                Title = "Tìm kiếm tin nhắn",
                Content = box,
                PrimaryButtonText = "Tìm",
                SecondaryButtonText = "Đóng"
            };
            App.StyleContentDialog(cd);
            if (await cd.ShowAsync() != ContentDialogResult.Primary) return;

            string query = (box.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(query)) return;

            NavigationManager.NavigateTo(new SearchResultsPage(new SearchPageRequest { ConversationKey = _conversation.Key, Query = query }));
        }

        private async void ShowAliasDialog()
        {
            ZaloFriend friend = FindFriend();
            if (friend == null)
            {
                await new MessageDialog("Chỉ có thể đặt tên gợi nhớ cho người trong danh bạ.", "Tên gợi nhớ").ShowAsync();
                return;
            }

            var input = new TextBox
            {
                Text = friend.Alias ?? string.Empty,
                PlaceholderText = friend.OriginalName ?? friend.Name,
                MaxLength = 40,
                MinHeight = 44
            };
            var dialog = new ContentDialog
            {
                Title = "Đổi tên gợi nhớ",
                Content = input,
                PrimaryButtonText = "Lưu",
                SecondaryButtonText = string.IsNullOrWhiteSpace(friend.Alias) ? "Hủy" : "Xóa tên"
            };
            App.StyleContentDialog(dialog);
            ContentDialogResult result = await dialog.ShowAsync();
            try
            {
                if (result == ContentDialogResult.Primary)
                {
                    string alias = (input.Text ?? string.Empty).Trim();
                    if (alias.Length == 0) throw new InvalidOperationException("Nhập tên gợi nhớ hoặc chọn Xóa tên.");
                    await ZaloAuth.SetFriendAliasAsync(ZaloApp.Session, friend.UserId, alias, CancellationToken.None);
                    friend.Alias = alias;
                    friend.Name = alias;
                }
                else if (result == ContentDialogResult.Secondary && !string.IsNullOrWhiteSpace(friend.Alias))
                {
                    await ZaloAuth.RemoveFriendAliasAsync(ZaloApp.Session, friend.UserId, CancellationToken.None);
                    friend.Alias = string.Empty;
                    friend.Name = friend.OriginalName ?? friend.Name;
                }
                else return;

                if (_conversation != null) _conversation.Name = friend.Name;
                if (_nameText != null) _nameText.Text = friend.Name;
                UpdateAliasText();
                await ZaloApp.SaveCachedConversationsAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không đổi được tên gợi nhớ: " + ex.Message, "Tên gợi nhớ").ShowAsync();
            }
        }

        private async void ShowWallpaperDialog()
        {
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Height = 100 };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            string selectedUri = null;

            for (int i = 1; i <= 20; i++)
            {
                string uri = $"ms-appx:///Assets/Backgrounds/bg_{i:D2}.png";
                var border = new Border
                {
                    Width = 60,
                    Height = 88,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xE0, 0xE5))
                };
                var img = new Image { Source = new BitmapImage(new Uri(uri)), Stretch = Stretch.UniformToFill };
                border.Child = img;
                border.Tapped += (s, e) =>
                {
                    selectedUri = uri;
                    foreach (Border b in row.Children)
                    {
                        b.BorderThickness = new Thickness(b == border ? 3 : 1);
                        b.BorderBrush = b == border ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0xE0, 0xE5));
                    }
                };
                row.Children.Add(border);
            }
            scroll.Content = row;

            var cd = new ContentDialog
            {
                Title = "Đổi hình nền cuộc trò chuyện",
                Content = scroll,
                PrimaryButtonText = "Áp dụng",
                SecondaryButtonText = "Dùng mặc định"
            };
            App.StyleContentDialog(cd);
            var res = await cd.ShowAsync();
            string uid = ZaloApp.Session?.Uid ?? string.Empty;
            if (res == ContentDialogResult.Primary && selectedUri != null && _conversation != null)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(uid, _conversation.Key, selectedUri);
                await new MessageDialog("Đã cập nhật hình nền cuộc trò chuyện!", "Thành công").ShowAsync();
            }
            else if (res == ContentDialogResult.Secondary && _conversation != null)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(uid, _conversation.Key, string.Empty);
                await new MessageDialog("Đã đặt lại hình nền mặc định.", "Thành công").ShowAsync();
            }
        }

        private async void ShowMediaDialog()
        {
            if (_mediaItems.Count == 0)
            {
                await new MessageDialog("Chưa có ảnh, tệp hay liên kết nào trong cuộc trò chuyện này.", "Ảnh, file, link").ShowAsync();
                return;
            }

            var scroll = new ScrollViewer { MaxHeight = 380 };
            var stack = new StackPanel();

            foreach (var item in _mediaItems)
            {
                var row = new Border
                {
                    Padding = new Thickness(8, 10, 8, 10),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Background = new SolidColorBrush(Colors.Transparent)
                };
                var rowStack = new StackPanel();
                rowStack.Children.Add(new TextBlock { Text = item.Title, FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis });
                rowStack.Children.Add(new TextBlock { Text = item.Subtitle, FontSize = 11, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) });
                row.Child = rowStack;

                row.PointerPressed += (s, e) => row.Opacity = 0.6;
                row.PointerReleased += (s, e) => row.Opacity = 1.0;
                row.PointerCanceled += (s, e) => row.Opacity = 1.0;
                row.Tapped += async (s, e) =>
                {
                    if (item.IsImage)
                    {
                        NavigationManager.NavigateTo(new ImageViewerPage(item.Url));
                    }
                    else if (Uri.TryCreate(item.Url, UriKind.Absolute, out Uri uri))
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                };
                stack.Children.Add(row);
            }
            scroll.Content = stack;

            var cd = new ContentDialog
            {
                Title = $"Ảnh, file, link ({_mediaItems.Count})",
                Content = scroll,
                PrimaryButtonText = "Đóng"
            };
            App.StyleContentDialog(cd);
            await cd.ShowAsync();
        }

        private async void ShowCommonGroupsDialog()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            _commonGroupsStatusText.Text = "Đang tìm nhóm chung...";

            try
            {
                List<string> ids = await ZaloMessaging.GetGroupIdsAsync(ZaloApp.Session, CancellationToken.None);
                Dictionary<string, JsonObject> details = await ZaloMessaging.GetGroupInfoMapAsync(ZaloApp.Session, ids, CancellationToken.None);
                var matches = new List<CommonGroupItem>();

                foreach (KeyValuePair<string, JsonObject> pair in details)
                {
                    JsonObject info = pair.Value;
                    bool isMember = ZaloApp.GroupContainsMember(info, _conversation.ThreadId);
                    if (!isMember) continue;
                    string name = ZaloAuth.Str(info, "name");
                    if (name.Length == 0) name = "Nhóm " + pair.Key;
                    matches.Add(new CommonGroupItem { ThreadId = pair.Key, Name = name });
                }

                _commonGroupsStatusText.Text = matches.Count == 0 ? "Không có nhóm chung nào." : $"{matches.Count} nhóm chung";

                _commonGroupsStatusText.Text = matches.Count == 0 ? "Không có nhóm chung nào." : $"{matches.Count} nhóm chung";
                NavigationManager.NavigateTo(new MutualGroupsPage(_conversation?.Name ?? "Bạn bè", matches));
            }
            catch (Exception ex)
            {
                _commonGroupsStatusText.Text = "Không tải được nhóm chung. Chạm để thử lại.";
                ZaloLog.Add("Common groups lookup failed: " + ex.Message);
            }
        }

        private static bool HasMember(JsonObject info, string userId)
        {
            IJsonValue value;
            if (info.TryGetValue("memberIds", out value) && value.ValueType == JsonValueType.Array)
                foreach (IJsonValue member in value.GetArray())
                    if (member.ValueType == JsonValueType.String && member.GetString() == userId) return true;
            if (info.TryGetValue("currentMems", out value) && value.ValueType == JsonValueType.Array)
                foreach (IJsonValue member in value.GetArray())
                    if (member.ValueType == JsonValueType.Object && ZaloAuth.Str(member.GetObject(), "id") == userId) return true;
            return false;
        }

        private async Task LoadHiddenStateAsync()
        {
            _loadingHidden = true;
            if (_hiddenToggle != null) _hiddenToggle.IsEnabled = false;
            if (_hiddenStatusText != null) _hiddenStatusText.Text = "Đang tải trạng thái ẩn…";

            try
            {
                var keys = await ZaloAuth.GetHiddenConversationKeysAsync(ZaloApp.Session, CancellationToken.None);
                _updatingHidden = true;
                if (_hiddenToggle != null)
                {
                    _hiddenToggle.IsOn = keys.Contains(_conversation?.Key);
                    _hiddenToggle.IsEnabled = true;
                }
                if (_hiddenStatusText != null)
                    _hiddenStatusText.Text = "Ẩn cuộc trò chuyện khỏi danh sách chính; tìm lại qua ô tìm kiếm.";
            }
            catch (Exception ex)
            {
                if (_hiddenStatusText != null) _hiddenStatusText.Text = "Chưa đồng bộ trạng thái ẩn.";
                ZaloLog.Add("Hidden conversation load failed: " + ex.Message);
            }
            finally
            {
                _loadingHidden = false;
                _updatingHidden = false;
            }
        }

        private async void HiddenToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loadingHidden || _updatingHidden || _conversation == null) return;
            bool requested = _hiddenToggle.IsOn;
            _hiddenToggle.IsEnabled = false;
            _hiddenStatusText.Text = requested ? "Đang ẩn cuộc trò chuyện…" : "Đang hiện lại cuộc trò chuyện…";

            try
            {
                await ZaloApp.SetHiddenAsync(_conversation, requested);
                _hiddenStatusText.Text = requested ? "Đã ẩn khỏi danh sách. Hãy dùng tìm kiếm để mở lại." : "Đã hiện lại trong danh sách tin nhắn.";
            }
            catch (Exception ex)
            {
                _updatingHidden = true;
                _hiddenToggle.IsOn = !requested;
                _updatingHidden = false;
                _hiddenStatusText.Text = "Lỗi đổi trạng thái ẩn: " + ex.Message;
            }
            finally
            {
                _hiddenToggle.IsEnabled = true;
            }
        }
    }

    public sealed class ZaloModernSwitch : Grid
    {
        private readonly Border _track;
        private readonly Ellipse _thumb;
        private bool _isOn = false;
        private bool _isEnabled = true;

        public event RoutedEventHandler Toggled;

        public ZaloModernSwitch()
        {
            Width = 50;
            Height = 28;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Right;

            _thumb = new Ellipse
            {
                Width = 22,
                Height = 22,
                Fill = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(3, 0, 0, 0)
            };

            _track = new Border
            {
                Width = 50,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1)),
                Child = _thumb
            };
            Children.Add(_track);

            Tapped += (s, e) =>
            {
                if (!_isEnabled) return;
                IsOn = !IsOn;
                Toggled?.Invoke(this, new RoutedEventArgs());
            };
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                Opacity = value ? 1.0 : 0.45;
                IsHitTestVisible = value;
            }
        }

        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                _track.Background = _isOn
                    ? new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0x86, 0xF9))
                    : new SolidColorBrush(Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));
                _thumb.HorizontalAlignment = _isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                _thumb.Margin = _isOn ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
            }
        }
    }

    public sealed class ConversationMediaItem
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Url { get; set; }
        public bool IsImage { get; set; }
    }

    public sealed class CommonGroupItem
    {
        public string ThreadId { get; set; }
        public string Name { get; set; }
    }
}
