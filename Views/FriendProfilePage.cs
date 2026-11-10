using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
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
    public sealed class FriendProfilePage : Page
    {
        private readonly ZaloFriend _friend;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x84, 0x90));

        private TextBlock _nameText;
        private TextBlock _statusText;
        private TextBlock _onlineText;
        private TextBlock _userIdText;
        private TextBlock _phoneText;
        private Grid _phoneRow;
        private TextBlock _birthdayText;
        private Grid _birthdayRow;
        private TextBlock _genderText;
        private Grid _genderRow;
        private Grid _avatarGrid;
        private TextBlock _blockButtonText;

        public FriendProfilePage(ZaloFriend friend)
        {
            _friend = friend;
            BuildUI();
            this.Loaded += async (s, e) => await LoadFriendProfileAsync();
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Header (Gradient)
            var headerBorder = new Border { Padding = new Thickness(6, 0, 10, 0) };
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
            backBtn.Child = new TextBlock
            {
                Text = "←",
                FontSize = 22,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            backBtn.Tapped += (s, e) => NavigationManager.GoBack();
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var title = new TextBlock
            {
                Text = "Thông tin cá nhân",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Body
            var scroll = new ScrollViewer { Padding = new Thickness(14) };
            var stack = new StackPanel();

            // Card 1: Main Profile Card
            var card1 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 20, 18, 18),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card1Stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            _avatarGrid = new Grid { Width = 92, Height = 92, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse
            {
                Width = 92,
                Height = 92,
                Fill = _friend?.AvatarColor ?? _activeBlue
            };
            var initial = new TextBlock
            {
                Text = _friend?.Initial ?? "?",
                FontSize = 34,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _avatarGrid.Children.Add(avatarEllipse);
            _avatarGrid.Children.Add(initial);

            if (_friend != null && !string.IsNullOrEmpty(_friend.Avatar))
            {
                var brush = ZaloAvatar.ImageFor(_friend.Avatar);
                if (brush != null)
                {
                    _avatarGrid.Children.Add(new Ellipse
                    {
                        Width = 92,
                        Height = 92,
                        Fill = brush,
                        Stroke = new SolidColorBrush(Colors.White),
                        StrokeThickness = 3
                    });
                }
            }
            card1Stack.Children.Add(_avatarGrid);

            _nameText = new TextBlock
            {
                Text = _friend?.Name ?? "Người dùng Zalo",
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            card1Stack.Children.Add(_nameText);

            _statusText = new TextBlock
            {
                Text = "",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x63, 0x72, 0x82)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };
            card1Stack.Children.Add(_statusText);

            _onlineText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8A, 0x55)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 16),
                Visibility = Visibility.Collapsed
            };
            card1Stack.Children.Add(_onlineText);

            // Action Buttons (Nhắn tin, Gọi điện)
            var actionRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var chatBtn = new Border
            {
                Background = _activeBlue,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22, 9, 22, 9),
                Margin = new Thickness(0, 0, 10, 0)
            };
            chatBtn.Child = new TextBlock
            {
                Text = "💬  Nhắn tin",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            chatBtn.PointerPressed += (s, e) => chatBtn.Opacity = 0.6;
            chatBtn.PointerReleased += (s, e) => chatBtn.Opacity = 1.0;
            chatBtn.PointerCanceled += (s, e) => chatBtn.Opacity = 1.0;
            chatBtn.Tapped += (s, e) =>
            {
                if (_friend == null) return;
                var conv = ZaloApp.GetOrCreate(_friend.UserId, false);
                NavigationManager.NavigateTo(new ChatPage(conv.Key));
            };
            actionRow.Children.Add(chatBtn);

            var callBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0xB9, 0x81)),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22, 9, 22, 9)
            };
            callBtn.Child = new TextBlock
            {
                Text = "📞  Gọi thoại",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            callBtn.PointerPressed += (s, e) => callBtn.Opacity = 0.6;
            callBtn.PointerReleased += (s, e) => callBtn.Opacity = 1.0;
            callBtn.PointerCanceled += (s, e) => callBtn.Opacity = 1.0;
            callBtn.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Đang kết nối cuộc gọi thoại tới người dùng…", "Zalo Call");
                await md.ShowAsync();
            };
            actionRow.Children.Add(callBtn);

            card1Stack.Children.Add(actionRow);
            card1.Child = card1Stack;
            stack.Children.Add(card1);

            // Card 2: Personal Details
            var card2 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card2Stack = new StackPanel();

            // Zalo ID
            var idRow = CreateInfoRow("Zalo ID", _friend?.UserId ?? "");
            _userIdText = (TextBlock)((Grid)idRow).Children[1];
            card2Stack.Children.Add(idRow);

            // Phone
            card2Stack.Children.Add(CreateDivider());
            _phoneRow = (Grid)CreateInfoRow("Điện thoại", "");
            _phoneText = (TextBlock)_phoneRow.Children[1];
            _phoneRow.Visibility = Visibility.Collapsed;
            card2Stack.Children.Add(_phoneRow);

            // Birthday
            card2Stack.Children.Add(CreateDivider());
            _birthdayRow = (Grid)CreateInfoRow("Ngày sinh", "");
            _birthdayText = (TextBlock)_birthdayRow.Children[1];
            _birthdayRow.Visibility = Visibility.Collapsed;
            card2Stack.Children.Add(_birthdayRow);

            // Gender
            card2Stack.Children.Add(CreateDivider());
            _genderRow = (Grid)CreateInfoRow("Giới tính", "");
            _genderText = (TextBlock)_genderRow.Children[1];
            _genderRow.Visibility = Visibility.Collapsed;
            card2Stack.Children.Add(_genderRow);

            card2.Child = card2Stack;
            stack.Children.Add(card2);

            // Card 3: Mutual Groups (Nhóm chung)
            var card3 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card3Grid = new Grid();
            card3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            card3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card3Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var groupIcon = new TextBlock { Text = "👥", FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(groupIcon, 0);
            card3Grid.Children.Add(groupIcon);

            var groupTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            groupTextStack.Children.Add(new TextBlock { Text = "Xem nhóm chung", FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            var mutualCountText = new TextBlock { Text = "Chạm để kiểm tra nhóm cùng tham gia", FontSize = 12, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) };
            groupTextStack.Children.Add(mutualCountText);
            Grid.SetColumn(groupTextStack, 1);
            card3Grid.Children.Add(groupTextStack);

            var chevron1 = new TextBlock { Text = "›", FontSize = 20, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chevron1, 2);
            card3Grid.Children.Add(chevron1);

            card3.Child = card3Grid;
            card3.PointerPressed += (s, e) => card3.Opacity = 0.6;
            card3.PointerReleased += (s, e) => card3.Opacity = 1.0;
            card3.PointerCanceled += (s, e) => card3.Opacity = 1.0;
            card3.Tapped += async (s, e) => await ShowMutualGroupsAsync(mutualCountText);
            stack.Children.Add(card3);

            // Card 4: Friend Posts (Nhật ký / Bài đăng bạn bè)
            var card4 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card4Grid = new Grid();
            card4Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            card4Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            card4Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var postIcon = new TextBlock { Text = "📰", FontSize = 18, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(postIcon, 0);
            card4Grid.Children.Add(postIcon);

            var postTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            postTextStack.Children.Add(new TextBlock { Text = "Nhật ký bạn bè", FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            var postStatusText = new TextBlock { Text = "Chạm để xem bài đăng gần đây", FontSize = 12, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) };
            postTextStack.Children.Add(postStatusText);
            Grid.SetColumn(postTextStack, 1);
            card4Grid.Children.Add(postTextStack);

            var chevron2 = new TextBlock { Text = "›", FontSize = 20, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(chevron2, 2);
            card4Grid.Children.Add(chevron2);

            card4.Child = card4Grid;
            card4.PointerPressed += (s, e) => card4.Opacity = 0.6;
            card4.PointerReleased += (s, e) => card4.Opacity = 1.0;
            card4.PointerCanceled += (s, e) => card4.Opacity = 1.0;
            card4.Tapped += async (s, e) => await ShowFriendPostsAsync(postStatusText);
            stack.Children.Add(card4);

            // Card 5: Options (Chặn, Xóa)
            var card5 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var card5Stack = new StackPanel();

            var blockBorder = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            string uid = ZaloApp.Session?.Uid;
            bool isBlocked = uid != null && ZaloSettingsService.Current.GetBlockedUserIds(uid).Contains(_friend?.UserId);
            _blockButtonText = new TextBlock
            {
                Text = isBlocked ? "Bỏ chặn người này" : "Chặn người này",
                FontSize = 14.5,
                Foreground = _textDark
            };
            blockBorder.Child = _blockButtonText;
            blockBorder.Tapped += async (s, e) =>
            {
                if (_friend == null) return;
                var blockedIds = ZaloSettingsService.Current.GetBlockedUserIds(uid);
                if (blockedIds.Contains(_friend.UserId))
                {
                    blockedIds.Remove(_friend.UserId);
                    ZaloSettingsService.Current.SetBlockedUserIds(uid, blockedIds);
                    _blockButtonText.Text = "Chặn người này";
                    await new MessageDialog($"Đã bỏ chặn {_friend.Name}.", "Thông báo").ShowAsync();
                }
                else
                {
                    var md = new MessageDialog($"Bạn có chắc muốn chặn {_friend.Name}? Người này sẽ không thể gửi tin nhắn cho bạn.", "Chặn bạn bè");
                    md.Commands.Add(new UICommand("Chặn", cmd =>
                    {
                        blockedIds.Add(_friend.UserId);
                        ZaloSettingsService.Current.SetBlockedUserIds(uid, blockedIds);
                        _blockButtonText.Text = "Bỏ chặn người này";
                    }));
                    md.Commands.Add(new UICommand("Hủy"));
                    await md.ShowAsync();
                }
            };
            card5Stack.Children.Add(blockBorder);

            var deleteBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var deleteText = new TextBlock
            {
                Text = "Xóa khỏi danh bạ",
                FontSize = 14.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26))
            };
            deleteBorder.Child = deleteText;
            deleteBorder.Tapped += async (s, e) =>
            {
                if (_friend == null) return;
                var md = new MessageDialog($"Bạn có chắc muốn xóa {_friend.Name} khỏi danh bạ bạn bè?", "Xóa bạn bè");
                md.Commands.Add(new UICommand("Xóa", cmd =>
                {
                    ZaloApp.Friends.Remove(_friend);
                    NavigationManager.GoBack();
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            card5Stack.Children.Add(deleteBorder);

            card5.Child = card5Stack;
            stack.Children.Add(card5);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private async Task LoadFriendProfileAsync()
        {
            if (_friend == null || string.IsNullOrWhiteSpace(_friend.UserId) || ZaloApp.Session == null) return;
            try
            {
                var profile = await ZaloAuth.GetFriendProfileAsync(ZaloApp.Session, _friend.UserId, CancellationToken.None);
                if (profile != null)
                {
                    string name = First(profile, "display_name", "displayName", "zalo_name", "zaloName");
                    string avatar = First(profile, "avatar");
                    string status = First(profile, "status", "statusMessage");
                    string phone = First(profile, "phoneNumber", "phone");
                    string birthday = First(profile, "sdob", "birthday");
                    string gender = First(profile, "gender");

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _nameText.Text = name;
                        _friend.Name = name;
                    }
                    if (!string.IsNullOrWhiteSpace(avatar))
                    {
                        _friend.Avatar = avatar;
                        var brush = ZaloAvatar.ImageFor(avatar);
                        if (brush != null)
                        {
                            _avatarGrid.Children.Add(new Ellipse { Width = 92, Height = 92, Fill = brush, Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 3 });
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(status))
                    {
                        _statusText.Text = status;
                        _statusText.Visibility = Visibility.Visible;
                    }
                    if (!string.IsNullOrWhiteSpace(phone))
                    {
                        _phoneText.Text = phone;
                        _phoneRow.Visibility = Visibility.Visible;
                    }
                    if (!string.IsNullOrWhiteSpace(birthday))
                    {
                        _birthdayText.Text = birthday;
                        _birthdayRow.Visibility = Visibility.Visible;
                    }
                    if (!string.IsNullOrWhiteSpace(gender))
                    {
                        _genderText.Text = gender == "0" ? "Nam" : (gender == "1" ? "Nữ" : gender);
                        _genderRow.Visibility = Visibility.Visible;
                    }

                    string lastAction = First(profile, "lastActionTime");
                    if (long.TryParse(lastAction, out long timestamp) && timestamp > 0)
                    {
                        if (timestamp < 100000000000L) timestamp *= 1000;
                        try
                        {
                            var lastSeen = UnixTime.FromMilliseconds(timestamp);
                            _onlineText.Text = lastSeen > DateTimeOffset.Now.AddMinutes(-5)
                                ? "Đang hoạt động" : "Truy cập " + lastSeen.ToLocalTime().ToString("dd/MM HH:mm");
                            _onlineText.Visibility = Visibility.Visible;
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("Friend profile lookup failed: " + ex.Message);
            }
        }

        private async Task ShowMutualGroupsAsync(TextBlock statusText)
        {
            if (_friend == null || ZaloApp.Session == null) return;
            statusText.Text = "Đang tìm nhóm chung...";
            try
            {
                var groups = ZaloApp.Conversations.Where(c => c.IsGroup && !string.IsNullOrWhiteSpace(c.ThreadId)).ToList();
                if (groups.Count == 0)
                {
                    statusText.Text = "Chưa có nhóm nào.";
                    await new MessageDialog("Bạn và người này chưa có nhóm chung nào.", "Nhóm chung").ShowAsync();
                    return;
                }

                var ids = groups.Select(g => g.ThreadId).ToList();
                var infoMap = await ZaloMessaging.GetGroupInfoMapAsync(ZaloApp.Session, ids, CancellationToken.None);
                var mutual = new List<ZaloConversation>();

                foreach (var g in groups)
                {
                    if (infoMap != null && infoMap.TryGetValue(g.ThreadId, out JsonObject groupData))
                    {
                        IJsonValue memValue;
                        if (groupData.TryGetValue("currentMems", out memValue) && memValue.ValueType == JsonValueType.Array)
                        {
                            foreach (IJsonValue value in memValue.GetArray())
                            {
                                if (value.ValueType == JsonValueType.Object)
                                {
                                    string id = ZaloAuth.Str(value.GetObject(), "id");
                                    if (id == _friend.UserId)
                                    {
                                        mutual.Add(g);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                statusText.Text = mutual.Count == 0 ? "Không có nhóm chung nào." : $"{mutual.Count} nhóm chung";

                if (mutual.Count > 0)
                {
                    var scroll = new ScrollViewer { MaxHeight = 300 };
                    var stack = new StackPanel();
                    foreach (var mg in mutual)
                    {
                        var row = new Border { Padding = new Thickness(0, 8, 0, 8), BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)), BorderThickness = new Thickness(0, 0, 0, 1) };
                        var rowText = new TextBlock { Text = mg.Name ?? "Nhóm", FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark };
                        row.Child = rowText;
                        stack.Children.Add(row);
                    }
                    scroll.Content = stack;
                    var cd = new ContentDialog
                    {
                        Title = $"Nhóm chung ({mutual.Count})",
                        Content = scroll,
                        PrimaryButtonText = "Đóng"
                    };
                    await cd.ShowAsync();
                }
                else
                {
                    await new MessageDialog("Bạn và người này chưa tham gia nhóm chung nào.", "Nhóm chung").ShowAsync();
                }
            }
            catch (Exception ex)
            {
                statusText.Text = "Chưa kiểm tra được nhóm chung.";
                ZaloLog.Add("Mutual groups check failed: " + ex.Message);
            }
        }

        private async Task ShowFriendPostsAsync(TextBlock statusText)
        {
            if (_friend == null || ZaloApp.Session == null) return;
            statusText.Text = "Đang tải bài viết...";
            try
            {
                var rawPosts = await ZaloMessaging.GetFriendBoardListAsync(ZaloApp.Session, _friend.UserId, CancellationToken.None);
                if (rawPosts == null || rawPosts.Count == 0)
                {
                    statusText.Text = "Chưa có bài đăng nào.";
                    await new MessageDialog("Người dùng này chưa chia sẻ bài đăng nào trên nhật ký.", "Nhật ký bạn bè").ShowAsync();
                    return;
                }

                statusText.Text = $"{rawPosts.Count} bài viết gần đây";
                var scroll = new ScrollViewer { MaxHeight = 350 };
                var stack = new StackPanel();

                foreach (var p in rawPosts)
                {
                    string body = First(p, "content", "text", "description", "desc", "title");
                    string timestamp = First(p, "ts", "createTime", "createdTime", "time");
                    string timeStr = "";
                    if (long.TryParse(timestamp, out long ts) && ts > 0)
                    {
                        if (ts < 100000000000L) ts *= 1000;
                        try { timeStr = UnixTime.FromMilliseconds(ts).ToLocalTime().ToString("dd/MM/yyyy HH:mm"); } catch { }
                    }

                    var postCard = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF8, 0xFC)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(12, 10, 12, 10),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var pStack = new StackPanel();
                    pStack.Children.Add(new TextBlock { Text = body, FontSize = 13.5, Foreground = _textDark, TextWrapping = TextWrapping.Wrap });
                    if (!string.IsNullOrEmpty(timeStr))
                    {
                        pStack.Children.Add(new TextBlock { Text = timeStr, FontSize = 11, Foreground = _textSub, Margin = new Thickness(0, 4, 0, 0) });
                    }
                    postCard.Child = pStack;
                    stack.Children.Add(postCard);
                }

                scroll.Content = stack;
                var cd = new ContentDialog
                {
                    Title = $"Bài đăng của {_friend.Name}",
                    Content = scroll,
                    PrimaryButtonText = "Đóng"
                };
                await cd.ShowAsync();
            }
            catch (Exception ex)
            {
                statusText.Text = "Không tải được bài viết.";
                ZaloLog.Add("Friend board load failed: " + ex.Message);
            }
        }

        private static FrameworkElement CreateInfoRow(string label, string val)
        {
            var grid = new Grid { MinHeight = 52 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock { Text = label, FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x84, 0x90)), VerticalAlignment = VerticalAlignment.Center };
            var valBlock = new TextBlock { Text = val, FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B)), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(labelBlock, 0);
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(valBlock);
            return grid;
        }

        private static Border CreateDivider()
        {
            return new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2))
            };
        }

        private static string First(JsonObject obj, params string[] keys)
        {
            if (obj == null) return string.Empty;
            foreach (string key in keys)
            {
                string val = ZaloAuth.Str(obj, key);
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
            return string.Empty;
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
    }
}
