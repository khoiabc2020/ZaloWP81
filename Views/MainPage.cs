using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private Border _strangerInboxBanner;
        private Border _strangerBadgeBorder;
        private TextBlock _strangerBadgeText;

        // Header Buttons matching WP10
        private Border _plusBtn;
        private Border _contactGroupsHeaderBtn;
        private Border _contactRequestsHeaderBtn;
        private Border _profileSettingsHeaderBtn;

        // Contacts Elements
        private StackPanel _friendStack;
        private int _contactSubTab = 0; // 0 = All, 1 = Friends, 2 = OA, 3 = Groups
        private Border _chipAll;
        private Border _chipFriends;
        private Border _chipOA;
        private TextBlock _chipAllText;
        private TextBlock _chipFriendsText;
        private TextBlock _chipOAText;
        private ScrollViewer _contactsScrollViewer;
        private ScrollViewer _groupsScrollViewer;
        private StackPanel _groupsStack;
        private Grid _oaView;
        private TextBlock _quickContactCountText;
        private StackPanel _quickContactStack;

        // Discovery Elements
        private TextBlock _recommendationsStatusText;
        private StackPanel _recommendationsStack;
        private bool _recommendationsLoaded = false;
        private bool _recommendationsLoading = false;

        // Profile Elements
        private Ellipse _profileAvatarColor;
        private Ellipse _profileAvatarImage;
        private TextBlock _profileInitial;
        private TextBlock _profileName;
        private TextBlock _profileStatusPreview;

        // Cached Tab Icons to eliminate GC and Disk I/O during tab switching
        private static readonly BitmapImage _iconMsgActive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_tab_message_o.png"));
        private static readonly BitmapImage _iconMsgInactive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/tab_msg_inactive.png"));
        private static readonly BitmapImage _iconContactActive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_tab_contact_o.png"));
        private static readonly BitmapImage _iconContactInactive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/tab_contact_inactive.png"));
        private static readonly BitmapImage _iconMoreActive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_tab_more_o.png"));
        private static readonly BitmapImage _iconMoreInactive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/tab_discovery_inactive.png"));
        private static readonly BitmapImage _iconMeActive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_tab_me_o.png"));
        private static readonly BitmapImage _iconMeInactive = new BitmapImage(new Uri("ms-appx:///Assets/Icons/tab_me_inactive.png"));

        private bool _conversationsRendered = false;
        private bool _conversationsDirty = true;
        private bool _friendsRendered = false;
        private bool _friendsDirty = true;
        private bool _profileRendered = false;

        private class ConversationItemViewHolder
        {
            public Border Container;
            public TextBlock NameBlock;
            public TextBlock LastTextBlock;
            public TextBlock TimeBlock;
            public Border UnreadBadge;
            public TextBlock UnreadText;
            public Image PinIcon;
            public Image MuteIcon;
            public Ellipse AvatarImageEllipse;
        }

        private readonly Dictionary<string, ConversationItemViewHolder> _convItemCache = new Dictionary<string, ConversationItemViewHolder>();
        private readonly Dictionary<string, FrameworkElement> _friendItemCache = new Dictionary<string, FrameworkElement>();
        private bool _refreshPending = false;

        public MainPage()
        {
            BuildUI();
            this.Loaded += (s, e) => Initialize();
        }

        private async void Initialize()
        {
            ZaloApp.SetDispatcher(Dispatcher);

            if (ZaloApp.Session != null)
            {
                CleanupMockConversations();
                RefreshConversations();
                RefreshFriends();
                RefreshProfile();
                UpdateUnreadBadge();

                ZaloApp.Conversations.CollectionChanged -= Conversations_CollectionChanged;
                ZaloApp.Conversations.CollectionChanged += Conversations_CollectionChanged;
                return;
            }

            // Cold start session bootstrap: restore stored session seamlessly without flashing QR login!
            try
            {
                if (await ZaloStore.HasSessionAsync())
                {
                    string cachedUid = await ZaloStore.GetCachedUserIdAsync();
                    if (!string.IsNullOrWhiteSpace(cachedUid))
                    {
                        await ZaloApp.PreloadOfflineCacheAsync(cachedUid);
                        CleanupMockConversations();
                        RefreshConversations();
                        RefreshFriends();
                    }

                    // Give RestoreAsync sufficient time; do NOT abort after 5s if session was present on disk
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    var session = await ZaloStore.RestoreAsync(cts.Token);
                    if (session != null)
                    {
                        await ZaloApp.StartAsync(session);
                        CleanupMockConversations();
                        RefreshConversations();
                        RefreshFriends();
                        RefreshProfile();
                        UpdateUnreadBadge();

                        ZaloApp.Conversations.CollectionChanged -= Conversations_CollectionChanged;
                        ZaloApp.Conversations.CollectionChanged += Conversations_CollectionChanged;
                        return;
                    }
                    else if (!string.IsNullOrWhiteSpace(cachedUid))
                    {
                        // Offline mode: stay on cached UI rather than kicking to QR screen
                        ZaloLog.Add("Session restore returned null; staying in offline cached state.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("MainPage Initialize error: " + ex.Message);
            }

            if (ZaloApp.Session == null)
            {
                NavigationManager.NavigateTo(new LoginPage(), clearStack: true);
            }
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

        private double _appliedScale = 1.0;

        protected override void OnNavigatedTo(Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            double currentScale = ZaloSettingsService.Current.UiScaleFactor;
            if (Math.Abs(_appliedScale - currentScale) > 0.01)
            {
                _appliedScale = currentScale;
                _convItemCache.Clear();
                _friendItemCache.Clear();
                BuildUI();
                _conversationsDirty = true;
                _friendsDirty = true;
                RefreshConversations();
                RefreshFriends();
            }
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            _appliedScale = scale;
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Colors.White)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(68 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(58 * scale)) });

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
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var headerBorder = new Border
            {
                Padding = new Thickness(10, Math.Round(20 * scale), 10, Math.Round(6 * scale))
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
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Search Bar (Translucent Pill - completely transparent with overlay placeholder, zero white focus box)
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(21),
                Height = Math.Round(40 * scale),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(38 * scale)) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lensIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_search_white.png")),
                Width = Math.Round(22 * scale),
                Height = Math.Round(22 * scale),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(lensIcon, 0);
            searchGrid.Children.Add(lensIcon);

            var searchInputContainer = new Grid { VerticalAlignment = VerticalAlignment.Center };

            var searchPlaceholder = new TextBlock
            {
                Text = "Tìm kiếm",
                FontSize = Math.Round(15.5 * scale, 1),
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsHitTestVisible = false
            };
            searchInputContainer.Children.Add(searchPlaceholder);

            _searchBox = new TextBox
            {
                Height = Math.Round(38 * scale),
                FontSize = Math.Round(15.5 * scale, 1),
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            App.MakeTextBoxTransparent(_searchBox, isSearch: true);
            searchInputContainer.Children.Add(_searchBox);

            var clearSearchBtn = CreateTouchBorder(Math.Round(34 * scale), Math.Round(38 * scale));
            clearSearchBtn.Visibility = Visibility.Collapsed;
            var clearText = new TextBlock
            {
                Text = "✕",
                FontSize = Math.Round(13 * scale),
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            clearSearchBtn.Child = clearText;
            clearSearchBtn.Tapped += (s, e) =>
            {
                _searchBox.Text = string.Empty;
                searchPlaceholder.Visibility = Visibility.Visible;
                OnSearchChanged(string.Empty);
            };
            Grid.SetColumn(clearSearchBtn, 2);
            searchGrid.Children.Add(clearSearchBtn);

            _searchBox.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrEmpty(_searchBox.Text);
                searchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
                clearSearchBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                OnSearchChanged(_searchBox.Text);
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    e.Handled = true;
                    string q = _searchBox.Text?.Trim();
                    if (!string.IsNullOrEmpty(q))
                    {
                        NavigationManager.NavigateTo(new SearchResultsPage(q));
                    }
                }
            };
            Grid.SetColumn(searchInputContainer, 1);
            searchGrid.Children.Add(searchInputContainer);

            searchBorder.Child = searchGrid;
            Grid.SetColumn(searchBorder, 0);
            header.Children.Add(searchBorder);

            // Group Switcher Button (shown only on Tab 1 Contacts, matching WP10)
            _contactGroupsHeaderBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(42 * scale));
            _contactGroupsHeaderBtn.Visibility = Visibility.Collapsed;
            var groupIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_clean_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _contactGroupsHeaderBtn.Child = groupIcon;
            _contactGroupsHeaderBtn.Tapped += (s, e) => ToggleGroupsView();
            Grid.SetColumn(_contactGroupsHeaderBtn, 1);
            header.Children.Add(_contactGroupsHeaderBtn);

            // Action Button Container (Col 2)
            var actionGrid = new Grid();

            // 1. Plus Button (Tab 0)
            _plusBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(42 * scale));
            var plusIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_plus_white.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _plusBtn.Child = plusIcon;
            _plusBtn.Tapped += (s, e) => ShowHeaderMenu();
            actionGrid.Children.Add(_plusBtn);

            // 2. Contact Requests Button (Tab 1)
            _contactRequestsHeaderBtn = CreateTouchBorder(Math.Round(42 * scale), Math.Round(42 * scale));
            _contactRequestsHeaderBtn.Visibility = Visibility.Collapsed;
            var reqIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png")),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _contactRequestsHeaderBtn.Child = reqIcon;
            _contactRequestsHeaderBtn.Tapped += (s, e) => ShowContactRequestsMenu();
            actionGrid.Children.Add(_contactRequestsHeaderBtn);

            // 3. Profile Settings Button (Tab 3)
            _profileSettingsHeaderBtn = CreateTouchBorder(38, 38);
            _profileSettingsHeaderBtn.Visibility = Visibility.Collapsed;
            var settingsGear = new TextBlock
            {
                Text = "\uE115",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 21,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileSettingsHeaderBtn.Child = settingsGear;
            _profileSettingsHeaderBtn.Tapped += (s, e) => NavigationManager.NavigateTo(new SettingsPage());
            actionGrid.Children.Add(_profileSettingsHeaderBtn);

            Grid.SetColumn(actionGrid, 2);
            header.Children.Add(actionGrid);

            headerBorder.Child = header;
            return headerBorder;
        }

        private void ShowContactRequestsMenu()
        {
            var sheet = new Common.ZaloActionSheet("Tùy chọn danh bạ");
            sheet.AddAction("Lời mời kết bạn", () => NavigationManager.NavigateTo(new FriendRequestsPage(0)));
            sheet.AddAction("Lời mời đã gửi", () => NavigationManager.NavigateTo(new FriendRequestsPage(1)));
            sheet.AddAction("Thêm bạn mới", () => NavigationManager.NavigateTo(new AddFriendPage()));
            sheet.AddAction("Làm mới danh bạ", () => { _friendsDirty = true; RefreshFriends(); });
            sheet.AddAction("Tạo nhóm chat", () => NavigationManager.NavigateTo(new CreateGroupPage()));
            sheet.Show();
        }

        private void ToggleGroupsView()
        {
            if (_groupsScrollViewer == null || _contactsScrollViewer == null) return;
            bool showGroups = _groupsScrollViewer.Visibility != Visibility.Visible;
            _groupsScrollViewer.Visibility = showGroups ? Visibility.Visible : Visibility.Collapsed;
            _contactsScrollViewer.Visibility = showGroups ? Visibility.Collapsed : Visibility.Visible;
            if (_oaView != null) _oaView.Visibility = Visibility.Collapsed;
            if (showGroups) UpdateGroupList();
        }

        private void ShowHeaderMenu()
        {
            var sheet = new Common.ZaloActionSheet("Tùy chọn Zalo");
            sheet.AddAction("Tạo nhóm mới", () => NavigationManager.NavigateTo(new CreateGroupPage()));
            sheet.AddAction("Thêm bạn mới", () => NavigationManager.NavigateTo(new AddFriendPage()));
            sheet.AddAction("Cài đặt", () => NavigationManager.NavigateTo(new SettingsPage()));
            sheet.Show();
        }

        private Grid CreateChatsTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Subtabs: "Ưu tiên" & "Khác"
            var subtabsBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var subtabsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 16, 0) };

            // Subtab 1: Ưu tiên
            var priorityStack = new StackPanel { Margin = new Thickness(0, 0, 26, 0), Background = new SolidColorBrush(Colors.Transparent) };
            _subTabPriorityText = new TextBlock
            {
                Text = "Ưu tiên",
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = ColorBrush(0xFF, 0x11, 0x18, 0x27),
                Margin = new Thickness(0, 9, 0, 6)
            };
            _subTabPriorityIndicator = new Border
            {
                Height = 3,
                Width = 54,
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
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.Normal,
                Foreground = _inactiveGray,
                Margin = new Thickness(0, 9, 0, 6)
            };
            _subTabOtherIndicator = new Border
            {
                Height = 3,
                Width = 42,
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

            // Stranger Messages Header Banner
            _strangerInboxBanner = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 12, 16, 12),
                Visibility = Visibility.Collapsed
            };
            var strangerGrid = new Grid();
            strangerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            strangerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            strangerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var strangerIconBorder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF6, 0xFF)),
                VerticalAlignment = VerticalAlignment.Center
            };
            strangerIconBorder.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_user_clean_blue.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(strangerIconBorder, 0);
            strangerGrid.Children.Add(strangerIconBorder);

            var strangerTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            strangerTextStack.Children.Add(new TextBlock
            {
                Text = "Tin nhắn từ người lạ",
                FontSize = 15.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark
            });
            strangerTextStack.Children.Add(new TextBlock
            {
                Text = "Xem tin nhắn từ người chưa kết bạn",
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(strangerTextStack, 1);
            strangerGrid.Children.Add(strangerTextStack);

            _strangerBadgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE5, 0x39, 0x35)),
                CornerRadius = new CornerRadius(10),
                MinWidth = 20,
                Height = 20,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _strangerBadgeText = new TextBlock
            {
                Text = "0",
                FontSize = 11,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _strangerBadgeBorder.Child = _strangerBadgeText;
            Grid.SetColumn(_strangerBadgeBorder, 2);
            strangerGrid.Children.Add(_strangerBadgeBorder);

            _strangerInboxBanner.Child = strangerGrid;
            _strangerInboxBanner.PointerPressed += (s, e) => _strangerInboxBanner.Opacity = 0.6;
            _strangerInboxBanner.PointerReleased += (s, e) => _strangerInboxBanner.Opacity = 1.0;
            _strangerInboxBanner.PointerCanceled += (s, e) => _strangerInboxBanner.Opacity = 1.0;
            _strangerInboxBanner.Tapped += (s, e) => NavigationManager.NavigateTo(new StrangerMessagesPage());

            listStack.Children.Add(_strangerInboxBanner);

            _conversationStack = new StackPanel();
            listStack.Children.Add(_conversationStack);

            scroll.Content = listStack;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            return grid;
        }

        private Grid CreateContactsTab()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(48 * scale)) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Row 0: Rounded filter chips matching WP10 ("Tất cả", "Bạn bè", "OA")
            var filterBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(6 * scale), Math.Round(14 * scale), Math.Round(6 * scale))
            };
            var filterStack = new StackPanel { Orientation = Orientation.Horizontal };

            _chipAll = CreateChip("Tất cả", true, out _chipAllText);
            _chipFriends = CreateChip("Bạn bè", false, out _chipFriendsText);
            _chipOA = CreateChip("OA", false, out _chipOAText);

            _chipAll.Tapped += (s, e) => SwitchContactSubTab(0);
            _chipFriends.Tapped += (s, e) => SwitchContactSubTab(1);
            _chipOA.Tapped += (s, e) => SwitchContactSubTab(2);

            filterStack.Children.Add(_chipAll);
            filterStack.Children.Add(_chipFriends);
            filterStack.Children.Add(_chipOA);
            filterBorder.Child = filterStack;
            Grid.SetRow(filterBorder, 0);
            grid.Children.Add(filterBorder);

            // Row 1: Content Grid containing _contactsScrollViewer, _groupsScrollViewer, _oaView
            var contentGrid = new Grid();

            // 1. Friends view (ScrollViewer with mainStack)
            _contactsScrollViewer = new ScrollViewer { Background = new SolidColorBrush(Colors.White) };
            var mainStack = new StackPanel { Background = new SolidColorBrush(Colors.White) };

            // "Lời mời kết bạn" card
            var reqCard = CreateActionRow("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png", "Lời mời kết bạn", "Xem và phản hồi lời mời mới", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            reqCard.Tapped += (s, e) => NavigationManager.NavigateTo(new FriendRequestsPage(0));
            mainStack.Children.Add(reqCard);

            // "Lời mời đã gửi" card
            var sentReqCard = CreateActionRow("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png", "Lời mời đã gửi", "Xem hoặc thu hồi lời mời đang chờ", Color.FromArgb(0xFF, 0x39, 0x78, 0xD4));
            sentReqCard.Tapped += (s, e) => NavigationManager.NavigateTo(new FriendRequestsPage(1));
            mainStack.Children.Add(sentReqCard);

            // "Liên hệ nhanh (0/10)" card with "+ Thêm"
            var quickContactHeader = new Grid { Margin = new Thickness(Math.Round(14 * scale), Math.Round(10 * scale), Math.Round(14 * scale), Math.Round(6 * scale)) };
            quickContactHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            quickContactHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var qcLeftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            qcLeftStack.Children.Add(new TextBlock { Text = "★ ", FontSize = Math.Round(14 * scale, 1), Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xE3, 0xB2, 0x1A)), Margin = new Thickness(0, 0, Math.Round(7 * scale), 0) });
            _quickContactCountText = new TextBlock { Text = "Liên hệ nhanh (0/10)", FontSize = Math.Round(14 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark };
            qcLeftStack.Children.Add(_quickContactCountText);
            Grid.SetColumn(qcLeftStack, 0);
            quickContactHeader.Children.Add(qcLeftStack);

            var qcAddBtn = new TextBlock
            {
                Text = "+ Thêm",
                FontSize = Math.Round(14 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _activeBlue,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(Math.Round(8 * scale), Math.Round(2 * scale), Math.Round(8 * scale), Math.Round(2 * scale))
            };
            qcAddBtn.Tapped += (s, e) => ShowAddQuickContactDialog();
            Grid.SetColumn(qcAddBtn, 1);
            quickContactHeader.Children.Add(qcAddBtn);
            mainStack.Children.Add(quickContactHeader);

            _quickContactStack = new StackPanel();
            mainStack.Children.Add(_quickContactStack);

            // Section Header: "TẤT CẢ BẠN BÈ"
            var sectionHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(8 * scale), Math.Round(14 * scale), Math.Round(8 * scale))
            };
            sectionHeader.Child = new TextBlock
            {
                Text = "TẤT CẢ BẠN BÈ",
                FontSize = Math.Round(12 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub
            };
            mainStack.Children.Add(sectionHeader);

            _friendStack = new StackPanel();
            mainStack.Children.Add(_friendStack);

            _contactsScrollViewer.Content = mainStack;
            contentGrid.Children.Add(_contactsScrollViewer);

            // 2. Groups view (ScrollViewer with groups stack)
            _groupsScrollViewer = new ScrollViewer { Background = new SolidColorBrush(Colors.White), Visibility = Visibility.Collapsed };
            _groupsStack = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            _groupsScrollViewer.Content = _groupsStack;
            contentGrid.Children.Add(_groupsScrollViewer);

            // 3. OA view
            _oaView = new Grid { Background = new SolidColorBrush(Colors.White), Visibility = Visibility.Collapsed };
            var oaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(20) };
            oaStack.Children.Add(new TextBlock
            {
                Text = "Chưa có Official Account nào quan tâm.",
                FontSize = 14,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _oaView.Children.Add(oaStack);
            contentGrid.Children.Add(_oaView);

            Grid.SetRow(contentGrid, 1);
            grid.Children.Add(contentGrid);

            return grid;
        }

        private Border CreateChip(string text, bool isSelected, out TextBlock textBlock)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var border = new Border
            {
                Background = isSelected ? ColorBrush(0xFF, 0xE5, 0xE7, 0xEB) : new SolidColorBrush(Colors.White),
                BorderBrush = isSelected ? new SolidColorBrush(Colors.Transparent) : ColorBrush(0xFF, 0xDD, 0xDD, 0xDD),
                BorderThickness = new Thickness(isSelected ? 0 : 1),
                CornerRadius = new CornerRadius(Math.Round(18 * scale)),
                Padding = new Thickness(Math.Round(15 * scale), Math.Round(6 * scale), Math.Round(15 * scale), Math.Round(6 * scale)),
                Margin = new Thickness(0, 0, Math.Round(9 * scale), 0),
                IsTapEnabled = true
            };
            textBlock = new TextBlock
            {
                Text = text,
                FontSize = Math.Round(13.5 * scale, 1),
                FontWeight = isSelected ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = isSelected ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray
            };
            border.Child = textBlock;
            return border;
        }

        private void SwitchContactSubTab(int subTab)
        {
            _contactSubTab = subTab;

            // Reset Groups view if visible
            if (_groupsScrollViewer != null) _groupsScrollViewer.Visibility = Visibility.Collapsed;

            // Update chip styles
            _chipAll.Background = subTab == 0 ? ColorBrush(0xFF, 0xE5, 0xE7, 0xEB) : new SolidColorBrush(Colors.White);
            _chipAll.BorderThickness = new Thickness(subTab == 0 ? 0 : 1);
            _chipAllText.Foreground = subTab == 0 ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray;
            _chipAllText.FontWeight = subTab == 0 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;

            _chipFriends.Background = subTab == 1 ? ColorBrush(0xFF, 0xE5, 0xE7, 0xEB) : new SolidColorBrush(Colors.White);
            _chipFriends.BorderThickness = new Thickness(subTab == 1 ? 0 : 1);
            _chipFriendsText.Foreground = subTab == 1 ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray;
            _chipFriendsText.FontWeight = subTab == 1 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;

            _chipOA.Background = subTab == 2 ? ColorBrush(0xFF, 0xE5, 0xE7, 0xEB) : new SolidColorBrush(Colors.White);
            _chipOA.BorderThickness = new Thickness(subTab == 2 ? 0 : 1);
            _chipOAText.Foreground = subTab == 2 ? ColorBrush(0xFF, 0x11, 0x18, 0x27) : _inactiveGray;
            _chipOAText.FontWeight = subTab == 2 ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;

            if (subTab == 2)
            {
                if (_contactsScrollViewer != null) _contactsScrollViewer.Visibility = Visibility.Collapsed;
                if (_oaView != null) _oaView.Visibility = Visibility.Visible;
            }
            else
            {
                if (_oaView != null) _oaView.Visibility = Visibility.Collapsed;
                if (_contactsScrollViewer != null) _contactsScrollViewer.Visibility = Visibility.Visible;
                _friendsDirty = true;
                RefreshFriends();
            }
        }

        private Border CreateActionRow(string iconUri, string title, string subtitle, Color bg)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var border = new Border
            {
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(12 * scale), Math.Round(14 * scale), Math.Round(12 * scale)),
                BorderBrush = ColorBrush(0xFF, 0xF0, 0xF0, 0xF0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Colors.White),
                IsTapEnabled = true
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double boxSize = Math.Round(40 * scale);
            var iconBox = new Border
            {
                Background = new SolidColorBrush(bg),
                Width = boxSize,
                Height = boxSize,
                CornerRadius = new CornerRadius(Math.Round(boxSize / 2)),
                Margin = new Thickness(0, 0, Math.Round(12 * scale), 0)
            };
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = Math.Round(20 * scale),
                Height = Math.Round(20 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = img;
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txtStack.Children.Add(new TextBlock { Text = title, FontSize = Math.Round(15 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = ColorBrush(0xFF, 0x17, 0x21, 0x2B) });
            txtStack.Children.Add(new TextBlock { Text = subtitle, FontSize = Math.Round(12.5 * scale, 1), Foreground = ColorBrush(0xFF, 0x77, 0x83, 0x92), Margin = new Thickness(0, Math.Round(2 * scale), 0, 0) });
            Grid.SetColumn(txtStack, 1);
            grid.Children.Add(txtStack);

            var chevron = new TextBlock
            {
                Text = "›",
                FontSize = Math.Round(18 * scale, 1),
                Foreground = ColorBrush(0xFF, 0x94, 0xA3, 0xB8),
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

        private void ShowAddQuickContactDialog()
        {
            var friends = (ZaloApp.Friends ?? new List<ZaloFriend>()).Where(f => !string.IsNullOrEmpty(f.UserId)).ToList();
            if (friends.Count == 0)
            {
                var md = new MessageDialog("Danh bạ chưa có bạn bè để thêm.", "Liên hệ nhanh");
                var ignored = md.ShowAsync();
                return;
            }

            string accountId = ZaloApp.Session?.Uid;
            var currentIds = ZaloSettingsService.Current.GetQuickContactIds(accountId);
            var sheet = new Common.ZaloActionSheet("Liên hệ nhanh (tối đa 10)");
            foreach (var f in friends.Take(10))
            {
                string uid = f.UserId;
                string name = f.DisplayName ?? f.Name;
                bool isAdded = currentIds.Contains(uid);
                sheet.AddAction((isAdded ? "✓ Đã thêm: " : "+ Thêm: ") + name, () =>
                {
                    if (isAdded) currentIds.Remove(uid);
                    else if (currentIds.Count < 10) currentIds.Add(uid);
                    ZaloSettingsService.Current.SetQuickContactIds(accountId, currentIds);
                    UpdateQuickContacts();
                });
            }
            sheet.Show();
        }

        private void UpdateQuickContacts()
        {
            if (_quickContactStack == null) return;
            _quickContactStack.Children.Clear();
            var ids = ZaloSettingsService.Current.GetQuickContactIds(ZaloApp.Session?.Uid);
            var friends = (ZaloApp.Friends ?? new List<ZaloFriend>()).Where(f => ids.Contains(f.UserId)).ToList();
            if (_quickContactCountText != null) _quickContactCountText.Text = $"Liên hệ nhanh ({friends.Count}/10)";

            foreach (var friend in friends)
            {
                var row = CreateFriendItem(friend);
                _quickContactStack.Children.Add(row);
            }
        }

        private void UpdateGroupList()
        {
            if (_groupsStack == null) return;
            _groupsStack.Children.Clear();
            var groups = ZaloApp.Conversations.Where(c => c.IsGroup).ToList();
            if (groups.Count == 0)
            {
                _groupsStack.Children.Add(new TextBlock
                {
                    Text = "Chưa tham gia nhóm nào.",
                    FontSize = 14,
                    Foreground = _textSub,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            double scale = ZaloSettingsService.Current.UiScaleFactor;
            double rowHeight = Math.Round(64 * scale);
            double avatarSize = Math.Round(44 * scale);
            double col0Width = Math.Round(48 * scale);

            foreach (var group in groups)
            {
                var itemBorder = new Border
                {
                    Height = rowHeight,
                    Padding = new Thickness(14, 0, 14, 0),
                    Background = new SolidColorBrush(Colors.White),
                    IsTapEnabled = true
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col0Width) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var avaGrid = new Grid { Width = avatarSize, Height = avatarSize, VerticalAlignment = VerticalAlignment.Center };
                avaGrid.Children.Add(new Ellipse { Fill = group.AvatarColor ?? _activeBlue });
                avaGrid.Children.Add(new TextBlock { Text = group.Initial ?? "N", Foreground = new SolidColorBrush(Colors.White), FontSize = Math.Round(17 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                if (group.AvatarImageBrush != null) avaGrid.Children.Add(new Ellipse { Fill = group.AvatarImageBrush });
                Grid.SetColumn(avaGrid, 0);
                grid.Children.Add(avaGrid);

                var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
                txtStack.Children.Add(new TextBlock { Text = group.Name ?? "Nhóm", FontSize = Math.Round(15 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis });
                txtStack.Children.Add(new TextBlock { Text = group.LastText ?? "", FontSize = Math.Round(13 * scale, 1), Foreground = _textSub, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) });
                Grid.SetColumn(txtStack, 1);
                grid.Children.Add(txtStack);

                var timeTxt = new TextBlock { Text = group.TimeText ?? "", FontSize = Math.Round(11.5 * scale, 1), Foreground = _textMuted, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(timeTxt, 2);
                grid.Children.Add(timeTxt);

                var div = new Border { BorderBrush = ColorBrush(0xFF, 0xF0, 0xF0, 0xF0), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(56, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
                Grid.SetColumnSpan(div, 3);
                grid.Children.Add(div);

                itemBorder.Child = grid;
                itemBorder.Tapped += (s, e) => NavigationManager.NavigateTo(new ChatPage(group.Key));
                _groupsStack.Children.Add(itemBorder);
            }
        }

        private Grid CreateDiscoveryTab()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var grid = new Grid { Background = new SolidColorBrush(Colors.White) };
            var scroll = new ScrollViewer { Padding = new Thickness(Math.Round(14 * scale)) };
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Khám phá",
                FontSize = Math.Round(22 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                Margin = new Thickness(Math.Round(2 * scale), Math.Round(2 * scale), 0, Math.Round(12 * scale))
            });

            // 1. "Gợi ý kết bạn" Card (matching WP10 & modern Zalo)
            var recBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = ColorBrush(0xFF, 0xE6, 0xEB, 0xF1),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(Math.Round(12 * scale)),
                Padding = new Thickness(Math.Round(14 * scale)),
                Margin = new Thickness(0, 0, 0, Math.Round(14 * scale))
            };
            var recStack = new StackPanel();

            var recHeaderGrid = new Grid { Margin = new Thickness(Math.Round(2 * scale), 0, 0, Math.Round(4 * scale)) };
            recHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            recHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var recTitle = new TextBlock
            {
                Text = "Gợi ý kết bạn",
                FontSize = Math.Round(17 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = ColorBrush(0xFF, 0x17, 0x21, 0x2B),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(recTitle, 0);
            recHeaderGrid.Children.Add(recTitle);

            var recRefreshBtn = new TextBlock
            {
                Text = "Làm mới",
                FontSize = Math.Round(13.5 * scale, 1),
                Foreground = _activeBlue,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(Math.Round(8 * scale), Math.Round(4 * scale), Math.Round(8 * scale), Math.Round(4 * scale))
            };
            recRefreshBtn.Tapped += (s, e) => RefreshRecommendations();
            Grid.SetColumn(recRefreshBtn, 1);
            recHeaderGrid.Children.Add(recRefreshBtn);
            recStack.Children.Add(recHeaderGrid);

            _recommendationsStatusText = new TextBlock
            {
                Text = "Chạm để tải gợi ý từ Zalo",
                FontSize = Math.Round(13 * scale, 1),
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(Math.Round(2 * scale), Math.Round(2 * scale), Math.Round(2 * scale), Math.Round(8 * scale))
            };
            recStack.Children.Add(_recommendationsStatusText);

            _recommendationsStack = new StackPanel();
            recStack.Children.Add(_recommendationsStack);

            recBorder.Child = recStack;
            stack.Children.Add(recBorder);

            // 2. "TIỆN ÍCH THIẾT YẾU" Section (matching WP10)
            stack.Children.Add(new TextBlock
            {
                Text = "TIỆN ÍCH THIẾT YẾU",
                FontSize = Math.Round(12.5 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _inactiveGray,
                Margin = new Thickness(Math.Round(4 * scale), Math.Round(6 * scale), 0, Math.Round(8 * scale))
            });

            var utilityBorder = new Border
            {
                Background = ColorBrush(0xFF, 0xFA, 0xFA, 0xFA),
                BorderBrush = ColorBrush(0xFF, 0xE5, 0xE7, 0xEB),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(Math.Round(12 * scale)),
                Margin = new Thickness(0, 0, 0, Math.Round(14 * scale))
            };
            var utilityStack = new StackPanel();

            // Row 1: Tạo nhóm mới -> CreateGroupPage
            var createGroupRow = CreateUtilityRow("ms-appx:///Assets/Icons/ic_group_action_blue.png", "Tạo nhóm mới", "Trò chuyện nhóm công việc hoặc bạn bè", Color.FromArgb(0xFF, 0xEF, 0xF6, 0xFF), scale);
            createGroupRow.Tapped += (s, e) => NavigationManager.NavigateTo(new CreateGroupPage());
            utilityStack.Children.Add(createGroupRow);

            utilityStack.Children.Add(new Border { Height = 1, Background = ColorBrush(0xFF, 0xF1, 0xF5, 0xF9), Margin = new Thickness(Math.Round(56 * scale), 0, 0, 0) });

            // Row 2: Tìm và thêm bạn -> AddFriendPage
            var addFriendRow = CreateUtilityRow("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png", "Tìm và thêm bạn", "Tìm bạn bằng số điện thoại hoặc Zalo ID", Color.FromArgb(0xFF, 0xEC, 0xFD, 0xF5), scale);
            addFriendRow.Tapped += (s, e) => NavigationManager.NavigateTo(new AddFriendPage());
            utilityStack.Children.Add(addFriendRow);

            utilityBorder.Child = utilityStack;
            stack.Children.Add(utilityBorder);

            scroll.Content = stack;
            grid.Children.Add(scroll);
            return grid;
        }

        private Border CreateUtilityRow(string iconUri, string title, string subtitle, Color bg, double scale)
        {
            var border = new Border
            {
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(12 * scale), Math.Round(14 * scale), Math.Round(12 * scale)),
                Background = new SolidColorBrush(Colors.Transparent),
                IsTapEnabled = true
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBox = new Border
            {
                Background = new SolidColorBrush(bg),
                Width = Math.Round(44 * scale),
                Height = Math.Round(44 * scale),
                CornerRadius = new CornerRadius(Math.Round(22 * scale)),
                Margin = new Thickness(0, 0, Math.Round(12 * scale), 0)
            };
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = Math.Round(24 * scale),
                Height = Math.Round(24 * scale),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = img;
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txtStack.Children.Add(new TextBlock { Text = title, FontSize = Math.Round(15.5 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = ColorBrush(0xFF, 0x1E, 0x29, 0x3B) });
            txtStack.Children.Add(new TextBlock { Text = subtitle, FontSize = Math.Round(12.5 * scale, 1), Foreground = ColorBrush(0xFF, 0x64, 0x74, 0x8B), Margin = new Thickness(0, Math.Round(2 * scale), 0, 0) });
            Grid.SetColumn(txtStack, 1);
            grid.Children.Add(txtStack);

            var chevron = new TextBlock
            {
                Text = "›",
                FontSize = Math.Round(20 * scale, 1),
                Foreground = ColorBrush(0xFF, 0x94, 0xA3, 0xB8),
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

        private async void RefreshRecommendations(bool force = false)
        {
            if ((_recommendationsLoaded && !force) || _recommendationsLoading || ZaloApp.Session == null || _recommendationsStack == null) return;
            _recommendationsLoading = true;
            if (_recommendationsStatusText != null) _recommendationsStatusText.Text = "Đang tải gợi ý kết bạn…";
            try
            {
                double scale = ZaloSettingsService.Current.UiScaleFactor;
                var suggested = await ZaloAuth.GetRecommendedFriendsAsync(ZaloApp.Session, CancellationToken.None);
                var existingIds = new HashSet<string>((ZaloApp.Friends ?? new List<ZaloFriend>()).Select(f => f.UserId).Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
                var recs = suggested.Where(f => !existingIds.Contains(f.UserId)).GroupBy(f => f.UserId).Select(g => g.First()).Take(15).ToList();

                _recommendationsStack.Children.Clear();
                if (recs.Count == 0)
                {
                    if (_recommendationsStatusText != null) _recommendationsStatusText.Text = "Chưa có gợi ý kết bạn mới.";
                }
                else
                {
                    if (_recommendationsStatusText != null) _recommendationsStatusText.Text = $"Gợi ý cho bạn ({recs.Count}):";
                    foreach (var rec in recs)
                    {
                        var recItem = CreateRecommendationItem(rec, scale);
                        _recommendationsStack.Children.Add(recItem);
                    }
                }
                _recommendationsLoaded = true;
            }
            catch (Exception ex)
            {
                if (_recommendationsStatusText != null) _recommendationsStatusText.Text = "Không thể tải gợi ý lúc này.";
                ZaloLog.Add("RefreshRecommendations error: " + ex.Message);
            }
            finally
            {
                _recommendationsLoading = false;
            }
        }

        private Border CreateRecommendationItem(ZaloFriend friend, double scale)
        {
            var border = new Border
            {
                Padding = new Thickness(0, Math.Round(8 * scale), 0, Math.Round(8 * scale)),
                Background = new SolidColorBrush(Colors.White)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(48 * scale)) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            double avaSize = Math.Round(40 * scale);
            var avaGrid = new Grid { Width = avaSize, Height = avaSize, VerticalAlignment = VerticalAlignment.Center };
            avaGrid.Children.Add(new Ellipse { Fill = friend.AvatarColor ?? _activeBlue });
            avaGrid.Children.Add(new TextBlock { Text = friend.Initial ?? "Z", Foreground = new SolidColorBrush(Colors.White), FontSize = Math.Round(16 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            if (friend.AvatarImageBrush != null) avaGrid.Children.Add(new Ellipse { Fill = friend.AvatarImageBrush });
            Grid.SetColumn(avaGrid, 0);
            grid.Children.Add(avaGrid);

            var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Math.Round(8 * scale), 0, Math.Round(8 * scale), 0) };
            txtStack.Children.Add(new TextBlock { Text = friend.Name ?? "Người dùng Zalo", FontSize = Math.Round(15.5 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = ColorBrush(0xFF, 0x17, 0x21, 0x2B), TextTrimming = TextTrimming.CharacterEllipsis });
            if (!string.IsNullOrEmpty(friend.Subtitle))
            {
                txtStack.Children.Add(new TextBlock { Text = friend.Subtitle, FontSize = Math.Round(12.5 * scale, 1), Foreground = _textSub, TextTrimming = TextTrimming.CharacterEllipsis });
            }
            Grid.SetColumn(txtStack, 1);
            grid.Children.Add(txtStack);

            var addBtn = new Border
            {
                Background = ColorBrush(0xFF, 0xEA, 0xF3, 0xFF),
                CornerRadius = new CornerRadius(Math.Round(16 * scale)),
                Padding = new Thickness(Math.Round(14 * scale), Math.Round(6 * scale), Math.Round(14 * scale), Math.Round(6 * scale)),
                VerticalAlignment = VerticalAlignment.Center,
                IsTapEnabled = true
            };
            var addBtnText = new TextBlock { Text = "Kết bạn", FontSize = Math.Round(13.5 * scale, 1), FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
            addBtn.Child = addBtnText;
            addBtn.Tapped += async (s, e) =>
            {
                try
                {
                    addBtn.Opacity = 0.5;
                    await ZaloAuth.SendFriendRequestAsync(ZaloApp.Session, friend.UserId, "Xin chào, mình kết bạn nhé!", CancellationToken.None);
                    addBtnText.Text = "Đã gửi";
                    addBtn.Background = ColorBrush(0xFF, 0xF0, 0xF0, 0xF0);
                    addBtnText.Foreground = _inactiveGray;
                    addBtn.IsTapEnabled = false;
                }
                catch (Exception ex)
                {
                    addBtn.Opacity = 1.0;
                    var md = new MessageDialog("Không thể gửi lời mời kết bạn: " + ex.Message, "Lỗi kết bạn");
                    await md.ShowAsync();
                }
            };
            Grid.SetColumn(addBtn, 2);
            grid.Children.Add(addBtn);

            border.Child = grid;
            return border;
        }

        private Grid CreateProfileTab()
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };
            var scroll = new ScrollViewer { Padding = new Thickness(0) };
            var stack = new StackPanel { Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)) };

            // 1. TOP WHITE PROFILE CARD (Horizontal Layout matching modern Zalo Android)
            var topCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Padding = new Thickness(16, 14, 16, 14)
            };
            var topCardStack = new StackPanel();
            topCard.Child = topCardStack;

            var profileRow = new Grid();
            profileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            profileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            profileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarGrid = new Grid { Width = 60, Height = 60, VerticalAlignment = VerticalAlignment.Center };
            _profileAvatarColor = new Ellipse { Width = 60, Height = 60, Fill = _activeBlue };
            _profileInitial = new TextBlock
            {
                Text = "Z",
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileAvatarImage = new Ellipse
            {
                Width = 60,
                Height = 60
            };
            avatarGrid.Children.Add(_profileAvatarColor);
            avatarGrid.Children.Add(_profileInitial);
            avatarGrid.Children.Add(_profileAvatarImage);
            Grid.SetColumn(avatarGrid, 0);
            profileRow.Children.Add(avatarGrid);

            var infoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 8, 0)
            };
            _profileName = new TextBlock
            {
                Text = "Người dùng Zalo",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 230
            };
            var subText = new TextBlock
            {
                Text = "Xem trang cá nhân ›",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B)),
                Margin = new Thickness(0, 3, 0, 0)
            };
            infoStack.Children.Add(_profileName);
            infoStack.Children.Add(subText);
            Grid.SetColumn(infoStack, 1);
            profileRow.Children.Add(infoStack);

            var qrBtn = CreateTouchBorder(36, 36);
            var qrText = new TextBlock
            {
                Text = "\uE14A",
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x68, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            qrBtn.Child = qrText;
            qrBtn.Tapped += (s, e) => NavigationManager.NavigateTo(new ProfilePage());
            Grid.SetColumn(qrBtn, 2);
            profileRow.Children.Add(qrBtn);

            var profileCardTap = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Child = profileRow
            };
            profileCardTap.PointerPressed += (s, e) => profileCardTap.Opacity = 0.6;
            profileCardTap.PointerReleased += (s, e) => profileCardTap.Opacity = 1.0;
            profileCardTap.PointerCanceled += (s, e) => profileCardTap.Opacity = 1.0;
            profileCardTap.Tapped += (s, e) => NavigationManager.NavigateTo(new ProfilePage());
            topCardStack.Children.Add(profileCardTap);

            // Status Pill Button
            var statusPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF8, 0xF9, 0xFB)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            var statusIcon = new TextBlock
            {
                Text = "\uD83D\uDCAD",
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileStatusPreview = new TextBlock
            {
                Text = "Cập nhật trạng thái...",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x61, 0x6A, 0x73)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 260,
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

            stack.Children.Add(topCard);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 2. SECTION 1: Tiện ích cá nhân ("Cloud của tôi" & "zStyle")
            var sec1 = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            
            var rowCloud = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_cloud_blue.png", "Cloud của tôi", "Lưu trữ tin nhắn, ảnh, file quan trọng của bạn", minHeight: 68);
            rowCloud.Tapped += (s, e) =>
            {
                var myUid = ZaloApp.Session?.Uid;
                if (!string.IsNullOrEmpty(myUid))
                {
                    NavigationManager.NavigateTo(new ChatPage(myUid));
                }
            };
            sec1.Children.Add(rowCloud);

            sec1.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF1, 0xF2)), Margin = new Thickness(62, 0, 0, 0) });

            var rowStyle = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_photo_gray.png", "zStyle - Cá nhân hóa", "Hình nền trò chuyện, âm báo cuộc gọi", minHeight: 68);
            rowStyle.Tapped += (s, e) =>
            {
                NavigationManager.NavigateTo(new SettingsPage());
            };
            sec1.Children.Add(rowStyle);
            stack.Children.Add(sec1);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 3. SECTION 2: Dữ liệu & Bảo mật
            var sec2 = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            var rowStorage = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_storage_blue.png", "Dung lượng và dữ liệu", "Quản lý bộ nhớ đệm và tệp tin tải về", minHeight: 68);
            rowStorage.Tapped += async (s, e) =>
            {
                var sheet = new Common.ZaloActionSheet("Dung lượng và dữ liệu");
                sheet.AddAction("Xóa bộ nhớ đệm (Giải phóng dung lượng)", async () =>
                {
                    try
                    {
                        var temp = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                        var files = await temp.GetFilesAsync();
                        foreach (var f in files)
                        {
                            try { await f.DeleteAsync(); } catch { }
                        }
                        await new MessageDialog("Đã dọn dẹp sạch bộ nhớ đệm thành công!", "Dung lượng").ShowAsync();
                    }
                    catch (Exception ex)
                    {
                        await new MessageDialog("Lỗi xóa bộ nhớ đệm: " + ex.Message, "Thông báo").ShowAsync();
                    }
                }, isDestructive: true);
                sheet.AddAction("Dọn dẹp ảnh và video đã lưu tạm", async () =>
                {
                    try
                    {
                        var local = Windows.Storage.ApplicationData.Current.LocalFolder;
                        var subFolder = await local.CreateFolderAsync("ImageCache", Windows.Storage.CreationCollisionOption.OpenIfExists);
                        var imgs = await subFolder.GetFilesAsync();
                        foreach (var img in imgs)
                        {
                            try { await img.DeleteAsync(); } catch { }
                        }
                        await new MessageDialog("Đã xóa ảnh tạm thành công!", "Dung lượng").ShowAsync();
                    }
                    catch (Exception ex)
                    {
                        await new MessageDialog("Lỗi dọn dẹp ảnh: " + ex.Message, "Thông báo").ShowAsync();
                    }
                }, isDestructive: true);
                sheet.Show();
            };
            sec2.Children.Add(rowStorage);

            sec2.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF1, 0xF2)), Margin = new Thickness(62, 0, 0, 0) });

            string phoneNum = ZaloApp.Session?.PhoneNumber;
            string maskedPhone = !string.IsNullOrEmpty(phoneNum) && phoneNum.Length >= 7 
                ? phoneNum.Substring(0, 3) + " **** " + phoneNum.Substring(phoneNum.Length - 3) 
                : "Đã kích hoạt bảo mật";
            var rowSecurity = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_security_blue.png", "Tài khoản và bảo mật", $"Số điện thoại: {maskedPhone} · Đã bảo vệ", minHeight: 68);
            rowSecurity.Tapped += async (s, e) =>
            {
                var session = ZaloApp.Session;
                string msg = $"• Tên tài khoản: {session?.DisplayName ?? "Người dùng Zalo"}\n" +
                             $"• Số điện thoại: {session?.PhoneNumber ?? "Chưa liên kết"}\n" +
                             $"• Zalo UID: {session?.Uid ?? "N/A"}\n" +
                             $"• Thiết bị: Lumia 640 XL Dual SIM\n" +
                             $"• Trạng thái bảo mật: An toàn (Token hoạt động)";
                await new MessageDialog(msg, "Tài khoản và bảo mật").ShowAsync();
            };
            sec2.Children.Add(rowSecurity);

            sec2.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF1, 0xF2)), Margin = new Thickness(62, 0, 0, 0) });

            var rowPrivacy = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/ic_lock_clean_blue.png", "Quyền riêng tư", "Trạng thái truy cập, tin nhắn từ người lạ", minHeight: 68);
            rowPrivacy.Tapped += async (s, e) =>
            {
                var sheet = new Common.ZaloActionSheet("Quyền riêng tư");
                sheet.AddAction("Trạng thái truy cập: Đang hiển thị", () => { });
                sheet.AddAction("Nhận tin nhắn từ người lạ: Cho phép", () => { });
                sheet.AddAction("Nhận cuộc gọi từ người lạ: Cho phép", () => { });
                sheet.Show();
            };
            sec2.Children.Add(rowPrivacy);
            stack.Children.Add(sec2);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 4. SECTION 3: Cài đặt chung
            var sec3 = new StackPanel { Background = new SolidColorBrush(Colors.White) };
            var rowSettings = CreateSettingRowWithBadge("ms-appx:///Assets/Icons/Android/Chat/icon_viewfull_kb_more.png", "Cài đặt & Giao diện Lumia", "Tỷ lệ thu phóng UI, cỡ chữ trò chuyện, âm báo", minHeight: 68);
            rowSettings.Tapped += (s, e) => NavigationManager.NavigateTo(new SettingsPage());
            sec3.Children.Add(rowSettings);
            stack.Children.Add(sec3);

            // Divider 8px
            stack.Children.Add(new Border { Height = 8, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xF3, 0xF5)) });

            // 5. LOGOUT SECTION (Height 44, red border #F0C8C8, padding 18,18,18,20)
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
                Text = "Đăng xuất tài khoản",
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

                double tabScale = ZaloSettingsService.Current.UiScaleFactor;
                var img = new Image
                {
                    Width = Math.Round(28 * tabScale),
                    Height = Math.Round(28 * tabScale),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _tabIcons[i] = img;
                stack.Children.Add(img);

                var txt = new TextBlock
                {
                    Text = titles[i],
                    FontSize = Math.Round(12 * tabScale, 1),
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

            // Instant zero-allocation icon swap
            _tabIcons[0].Source = index == 0 ? _iconMsgActive : _iconMsgInactive;
            _tabIcons[1].Source = index == 1 ? _iconContactActive : _iconContactInactive;
            _tabIcons[2].Source = index == 2 ? _iconMoreActive : _iconMoreInactive;
            _tabIcons[3].Source = index == 3 ? _iconMeActive : _iconMeInactive;

            for (int i = 0; i < 4; i++)
            {
                bool active = i == index;
                _tabLabels[i].Foreground = active ? _activeBlue : _inactiveGray;
                _tabLabels[i].FontWeight = active ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal;
            }

            if (_plusBtn != null) _plusBtn.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_contactGroupsHeaderBtn != null) _contactGroupsHeaderBtn.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (_contactRequestsHeaderBtn != null) _contactRequestsHeaderBtn.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (_profileSettingsHeaderBtn != null) _profileSettingsHeaderBtn.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;

            // Only perform heavy XAML tree rebuild if dirty or never rendered
            if (index == 0 && (!_conversationsRendered || _conversationsDirty))
            {
                RefreshConversations();
            }
            else if (index == 1)
            {
                UpdateQuickContacts();
                if (!_friendsRendered || _friendsDirty) RefreshFriends();
            }
            else if (index == 2)
            {
                RefreshRecommendations();
            }
            else if (index == 3 && !_profileRendered)
            {
                RefreshProfile();
                _profileRendered = true;
            }
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

            _conversationsDirty = true;
            RefreshConversations();
        }

        private void RefreshConversations()
        {
            _conversationStack.Children.Clear();
            string query = _searchBox?.Text?.Trim();

            var otherKeys = new HashSet<string>(
                ZaloSettingsService.Current.GetOtherConversationKeys(ZaloApp.Session?.Uid),
                StringComparer.Ordinal);

            if (_strangerInboxBanner != null)
            {
                int strangerCount = ZaloApp.FriendsLoaded
                    ? ZaloApp.Conversations.Count(c => c != null && !c.IsGroup && !ZaloApp.IsArchived(c)
                        && !otherKeys.Contains(c.Key) && !ZaloApp.IsFriend(c.ThreadId))
                    : 0;
                _strangerInboxBanner.Visibility = (!_isOtherSubTab && string.IsNullOrEmpty(query) && strangerCount > 0) ? Visibility.Visible : Visibility.Collapsed;
                if (_strangerBadgeText != null) _strangerBadgeText.Text = strangerCount > 99 ? "99+" : strangerCount.ToString();
            }

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
                if (!_convItemCache.TryGetValue(conv.Key, out var holder))
                {
                    holder = CreateConversationItem(conv);
                    _convItemCache[conv.Key] = holder;
                }
                else
                {
                    holder.LastTextBlock.Text = conv.LastText ?? string.Empty;
                    holder.TimeBlock.Text = conv.TimeText ?? string.Empty;
                    holder.UnreadText.Text = conv.UnreadLabel;
                    holder.UnreadBadge.Visibility = conv.UnreadVisibility;
                    holder.NameBlock.Text = conv.Name ?? "Trò chuyện";
                    if (holder.PinIcon != null) holder.PinIcon.Visibility = conv.IsPinned ? Visibility.Visible : Visibility.Collapsed;
                    if (holder.MuteIcon != null) holder.MuteIcon.Visibility = conv.IsMuted ? Visibility.Visible : Visibility.Collapsed;
                    if (holder.AvatarImageEllipse != null && conv.AvatarImageBrush != null)
                        holder.AvatarImageEllipse.Fill = conv.AvatarImageBrush;
                }
                _conversationStack.Children.Add(holder.Container);
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
            _conversationsRendered = true;
            _conversationsDirty = false;
        }

        // EXACT 100% RECONSTRUCTION OF CONVERSATION ITEM IN WP10 - PURE BORDER WITH SILKY TOUCH
        private ConversationItemViewHolder CreateConversationItem(ZaloConversation conv)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            double rowHeight = Math.Round(80 * scale);
            double avatarSize = Math.Round(52 * scale);
            double col0Width = Math.Round(60 * scale);

            var itemBorder = new Border
            {
                Height = rowHeight,
                Padding = new Thickness(14, 0, 14, 0),
                Background = new SolidColorBrush(Colors.White)
            };
            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col0Width) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(80 * scale)) });

            // 1. Column 0: Avatar with Group badge
            var avatarGrid = new Grid { Width = avatarSize, Height = avatarSize, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse
            {
                Width = avatarSize,
                Height = avatarSize,
                Fill = conv.AvatarColor ?? _activeBlue
            };
            var initial = new TextBlock
            {
                Text = conv.Initial ?? "?",
                FontSize = Math.Round(19 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);

            var imgEllipse = new Ellipse
            {
                Width = avatarSize,
                Height = avatarSize,
                Fill = conv.AvatarImageBrush ?? new SolidColorBrush(Colors.Transparent)
            };
            avatarGrid.Children.Add(imgEllipse);

            if (conv.IsGroup)
            {
                var groupBadge = new Border
                {
                    Width = Math.Round(20 * scale),
                    Height = Math.Round(20 * scale),
                    CornerRadius = new CornerRadius(10 * scale),
                    Background = ColorBrush(0xFF, 0xEA, 0xF3, 0xFC),
                    BorderBrush = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(1.5),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                groupBadge.Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_blue.png")),
                    Width = Math.Round(12 * scale),
                    Height = Math.Round(12 * scale)
                };
                avatarGrid.Children.Add(groupBadge);
            }

            Grid.SetColumn(avatarGrid, 0);
            itemGrid.Children.Add(avatarGrid);

            // 2. Column 1: Middle Info (Title + Last Text)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0) };
            var titleLine = new StackPanel { Orientation = Orientation.Horizontal };

            if (conv.IsGroup)
            {
                var groupIconBorder = new Border
                {
                    Width = Math.Round(18 * scale),
                    Height = Math.Round(18 * scale),
                    CornerRadius = new CornerRadius(5),
                    Background = ColorBrush(0xFF, 0xEE, 0xF4, 0xFA),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                groupIconBorder.Child = new Image
                {
                    Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_blue.png")),
                    Width = Math.Round(11 * scale),
                    Height = Math.Round(11 * scale)
                };
                titleLine.Children.Add(groupIconBorder);
            }

            var pinIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_pin_clean_gray.png")),
                Width = Math.Round(14 * scale),
                Height = Math.Round(14 * scale),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = conv.IsPinned ? Visibility.Visible : Visibility.Collapsed
            };
            titleLine.Children.Add(pinIcon);

            var nameBlock = new TextBlock
            {
                Text = conv.Name ?? "Trò chuyện",
                FontSize = Math.Round(17 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLine.Children.Add(nameBlock);

            var muteIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_mute_clean_gray.png")),
                Width = Math.Round(14 * scale),
                Height = Math.Round(14 * scale),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = conv.IsMuted ? Visibility.Visible : Visibility.Collapsed
            };
            titleLine.Children.Add(muteIcon);

            textStack.Children.Add(titleLine);

            var lastTextBlock = new TextBlock
            {
                Text = conv.LastText ?? string.Empty,
                FontSize = Math.Round(14 * scale, 1),
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
                FontSize = Math.Round(12 * scale, 1),
                Foreground = _textMuted,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            rightStack.Children.Add(timeBlock);

            var unreadBorder = new Border
            {
                Background = ColorBrush(0xFF, 0xE8, 0x4B, 0x50),
                CornerRadius = new CornerRadius(11),
                MinWidth = 22,
                Height = 22,
                Padding = new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 7, 0, 0),
                Visibility = conv.UnreadVisibility
            };
            var unreadText = new TextBlock
            {
                Text = conv.UnreadLabel,
                FontSize = 11.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            unreadBorder.Child = unreadText;
            rightStack.Children.Add(unreadBorder);

            Grid.SetColumn(rightStack, 2);
            itemGrid.Children.Add(rightStack);

            // 4. Bottom Divider (Margin 64px from left so it starts after avatar)
            var divider = new Border
            {
                BorderBrush = ColorBrush(0xFF, 0xEA, 0xEC, 0xEF),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(Math.Round(60 * scale), 0, 0, 0),
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

            itemBorder.Holding += (s, e) =>
            {
                if (e.HoldingState != Windows.UI.Input.HoldingState.Started) return;
                var sheet = new Common.ZaloActionSheet(conv.Name ?? "Trò chuyện");
                sheet.AddAction(conv.IsPinned ? "Bỏ ghim" : "Ghim lên đầu", () =>
                {
                    conv.IsPinned = !conv.IsPinned;
                    RefreshConversations();
                });
                sheet.AddAction(conv.IsMuted ? "Bật thông báo" : "Tắt thông báo", async () =>
                {
                    await ZaloApp.SetMutedAsync(conv, !conv.IsMuted);
                    RefreshConversations();
                });
                sheet.AddAction(_isOtherSubTab ? "Chuyển sang Ưu tiên" : "Chuyển sang Khác", () =>
                {
                    var keys = ZaloSettingsService.Current.GetOtherConversationKeys(ZaloApp.Session?.Uid);
                    keys.RemoveAll(k => string.Equals(k, conv.Key, StringComparison.Ordinal));
                    if (!_isOtherSubTab) keys.Add(conv.Key);
                    ZaloSettingsService.Current.SetOtherConversationKeys(ZaloApp.Session?.Uid, keys);
                    RefreshConversations();
                });
                if (conv.Unread > 0)
                {
                    sheet.AddAction("Đánh dấu đã đọc", () =>
                    {
                        conv.Unread = 0;
                        RefreshConversations();
                    });
                }
                sheet.AddAction("Xóa cuộc trò chuyện", async () =>
                {
                    var confirm = new MessageDialog("Bạn có chắc chắn muốn xóa cuộc trò chuyện này?", "Xác nhận xóa");
                    confirm.Commands.Add(new UICommand("Xóa", c =>
                    {
                        ZaloApp.Conversations.Remove(conv);
                        _convItemCache.Remove(conv.Key);
                        RefreshConversations();
                    }));
                    confirm.Commands.Add(new UICommand("Hủy"));
                    await confirm.ShowAsync();
                }, isDestructive: true);
                sheet.Show();
            };

            return new ConversationItemViewHolder
            {
                Container = itemBorder,
                NameBlock = nameBlock,
                LastTextBlock = lastTextBlock,
                TimeBlock = timeBlock,
                UnreadBadge = unreadBorder,
                UnreadText = unreadText,
                PinIcon = pinIcon,
                MuteIcon = muteIcon,
                AvatarImageEllipse = imgEllipse
            };
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
            int renderLimit = string.IsNullOrEmpty(query) ? 45 : 300;
            var friendList = friends.ToList();
            for (int i = 0; i < friendList.Count; i++)
            {
                if (count >= renderLimit)
                {
                    int remaining = friendList.Count - count;
                    var loadMoreBorder = new Border
                    {
                        Height = 44,
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA)),
                        Margin = new Thickness(16, 8, 16, 12),
                        CornerRadius = new CornerRadius(8)
                    };
                    var loadMoreText = new TextBlock
                    {
                        Text = $"Hiển thị thêm {remaining} bạn bè...",
                        FontSize = 13.5,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _activeBlue,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    loadMoreBorder.Child = loadMoreText;
                    loadMoreBorder.Tapped += (s, e) =>
                    {
                        _friendStack.Children.Remove(loadMoreBorder);
                        for (int j = count; j < friendList.Count; j++)
                        {
                            var f = friendList[j];
                            string k = f.UserId ?? f.PhoneNumber ?? f.Name ?? j.ToString();
                            if (!_friendItemCache.TryGetValue(k, out var itm))
                            {
                                itm = CreateFriendItem(f);
                                _friendItemCache[k] = itm;
                            }
                            _friendStack.Children.Add(itm);
                        }
                    };
                    _friendStack.Children.Add(loadMoreBorder);
                    break;
                }

                var friend = friendList[i];
                string fKey = friend.UserId ?? friend.PhoneNumber ?? friend.Name ?? count.ToString();
                if (!_friendItemCache.TryGetValue(fKey, out var item))
                {
                    item = CreateFriendItem(friend);
                    _friendItemCache[fKey] = item;
                }
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

            _friendsRendered = true;
            _friendsDirty = false;
        }

        private FrameworkElement CreateFriendItem(ZaloFriend friend)
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            double rowHeight = Math.Round(64 * scale);
            double avatarSize = Math.Round(44 * scale);
            double col0Width = Math.Round(50 * scale);

            var itemBorder = new Border
            {
                Height = rowHeight,
                Padding = new Thickness(16, 0, 16, 0),
                Background = new SolidColorBrush(Colors.White),
                IsTapEnabled = true,
                IsHoldingEnabled = true
            };
            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col0Width) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarGrid = new Grid { Width = avatarSize, Height = avatarSize, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = avatarSize, Height = avatarSize, Fill = friend.AvatarColor ?? _activeBlue };
            var initial = new TextBlock
            {
                Text = friend.Initial ?? "?",
                FontSize = Math.Round(17 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);

            if (!string.IsNullOrEmpty(friend.Avatar))
            {
                var imgBrush = friend.AvatarImageBrush ?? ZaloAvatar.ImageFor(friend.Avatar);
                if (imgBrush != null)
                {
                    avatarGrid.Children.Add(new Ellipse { Width = avatarSize, Height = avatarSize, Fill = imgBrush });
                }
            }

            Grid.SetColumn(avatarGrid, 0);
            itemGrid.Children.Add(avatarGrid);

            var nameBlock = new TextBlock
            {
                Text = friend.Name ?? "Bạn bè",
                FontSize = Math.Round(16 * scale, 1),
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

            // DIRECTLY OPEN CHAT ON TAP! (Matches WP10 & official Zalo)
            itemBorder.Tapped += (s, e) =>
            {
                var conv = ZaloApp.OpenFriend(friend);
                if (conv != null)
                {
                    NavigationManager.NavigateTo(new ChatPage(conv.Key));
                }
            };

            // HOLD TO SHOW OPTIONS (Message or View Profile)
            itemBorder.Holding += (s, e) =>
            {
                var sheet = new Common.ZaloActionSheet(friend.Name ?? "Bạn bè");
                sheet.AddAction("Nhắn tin", () =>
                {
                    var conv = ZaloApp.OpenFriend(friend);
                    if (conv != null) NavigationManager.NavigateTo(new ChatPage(conv.Key));
                });
                sheet.AddAction("Xem trang cá nhân", () =>
                {
                    NavigationManager.NavigateTo(new FriendProfilePage(friend));
                });
                sheet.Show();
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
            if (_selectedTab == 0) { _conversationsDirty = true; RefreshConversations(); }
            else if (_selectedTab == 1) { _friendsDirty = true; RefreshFriends(); }
        }

        private void Conversations_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_refreshPending) return;
            _refreshPending = true;
            var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                await Task.Delay(200);
                _refreshPending = false;
                _conversationsDirty = true;
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
