using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Core;
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
        private Border[] _tabButtons;
        private Border _tab0Badge;
        private TextBlock _tab0BadgeText;

        // Chats Elements
        private TextBlock _subTabPriorityText;
        private Border _subTabPriorityIndicator;
        private TextBlock _subTabOtherText;
        private Border _subTabOtherIndicator;
        private StackPanel _conversationStack;
        private TextBox _searchBox;
        private Border _syncBanner;

        // Contacts Elements
        private StackPanel _friendStack;

        // Profile Elements
        private Ellipse _profileAvatarColor;
        private Ellipse _profileAvatarImage;
        private TextBlock _profileInitial;
        private TextBlock _profileName;
        private TextBlock _profileStatusPreview;

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
            CleanupMockConversations();
            RefreshConversations();
            RefreshFriends();
            RefreshProfile();
            UpdateUnreadBadge();

            ZaloApp.Conversations.CollectionChanged -= Conversations_CollectionChanged;
            ZaloApp.Conversations.CollectionChanged += Conversations_CollectionChanged;
        }

        private void CleanupMockConversations()
        {
            var mockKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "group_aio2026", "group_shopee", "group_zaloai", 
                "user_sondoan", "user_duykhanh", "user_bachoang", 
                "user_nguyenvanduc", "user_pussy" 
            };
            var mocks = ZaloApp.Conversations.Where(c => mockKeys.Contains(c.Key) || c.Name == "AIO2026" || c.Name == "pUssY" || (c.Name != null && c.Name.Contains("SĂN SALE"))).ToList();
            bool changed = false;
            foreach (var m in mocks)
            {
                ZaloApp.Conversations.Remove(m);
                changed = true;
            }
            var mockFriends = ZaloApp.Friends.Where(f => mockKeys.Contains(f.UserId) || f.Name == "pUssY" || f.Name == "Sơn Đoàn").ToList();
            foreach (var f in mockFriends)
            {
                ZaloApp.Friends.Remove(f);
                changed = true;
            }
            if (changed)
            {
                _ = ZaloApp.SaveCachedConversationsAsync();
                _ = ZaloApp.SaveCachedFriendsAsync();
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
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });

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

            // Plus Button (Touch Border with Zero System Box)
            var plusBtn = CreateTouchBorder(38, 38);
            var plusIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_plus_white.png")),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plusBtn.Child = plusIcon;

            plusBtn.Tapped += (s, e) => ShowHeaderMenu();
            Grid.SetColumn(plusBtn, 1);
            header.Children.Add(plusBtn);

            headerBorder.Child = header;
            return headerBorder;
        }

        private async void ShowHeaderMenu()
        {
            var md = new MessageDialog("Chọn thao tác:", "Zalo");
            md.Commands.Add(new UICommand("Tạo nhóm mới", cmd => NavigationManager.NavigateTo(new CreateGroupPage())));
            md.Commands.Add(new UICommand("Thêm bạn mới", cmd => NavigationManager.NavigateTo(new AddFriendPage())));
            md.Commands.Add(new UICommand("Cài đặt", cmd => NavigationManager.NavigateTo(new SettingsPage())));
            md.Commands.Add(new UICommand("Đóng"));
            await md.ShowAsync();
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
            var priorityStack = new StackPanel { Margin = new Thickness(0, 0, 24, 0), Background = new SolidColorBrush(Colors.Transparent) };
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
            var otherStack = new StackPanel { Background = new SolidColorBrush(Colors.Transparent) };
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

            // Conversation Scroll Area
            var scroll = new ScrollViewer { Background = new SolidColorBrush(Colors.White) };
            var listStack = new StackPanel();

            // Phone Sync Banner (Top of Chats)
            _syncBanner = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF4, 0xFD)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xCC, 0xE4, 0xF7)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10)
            };
            var syncGrid = new Grid();
            syncGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            syncGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            syncGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var syncIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_cloud_blue.png")),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(syncIcon, 0);
            syncGrid.Children.Add(syncIcon);

            var syncTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) };
            syncTextStack.Children.Add(new TextBlock { Text = "Đồng bộ tin nhắn từ điện thoại", FontSize = 13, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue });
            syncTextStack.Children.Add(new TextBlock { Text = "Nhấn để làm mới và nạp danh sách tin nhắn", FontSize = 11.5, Foreground = _textSub });
            Grid.SetColumn(syncTextStack, 1);
            syncGrid.Children.Add(syncTextStack);

            var syncBtn = new Border
            {
                Background = _activeBlue,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            syncBtn.Child = new TextBlock { Text = "Đồng bộ", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
            syncBtn.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Đang đồng bộ dữ liệu cuộc trò chuyện từ máy chủ...", "Đồng bộ tin nhắn");
                await md.ShowAsync();
                RefreshConversations();
            };
            Grid.SetColumn(syncBtn, 2);
            syncGrid.Children.Add(syncBtn);

            _syncBanner.Child = syncGrid;
            listStack.Children.Add(_syncBanner);

            _conversationStack = new StackPanel();
            listStack.Children.Add(_conversationStack);

            scroll.Content = listStack;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            return grid;
        }

        private Grid CreateContactsTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            var scroll = new ScrollViewer();
            var mainStack = new StackPanel();

            var topActions = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            
            var addFriendRow = CreateActionRow("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png", "Lời mời kết bạn", Color.FromArgb(0xFF, 0x16, 0x84, 0xD7));
            addFriendRow.Tapped += (s, e) => NavigationManager.NavigateTo(new AddFriendPage());
            topActions.Children.Add(addFriendRow);

            var deviceContactsRow = CreateActionRow("ms-appx:///Assets/Icons/Android/icn_contact_callvoice.png", "Danh bạ máy", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            deviceContactsRow.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Đã kết nối danh bạ điện thoại.", "Danh bạ máy");
                await md.ShowAsync();
            };
            topActions.Children.Add(deviceContactsRow);

            var createGroupRow = CreateActionRow("ms-appx:///Assets/Icons/ic_group_action_blue.png", "Tạo nhóm chat mới", Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
            createGroupRow.Tapped += (s, e) => NavigationManager.NavigateTo(new CreateGroupPage());
            topActions.Children.Add(createGroupRow);

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

            mainStack.Children.Add(topActions);

            _friendStack = new StackPanel();
            mainStack.Children.Add(_friendStack);

            scroll.Content = mainStack;
            grid.Children.Add(scroll);

            return grid;
        }

        private Border CreateActionRow(string iconUri, string label, Color color)
        {
            var border = new Border
            {
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Colors.White)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBox = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = new SolidColorBrush(color),
                Margin = new Thickness(0, 0, 14, 0)
            };
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = img;
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
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
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

            stack.Children.Add(CreateServiceCard("ms-appx:///Assets/Icons/ic_video_blue.png", "Zalo Video", "Kho video ngắn giải trí, tin tức mỗi ngày", Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)));
            stack.Children.Add(CreateServiceCard("ms-appx:///Assets/Icons/ic_group_action_blue.png", "Zalo Shop & Nhóm", "Mua sắm ưu đãi và cộng đồng", Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)));
            stack.Children.Add(CreateServiceCard("ms-appx:///Assets/Icons/ic_security_blue.png", "Dịch vụ công & Bảo mật", "Tra cứu thủ tục hành chính trực tuyến", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)));
            stack.Children.Add(CreateServiceCard("ms-appx:///Assets/Icons/ic_qr_blue.png", "Ví QR & Tài chính", "Thanh toán hóa đơn, nạp tiền điện thoại", Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)));

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private FrameworkElement CreateServiceCard(string iconUri, string title, string subtitle, Color iconBg)
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
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = img;
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            var textStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            textStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            border.Child = grid;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private Grid CreateProfileTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };
            var scroll = new ScrollViewer { Padding = new Thickness(0) };
            var stack = new StackPanel { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };

            // 1. TOP WHITE CARD (Status preview + Huge 116x116 Avatar + Name with chevron)
            var topCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(16, 16, 16, 22)
            };
            var topCardStack = new StackPanel();
            topCard.Child = topCardStack;

            // Status Pill Button
            var statusPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xF9, 0xFB)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(14, 7, 14, 7),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var statusIcon = new TextBlock
            {
                Text = "\uD83D\uDCAD",
                FontSize = 13,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileStatusPreview = new TextBlock
            {
                Text = "Cập nhật trạng thái...",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x61, 0x6A, 0x73)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 230,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusStack.Children.Add(statusIcon);
            statusStack.Children.Add(_profileStatusPreview);
            statusPill.Child = statusStack;

            statusPill.PointerPressed += (s, e) => statusPill.Opacity = 0.6;
            statusPill.PointerReleased += (s, e) => statusPill.Opacity = 1.0;
            statusPill.PointerCanceled += (s, e) => statusPill.Opacity = 1.0;
            statusPill.Tapped += (s, e) => NavigationManager.NavigateTo(new ProfilePage());
            topCardStack.Children.Add(statusPill);

            // Avatar & Name Card
            var avatarHeader = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var avatarGrid = new Grid { Width = 116, Height = 116, HorizontalAlignment = HorizontalAlignment.Center };
            _profileAvatarColor = new Ellipse { Width = 116, Height = 116, Fill = _activeBlue };
            _profileInitial = new TextBlock
            {
                Text = "Z",
                FontSize = 38,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileAvatarImage = new Ellipse
            {
                Width = 116,
                Height = 116,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 3
            };
            avatarGrid.Children.Add(_profileAvatarColor);
            avatarGrid.Children.Add(_profileInitial);
            avatarGrid.Children.Add(_profileAvatarImage);
            avatarHeader.Children.Add(avatarGrid);

            var nameRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0)
            };
            _profileName = new TextBlock
            {
                Text = "Người dùng Zalo",
                FontSize = 21,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24)),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 280,
                VerticalAlignment = VerticalAlignment.Center
            };
            var nameChevron = new TextBlock
            {
                Text = "\u203A",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0x85, 0x8C)),
                Margin = new Thickness(8, -1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            nameRow.Children.Add(_profileName);
            nameRow.Children.Add(nameChevron);
            avatarHeader.Children.Add(nameRow);

            var avatarCard = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Child = avatarHeader
            };
            avatarCard.PointerPressed += (s, e) => avatarCard.Opacity = 0.6;
            avatarCard.PointerReleased += (s, e) => avatarCard.Opacity = 1.0;
            avatarCard.PointerCanceled += (s, e) => avatarCard.Opacity = 1.0;
            avatarCard.Tapped += (s, e) => NavigationManager.NavigateTo(new ProfilePage());
            topCardStack.Children.Add(avatarCard);

            stack.Children.Add(topCard);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 2. SECTION 1: zStyle (MinHeight 78)
            var sec1 = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            var rowStyle = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_photo_gray.png", "zStyle", "Hình nền trò chuyện và âm báo", minHeight: 78);
            rowStyle.Tapped += (s, e) => NavigationManager.NavigateTo(new SettingsPage());
            sec1.Children.Add(rowStyle);
            stack.Children.Add(sec1);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 3. SECTION 2: Dữ liệu trên máy (MinHeight 76) & Tài khoản và bảo mật (MinHeight 68)
            var sec2 = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            var rowStorage = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_storage_blue.png", "Dữ liệu trên máy", "Quản lý cài đặt và bộ nhớ đệm", minHeight: 76);
            rowStorage.Tapped += (s, e) => NavigationManager.NavigateTo(new SettingsPage());
            sec2.Children.Add(rowStorage);

            sec2.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF1, 0xF2)), Margin = new Thickness(62, 0, 0, 0) });

            var rowSecurity = CreateSettingRowDirectIcon("ms-appx:///Assets/Icons/ic_security_blue.png", "Tài khoản và bảo mật", minHeight: 68);
            rowSecurity.Tapped += (s, e) => NavigationManager.NavigateTo(new SettingsPage());
            sec2.Children.Add(rowSecurity);
            stack.Children.Add(sec2);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 4. LOGOUT SECTION (Height 44, red border #F0C8C8, padding 18,18,18,20)
            var logoutContainer = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(18, 18, 18, 20)
            };
            var logoutBorder = new Border
            {
                Height = 44,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xC8, 0xC8)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            var logoutStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var logoutIcon = new TextBlock
            {
                Text = "\uE106",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x4B, 0x4B)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var logoutText = new TextBlock
            {
                Text = "Đăng xuất",
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x4B, 0x4B)),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoutStack.Children.Add(logoutIcon);
            logoutStack.Children.Add(logoutText);
            logoutBorder.Child = logoutStack;

            logoutBorder.PointerPressed += (s, e) => logoutBorder.Opacity = 0.6;
            logoutBorder.PointerReleased += (s, e) => logoutBorder.Opacity = 1.0;
            logoutBorder.PointerCanceled += (s, e) => logoutBorder.Opacity = 1.0;
            logoutBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Bạn có chắc muốn đăng xuất?", "Đăng xuất");
                md.Commands.Add(new UICommand("Đăng xuất", async cmd =>
                {
                    ZaloApp.Reset();
                    await ZaloStore.ClearAsync();
                    NavigationManager.NavigateTo(new LoginPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            logoutContainer.Child = logoutBorder;
            stack.Children.Add(logoutContainer);

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private Border CreateSettingRowWithBadge(string iconUri, string title, string sub, double minHeight)
        {
            var border = new Border
            {
                MinHeight = minHeight,
                Padding = new Thickness(20, 12, 20, 12),
                Background = new SolidColorBrush(Colors.White)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF2, 0xF7, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = img;
            Grid.SetColumn(badge, 0);
            grid.Children.Add(badge);

            var textStack = new StackPanel { Margin = new Thickness(10, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24))
            });
            textStack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x85, 0x8B, 0x91)),
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var chevron = new TextBlock
            {
                Text = "\u203A",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);

            border.Child = grid;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private Border CreateSettingRowDirectIcon(string iconUri, string title, double minHeight)
        {
            var border = new Border
            {
                MinHeight = minHeight,
                Padding = new Thickness(20, 10, 20, 10),
                Background = new SolidColorBrush(Colors.White)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(img, 0);
            grid.Children.Add(img);

            var textBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 8, 0)
            };
            Grid.SetColumn(textBlock, 1);
            grid.Children.Add(textBlock);

            var chevron = new TextBlock
            {
                Text = "\u203A",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);

            border.Child = grid;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        // EXACT 4 TABS MATCHING WP10 - USING PURE BORDER WITH ZERO SYSTEM BUTTON HIGHLIGHT
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
            _tabButtons = new Border[4];

            string[] titles = { "Tin nhắn", "Danh bạ", "Khám phá", "Cá nhân" };

            for (int i = 0; i < 4; i++)
            {
                int tabIdx = i;
                var tabBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.Transparent),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
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

                tabBorder.Child = itemGrid;
                tabBorder.PointerPressed += (s, e) => tabBorder.Opacity = 0.6;
                tabBorder.PointerReleased += (s, e) => tabBorder.Opacity = 1.0;
                tabBorder.PointerCanceled += (s, e) => tabBorder.Opacity = 1.0;
                tabBorder.Tapped += (s, e) => SwitchTab(tabIdx);

                _tabButtons[i] = tabBorder;
                Grid.SetColumn(tabBorder, i);
                grid.Children.Add(tabBorder);
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
            _conversationStack.Children.Clear();
            string query = _searchBox?.Text?.Trim();

            var otherKeys = new HashSet<string>(
                ZaloSettingsService.Current.GetOtherConversationKeys(ZaloApp.Session?.Uid),
                StringComparer.Ordinal);

            var list = ZaloApp.Conversations.Where(c =>
            {
                if (c == null) return false;
                bool isOther = ZaloApp.IsArchived(c) || otherKeys.Contains(c.Key) || (!c.IsGroup && !ZaloApp.IsFriend(c.ThreadId));
                if (isOther != _isOtherSubTab) return false;
                return true;
            });

            if (!string.IsNullOrEmpty(query))
            {
                string norm = ZaloFormat.NormalizeSearch(query);
                list = list.Where(c => ZaloFormat.SearchContains(c.Name, norm));
            }

            int count = 0;
            foreach (var conv in list.OrderByDescending(c => c.IsPinned).ThenByDescending(c => c.LastTs))
            {
                var item = CreateConversationItem(conv);
                _conversationStack.Children.Add(item);
                count++;
            }

            if (count == 0)
            {
                var emptyCard = new Border
                {
                    Padding = new Thickness(24, 48, 24, 48),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var emptyImg = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_tab_message_o.png")),
                    Width = 44,
                    Height = 44,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12),
                    Opacity = 0.5
                };
                emptyStack.Children.Add(emptyImg);
                emptyStack.Children.Add(new TextBlock
                {
                    Text = _isOtherSubTab ? "Không có tin nhắn nào trong mục Khác" : "Chưa có cuộc trò chuyện nào",
                    FontSize = 16,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = _isOtherSubTab
                        ? "Tin nhắn từ người lạ hoặc cuộc trò chuyện chuyển sang Khác sẽ xuất hiện tại đây."
                        : "Bấm dấu + ở trên để bắt đầu nhắn tin hoặc tạo nhóm mới.",
                    FontSize = 13,
                    Foreground = _textSub,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0),
                    MaxWidth = 260,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                _conversationStack.Children.Add(emptyCard);
            }

            UpdateUnreadBadge();
        }

        // EXACT 100% RECONSTRUCTION OF CONVERSATION ITEM IN WP10 - PURE BORDER WITH SILKY TOUCH
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

            if (conv.IsPinned)
            {
                var pinIcon = new TextBlock
                {
                    Text = "📌",
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleLine.Children.Add(pinIcon);
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

            if (conv.IsMuted)
            {
                var muteIcon = new TextBlock
                {
                    Text = "🔕",
                    FontSize = 11,
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleLine.Children.Add(muteIcon);
            }
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

            // Touch Feedback (Silky opacity, Zero system button highlight)
            itemBorder.PointerPressed += (s, e) => itemBorder.Opacity = 0.6;
            itemBorder.PointerReleased += (s, e) => itemBorder.Opacity = 1.0;
            itemBorder.PointerCanceled += (s, e) => itemBorder.Opacity = 1.0;
            itemBorder.Tapped += (s, e) =>
            {
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };

            itemBorder.Holding += async (s, e) =>
            {
                if (e.HoldingState != Windows.UI.Input.HoldingState.Started) return;
                var dialog = new MessageDialog(conv.Name ?? "Trò chuyện", "Tùy chọn");
                dialog.Commands.Add(new UICommand(conv.IsPinned ? "Bỏ ghim" : "Ghim lên đầu", cmd =>
                {
                    conv.IsPinned = !conv.IsPinned;
                    RefreshConversations();
                }));
                dialog.Commands.Add(new UICommand(conv.IsMuted ? "Bật thông báo" : "Tắt thông báo", async cmd =>
                {
                    await ZaloApp.SetMutedAsync(conv, !conv.IsMuted);
                    RefreshConversations();
                }));
                dialog.Commands.Add(new UICommand(_isOtherSubTab ? "Chuyển sang Ưu tiên" : "Chuyển sang Khác", cmd =>
                {
                    var keys = ZaloSettingsService.Current.GetOtherConversationKeys(ZaloApp.Session?.Uid);
                    keys.RemoveAll(k => string.Equals(k, conv.Key, StringComparison.Ordinal));
                    if (!_isOtherSubTab) keys.Add(conv.Key);
                    ZaloSettingsService.Current.SetOtherConversationKeys(ZaloApp.Session?.Uid, keys);
                    RefreshConversations();
                }));
                if (conv.Unread > 0)
                {
                    dialog.Commands.Add(new UICommand("Đánh dấu đã đọc", cmd =>
                    {
                        conv.Unread = 0;
                        RefreshConversations();
                    }));
                }
                dialog.Commands.Add(new UICommand("Xóa cuộc trò chuyện", async cmd =>
                {
                    var confirm = new MessageDialog("Bạn có chắc chắn muốn xóa cuộc trò chuyện này?", "Xác nhận xóa");
                    confirm.Commands.Add(new UICommand("Xóa", c =>
                    {
                        ZaloApp.Conversations.Remove(conv);
                        RefreshConversations();
                    }));
                    confirm.Commands.Add(new UICommand("Hủy"));
                    await confirm.ShowAsync();
                }));
                dialog.Commands.Add(new UICommand("Đóng"));
                await dialog.ShowAsync();
            };

            return itemBorder;
        }

        private void RefreshFriends()
        {
            _friendStack.Children.Clear();
            string query = _searchBox?.Text?.Trim();

            var friends = ZaloApp.Friends.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
            {
                string norm = ZaloFormat.NormalizeSearch(query);
                friends = friends.Where(f => ZaloFormat.SearchContains(f.Name, norm));
            }

            int count = 0;
            foreach (var friend in friends)
            {
                var item = CreateFriendItem(friend);
                _friendStack.Children.Add(item);
                count++;
            }

            if (count == 0)
            {
                var emptyCard = new Border
                {
                    Padding = new Thickness(24, 40, 24, 40),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Chưa có bạn bè trong danh bạ",
                    FontSize = 15.5,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Nhấn 'Lời mời kết bạn' ở trên để kết bạn với mọi người qua số điện thoại.",
                    FontSize = 12.5,
                    Foreground = _textSub,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0),
                    MaxWidth = 260,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyCard.Child = emptyStack;
                _friendStack.Children.Add(emptyCard);
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

            if (!string.IsNullOrEmpty(friend.Avatar))
            {
                var imgBrush = ZaloAvatar.ImageFor(friend.Avatar);
                if (imgBrush != null)
                {
                    avatarGrid.Children.Add(new Ellipse { Width = 44, Height = 44, Fill = imgBrush });
                }
            }

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
            itemBorder.PointerPressed += (s, e) => itemBorder.Opacity = 0.6;
            itemBorder.PointerReleased += (s, e) => itemBorder.Opacity = 1.0;
            itemBorder.PointerCanceled += (s, e) => itemBorder.Opacity = 1.0;
            itemBorder.Tapped += (s, e) =>
            {
                NavigationManager.NavigateTo(new FriendProfilePage(friend));
            };

            return itemBorder;
        }

        private void RefreshProfile()
        {
            var session = ZaloApp.Session;
            if (session != null)
            {
                string displayName = session.DisplayName;
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = !string.IsNullOrEmpty(session.Uid) ? ("Zalo ID: " + session.Uid) : "Người dùng Zalo";
                }
                _profileName.Text = displayName;
                _profileInitial.Text = ZaloAvatar.InitialOf(displayName);
                _profileAvatarColor.Fill = ZaloAvatar.ColorFor(displayName);

                string status = string.IsNullOrWhiteSpace(session.ProfileStatus)
                    ? "Cập nhật trạng thái..."
                    : session.ProfileStatus;
                _profileStatusPreview.Text = status;

                if (!string.IsNullOrEmpty(session.AvatarUrl))
                {
                    _profileAvatarImage.Fill = ZaloAvatar.ImageFor(session.AvatarUrl);
                }
                else
                {
                    _profileAvatarImage.Fill = null;
                }
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

        private static Border CreateTouchBorder(double width, double height)
        {
            var border = new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Colors.Transparent),
                VerticalAlignment = VerticalAlignment.Center
            };
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private static SolidColorBrush ColorBrush(byte a, byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
    }
}
