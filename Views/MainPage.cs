using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class MainPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _inactiveGray = new SolidColorBrush(Color.FromArgb(0xFF, 0x75, 0x75, 0x75));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));
        private readonly SolidColorBrush _textMuted = new SolidColorBrush(Color.FromArgb(0xFF, 0x85, 0x8A, 0x90));

        private int _selectedTab = 0;
        private bool _isOtherSubTab = false;

        // Container Grids
        private Grid _tab0Content; // Tin nhắn
        private Grid _tab1Content; // Danh bạ
        private Grid _tab2Content; // Khám phá
        private Grid _tab3Content; // Cá nhân

        // Tab Bar Elements (4 visible tabs exactly matching WP10)
        private Image[] _tabIcons;
        private TextBlock[] _tabLabels;
        private Border _tab0Badge;
        private TextBlock _tab0BadgeText;

        // Chats Elements
        private TextBlock _subTabPriorityText;
        private Border _subTabPriorityIndicator;
        private TextBlock _subTabOtherText;
        private Border _subTabOtherIndicator;
        private ListView _conversationList;
        private TextBox _searchBox;

        // Contacts Elements
        private ListView _friendList;

        // Profile Elements
        private Ellipse _profileAvatar;
        private TextBlock _profileInitial;
        private TextBlock _profileName;
        private TextBlock _profilePhone;

        public MainPage()
        {
            BuildUI();
            this.Loaded += (s, e) => Initialize();
        }

        private void Initialize()
        {
            if (ZaloApp.Session == null)
            {
                NavigationManager.NavigateTo(new LoginPage(), clearStack: true);
                return;
            }

            ZaloApp.SetDispatcher(Dispatcher);
            EnsureSampleConversations();
            RefreshConversations();
            RefreshFriends();
            RefreshProfile();
            UpdateUnreadBadge();

            ZaloApp.Conversations.CollectionChanged -= Conversations_CollectionChanged;
            ZaloApp.Conversations.CollectionChanged += Conversations_CollectionChanged;
        }

        private void EnsureSampleConversations()
        {
            if (ZaloApp.Conversations.Count > 0) return;

            long nowMs = DateTime.UtcNow.Ticks / 10000;
            var c1 = new ZaloConversation
            {
                Key = "group_aio2026",
                Name = "AIO2026",
                IsGroup = true,
                GroupMemberCount = 120,
                LastText = "Thành viên đã tham gia nhóm",
                LastTs = nowMs,
                AvatarUrl = "ms-appx:///Assets/Avatars/avatar_aio2026.png"
            };
            ZaloApp.Conversations.Add(c1);

            var c2 = new ZaloConversation
            {
                Key = "group_shopee",
                Name = "SĂN SALE SHOPEE NHÀ C...",
                IsGroup = true,
                GroupMemberCount = 450,
                LastText = "Ưu đãi siêu hội mua sắm",
                LastTs = nowMs - 7200000
            };
            ZaloApp.Conversations.Add(c2);

            var c3 = new ZaloConversation
            {
                Key = "group_zaloai",
                Name = "Zalo AI - AI VIET NAM",
                IsGroup = true,
                GroupMemberCount = 85,
                LastText = "Zalo AI trợ lý ảo",
                LastTs = nowMs - 86400000,
                AvatarUrl = "ms-appx:///Assets/Avatars/avatar_zalo_ai.png"
            };
            ZaloApp.Conversations.Add(c3);

            var c4 = new ZaloConversation
            {
                Key = "user_sondoan",
                Name = "Sơn Đoàn",
                IsGroup = false,
                LastText = "Alo cậu ơi",
                LastTs = nowMs - 86400000
            };
            ZaloApp.Conversations.Add(c4);

            var c5 = new ZaloConversation
            {
                Key = "user_duykhanh",
                Name = "Duy Khánh",
                IsGroup = false,
                LastText = "Hôm nay có đi không?",
                LastTs = nowMs - 172800000
            };
            ZaloApp.Conversations.Add(c5);

            var c6 = new ZaloConversation
            {
                Key = "user_bachoang",
                Name = "Bác Hoàng",
                IsGroup = false,
                LastText = "Bác vừa gửi file nhé",
                LastTs = nowMs - 259200000
            };
            ZaloApp.Conversations.Add(c6);

            var c7 = new ZaloConversation
            {
                Key = "user_nguyenvanduc",
                Name = "Nguyễn Văn Đức",
                IsGroup = false,
                LastText = "[Ảnh động]",
                LastTs = nowMs
            };
            c7.Messages.Add(new ZaloChatMessage { Text = ";-)", FromMe = true, Ts = nowMs - 180000 });
            c7.Messages.Add(new ZaloChatMessage { Text = ";-)", FromMe = true, Ts = nowMs - 120000 });
            c7.Messages.Add(new ZaloChatMessage { Text = "https://ot145.dlfl.vn/b3a20e6445b8c5e6", FromMe = true, Ts = nowMs, MsgType = "chat.sticker", MediaUrl = "ms-appx:///Assets/Emoticons/Emoji/emo_05.png" });
            ZaloApp.Conversations.Add(c7);

            var c8 = new ZaloConversation
            {
                Key = "user_pussy",
                Name = "pUssY",
                IsGroup = false,
                LastText = "Test app",
                LastTs = nowMs - 36000000
            };
            ZaloApp.Conversations.Add(c8);

            // Sample Friends
            if (ZaloApp.Friends.Count == 0)
            {
                ZaloApp.Friends.Add(new ZaloFriend { UserId = "user_sondoan", Name = "Sơn Đoàn" });
                ZaloApp.Friends.Add(new ZaloFriend { UserId = "user_duykhanh", Name = "Duy Khánh" });
                ZaloApp.Friends.Add(new ZaloFriend { UserId = "user_bachoang", Name = "Bác Hoàng" });
                ZaloApp.Friends.Add(new ZaloFriend { UserId = "user_nguyenvanduc", Name = "Nguyễn Văn Đức" });
                ZaloApp.Friends.Add(new ZaloFriend { UserId = "user_pussy", Name = "pUssY" });
            }
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.White)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient #2878F0 -> #08A9E8)
            var headerGrid = CreateTopHeader();
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. MAIN CONTENT (4 TABS)
            var contentContainer = new Grid();
            _tab0Content = CreateChatsTab();
            _tab1Content = CreateContactsTab();
            _tab2Content = CreateDiscoveryTab();
            _tab3Content = CreateProfileTab();

            contentContainer.Children.Add(_tab0Content);
            contentContainer.Children.Add(_tab1Content);
            contentContainer.Children.Add(_tab2Content);
            contentContainer.Children.Add(_tab3Content);

            Grid.SetRow(contentContainer, 1);
            rootGrid.Children.Add(contentContainer);

            // 3. BOTTOM NAVIGATION BAR (4 Tabs: Tin nhắn, Danh bạ, Khám phá, Cá nhân)
            var bottomBar = CreateBottomBar();
            Grid.SetRow(bottomBar, 2);
            rootGrid.Children.Add(bottomBar);

            this.Content = rootGrid;
            SwitchTab(0);
        }

        private FrameworkElement CreateTopHeader()
        {
            var headerBorder = new Border
            {
                Padding = new Thickness(10, 0, 10, 0)
            };
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerBorder.Background = gradient;

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Search Bar (Translucent Pill)
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lensIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_search_white.png")),
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lensIcon, 0);
            searchGrid.Children.Add(lensIcon);

            _searchBox = new TextBox
            {
                PlaceholderText = "Tìm kiếm",
                Height = 36,
                FontSize = 15,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (s, e) => OnSearchChanged(_searchBox.Text);
            Grid.SetColumn(_searchBox, 1);
            searchGrid.Children.Add(_searchBox);

            searchBorder.Child = searchGrid;
            Grid.SetColumn(searchBorder, 0);
            header.Children.Add(searchBorder);

            // Plus Button
            var plusBtn = new Button
            {
                Width = 36,
                Height = 36,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var plusIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_plus_white.png")),
                Width = 20,
                Height = 20
            };
            plusBtn.Content = plusIcon;

            var flyout = new MenuFlyout();
            var itemGroup = new MenuFlyoutItem { Text = "Tạo nhóm mới" };
            var itemFriend = new MenuFlyoutItem { Text = "Thêm bạn mới" };
            var itemRefresh = new MenuFlyoutItem { Text = "Làm mới danh sách" };
            itemRefresh.Click += (s, e) => RefreshConversations();
            flyout.Items.Add(itemGroup);
            flyout.Items.Add(itemFriend);
            flyout.Items.Add(itemRefresh);

            plusBtn.Flyout = flyout;
            Grid.SetColumn(plusBtn, 1);
            header.Children.Add(plusBtn);

            headerBorder.Child = header;
            return headerBorder;
        }

        private Grid CreateChatsTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Subtabs: "Ưu tiên" & "Khác"
            var subtabsBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var subtabsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 14, 0) };

            // Subtab 1: Ưu tiên
            var priorityStack = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            _subTabPriorityText = new TextBlock
            {
                Text = "Ưu tiên",
                FontSize = 13.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = ColorBrush(0xFF, 0x11, 0x18, 0x27),
                Margin = new Thickness(0, 7, 0, 5)
            };
            _subTabPriorityIndicator = new Border
            {
                Height = 2.5,
                Width = 50,
                Background = ColorBrush(0xFF, 0x11, 0x18, 0x27)
            };
            priorityStack.Children.Add(_subTabPriorityText);
            priorityStack.Children.Add(_subTabPriorityIndicator);
            priorityStack.Tapped += (s, e) => SwitchSubTab(false);
            subtabsStack.Children.Add(priorityStack);

            // Subtab 2: Khác
            var otherStack = new StackPanel();
            _subTabOtherText = new TextBlock
            {
                Text = "Khác",
                FontSize = 13.5,
                FontWeight = Windows.UI.Text.FontWeights.Normal,
                Foreground = _inactiveGray,
                Margin = new Thickness(0, 7, 0, 5)
            };
            _subTabOtherIndicator = new Border
            {
                Height = 2.5,
                Width = 38,
                Background = new SolidColorBrush(Colors.Transparent)
            };
            otherStack.Children.Add(_subTabOtherText);
            otherStack.Children.Add(_subTabOtherIndicator);
            otherStack.Tapped += (s, e) => SwitchSubTab(true);
            subtabsStack.Children.Add(otherStack);

            subtabsBorder.Child = subtabsStack;
            Grid.SetRow(subtabsBorder, 0);
            grid.Children.Add(subtabsBorder);

            // Conversation ListView
            _conversationList = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                Background = new SolidColorBrush(Colors.White)
            };
            Grid.SetRow(_conversationList, 1);
            grid.Children.Add(_conversationList);

            return grid;
        }

        private Grid CreateContactsTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topActions = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            topActions.Children.Add(CreateActionRow("👤", "Lời mời kết bạn", Color.FromArgb(0xFF, 0x16, 0x84, 0xD7)));
            topActions.Children.Add(CreateActionRow("📱", "Danh bạ máy", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)));
            topActions.Children.Add(CreateActionRow("🎂", "Lịch sinh nhật", Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)));

            var sectionHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)),
                Padding = new Thickness(16, 8, 16, 8)
            };
            sectionHeader.Child = new TextBlock
            {
                Text = "TẤT CẢ BẠN BÈ",
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub
            };
            topActions.Children.Add(sectionHeader);

            Grid.SetRow(topActions, 0);
            grid.Children.Add(topActions);

            _friendList = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                Background = new SolidColorBrush(Colors.White)
            };
            Grid.SetRow(_friendList, 1);
            grid.Children.Add(_friendList);

            return grid;
        }

        private FrameworkElement CreateActionRow(string emoji, string label, Color color)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 14, 0)
            };
            var iconText = new TextBlock
            {
                Text = emoji,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = iconText;
            stack.Children.Add(iconBox);

            var text = new TextBlock
            {
                Text = label,
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(text);

            border.Child = stack;
            return border;
        }

        private Grid CreateDiscoveryTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 16) };
            var stack = new StackPanel();

            var banner = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var bannerStack = new StackPanel();
            bannerStack.Children.Add(new TextBlock
            {
                Text = "Khám phá tiện ích Zalo",
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark
            });
            bannerStack.Children.Add(new TextBlock
            {
                Text = "Trải nghiệm các dịch vụ số tiện lợi ngay trên Windows Phone",
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(0, 4, 0, 0)
            });
            banner.Child = bannerStack;
            stack.Children.Add(banner);

            stack.Children.Add(CreateServiceCard("🎬", "Zalo Video", "Kho video ngắn giải trí, tin tức mỗi ngày", Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)));
            stack.Children.Add(CreateServiceCard("🛍️", "Zalo Shop", "Ưu đãi mua sắm online tiện lợi", Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)));
            stack.Children.Add(CreateServiceCard("🏛️", "Dịch vụ công", "Tra cứu thủ tục hành chính, nộp hồ sơ trực tuyến", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)));
            stack.Children.Add(CreateServiceCard("💳", "Ví điện tử & Tài chính", "Thanh toán hóa đơn, nạp tiền điện thoại", Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)));

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private FrameworkElement CreateServiceCard(string emoji, string title, string subtitle, Color iconBg)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(iconBg)
            };
            var iconText = new TextBlock
            {
                Text = emoji,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            var textStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            textStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            border.Child = grid;
            return border;
        }

        private Grid CreateProfileTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Profile Card
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 18, 16, 18),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarGrid = new Grid { Width = 60, Height = 60 };
            _profileAvatar = new Ellipse { Width = 60, Height = 60, Fill = _activeBlue };
            _profileInitial = new TextBlock
            {
                Text = "Z",
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_profileAvatar);
            avatarGrid.Children.Add(_profileInitial);
            Grid.SetColumn(avatarGrid, 0);
            cardGrid.Children.Add(avatarGrid);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            _profileName = new TextBlock { Text = "Người dùng Zalo", FontSize = 18, FontWeight = Windows.UI.Text.FontWeights.Bold, Foreground = _textDark };
            _profilePhone = new TextBlock { Text = "09xx xxx xxx", FontSize = 13.5, Foreground = _textSub, Margin = new Thickness(0, 3, 0, 4) };
            var viewProfileLink = new TextBlock { Text = "Xem trang cá nhân", FontSize = 13, Foreground = _activeBlue, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
            infoStack.Children.Add(_profileName);
            infoStack.Children.Add(_profilePhone);
            infoStack.Children.Add(viewProfileLink);
            Grid.SetColumn(infoStack, 1);
            cardGrid.Children.Add(infoStack);

            card.Child = cardGrid;
            stack.Children.Add(card);

            // Settings Group
            var settingsGroup = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var settingsStack = new StackPanel();
            settingsStack.Children.Add(CreateSettingItem("🪪", "Ví QR", "Quản lý thẻ và mã QR cá nhân"));
            settingsStack.Children.Add(CreateSettingItem("☁️", "Zalo Cloud của tôi", "Lưu trữ tin nhắn và ảnh quan trọng"));
            settingsStack.Children.Add(CreateSettingItem("💾", "Dữ liệu trên máy", "Quản lý bộ nhớ đệm và tài nguyên"));
            settingsStack.Children.Add(CreateSettingItem("⚙️", "Cài đặt & Quyền riêng tư", "Bảo mật tài khoản, thông báo"));
            settingsGroup.Child = settingsStack;
            stack.Children.Add(settingsGroup);

            // Logout Button
            var logoutBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFE, 0xE2, 0xE2)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12)
            };
            var logoutText = new TextBlock
            {
                Text = "Đăng xuất tài khoản",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logoutBorder.Child = logoutText;
            logoutBorder.Tapped += async (s, e) =>
            {
                ZaloApp.Reset();
                await ZaloStore.ClearAsync();
                NavigationManager.NavigateTo(new LoginPage(), clearStack: true);
            };
            stack.Children.Add(logoutBorder);

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private FrameworkElement CreateSettingItem(string icon, string title, string sub)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 13, 16, 13),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconText = new TextBlock { Text = icon, FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(iconText, 0);
            grid.Children.Add(iconText);

            var textStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            textStack.Children.Add(new TextBlock { Text = title, FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            textStack.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = _textSub, Margin = new Thickness(0, 1, 0, 0) });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var arrow = new TextBlock { Text = "›", FontSize = 20, Foreground = _textMuted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrow, 2);
            grid.Children.Add(arrow);

            border.Child = grid;
            return border;
        }

        // EXACT 4 TABS MATCHING WP10 SCREENSHOT (media_1794286660462_fdcecefd.jpg)
        private Border CreateBottomBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            var grid = new Grid();
            for (int i = 0; i < 4; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            _tabIcons = new Image[4];
            _tabLabels = new TextBlock[4];

            string[] titles = { "Tin nhắn", "Danh bạ", "Khám phá", "Cá nhân" };

            for (int i = 0; i < 4; i++)
            {
                int tabIdx = i;
                var btn = new Button
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Padding = new Thickness(0)
                };

                var itemGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

                var img = new Image
                {
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _tabIcons[i] = img;
                stack.Children.Add(img);

                var txt = new TextBlock
                {
                    Text = titles[i],
                    FontSize = 10.5,
                    FontWeight = i == 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                    Foreground = i == 0 ? _activeBlue : _inactiveGray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                _tabLabels[i] = txt;
                stack.Children.Add(txt);

                itemGrid.Children.Add(stack);

                if (i == 0)
                {
                    _tab0Badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x39, 0x35)),
                        CornerRadius = new CornerRadius(8),
                        MinWidth = 16,
                        Height = 16,
                        Padding = new Thickness(3, 0, 3, 0),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(14, -3, -8, 0),
                        Visibility = Visibility.Collapsed
                    };
                    _tab0BadgeText = new TextBlock
                    {
                        Text = "0",
                        FontSize = 9,
                        FontWeight = Windows.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    _tab0Badge.Child = _tab0BadgeText;
                    itemGrid.Children.Add(_tab0Badge);
                }

                btn.Content = itemGrid;
                btn.Click += (s, e) => SwitchTab(tabIdx);
                Grid.SetColumn(btn, i);
                grid.Children.Add(btn);
            }

            border.Child = grid;
            return border;
        }

        private void SwitchTab(int index)
        {
            _selectedTab = index;

            _tab0Content.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            _tab1Content.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            _tab2Content.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            _tab3Content.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Load exact Android icons when active, standard inactive when not
            _tabIcons[0].Source = new BitmapImage(new Uri(index == 0 ? "ms-appx:///Assets/Icons/Android/icn_tab_message_o.png" : "ms-appx:///Assets/Icons/tab_msg_inactive.png"));
            _tabIcons[1].Source = new BitmapImage(new Uri(index == 1 ? "ms-appx:///Assets/Icons/Android/icn_tab_contact_o.png" : "ms-appx:///Assets/Icons/tab_contact_inactive.png"));
            _tabIcons[2].Source = new BitmapImage(new Uri(index == 2 ? "ms-appx:///Assets/Icons/Android/icn_tab_more_o.png" : "ms-appx:///Assets/Icons/tab_discovery_inactive.png"));
            _tabIcons[3].Source = new BitmapImage(new Uri(index == 3 ? "ms-appx:///Assets/Icons/Android/icn_tab_me_o.png" : "ms-appx:///Assets/Icons/tab_me_inactive.png"));

            for (int i = 0; i < 4; i++)
            {
                bool active = i == index;
                _tabLabels[i].Foreground = active ? _activeBlue : _inactiveGray;
                _tabLabels[i].FontWeight = active ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            }

            if (index == 0) RefreshConversations();
            else if (index == 1) RefreshFriends();
            else if (index == 3) RefreshProfile();
        }

        private void SwitchSubTab(bool other)
        {
            _isOtherSubTab = other;
            _subTabPriorityText.FontWeight = !other ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _subTabPriorityText.Foreground = !other ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray;
            _subTabPriorityIndicator.Background = !other ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : new SolidColorBrush(Colors.Transparent);

            _subTabOtherText.FontWeight = other ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _subTabOtherText.Foreground = other ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray;
            _subTabOtherIndicator.Background = other ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : new SolidColorBrush(Colors.Transparent);

            RefreshConversations();
        }

        private void RefreshConversations()
        {
            _conversationList.Items.Clear();
            string query = _searchBox?.Text?.Trim();

            var list = ZaloApp.Conversations.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
            {
                string norm = ZaloFormat.NormalizeSearch(query);
                list = list.Where(c => ZaloFormat.SearchContains(c.Name, norm));
            }

            foreach (var conv in list)
            {
                var item = CreateConversationItem(conv);
                _conversationList.Items.Add(item);
            }

            UpdateUnreadBadge();
        }

        // EXACT 100% RECONSTRUCTION OF CONVERSATION ITEM IN WP10 (media_1794286660462_fdcecefd.jpg)
        private FrameworkElement CreateConversationItem(ZaloConversation conv)
        {
            var itemBorder = new Border
            {
                Height = 78,
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush(Colors.White)
            };
            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });

            // 1. Column 0: 50x50 Avatar with Group badge
            var avatarGrid = new Grid { Width = 50, Height = 50, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = conv.AvatarColor ?? _activeBlue
            };
            var initial = new TextBlock
            {
                Text = conv.Initial ?? "?",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);

            if (conv.AvatarImageBrush != null)
            {
                var imgEllipse = new Ellipse { Width = 50, Height = 50, Fill = conv.AvatarImageBrush };
                avatarGrid.Children.Add(imgEllipse);
            }

            if (conv.IsGroup)
            {
                var groupBadge = new Border
                {
                    Width = 19,
                    Height = 19,
                    CornerRadius = new CornerRadius(10),
                    Background = ColorBrush(0xFF, 0xEA, 0xF3, 0xFC),
                    BorderBrush = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(1.5),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                groupBadge.Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_blue.png")),
                    Width = 11,
                    Height = 11
                };
                avatarGrid.Children.Add(groupBadge);
            }

            Grid.SetColumn(avatarGrid, 0);
            itemGrid.Children.Add(avatarGrid);

            // 2. Column 1: Middle Info (Title + Last Text)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) };
            var titleLine = new StackPanel { Orientation = Orientation.Horizontal };

            if (conv.IsGroup)
            {
                var groupIconBorder = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    Background = ColorBrush(0xFF, 0xEE, 0xF4, 0xFA),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                groupIconBorder.Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_blue.png")),
                    Width = 11,
                    Height = 11
                };
                titleLine.Children.Add(groupIconBorder);
            }

            var nameBlock = new TextBlock
            {
                Text = conv.Name ?? "Trò chuyện",
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLine.Children.Add(nameBlock);
            textStack.Children.Add(titleLine);

            var lastTextBlock = new TextBlock
            {
                Text = conv.LastText ?? string.Empty,
                FontSize = 14,
                Foreground = _textSub,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 0, 0)
            };
            textStack.Children.Add(lastTextBlock);
            Grid.SetColumn(textStack, 1);
            itemGrid.Children.Add(textStack);

            // 3. Column 2: Right side (Time + Unread Badge)
            var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var timeBlock = new TextBlock
            {
                Text = conv.TimeText ?? string.Empty,
                FontSize = 12,
                Foreground = _textMuted,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rightStack.Children.Add(timeBlock);

            if (conv.Unread > 0)
            {
                var unreadBorder = new Border
                {
                    Background = ColorBrush(0xFF, 0xE8, 0x4B, 0x50),
                    CornerRadius = new CornerRadius(10),
                    MinWidth = 19,
                    Height = 19,
                    Padding = new Thickness(5, 0, 5, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 7, 0, 0)
                };
                var unreadText = new TextBlock
                {
                    Text = conv.UnreadLabel,
                    FontSize = 10.5,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                unreadBorder.Child = unreadText;
                rightStack.Children.Add(unreadBorder);
            }

            Grid.SetColumn(rightStack, 2);
            itemGrid.Children.Add(rightStack);

            // 4. Bottom Divider (Margin 64px from left so it starts after avatar)
            var divider = new Border
            {
                BorderBrush = ColorBrush(0xFF, 0xEA, 0xEC, 0xEF),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(64, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(divider, 3);
            itemGrid.Children.Add(divider);

            itemBorder.Child = itemGrid;
            itemBorder.Tapped += (s, e) =>
            {
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };

            return itemBorder;
        }

        private void RefreshFriends()
        {
            _friendList.Items.Clear();
            string query = _searchBox?.Text?.Trim();

            var friends = ZaloApp.Friends.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
            {
                string norm = ZaloFormat.NormalizeSearch(query);
                friends = friends.Where(f => ZaloFormat.SearchContains(f.Name, norm));
            }

            foreach (var friend in friends)
            {
                var item = CreateFriendItem(friend);
                _friendList.Items.Add(item);
            }
        }

        private FrameworkElement CreateFriendItem(ZaloFriend friend)
        {
            var itemBorder = new Border
            {
                Height = 64,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Colors.White)
            };
            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarGrid = new Grid { Width = 44, Height = 44, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = 44, Height = 44, Fill = friend.AvatarColor ?? _activeBlue };
            var initial = new TextBlock
            {
                Text = friend.Initial ?? "?",
                FontSize = 17,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);
            Grid.SetColumn(avatarGrid, 0);
            itemGrid.Children.Add(avatarGrid);

            var nameBlock = new TextBlock
            {
                Text = friend.Name ?? "Bạn bè",
                FontSize = 16,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 1);
            itemGrid.Children.Add(nameBlock);

            var divider = new Border
            {
                BorderBrush = ColorBrush(0xFF, 0xEA, 0xEC, 0xEF),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(60, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(divider, 2);
            itemGrid.Children.Add(divider);

            itemBorder.Child = itemGrid;
            itemBorder.Tapped += (s, e) =>
            {
                var conv = ZaloApp.GetOrCreate(friend.UserId, false);
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };

            return itemBorder;
        }

        private void RefreshProfile()
        {
            if (ZaloApp.Session != null)
            {
                _profileName.Text = ZaloApp.Session.DisplayName ?? "Người dùng Zalo";
                _profilePhone.Text = ZaloApp.Session.PhoneNumber ?? "";
                _profileInitial.Text = ZaloAvatar.InitialOf(ZaloApp.Session.DisplayName);
            }
        }

        private void UpdateUnreadBadge()
        {
            int total = ZaloApp.Conversations.Sum(c => c.Unread);
            if (total > 0)
            {
                _tab0Badge.Visibility = Visibility.Visible;
                _tab0BadgeText.Text = total > 99 ? "99+" : total.ToString();
            }
            else
            {
                _tab0Badge.Visibility = Visibility.Collapsed;
            }
        }

        private void OnSearchChanged(string text)
        {
            if (_selectedTab == 0) RefreshConversations();
            else if (_selectedTab == 1) RefreshFriends();
        }

        private async void Conversations_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_selectedTab == 0) RefreshConversations();
                UpdateUnreadBadge();
            });
        }

        private static SolidColorBrush ColorBrush(byte a, byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
    }
}
