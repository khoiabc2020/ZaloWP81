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
        private readonly SolidColorBrush _cardBg = new SolidColorBrush(Colors.White);

        private TextBlock _nameTextBlock;
        private TextBlock _memberCountTextBlock;
        private StackPanel _membersStack;
        private TextBlock _groupLinkStatusTextBlock;
        private TextBlock _groupLinkTextBlock;
        private Border _groupInviteLinkPanel;
        private Border _changeGroupNameButton;
        private TextBlock _mediaCountText;

        // Group Board (Ghi chú, Bình chọn, Tin ghim)
        private StackPanel _boardStack;
        private TextBlock _boardStatusTextBlock;

        // Group Reminders (Lịch nhóm / Nhắc hẹn)
        private StackPanel _remindersStack;
        private TextBlock _remindersStatusTextBlock;

        private string _creatorId = string.Empty;
        private readonly HashSet<string> _adminIds = new HashSet<string>(StringComparer.Ordinal);
        private string _groupInviteLink = string.Empty;

        public GroupInfoPage(string conversationKey)
        {
            _conversationKey = conversationKey;
            _conversation = ZaloApp.Find(conversationKey) ?? ZaloApp.Conversations.FirstOrDefault(c => c.Key == conversationKey);
            BuildUI();
            Loaded += async (s, e) =>
            {
                LoadGroupInfoAsync();
                await Task.WhenAll(LoadGroupBoardAsync(), LoadGroupRemindersAsync());
            };
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
                Text = "Tùy chọn",
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
            var scroll = new ScrollViewer { Padding = new Thickness(8) };
            var stack = new StackPanel();

            // Group Profile Card (EXACT WIN10 DIMENSIONS: 104x104 Avatar, 22px Title, 14px Member count)
            var card = new Border
            {
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(20, 24, 20, 18),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var cardStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 104, Height = 104, Margin = new Thickness(0, 0, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse { Width = 104, Height = 104, Fill = _conversation?.AvatarColor ?? _activeBlue };
            avatarGrid.Children.Add(avatarEllipse);

            var initial = new TextBlock
            {
                Text = _conversation?.Initial ?? "G",
                FontSize = 40,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(initial);

            if (_conversation?.AvatarImageBrush != null)
            {
                var imgEllipse = new Ellipse { Width = 104, Height = 104, Fill = _conversation.AvatarImageBrush };
                avatarGrid.Children.Add(imgEllipse);
                initial.Text = string.Empty;
            }
            cardStack.Children.Add(avatarGrid);

            _nameTextBlock = new TextBlock
            {
                Text = _conversation?.Name ?? "Nhóm trò chuyện",
                FontSize = 22,
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
                FontSize = 14,
                Foreground = ColorBrush(0xFF, 0x7A, 0x83, 0x8D),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            cardStack.Children.Add(_memberCountTextBlock);

            // Change Group Name Button
            _changeGroupNameButton = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xF2, 0xFF)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 7, 14, 7),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            _changeGroupNameButton.Child = new TextBlock
            {
                Text = "Đổi tên nhóm",
                FontSize = 13,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = ColorBrush(0xFF, 0x00, 0x68, 0xFF)
            };
            _changeGroupNameButton.PointerPressed += (s, e) => _changeGroupNameButton.Opacity = 0.6;
            _changeGroupNameButton.PointerReleased += (s, e) => _changeGroupNameButton.Opacity = 1.0;
            _changeGroupNameButton.PointerCanceled += (s, e) => _changeGroupNameButton.Opacity = 1.0;
            _changeGroupNameButton.Tapped += (s, e) => ShowRenameDialog();
            cardStack.Children.Add(_changeGroupNameButton);

            card.Child = cardStack;
            stack.Children.Add(card);

            // 4 Quick Actions (EXACT WIN10 DIMENSIONS: 48x48 circle with #F4F5F7 background)
            var actionCard = new Border
            {
                Background = _cardBg,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 16, 12, 18),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var actionGrid = new Grid();
            for (int i = 0; i < 4; i++) actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col0 = CreateActionColumn("ms-appx:///Assets/Icons/ic_search_clean_white.png", "Tìm tin nhắn", Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));
            col0.Tapped += (s, e) => PromptSearchMessages();
            Grid.SetColumn(col0, 0);
            actionGrid.Children.Add(col0);

            var col1 = CreateActionColumn("ms-appx:///Assets/Icons/ic_link_clean_white.png", "Link mời", Color.FromArgb(0xFF, 0x05, 0x96, 0x69));
            col1.Tapped += (s, e) => ShowGroupInviteLink();
            Grid.SetColumn(col1, 1);
            actionGrid.Children.Add(col1);

            var col2 = CreateActionColumn("ms-appx:///Assets/Icons/ic_wallpaper_clean_white.png", "Đổi hình nền", Color.FromArgb(0xFF, 0xD9, 0x77, 0x06));
            col2.Tapped += (s, e) => ShowWallpaperPicker();
            Grid.SetColumn(col2, 2);
            actionGrid.Children.Add(col2);

            var col3 = CreateActionColumn("ms-appx:///Assets/Icons/ic_bell_mute_clean_white.png", "Tắt thông báo", Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
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
                Background = _cardBg,
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
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8)
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

            // Group Board Section (Tin ghim, ghi chú, bình chọn)
            var boardCard = new Border
            {
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var boardRootStack = new StackPanel();

            var boardHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            boardHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            boardHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var boardHeaderTitle = new TextBlock
            {
                Text = "Nội dung nhóm (Bảng tin)",
                FontSize = 15.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(boardHeaderTitle, 0);
            boardHeaderGrid.Children.Add(boardHeaderTitle);

            var refreshBoardBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshBoardBtn.Child = new TextBlock { Text = "Làm mới", FontSize = 11, Foreground = ColorBrush(0xFF, 0x46, 0x53, 0x65) };
            refreshBoardBtn.Tapped += async (s, e) => await LoadGroupBoardAsync();
            Grid.SetColumn(refreshBoardBtn, 1);
            boardHeaderGrid.Children.Add(refreshBoardBtn);
            boardRootStack.Children.Add(boardHeaderGrid);

            // Action row: Ghi chú / Tạo bình chọn
            var boardActionsRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            boardActionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            boardActionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var createNoteBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 4, 0)
            };
            var noteBtnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            noteBtnStack.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_note_clean_blue.png")), Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
            noteBtnStack.Children.Add(new TextBlock { Text = "Ghi chú", FontSize = 13.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center });
            createNoteBtn.Child = noteBtnStack;
            createNoteBtn.Tapped += (s, e) => CreateGroupNote_Click();
            Grid.SetColumn(createNoteBtn, 0);
            boardActionsRow.Children.Add(createNoteBtn);

            var createPollBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(4, 0, 0, 0)
            };
            var pollBtnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            pollBtnStack.Children.Add(new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_poll_clean_blue.png")), Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
            pollBtnStack.Children.Add(new TextBlock { Text = "Tạo bình chọn", FontSize = 13.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue, VerticalAlignment = VerticalAlignment.Center });
            createPollBtn.Child = pollBtnStack;
            createPollBtn.Tapped += (s, e) => CreateGroupPoll_Click();
            Grid.SetColumn(createPollBtn, 1);
            boardActionsRow.Children.Add(createPollBtn);
            boardRootStack.Children.Add(boardActionsRow);

            _boardStatusTextBlock = new TextBlock
            {
                Text = "Đang tải bảng nhóm…",
                FontSize = 12,
                Foreground = _textSub,
                Margin = new Thickness(0, 2, 0, 6)
            };
            boardRootStack.Children.Add(_boardStatusTextBlock);

            _boardStack = new StackPanel();
            boardRootStack.Children.Add(_boardStack);

            boardCard.Child = boardRootStack;
            stack.Children.Add(boardCard);

            // Group Reminders Section (Lịch nhóm)
            var remindersCard = new Border
            {
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var remRootStack = new StackPanel();

            var remHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            remHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            remHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            remHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var remTitle = new TextBlock { Text = "Lịch nhóm (Nhắc hẹn)", FontSize = 15.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(remTitle, 0);
            remHeaderGrid.Children.Add(remTitle);

            var createRemBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            createRemBtn.Child = new TextBlock { Text = "+ Nhắc hẹn", FontSize = 11, Foreground = _activeBlue, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
            createRemBtn.Tapped += (s, e) => CreateGroupReminder_Click();
            Grid.SetColumn(createRemBtn, 1);
            remHeaderGrid.Children.Add(createRemBtn);

            var refreshRemBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshRemBtn.Child = new TextBlock { Text = "Làm mới", FontSize = 11, Foreground = ColorBrush(0xFF, 0x46, 0x53, 0x65) };
            refreshRemBtn.Tapped += async (s, e) => await LoadGroupRemindersAsync();
            Grid.SetColumn(refreshRemBtn, 2);
            remHeaderGrid.Children.Add(refreshRemBtn);
            remRootStack.Children.Add(remHeaderGrid);

            _remindersStatusTextBlock = new TextBlock
            {
                Text = "Đang tải nhắc hẹn…",
                FontSize = 12,
                Foreground = _textSub,
                Margin = new Thickness(0, 2, 0, 6)
            };
            remRootStack.Children.Add(_remindersStatusTextBlock);

            _remindersStack = new StackPanel();
            remRootStack.Children.Add(_remindersStack);

            remindersCard.Child = remRootStack;
            stack.Children.Add(remindersCard);

            // Add Member Button
            var addMemberCard = new Border
            {
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 8)
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
                Background = _cardBg,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 16, 16, 12),
                Margin = new Thickness(0, 0, 0, 8)
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
                Background = _cardBg,
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
                    if (string.IsNullOrWhiteSpace(avatar)) avatar = ZaloAuth.Str(info, "avt");
                    if (!string.IsNullOrWhiteSpace(avatar))
                    {
                        _conversation.AvatarUrl = avatar;
                    }

                    _creatorId = ZaloAuth.Str(info, "creatorId");
                    _adminIds.Clear();
                    IJsonValue adminVal;
                    if (info.TryGetValue("adminIds", out adminVal) && adminVal.ValueType == JsonValueType.Array)
                    {
                        foreach (IJsonValue v in adminVal.GetArray())
                        {
                            if (v.ValueType == JsonValueType.String) _adminIds.Add(v.GetString());
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
                            if (string.IsNullOrEmpty(id)) id = ZaloAuth.Str(member, "uid");
                            if (string.IsNullOrEmpty(id)) continue;

                            string memberName = ZaloAuth.Str(member, "dName");
                            if (memberName.Length == 0) memberName = ZaloAuth.Str(member, "displayName");
                            if (memberName.Length == 0) memberName = ZaloAuth.Str(member, "zaloName");
                            if (memberName.Length == 0) memberName = ZaloAuth.Str(member, "name");
                            if (memberName.Length == 0)
                            {
                                var fr = ZaloApp.Friends?.FirstOrDefault(f => f.UserId == id);
                                if (fr != null && !string.IsNullOrEmpty(fr.DisplayName)) memberName = fr.DisplayName;
                            }
                            if (memberName.Length == 0) memberName = id;

                            string memAvatar = ZaloAuth.Str(member, "avatar");
                            if (memAvatar.Length == 0) memAvatar = ZaloAuth.Str(member, "avatarUrl");
                            if (memAvatar.Length == 0) memAvatar = ZaloAuth.Str(member, "avt");
                            if (memAvatar.Length == 0) memAvatar = ZaloAuth.Str(member, "fullAvt");
                            if (memAvatar.Length == 0)
                            {
                                var fr = ZaloApp.Friends?.FirstOrDefault(f => f.UserId == id);
                                if (fr != null && !string.IsNullOrEmpty(fr.Avatar)) memAvatar = fr.Avatar;
                            }

                            bool isOwner = id == _creatorId;
                            bool isDeputy = id != _creatorId && _adminIds.Contains(id);

                            _conversation.SetGroupMember(id, memberName, memAvatar, isOwner, isDeputy);

                            members.Add(new UserInfo
                            {
                                UserId = id,
                                DisplayName = memberName,
                                AvatarUrl = memAvatar,
                                IsGroupOwner = isOwner,
                                IsGroupDeputy = isDeputy
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

                    _changeGroupNameButton.Visibility = CanManageGroup() ? Visibility.Visible : Visibility.Collapsed;

                    if (CanManageGroup() || !string.IsNullOrEmpty(_groupInviteLink))
                    {
                        await LoadGroupLinkAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("Group details load failed: " + ex.Message);
                _memberCountTextBlock.Text = $"{_conversation.GroupMemberCount} thành viên (ngoại tuyến)";
            }
        }

        private async Task LoadGroupBoardAsync()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            if (_boardStatusTextBlock != null) _boardStatusTextBlock.Text = "Đang tải bảng nhóm…";
            if (_boardStack != null) _boardStack.Children.Clear();

            try
            {
                List<ZaloGroupBoardEntry> entries = await ZaloMessaging.GetGroupBoardAsync(
                    ZaloApp.Session, _conversation.ThreadId, 1, 30, CancellationToken.None);

                if (entries == null || entries.Count == 0)
                {
                    if (_boardStatusTextBlock != null) _boardStatusTextBlock.Text = "Chưa có tin ghim, ghi chú hay bình chọn nào.";
                    return;
                }

                if (_boardStatusTextBlock != null) _boardStatusTextBlock.Text = $"{entries.Count} nội dung gần đây";

                foreach (var entry in entries)
                {
                    var itemBorder = new Border
                    {
                        Padding = new Thickness(0, 8, 0, 8),
                        BorderBrush = ColorBrush(0xFF, 0xEE, 0xF0, 0xF2),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Background = new SolidColorBrush(Colors.Transparent)
                    };
                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var iconImg = new Image
                    {
                        Source = new BitmapImage(new Uri(entry.PollId > 0 ? "ms-appx:///Assets/Icons/ic_poll_clean_blue.png" : "ms-appx:///Assets/Icons/ic_pin_clean_blue.png")),
                        Width = 20,
                        Height = 20,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    Grid.SetColumn(iconImg, 0);
                    itemGrid.Children.Add(iconImg);

                    var infoStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = entry.Title ?? "",
                        FontSize = 14,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _textDark,
                        TextWrapping = TextWrapping.Wrap
                    });
                    if (!string.IsNullOrEmpty(entry.Subtitle))
                    {
                        infoStack.Children.Add(new TextBlock
                        {
                            Text = entry.Subtitle,
                            FontSize = 11.5,
                            Foreground = _textSub,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }

                    if (entry.PollId > 0)
                    {
                        var voteBtn = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(10, 4, 10, 4),
                            Margin = new Thickness(0, 6, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        voteBtn.Child = new TextBlock { Text = "Mở bình chọn", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
                        long pollId = entry.PollId;
                        voteBtn.Tapped += (s, e) => OpenPollDialog(pollId);
                        infoStack.Children.Add(voteBtn);
                    }
                    else if (!string.IsNullOrEmpty(entry.TopicId))
                    {
                        var noteBtn = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA)),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(10, 4, 10, 4),
                            Margin = new Thickness(0, 6, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        noteBtn.Child = new TextBlock { Text = "Sửa ghi chú", FontSize = 12, Foreground = ColorBrush(0xFF, 0x46, 0x53, 0x65) };
                        string topicId = entry.TopicId;
                        string currentNote = entry.Title;
                        noteBtn.Tapped += (s, e) => ShowEditNoteDialog(topicId, currentNote);
                        infoStack.Children.Add(noteBtn);
                    }

                    Grid.SetColumn(infoStack, 1);
                    itemGrid.Children.Add(infoStack);

                    if (!string.IsNullOrEmpty(entry.TimeText))
                    {
                        var timeBlock = new TextBlock
                        {
                            Text = entry.TimeText,
                            FontSize = 10.5,
                            Foreground = ColorBrush(0xFF, 0x98, 0xA2, 0xAE),
                            VerticalAlignment = VerticalAlignment.Top
                        };
                        Grid.SetColumn(timeBlock, 2);
                        itemGrid.Children.Add(timeBlock);
                    }

                    itemBorder.Child = itemGrid;
                    _boardStack.Children.Add(itemBorder);
                }
            }
            catch (Exception ex)
            {
                if (_boardStatusTextBlock != null) _boardStatusTextBlock.Text = "Chưa tải được bảng nhóm.";
                ZaloLog.Add("LoadGroupBoardAsync failed: " + ex.Message);
            }
        }

        private async Task LoadGroupRemindersAsync()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            if (_remindersStatusTextBlock != null) _remindersStatusTextBlock.Text = "Đang tải nhắc hẹn…";
            if (_remindersStack != null) _remindersStack.Children.Clear();

            try
            {
                List<ZaloGroupReminder> reminders = await ZaloMessaging.GetGroupRemindersAsync(
                    ZaloApp.Session, _conversation.ThreadId, 1, 30, CancellationToken.None);

                if (reminders == null || reminders.Count == 0)
                {
                    if (_remindersStatusTextBlock != null) _remindersStatusTextBlock.Text = "Chưa có nhắc hẹn nào trong nhóm.";
                    return;
                }

                if (_remindersStatusTextBlock != null) _remindersStatusTextBlock.Text = $"{reminders.Count} nhắc hẹn gần đây";

                foreach (var rem in reminders)
                {
                    var itemBorder = new Border
                    {
                        Padding = new Thickness(0, 8, 0, 8),
                        BorderBrush = ColorBrush(0xFF, 0xEE, 0xF0, 0xF2),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Background = new SolidColorBrush(Colors.Transparent)
                    };
                    var itemGrid = new Grid();
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var calIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_calendar_clean_blue.png")),
                        Width = 20,
                        Height = 20,
                        VerticalAlignment = VerticalAlignment.Top,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    Grid.SetColumn(calIcon, 0);
                    itemGrid.Children.Add(calIcon);

                    var remStack = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
                    remStack.Children.Add(new TextBlock
                    {
                        Text = rem.Title ?? "Nhắc hẹn",
                        FontSize = 14,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _textDark,
                        TextWrapping = TextWrapping.Wrap
                    });
                    if (!string.IsNullOrEmpty(rem.TimeText))
                    {
                        remStack.Children.Add(new TextBlock
                        {
                            Text = rem.TimeText,
                            FontSize = 12,
                            Foreground = ColorBrush(0xFF, 0x52, 0x61, 0x71),
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    if (!string.IsNullOrEmpty(rem.RepeatText))
                    {
                        remStack.Children.Add(new TextBlock
                        {
                            Text = rem.RepeatText,
                            FontSize = 11,
                            Foreground = _textSub,
                            Margin = new Thickness(0, 1, 0, 0)
                        });
                    }

                    Grid.SetColumn(remStack, 1);
                    itemGrid.Children.Add(remStack);

                    if (rem.CanManage)
                    {
                        var delBtn = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xF1, 0xF0)),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(8, 4, 8, 4),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        delBtn.Child = new TextBlock { Text = "Xóa", FontSize = 11, Foreground = ColorBrush(0xFF, 0xB4, 0x23, 0x18), FontWeight = Windows.UI.Text.FontWeights.SemiBold };
                        var rItem = rem;
                        delBtn.Tapped += async (s, e) =>
                        {
                            var md = new MessageDialog($"Bạn có muốn xóa nhắc hẹn '{rItem.Title}'?", "Xóa nhắc hẹn");
                            md.Commands.Add(new UICommand("Xóa", async cmd =>
                            {
                                try
                                {
                                    await ZaloMessaging.RemoveGroupReminderAsync(ZaloApp.Session, _conversation.ThreadId, rItem.Id, CancellationToken.None);
                                    await LoadGroupRemindersAsync();
                                }
                                catch (Exception ex)
                                {
                                    await new MessageDialog("Lỗi xóa nhắc hẹn: " + ex.Message).ShowAsync();
                                }
                            }));
                            md.Commands.Add(new UICommand("Hủy"));
                            await md.ShowAsync();
                        };
                        Grid.SetColumn(delBtn, 2);
                        itemGrid.Children.Add(delBtn);
                    }

                    itemBorder.Child = itemGrid;
                    _remindersStack.Children.Add(itemBorder);
                }
            }
            catch (Exception ex)
            {
                if (_remindersStatusTextBlock != null) _remindersStatusTextBlock.Text = "Chưa tải được nhắc hẹn.";
                ZaloLog.Add("LoadGroupRemindersAsync failed: " + ex.Message);
            }
        }

        private async void CreateGroupNote_Click()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            var noteText = new TextBox
            {
                PlaceholderText = "Nhập nội dung ghi chú cho nhóm",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxLength = 500,
                MinHeight = 80
            };
            var pin = new CheckBox { Content = "Ghim ghi chú lên đầu bảng nhóm", Margin = new Thickness(0, 8, 0, 0) };
            var form = new StackPanel();
            form.Children.Add(noteText);
            form.Children.Add(pin);

            var dialog = new ContentDialog
            {
                Title = "Tạo ghi chú nhóm",
                Content = form,
                PrimaryButtonText = "Tạo ghi chú",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            string txt = (noteText.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt))
            {
                await new MessageDialog("Vui lòng nhập nội dung ghi chú.", "Ghi chú nhóm").ShowAsync();
                return;
            }

            try
            {
                await ZaloMessaging.CreateGroupNoteAsync(ZaloApp.Session, _conversation.ThreadId, txt, pin.IsChecked == true, CancellationToken.None);
                await LoadGroupBoardAsync();
                await new MessageDialog("Đã tạo ghi chú cho nhóm thành công.", "Ghi chú nhóm").ShowAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không tạo được ghi chú: " + ex.Message, "Ghi chú nhóm").ShowAsync();
            }
        }

        private async void ShowEditNoteDialog(string topicId, string currentNote)
        {
            if (_conversation == null || ZaloApp.Session == null || string.IsNullOrEmpty(topicId)) return;
            var noteText = new TextBox
            {
                Text = currentNote ?? string.Empty,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxLength = 500,
                MinHeight = 80
            };

            var dialog = new ContentDialog
            {
                Title = "Sửa ghi chú nhóm",
                Content = noteText,
                PrimaryButtonText = "Lưu",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            string txt = (noteText.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(txt)) return;

            try
            {
                await ZaloMessaging.UpdateGroupNoteAsync(ZaloApp.Session, _conversation.ThreadId, topicId, txt, CancellationToken.None);
                await LoadGroupBoardAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không sửa được ghi chú: " + ex.Message, "Sửa ghi chú").ShowAsync();
            }
        }

        private async void CreateGroupPoll_Click()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = "Câu hỏi bình chọn:", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            var question = new TextBox { PlaceholderText = "Nhập câu hỏi...", MaxLength = 200, Margin = new Thickness(0, 4, 0, 8) };
            content.Children.Add(question);

            content.Children.Add(new TextBlock { Text = "Các lựa chọn (ít nhất 2):", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            var optionPanel = new StackPanel();
            var optionBoxes = new List<TextBox>();
            Action addBox = () =>
            {
                var box = new TextBox { PlaceholderText = "Lựa chọn " + (optionBoxes.Count + 1), MaxLength = 100, Margin = new Thickness(0, 2, 0, 6) };
                optionBoxes.Add(box);
                optionPanel.Children.Add(box);
            };
            addBox();
            addBox();
            content.Children.Add(optionPanel);

            var addOptionBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8)
            };
            addOptionBtn.Child = new TextBlock { Text = "+ Thêm lựa chọn", FontSize = 12, Foreground = _activeBlue, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
            addOptionBtn.Tapped += (s, e) =>
            {
                if (optionBoxes.Count < 10) addBox();
            };
            content.Children.Add(addOptionBtn);

            var multiChoice = new CheckBox { Content = "Cho phép chọn nhiều đáp án", Margin = new Thickness(0, 2, 0, 2) };
            var anonymous = new CheckBox { Content = "Ẩn danh người bình chọn", Margin = new Thickness(0, 2, 0, 4) };
            content.Children.Add(multiChoice);
            content.Children.Add(anonymous);

            var dialog = new ContentDialog
            {
                Title = "Tạo bình chọn nhóm",
                Content = new ScrollViewer { Content = content, MaxHeight = 420 },
                PrimaryButtonText = "Tạo",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            List<string> options = optionBoxes.Select(b => (b.Text ?? string.Empty).Trim())
                .Where(val => val.Length > 0).ToList();

            if (string.IsNullOrWhiteSpace(question.Text) || options.Count < 2)
            {
                await new MessageDialog("Vui lòng nhập câu hỏi và ít nhất 2 phương án lựa chọn.", "Tạo bình chọn").ShowAsync();
                return;
            }

            try
            {
                await ZaloMessaging.CreatePollAsync(ZaloApp.Session, _conversation.ThreadId, question.Text,
                    options, multiChoice.IsChecked == true, anonymous.IsChecked == true, CancellationToken.None);
                await LoadGroupBoardAsync();
                await new MessageDialog("Đã tạo cuộc bình chọn thành công.", "Bình chọn").ShowAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không tạo được bình chọn: " + ex.Message, "Bình chọn").ShowAsync();
            }
        }

        private async void OpenPollDialog(long pollId)
        {
            if (pollId <= 0 || ZaloApp.Session == null) return;
            try
            {
                ZaloPollDetail poll = await ZaloMessaging.GetPollDetailAsync(ZaloApp.Session, pollId, CancellationToken.None);
                var content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = $"{poll.VoteCount} lượt bình chọn" + (poll.AllowMultiple ? " · chọn nhiều đáp án" : ""),
                    FontSize = 12,
                    Foreground = _textSub,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var choices = new List<FrameworkElement>();
                int totalOptionVotes = poll.Options.Sum(opt => Math.Max(0, opt.Votes));

                foreach (var option in poll.Options)
                {
                    double pct = totalOptionVotes <= 0 ? 0 : (100.0 * Math.Max(0, option.Votes) / totalOptionVotes);
                    var optStack = new StackPanel { Margin = new Thickness(4, 2, 4, 6) };
                    optStack.Children.Add(new TextBlock
                    {
                        Text = option.Content ?? "Lựa chọn",
                        FontSize = 14.5,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _textDark,
                        TextWrapping = TextWrapping.Wrap
                    });
                    optStack.Children.Add(new TextBlock
                    {
                        Text = $"{option.Votes} phiếu ({pct:F0}%)",
                        FontSize = 11.5,
                        Foreground = _textSub,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                    optStack.Children.Add(new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = pct,
                        Height = 4,
                        Margin = new Thickness(0, 4, 0, 0),
                        Foreground = _activeBlue
                    });

                    FrameworkElement control;
                    if (poll.AllowMultiple)
                    {
                        control = new CheckBox
                        {
                            Content = optStack,
                            IsChecked = option.Voted,
                            Tag = option.OptionId,
                            IsEnabled = !poll.Closed,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                    }
                    else
                    {
                        control = new RadioButton
                        {
                            Content = optStack,
                            IsChecked = option.Voted,
                            Tag = option.OptionId,
                            IsEnabled = !poll.Closed,
                            GroupName = "PollRadioGroup_" + poll.PollId,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                    }
                    choices.Add(control);
                    content.Children.Add(control);
                }

                if (poll.Closed)
                {
                    content.Children.Add(new TextBlock { Text = "Bình chọn này đã kết thúc.", FontSize = 12, Foreground = ColorBrush(0xFF, 0xDC, 0x26, 0x26), Margin = new Thickness(0, 6, 0, 0) });
                }

                var dialog = new ContentDialog
                {
                    Title = poll.Question,
                    Content = new ScrollViewer { Content = content, MaxHeight = 450 },
                    PrimaryButtonText = poll.Closed ? "Đóng" : "Bình chọn",
                    SecondaryButtonText = poll.Closed ? null : "Hủy"
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary || poll.Closed) return;

                var selected = new List<long>();
                foreach (var choice in choices)
                {
                    bool isChecked = choice is CheckBox ? ((CheckBox)choice).IsChecked == true
                        : choice is RadioButton && ((RadioButton)choice).IsChecked == true;
                    if (isChecked && choice.Tag != null && long.TryParse(choice.Tag.ToString(), out long optId))
                    {
                        selected.Add(optId);
                    }
                }

                if (selected.Count == 0)
                {
                    await new MessageDialog("Vui lòng chọn ít nhất 1 phương án để bình chọn.", "Bình chọn").ShowAsync();
                    return;
                }

                await ZaloMessaging.VotePollAsync(ZaloApp.Session, poll.PollId, selected, CancellationToken.None);
                await LoadGroupBoardAsync();
                await new MessageDialog("Đã gửi bình chọn của bạn thành công.", "Bình chọn").ShowAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Lỗi gửi bình chọn: " + ex.Message, "Bình chọn").ShowAsync();
            }
        }

        private async void CreateGroupReminder_Click()
        {
            if (_conversation == null || ZaloApp.Session == null) return;
            DateTimeOffset initial = DateTimeOffset.Now.AddHours(1);
            var title = new TextBox { PlaceholderText = "Nội dung nhắc hẹn", MaxLength = 200, Margin = new Thickness(0, 4, 0, 8) };
            var date = new DatePicker { Date = initial.Date, Margin = new Thickness(0, 4, 0, 8) };
            var time = new TimePicker { Time = initial.TimeOfDay, ClockIdentifier = "24HourClock", Margin = new Thickness(0, 4, 0, 8) };

            var repeat = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 4, 0, 8) };
            repeat.Items.Add(new ComboBoxItem { Content = "Không lặp", Tag = "0" });
            repeat.Items.Add(new ComboBoxItem { Content = "Hằng ngày", Tag = "1" });
            repeat.Items.Add(new ComboBoxItem { Content = "Hằng tuần", Tag = "2" });
            repeat.Items.Add(new ComboBoxItem { Content = "Hằng tháng", Tag = "3" });
            repeat.SelectedIndex = 0;

            var form = new StackPanel();
            form.Children.Add(new TextBlock { Text = "Nội dung:", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            form.Children.Add(title);
            form.Children.Add(new TextBlock { Text = "Ngày diễn ra:", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            form.Children.Add(date);
            form.Children.Add(new TextBlock { Text = "Giờ nhắc hẹn:", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            form.Children.Add(time);
            form.Children.Add(new TextBlock { Text = "Lặp lại:", FontSize = 12, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });
            form.Children.Add(repeat);

            var dialog = new ContentDialog
            {
                Title = "Tạo nhắc hẹn nhóm",
                Content = new ScrollViewer { Content = form, MaxHeight = 450 },
                PrimaryButtonText = "Tạo nhắc hẹn",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string remTitle = (title.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(remTitle))
            {
                await new MessageDialog("Vui lòng nhập nội dung nhắc hẹn.", "Nhắc hẹn nhóm").ShowAsync();
                return;
            }

            DateTimeOffset reminderTime = date.Date.Date.Add(time.Time);
            int repeatMode = 0;
            if (repeat.SelectedItem is ComboBoxItem sel && int.TryParse(sel.Tag?.ToString(), out int rMode))
            {
                repeatMode = rMode;
            }

            try
            {
                await ZaloMessaging.CreateGroupReminderAsync(ZaloApp.Session, _conversation.ThreadId,
                    remTitle, reminderTime.ToUnixTimeMilliseconds(), repeatMode, CancellationToken.None);
                await LoadGroupRemindersAsync();
                await new MessageDialog("Đã tạo nhắc hẹn cho nhóm thành công.", "Nhắc hẹn nhóm").ShowAsync();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Lỗi tạo nhắc hẹn: " + ex.Message, "Nhắc hẹn nhóm").ShowAsync();
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
                Padding = new Thickness(0, 10, 0, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Colors.Transparent)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarGrid = new Grid { Width = 44, Height = 44, VerticalAlignment = VerticalAlignment.Center };
            var ellipse = new Ellipse { Width = 44, Height = 44, Fill = ZaloAvatar.ColorFor(name ?? "Z") };
            var initial = new TextBlock
            {
                Text = ZaloAvatar.InitialOf(name),
                FontSize = 16,
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
                    var img = new Ellipse { Width = 44, Height = 44, Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(avatarUrl)), Stretch = Stretch.UniformToFill } };
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
                FontSize = 16,
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
                FontSize = 13,
                Foreground = role == "Trưởng nhóm" ? _activeBlue : (role == "Phó nhóm" ? ColorBrush(0xFF, 0x10, 0xB9, 0x81) : _textSub),
                FontWeight = role != "Thành viên" ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(roleText, 2);
            grid.Children.Add(roleText);

            border.Child = grid;
            border.PointerPressed += (s, e) => border.Opacity = 0.6;
            border.PointerReleased += (s, e) => border.Opacity = 1.0;
            border.PointerCanceled += (s, e) => border.Opacity = 1.0;
            border.Tapped += async (s, e) =>
            {
                if (string.IsNullOrEmpty(userId)) return;
                if (userId == ZaloApp.Session?.Uid) return;

                var friend = ZaloApp.Friends.FirstOrDefault(f => f.UserId == userId)
                    ?? new ZaloFriend { UserId = userId, Name = name, Avatar = avatarUrl };

                var optDialog = new MessageDialog($"Tùy chọn cho thành viên {name}:", name);
                optDialog.Commands.Add(new UICommand("Trang cá nhân", cmd =>
                {
                    NavigationManager.NavigateTo(new FriendProfilePage(friend));
                }));

                if (CanManageGroup())
                {
                    bool isDeputy = role == "Phó nhóm";
                    bool isOwner = role == "Trưởng nhóm";

                    if (!isOwner)
                    {
                        optDialog.Commands.Add(new UICommand(isDeputy ? "Hạ cấp phó nhóm" : "Bổ nhiệm phó nhóm", async cmd =>
                        {
                            try
                            {
                                await ZaloMessaging.SetGroupDeputyAsync(ZaloApp.Session, _conversation.ThreadId, userId, !isDeputy, CancellationToken.None);
                                await new MessageDialog(isDeputy ? "Đã gỡ quyền phó nhóm." : "Đã bổ nhiệm phó nhóm thành công.", "Phó nhóm").ShowAsync();
                                LoadGroupInfoAsync();
                            }
                            catch (Exception ex)
                            {
                                await new MessageDialog("Lỗi: " + ex.Message, "Phó nhóm").ShowAsync();
                            }
                        }));

                        optDialog.Commands.Add(new UICommand("Xóa khỏi nhóm", async cmd =>
                        {
                            var confirm = new MessageDialog($"Bạn có chắc chắn muốn mời {name} ra khỏi nhóm không?", "Xóa thành viên");
                            confirm.Commands.Add(new UICommand("Xóa", async c =>
                            {
                                try
                                {
                                    await ZaloMessaging.RemoveGroupMemberAsync(ZaloApp.Session, _conversation.ThreadId, userId, CancellationToken.None);
                                    await new MessageDialog($"Đã xóa {name} khỏi nhóm.", "Thông báo").ShowAsync();
                                    LoadGroupInfoAsync();
                                }
                                catch (Exception ex)
                                {
                                    await new MessageDialog("Lỗi xóa thành viên: " + ex.Message).ShowAsync();
                                }
                            }));
                            confirm.Commands.Add(new UICommand("Hủy"));
                            await confirm.ShowAsync();
                        }));
                    }
                }

                optDialog.Commands.Add(new UICommand("Đóng"));
                await optDialog.ShowAsync();
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
                Width = 52,
                Height = 52,
                CornerRadius = new CornerRadius(26),
                Background = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var img = new Image
            {
                Source = new BitmapImage(new Uri(iconUri)),
                Width = 26,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBox.Child = img;
            stack.Children.Add(iconBox);

            var text = new TextBlock
            {
                Text = label,
                FontSize = 12.5,
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

            var dialog = new ContentDialog
            {
                Title = "Đổi hình nền nhóm",
                Content = scroll,
                PrimaryButtonText = "Áp dụng",
                SecondaryButtonText = "Dùng mặc định"
            };

            var res = await dialog.ShowAsync();
            string settingKey = ZaloApp.Session == null ? string.Empty : ZaloApp.Session.Uid;
            if (res == ContentDialogResult.Primary && selectedUri != null)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(settingKey, _conversation.Key, selectedUri);
                await new MessageDialog("Đã cập nhật hình nền nhóm!", "Thành công").ShowAsync();
            }
            else if (res == ContentDialogResult.Secondary)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(settingKey, _conversation.Key, string.Empty);
                await new MessageDialog("Đã đặt lại hình nền mặc định.", "Thành công").ShowAsync();
            }
        }

        private async void ShowGroupMediaDialog()
        {
            var mediaMsgs = _conversation.Messages.Where(m => m.HasMedia || m.HasFile || m.HasLinkPreview).Take(40).ToList();
            if (mediaMsgs.Count == 0)
            {
                await new MessageDialog("Nhóm chưa có hình ảnh, tập tin hoặc liên kết nào.", "Ảnh, file, link").ShowAsync();
                return;
            }

            var scroll = new ScrollViewer { MaxHeight = 400 };
            var stack = new StackPanel();

            var dialog = new ContentDialog
            {
                Title = $"Ảnh, file, link ({mediaMsgs.Count})",
                Content = scroll,
                PrimaryButtonText = "Đóng"
            };

            foreach (var m in mediaMsgs)
            {
                var row = new Border { Padding = new Thickness(0, 6, 0, 6), BorderBrush = ColorBrush(0xFF, 0xEE, 0xF0, 0xF2), BorderThickness = new Thickness(0, 0, 0, 1), Background = new SolidColorBrush(Colors.Transparent) };
                var rowStack = new StackPanel();
                rowStack.Children.Add(new TextBlock { Text = m.Sender ?? "Người gửi", FontSize = 12, Foreground = _activeBlue });
                rowStack.Children.Add(new TextBlock { Text = m.FileName ?? (m.HasMedia ? "[Hình ảnh]" : m.Text), FontSize = 13.5, Foreground = _textDark, TextTrimming = TextTrimming.CharacterEllipsis });
                rowStack.Children.Add(new TextBlock { Text = m.TimeText ?? "", FontSize = 11, Foreground = _textSub });
                row.Child = rowStack;

                if (m.HasMedia && !string.IsNullOrEmpty(m.MediaUrl))
                {
                    row.PointerPressed += (s, e) => row.Opacity = 0.6;
                    row.PointerReleased += (s, e) => row.Opacity = 1.0;
                    row.PointerCanceled += (s, e) => row.Opacity = 1.0;
                    row.Tapped += (s, e) =>
                    {
                        dialog.Hide();
                        NavigationManager.NavigateTo(new ImageViewerPage(m.ViewerUrl ?? m.MediaUrl));
                    };
                }

                stack.Children.Add(row);
            }
            scroll.Content = stack;

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
