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
using ZaloUWP.Models;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class GroupInfoPage : Page
    {
        private readonly string _conversationKey;
        private ZaloConversation _conversation;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x20, 0x21, 0x24));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x73, 0x77, 0x7C));

        private TextBlock _nameTextBlock;
        private TextBlock _memberCountTextBlock;
        private StackPanel _membersStack;
        private TextBlock _groupLinkStatusTextBlock;
        private TextBlock _groupLinkTextBlock;
        private Border _groupInviteLinkPanel;
        private Border _changeGroupNameButton;
        private TextBlock _mediaCountText;

        private string _creatorId = string.Empty;
        private readonly HashSet<string> _adminIds = new HashSet<string>(StringComparer.Ordinal);
        private string _groupInviteLink = string.Empty;

        public GroupInfoPage(string conversationKey)
        {
            _conversationKey = conversationKey;
            _conversation = ZaloApp.Find(conversationKey) ?? ZaloApp.Conversations.FirstOrDefault(c => c.Key == conversationKey);
            BuildUI();
            Loaded += (s, e) => LoadGroupInfoAsync();
        }

        private bool CanManageGroup()
        {
            string uid = ZaloApp.Session == null ? string.Empty : ZaloApp.Session.Uid;
            return uid.Length > 0 && (uid == _creatorId || _adminIds.Contains(uid));
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF7, 0xFA))
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
                Text = "Tùy chọn nhóm",
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
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Group Profile Card
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 20, 16, 20),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var cardStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 92, Height = 92, Margin = new Thickness(0, 0, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse { Width = 92, Height = 92, Fill = _conversation?.AvatarColor ?? _activeBlue };
            avatarGrid.Children.Add(avatarEllipse);

            var initial = new TextBlock
            {
                Text = _conversation?.Initial ?? "G",
                FontSize = 32,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(initial);

            if (_conversation?.AvatarImageBrush != null)
            {
                var imgEllipse = new Ellipse { Width = 92, Height = 92, Fill = _conversation.AvatarImageBrush };
                avatarGrid.Children.Add(imgEllipse);
                initial.Text = string.Empty;
            }
            cardStack.Children.Add(avatarGrid);

            _nameTextBlock = new TextBlock
            {
                Text = _conversation?.Name ?? "Nhóm trò chuyện",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300
            };
            cardStack.Children.Add(_nameTextBlock);

            _memberCountTextBlock = new TextBlock
            {
                Text = $"{_conversation?.GroupMemberCount ?? 0} thành viên",
                FontSize = 13.5,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            cardStack.Children.Add(_memberCountTextBlock);

            // Change Group Name Button
            _changeGroupNameButton = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 6, 16, 6),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _changeGroupNameButton.Child = new TextBlock
            {
                Text = "Đổi tên nhóm",
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _activeBlue
            };
            _changeGroupNameButton.PointerPressed += (s, e) => _changeGroupNameButton.Opacity = 0.6;
            _changeGroupNameButton.PointerReleased += (s, e) => _changeGroupNameButton.Opacity = 1.0;
            _changeGroupNameButton.PointerCanceled += (s, e) => _changeGroupNameButton.Opacity = 1.0;
            _changeGroupNameButton.Tapped += (s, e) => ShowRenameDialog();
            cardStack.Children.Add(_changeGroupNameButton);

            card.Child = cardStack;
            stack.Children.Add(card);

            // 4 Quick Actions (Search, Invite Link, Wallpaper, Mute)
            var actionCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 14, 10, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var actionGrid = new Grid();
            for (int i = 0; i < 4; i++) actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col0 = CreateActionColumn("ms-appx:///Assets/Icons/ic_search_white.png", "Tìm tin nhắn", Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));
            col0.Tapped += (s, e) => PromptSearchMessages();
            Grid.SetColumn(col0, 0);
            actionGrid.Children.Add(col0);

            var col1 = CreateActionColumn("ms-appx:///Assets/Icons/ic_link_blue.png", "Link mời", Color.FromArgb(0xFF, 0x05, 0x96, 0x69));
            col1.Tapped += (s, e) => ShowGroupInviteLink();
            Grid.SetColumn(col1, 1);
            actionGrid.Children.Add(col1);

            var col2 = CreateActionColumn("ms-appx:///Assets/Icons/ic_photo_gray.png", "Hình nền", Color.FromArgb(0xFF, 0xD9, 0x77, 0x06));
            col2.Tapped += (s, e) => ShowWallpaperPicker();
            Grid.SetColumn(col2, 2);
            actionGrid.Children.Add(col2);

            var col3 = CreateActionColumn("ms-appx:///Assets/Icons/ic_mute_gray.png", "Thông báo", Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
            col3.Tapped += async (s, e) =>
            {
                if (_conversation != null)
                {
                    await ZaloApp.SetMutedAsync(_conversation, !_conversation.IsMuted);
                    var md = new MessageDialog(_conversation.IsMuted ? "Đã tắt thông báo nhóm." : "Đã bật thông báo nhóm.", "Thông báo");
                    await md.ShowAsync();
                }
            };
            Grid.SetColumn(col3, 3);
            actionGrid.Children.Add(col3);

            actionCard.Child = actionGrid;
            stack.Children.Add(actionCard);

            // Group Invite Link Box
            _groupInviteLinkPanel = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            var linkStack = new StackPanel();
            linkStack.Children.Add(new TextBlock
            {
                Text = "LINK THAM GIA NHÓM",
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _activeBlue
            });
            _groupLinkStatusTextBlock = new TextBlock
            {
                Text = "Đang tải link mời…",
                FontSize = 12,
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4)
            };
            linkStack.Children.Add(_groupLinkStatusTextBlock);
            _groupLinkTextBlock = new TextBlock
            {
                Text = "",
                FontSize = 13,
                Foreground = _textDark,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            };
            linkStack.Children.Add(_groupLinkTextBlock);
            _groupInviteLinkPanel.Child = linkStack;
            stack.Children.Add(_groupInviteLinkPanel);

            // Group Media Section
            var mediaCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var mediaGrid = new Grid();
            mediaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mediaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var mediaTextStack = new StackPanel();
            mediaTextStack.Children.Add(new TextBlock { Text = "Ảnh, file, link đã gửi", FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            _mediaCountText = new TextBlock { Text = $"{_conversation?.Messages?.Count(m => m.HasMedia || m.HasFile || m.HasLinkPreview) ?? 0} mục gần đây", FontSize = 12.5, Foreground = _textSub, Margin = new Thickness(0, 2, 0, 0) };
            mediaTextStack.Children.Add(_mediaCountText);
            Grid.SetColumn(mediaTextStack, 0);
            mediaGrid.Children.Add(mediaTextStack);
            var arrow = new TextBlock { Text = "›", FontSize = 20, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(arrow, 1);
            mediaGrid.Children.Add(arrow);
            mediaCard.Child = mediaGrid;
            mediaCard.Tapped += (s, e) => ShowGroupMediaDialog();
            stack.Children.Add(mediaCard);

            // Add Member Button
            var addMemberCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var addMemberGrid = new Grid();
            addMemberGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            addMemberGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addMemberGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var plusText = new TextBlock { Text = "+", FontSize = 22, FontWeight = Windows.UI.Text.FontWeights.Bold, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(plusText, 0);
            addMemberGrid.Children.Add(plusText);

            var addMemberText = new TextBlock { Text = "Thêm thành viên", FontSize = 15.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(addMemberText, 1);
            addMemberGrid.Children.Add(addMemberText);

            var plusArrow = new TextBlock { Text = "›", FontSize = 20, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(plusArrow, 2);
            addMemberGrid.Children.Add(plusArrow);

            addMemberCard.Child = addMemberGrid;
            addMemberCard.PointerPressed += (s, e) => addMemberCard.Opacity = 0.6;
            addMemberCard.PointerReleased += (s, e) => addMemberCard.Opacity = 1.0;
            addMemberCard.PointerCanceled += (s, e) => addMemberCard.Opacity = 1.0;
            addMemberCard.Tapped += (s, e) => ShowAddMembersDialog();
            stack.Children.Add(addMemberCard);

            // Member List Card
            var membersCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 16, 16, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var membersStack = new StackPanel();
            membersStack.Children.Add(new TextBlock
            {
                Text = "THÀNH VIÊN TRONG NHÓM",
                FontSize = 12,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _membersStack = new StackPanel();
            membersStack.Children.Add(_membersStack);
            membersCard.Child = membersStack;
            stack.Children.Add(membersCard);

            // Danger Actions (Leave Group)
            var dangerCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var dangerStack = new StackPanel();

            var leaveBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var leaveText = new TextBlock { Text = "Rời nhóm", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)) };
            leaveBorder.Child = leaveText;
            leaveBorder.Tapped += async (s, e) =>
            {
                var dialog = new MessageDialog("Bạn sẽ không còn nhận tin nhắn mới từ nhóm này.", "Rời nhóm");
                dialog.Commands.Add(new UICommand("Rời nhóm", cmd => { var ignored = LeaveGroupCoreAsync(); }));
                dialog.Commands.Add(new UICommand("Hủy"));
                await dialog.ShowAsync();
            };
            dangerStack.Children.Add(leaveBorder);

            dangerCard.Child = dangerStack;
            stack.Children.Add(dangerCard);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private async void LoadGroupInfoAsync()
        {
            if (_conversation == null) return;
            _memberCountTextBlock.Text = "Đang tải thông tin nhóm…";

            try
            {
                JsonObject info = await ZaloMessaging.GetGroupInfoAsync(ZaloApp.Session, _conversation.ThreadId, CancellationToken.None);
                if (info != null)
                {
                    string name = ZaloAuth.Str(info, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _conversation.Name = name;
                        _nameTextBlock.Text = name;
                    }
                    string avatar = ZaloAuth.Str(info, "fullAvt");
                    if (avatar.Length == 0) avatar = ZaloAuth.Str(info, "avt");
                    if (!string.IsNullOrWhiteSpace(avatar)) _conversation.AvatarUrl = avatar;

                    _creatorId = ZaloAuth.Str(info, "creatorId");
                    _adminIds.Clear();
                    IJsonValue adminValue;
                    if (info.TryGetValue("adminIds", out adminValue) && adminValue.ValueType == JsonValueType.Array)
                    {
                        foreach (IJsonValue admin in adminValue.GetArray())
                        {
                            string adminId = admin.ValueType == JsonValueType.String ? admin.GetString() : string.Empty;
                            if (adminId.Length > 0) _adminIds.Add(adminId);
                        }
                    }

                    var members = new List<UserInfo>();
                    IJsonValue memValue;
                    if (info.TryGetValue("currentMems", out memValue) && memValue.ValueType == JsonValueType.Array)
                    {
                        foreach (IJsonValue value in memValue.GetArray())
                        {
                            if (value.ValueType != JsonValueType.Object) continue;
                            JsonObject member = value.GetObject();
                            string id = ZaloAuth.Str(member, "id");
                            string memberName = ZaloAuth.Str(member, "dName");
                            if (memberName.Length == 0) memberName = ZaloAuth.Str(member, "zaloName");
                            if (memberName.Length == 0) memberName = id;
                            members.Add(new UserInfo
                            {
                                UserId = id,
                                DisplayName = memberName,
                                AvatarUrl = ZaloAuth.Str(member, "avatar"),
                                IsGroupOwner = id == _creatorId,
                                IsGroupDeputy = id != _creatorId && _adminIds.Contains(id)
                            });
                        }
                    }

                    int total = (int)ZaloAuth.Num(info, "totalMember");
                    if (total < members.Count) total = members.Count;
                    _conversation.GroupMemberCount = total;
                    _memberCountTextBlock.Text = $"{total} thành viên";

                    _membersStack.Children.Clear();
                    foreach (var mem in members)
                    {
                        _membersStack.Children.Add(CreateMemberRow(mem.DisplayName, mem.GroupRoleText, mem.AvatarUrl, mem.UserId));
                    }

                    if (CanManageGroup())
                    {
                        _changeGroupNameButton.Visibility = Visibility.Visible;
                        await LoadGroupLinkAsync();
                    }
                    else
                    {
                        _changeGroupNameButton.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("LoadGroupInfo error: " + ex.Message);
                _memberCountTextBlock.Text = $"{_conversation.GroupMemberCount} thành viên (ngoại tuyến)";
            }
        }

        private async Task LoadGroupLinkAsync()
        {
            if (_conversation == null) return;
            try
            {
                ZaloGroupLinkInfo info = await ZaloMessaging.GetGroupLinkInfoAsync(ZaloApp.Session, _conversation.ThreadId, CancellationToken.None);
                if (info != null && info.Enabled && !string.IsNullOrWhiteSpace(info.Link))
                {
                    _groupInviteLink = info.Link;
                    _groupLinkTextBlock.Text = info.Link;
                    _groupLinkStatusTextBlock.Text = "Link mời đang hoạt động:";
                    _groupInviteLinkPanel.Visibility = Visibility.Visible;
                }
            }
            catch { }
        }

        private async void ShowGroupInviteLink()
        {
            if (string.IsNullOrEmpty(_groupInviteLink))
            {
                try
                {
                    var result = await ZaloMessaging.EnableGroupLinkAsync(ZaloApp.Session, _conversation.ThreadId, CancellationToken.None);
                    if (result != null && !string.IsNullOrEmpty(result.Link))
                    {
                        _groupInviteLink = result.Link;
                        _groupLinkTextBlock.Text = result.Link;
                        _groupLinkStatusTextBlock.Text = "Link mời đang hoạt động:";
                        _groupInviteLinkPanel.Visibility = Visibility.Visible;
                    }
                }
                catch (Exception ex)
                {
                    await new MessageDialog("Không tạo được link mời: " + ex.Message).ShowAsync();
                    return;
                }
            }
            else
            {
                _groupInviteLinkPanel.Visibility = Visibility.Visible;
            }

            var md = new MessageDialog(_groupInviteLink, "Link mời tham gia nhóm");
            md.Commands.Add(new UICommand("Đóng"));
            await md.ShowAsync();
        }

        private FrameworkElement CreateMemberRow(string name, string role, string avatarUrl, string userId)
        {
            var border = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Colors.Transparent)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarGrid = new Grid { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = 36, Height = 36, Fill = _activeBlue };
            var initial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(name),
                FontSize = 14,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(ellipse);
            avatarGrid.Children.Add(initial);

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                try
                {
                    var img = new Ellipse { Width = 36, Height = 36, Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarUrl)), Stretch = Stretch.UniformToFill } };
                    avatarGrid.Children.Add(img);
                    initial.Text = string.Empty;
                }
                catch { }
            }
            Grid.SetColumn(avatarGrid, 0);
            grid.Children.Add(avatarGrid);

            var nameText = new TextBlock
            {
                Text = name,
                FontSize = 14.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            var roleText = new TextBlock
            {
                Text = role,
                FontSize = 12,
                Foreground = role == "Trưởng nhóm" ? _activeBlue : (role == "Phó nhóm" ? ColorBrush(0xFF, 0x10, 0xB9, 0x81) : _textSub),
                FontWeight = role != "Thành viên" ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(roleText, 2);
            grid.Children.Add(roleText);

            border.Child = grid;
            border.Tapped += (s, e) =>
            {
                if (!string.IsNullOrEmpty(userId) && userId != ZaloApp.Session?.Uid)
                {
                    var friend = ZaloApp.Friends.FirstOrDefault(f => f.UserId == userId) 
                        ?? new ZaloFriend { UserId = userId, Name = name, Avatar = avatarUrl };
                    NavigationManager.NavigateTo(new FriendProfilePage(friend));
                }
            };
            return border;
        }

        private async Task LeaveGroupCoreAsync()
        {
            try
            {
                if (_conversation != null)
                {
                    await ZaloMessaging.LeaveGroupAsync(ZaloApp.Session, _conversation.ThreadId, CancellationToken.None);
                    ZaloApp.Groups.Remove(_conversation);
                    ZaloApp.Conversations.Remove(_conversation);
                    _ = ZaloApp.SaveCachedConversationsAsync();
                }
                NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không rời được nhóm: " + ex.Message, "Rời nhóm").ShowAsync();
            }
        }

        private Border CreateActionColumn(string iconUri, string label, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(4)
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var iconBox = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
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
                FontSize = 11,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(text);

            border.Child = stack;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            return border;
        }

        private async void ShowRenameDialog()
        {
            if (_conversation == null) return;
            var input = new TextBox
            {
                Text = _conversation.Name ?? string.Empty,
                PlaceholderText = "Nhập tên nhóm mới",
                MaxLength = 50
            };
            var dialog = new ContentDialog
            {
                Title = "Đổi tên nhóm",
                Content = input,
                PrimaryButtonText = "Lưu",
                SecondaryButtonText = "Hủy"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string newName = (input.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newName)) return;

            try
            {
                await ZaloMessaging.ChangeGroupNameAsync(ZaloApp.Session, _conversation.ThreadId, newName, CancellationToken.None);
                _conversation.Name = newName;
                _nameTextBlock.Text = newName;
                _ = ZaloApp.SaveCachedConversationsAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không đổi được tên nhóm: " + ex.Message).ShowAsync();
            }
        }

        private async void ShowAddMembersDialog()
        {
            if (_conversation == null) return;
            var friends = ZaloApp.Friends.Where(f => !string.IsNullOrEmpty(f.UserId)).ToList();
            if (friends.Count == 0)
            {
                await new MessageDialog("Chưa có bạn bè để thêm vào nhóm.", "Thêm thành viên").ShowAsync();
                return;
            }

            var selectedIds = new HashSet<string>();
            var scroll = new ScrollViewer { MaxHeight = 350 };
            var listStack = new StackPanel();

            foreach (var f in friends)
            {
                var chk = new CheckBox
                {
                    Content = f.Name,
                    FontSize = 14,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                chk.Checked += (s, e) => selectedIds.Add(f.UserId);
                chk.Unchecked += (s, e) => selectedIds.Remove(f.UserId);
                listStack.Children.Add(chk);
            }
            scroll.Content = listStack;

            var dialog = new ContentDialog
            {
                Title = "Chọn bạn bè thêm vào nhóm",
                Content = scroll,
                PrimaryButtonText = "Thêm",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary || selectedIds.Count == 0) return;

            try
            {
                await ZaloMessaging.InviteGroupMembersAsync(ZaloApp.Session, _conversation.ThreadId, selectedIds.ToList(), CancellationToken.None);
                await new MessageDialog("Đã thêm thành viên vào nhóm thành công.", "Thông báo").ShowAsync();
                LoadGroupInfoAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không thêm được thành viên: " + ex.Message).ShowAsync();
            }
        }

        private async void ShowWallpaperPicker()
        {
            if (_conversation == null) return;
            var wrap = new WrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 65, ItemHeight = 90 };
            string selectedUri = null;

            for (int i = 1; i <= 20; i++)
            {
                string uri = $"ms-appx:///Assets/Backgrounds/bg_{i:D2}.png";
                var border = new Border
                {
                    Width = 60,
                    Height = 85,
                    Margin = new Thickness(2),
                    BorderThickness = new Thickness(2),
                    BorderBrush = new SolidColorBrush(Colors.Transparent),
                    Child = new Image { Source = new BitmapImage(new Uri(uri)), Stretch = Stretch.UniformToFill }
                };
                border.Tapped += (s, e) =>
                {
                    selectedUri = uri;
                    border.BorderBrush = _activeBlue;
                };
                wrap.Children.Add(border);
            }

            var scroll = new ScrollViewer { Content = wrap, MaxHeight = 300 };
            var dialog = new ContentDialog
            {
                Title = "Chọn hình nền nhóm",
                Content = scroll,
                PrimaryButtonText = "Áp dụng",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && selectedUri != null)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(ZaloApp.Session?.Uid, _conversation.Key, selectedUri);
                await new MessageDialog("Đã đổi hình nền nhóm thành công!", "Thông báo").ShowAsync();
            }
        }

        private async void ShowGroupMediaDialog()
        {
            if (_conversation == null) return;
            var mediaMsgs = _conversation.Messages.Where(m => m.HasMedia || m.HasFile || m.HasLinkPreview).Take(20).ToList();
            if (mediaMsgs.Count == 0)
            {
                await new MessageDialog("Chưa có ảnh hoặc tệp nào được gửi trong nhóm.", "Ảnh & Tệp nhóm").ShowAsync();
                return;
            }

            var scroll = new ScrollViewer { MaxHeight = 350 };
            var stack = new StackPanel();
            foreach (var m in mediaMsgs)
            {
                var row = new Border { Padding = new Thickness(0, 6, 0, 6), BorderBrush = ColorBrush(0xFF, 0xEE, 0xF0, 0xF2), BorderThickness = new Thickness(0, 0, 0, 1) };
                var rowStack = new StackPanel();
                rowStack.Children.Add(new TextBlock { Text = m.Sender ?? "Người gửi", FontSize = 12, Foreground = _activeBlue });
                rowStack.Children.Add(new TextBlock { Text = m.FileName ?? (m.HasMedia ? "[Hình ảnh]" : m.Text), FontSize = 13.5, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis });
                rowStack.Children.Add(new TextBlock { Text = m.TimeText ?? "", FontSize = 11, Foreground = _textSub });
                row.Child = rowStack;
                stack.Children.Add(row);
            }
            scroll.Content = stack;

            var dialog = new ContentDialog
            {
                Title = "Nội dung gần đây",
                Content = scroll,
                PrimaryButtonText = "Đóng"
            };
            await dialog.ShowAsync();
        }

        private async void PromptSearchMessages()
        {
            var input = new TextBox { PlaceholderText = "Nhập từ khóa tìm kiếm…" };
            var dialog = new ContentDialog
            {
                Title = "Tìm tin nhắn trong nhóm",
                Content = input,
                PrimaryButtonText = "Tìm",
                SecondaryButtonText = "Hủy"
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string kw = (input.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(kw)) return;
                int matches = _conversation?.Messages?.Count(m => m.Text != null && m.Text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) ?? 0;
                await new MessageDialog($"Tìm thấy {matches} tin nhắn khớp với '{kw}'.", "Kết quả tìm kiếm").ShowAsync();
            }
        }

        private static SolidColorBrush ColorBrush(byte a, byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
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
