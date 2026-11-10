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
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x72, 0x80));
        private readonly SolidColorBrush _textMuted = new SolidColorBrush(Color.FromArgb(0xFF, 0x9C, 0xA3, 0xAF));

        private int _selectedTab = 0;
        private bool _isOtherSubTab = false;

        // Container Grids
        private Grid _tab0Content; // Tin nhắn
        private Grid _tab1Content; // Danh bạ
        private Grid _tab2Content; // Khám phá
        private Grid _tab3Content; // Nhật ký
        private Grid _tab4Content; // Cá nhân

        // Tab Bar Elements
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
            RefreshConversations();
            RefreshFriends();
            RefreshProfile();
            UpdateUnreadBadge();

            ZaloApp.Conversations.CollectionChanged -= Conversations_CollectionChanged;
            ZaloApp.Conversations.CollectionChanged += Conversations_CollectionChanged;
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });

            // 1. TOP HEADER (Vibrant Zalo Blue Gradient #2878F0 -> #08A9E8)
            var headerGrid = CreateTopHeader();
            Grid.SetRow(headerGrid, 0);
            rootGrid.Children.Add(headerGrid);

            // 2. MAIN CONTENT (5 TABS)
            var contentContainer = new Grid();
            _tab0Content = CreateChatsTab();
            _tab1Content = CreateContactsTab();
            _tab2Content = CreateDiscoveryTab();
            _tab3Content = CreateTimelineTab();
            _tab4Content = CreateProfileTab();

            contentContainer.Children.Add(_tab0Content);
            contentContainer.Children.Add(_tab1Content);
            contentContainer.Children.Add(_tab2Content);
            contentContainer.Children.Add(_tab3Content);
            contentContainer.Children.Add(_tab4Content);

            Grid.SetRow(contentContainer, 1);
            rootGrid.Children.Add(contentContainer);

            // 3. BOTTOM NAVIGATION BAR (5 Tabs)
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

            // Search Bar
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(11),
                Height = 38,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lensIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_search_white.png")),
                Width = 18,
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lensIcon, 0);
            searchGrid.Children.Add(lensIcon);

            _searchBox = new TextBox
            {
                PlaceholderText = "Tìm kiếm",
                Height = 38,
                FontSize = 14.5,
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
                Width = 38,
                Height = 38,
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
            itemGroup.Click += (s, e) => PromptCreateGroup();
            var itemFriend = new MenuFlyoutItem { Text = "Thêm bạn mới" };
            itemFriend.Click += (s, e) => PromptAddFriend();
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
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
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
                Foreground = _textDark,
                Margin = new Thickness(0, 8, 0, 6)
            };
            _subTabPriorityIndicator = new Border
            {
                Height = 2.5,
                Width = 52,
                Background = _textDark
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
                Foreground = _textSub,
                Margin = new Thickness(0, 8, 0, 6)
            };
            _subTabOtherIndicator = new Border
            {
                Height = 2.5,
                Width = 36,
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
                Margin = new Thickness(0, 0, 0, 16)
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
                Text = "Trải nghiệm các dịch vụ số thông minh ngay trên Windows Phone",
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

        private Grid CreateTimelineTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };
            var scroll = new ScrollViewer { Padding = new Thickness(0, 10, 0, 20) };
            var stack = new StackPanel();

            // Post creation card
            var postCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var postGrid = new Grid();
            postGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            postGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var myAvatar = new Ellipse
            {
                Width = 40,
                Height = 40,
                Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF))
            };
            Grid.SetColumn(myAvatar, 0);
            postGrid.Children.Add(myAvatar);

            var promptText = new TextBlock
            {
                Text = "Hôm nay bạn thế nào?",
                FontSize = 15,
                Foreground = _textSub,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(promptText, 1);
            postGrid.Children.Add(promptText);
            postCard.Child = postGrid;
            stack.Children.Add(postCard);

            // Sample story feed item
            stack.Children.Add(CreateFeedPost("Zalo Official", "Vừa xong", "Chào mừng bạn đến với Zalo trên Windows Phone! Hãy trải nghiệm tốc độ và sự tiện ích.", "👍 128   💬 32"));

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private FrameworkElement CreateFeedPost(string author, string time, string text, string stats)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stack = new StackPanel();

            var authorStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var avatar = new Ellipse { Width = 38, Height = 38, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)), Margin = new Thickness(0, 0, 10, 0) };
            authorStack.Children.Add(avatar);

            var nameStack = new StackPanel();
            nameStack.Children.Add(new TextBlock { Text = author, FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            nameStack.Children.Add(new TextBlock { Text = time, FontSize = 12, Foreground = _textMuted });
            authorStack.Children.Add(nameStack);
            stack.Children.Add(authorStack);

            var body = new TextBlock { Text = text, FontSize = 14.5, Foreground = _textDark, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            stack.Children.Add(body);

            var statsBlock = new TextBlock { Text = stats, FontSize = 12.5, Foreground = _textSub };
            stack.Children.Add(statsBlock);

            card.Child = stack;
            return card;
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
            _profileAvatar = new Ellipse { Width = 60, Height = 60, Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF)) };
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

        private Border CreateBottomBar()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0xE7, 0xEB)),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };

            var grid = new Grid();
            for (int i = 0; i < 5; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            _tabIcons = new Image[5];
            _tabLabels = new TextBlock[5];

            string[] titles = { "Tin nhắn", "Danh bạ", "Khám phá", "Nhật ký", "Cá nhân" };
            string[] iconNames = { "tab_msg", "tab_contact", "tab_discovery", "tab_timeline", "tab_me" };

            for (int i = 0; i < 5; i++)
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
                    Source = new BitmapImage(new Uri($"ms-appx:///Assets/Icons/{iconNames[i]}_inactive.png")),
                    Width = 22,
                    Height = 22,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _tabIcons[i] = img;
                stack.Children.Add(img);

                var txt = new TextBlock
                {
                    Text = titles[i],
                    FontSize = 10.5,
                    FontWeight = Windows.UI.Text.FontWeights.Normal,
                    Foreground = _inactiveGray,
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
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
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
            _tab4Content.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

            string[] iconNames = { "tab_msg", "tab_contact", "tab_discovery", "tab_timeline", "tab_me" };
            for (int i = 0; i < 5; i++)
            {
                bool active = i == index;
                string state = active ? "active" : "inactive";
                _tabIcons[i].Source = new BitmapImage(new Uri($"ms-appx:///Assets/Icons/{iconNames[i]}_{state}.png"));
                _tabLabels[i].Foreground = active ? _activeBlue : _inactiveGray;
                _tabLabels[i].FontWeight = active ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            }

            if (index == 0) RefreshConversations();
            else if (index == 1) RefreshFriends();
            else if (index == 4) RefreshProfile();
        }

        private void SwitchSubTab(bool other)
        {
            _isOtherSubTab = other;
            _subTabPriorityText.FontWeight = !other ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _subTabPriorityText.Foreground = !other ? _textDark : _textSub;
            _subTabPriorityIndicator.Background = !other ? _textDark : new SolidColorBrush(Colors.Transparent);

            _subTabOtherText.FontWeight = other ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            _subTabOtherText.Foreground = other ? _textDark : _textSub;
            _subTabOtherIndicator.Background = other ? _textDark : new SolidColorBrush(Colors.Transparent);

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

        private FrameworkElement CreateConversationItem(ZaloConversation conv)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF4, 0xF6)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Circular Avatar
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
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
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

            Grid.SetColumn(avatarGrid, 0);
            grid.Children.Add(avatarGrid);

            // Name & Preview
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 8, 0) };
            var nameBlock = new TextBlock
            {
                Text = conv.Name ?? "Trò chuyện",
                FontSize = 15.5,
                FontWeight = conv.Unread > 0 ? Windows.UI.Text.FontWeights.Bold : Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var lastTextBlock = new TextBlock
            {
                Text = conv.LastText ?? string.Empty,
                FontSize = 13.5,
                Foreground = conv.Unread > 0 ? _textDark : _textSub,
                FontWeight = conv.Unread > 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0)
            };
            textStack.Children.Add(nameBlock);
            textStack.Children.Add(lastTextBlock);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Right Stack (Time + Unread Badge)
            var rightStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var timeBlock = new TextBlock
            {
                Text = conv.TimeText ?? string.Empty,
                FontSize = 11.5,
                Foreground = conv.Unread > 0 ? _activeBlue : _textMuted,
                FontWeight = conv.Unread > 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rightStack.Children.Add(timeBlock);

            if (conv.Unread > 0)
            {
                var unreadBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44)),
                    CornerRadius = new CornerRadius(9),
                    Height = 18,
                    MinWidth = 18,
                    Padding = new Thickness(4, 0, 4, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                var unreadText = new TextBlock
                {
                    Text = conv.UnreadLabel,
                    FontSize = 10,
                    FontWeight = Windows.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                unreadBorder.Child = unreadText;
                rightStack.Children.Add(unreadBorder);
            }

            Grid.SetColumn(rightStack, 2);
            grid.Children.Add(rightStack);

            border.Child = grid;
            border.Tapped += (s, e) =>
            {
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };

            return border;
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
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF4, 0xF6)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 10, 16, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarGrid = new Grid { Width = 42, Height = 42, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = 42, Height = 42, Fill = friend.AvatarColor ?? _activeBlue };
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
            grid.Children.Add(avatarGrid);

            var nameBlock = new TextBlock
            {
                Text = friend.Name ?? "Bạn bè",
                FontSize = 15.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameBlock, 1);
            grid.Children.Add(nameBlock);

            border.Child = grid;
            border.Tapped += (s, e) =>
            {
                var conv = ZaloApp.GetOrCreate(friend.UserId, false);
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };

            return border;
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

        private void PromptCreateGroup()
        {
            // Placeholder: quick group creation dialog
        }

        private void PromptAddFriend()
        {
            // Placeholder: add friend dialog
        }

        private async void Conversations_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_selectedTab == 0) RefreshConversations();
                UpdateUnreadBadge();
            });
        }
    }
}
