using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public GroupInfoPage(string conversationKey)
        {
            _conversationKey = conversationKey;
            _conversation = ZaloApp.Find(conversationKey) ?? ZaloApp.Conversations.FirstOrDefault(c => c.Key == conversationKey);
            BuildUI();
            LoadGroupDataAsync();
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

            var avatarGrid = new Grid { Width = 84, Height = 84, HorizontalAlignment = HorizontalAlignment.Center };
            var avatarEllipse = new Ellipse { Width = 84, Height = 84, Fill = _conversation?.AvatarColor ?? _activeBlue };
            var avatarIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_group_blue.png")),
                Width = 36,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(avatarEllipse);
            avatarGrid.Children.Add(avatarIcon);

            if (_conversation?.AvatarImageBrush != null)
            {
                var img = new Ellipse { Width = 84, Height = 84, Fill = _conversation.AvatarImageBrush };
                avatarGrid.Children.Add(img);
            }
            cardStack.Children.Add(avatarGrid);

            _nameTextBlock = new TextBlock
            {
                Text = _conversation?.Name ?? "Nhóm trò chuyện",
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.Bold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 2),
                TextAlignment = TextAlignment.Center
            };
            cardStack.Children.Add(_nameTextBlock);

            _memberCountTextBlock = new TextBlock
            {
                Text = _conversation != null ? $"{_conversation.GroupMemberCount} thành viên" : "Đang tải thành viên...",
                FontSize = 13.5,
                Foreground = _textSub,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            cardStack.Children.Add(_memberCountTextBlock);

            var renameBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            renameBtn.Child = new TextBlock { Text = "Đổi tên nhóm", FontSize = 13, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
            renameBtn.PointerPressed += (s, e) => renameBtn.Opacity = 0.6;
            renameBtn.PointerReleased += (s, e) => renameBtn.Opacity = 1.0;
            renameBtn.PointerCanceled += (s, e) => renameBtn.Opacity = 1.0;
            renameBtn.Tapped += (s, e) => ShowRenameDialog();
            cardStack.Children.Add(renameBtn);

            card.Child = cardStack;
            stack.Children.Add(card);

            // 4 Action Buttons Grid (Tìm kiếm, Link mời, Đổi nền, Tắt thông báo)
            var actionGridCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 14, 10, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var actionGrid = new Grid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var col0 = CreateActionColumn("ms-appx:///Assets/Icons/ic_search_white.png", "Tìm tin nhắn", Color.FromArgb(0xFF, 0x16, 0x84, 0xD7));
            col0.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Nhập từ khóa cần tìm trong nhóm trên thanh tìm kiếm trang chủ.", "Tìm tin nhắn");
                await md.ShowAsync();
            };
            Grid.SetColumn(col0, 0);
            actionGrid.Children.Add(col0);

            var col1 = CreateActionColumn("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png", "Link mời", Color.FromArgb(0xFF, 0x10, 0xB9, 0x81));
            col1.Tapped += async (s, e) =>
            {
                string link = "https://zalo.me/g/" + (_conversation?.ThreadId ?? "group");
                var md = new MessageDialog($"Link tham gia nhóm:\n{link}", "Link mời vào nhóm");
                await md.ShowAsync();
            };
            Grid.SetColumn(col1, 1);
            actionGrid.Children.Add(col1);

            var col2 = CreateActionColumn("ms-appx:///Assets/Icons/ic_photo_gray.png", "Đổi hình nền", Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
            col2.Tapped += (s, e) => ShowWallpaperPicker();
            Grid.SetColumn(col2, 2);
            actionGrid.Children.Add(col2);

            var col3 = CreateActionColumn("ms-appx:///Assets/Icons/ic_mute_gray.png", "Thông báo", Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
            col3.Tapped += (s, e) =>
            {
                if (_conversation != null)
                {
                    _conversation.IsMuted = !_conversation.IsMuted;
                    var md = new MessageDialog(_conversation.IsMuted ? "Đã tắt thông báo nhóm." : "Đã bật thông báo nhóm.", "Thông báo");
                    var t = md.ShowAsync();
                }
            };
            Grid.SetColumn(col3, 3);
            actionGrid.Children.Add(col3);

            actionGridCard.Child = actionGrid;
            stack.Children.Add(actionGridCard);

            // Add Member Button
            var addMemberBorder = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var addGrid = new Grid();
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var addIcon = new Image { Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/Android/icn_header_addfriend.png")), Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(addIcon, 0);
            addGrid.Children.Add(addIcon);

            var addText = new TextBlock { Text = "Thêm thành viên vào nhóm", FontSize = 15, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(addText, 1);
            addGrid.Children.Add(addText);

            var addArrow = new TextBlock { Text = "›", FontSize = 20, Foreground = _textSub, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(addArrow, 2);
            addGrid.Children.Add(addArrow);

            addMemberBorder.Child = addGrid;
            addMemberBorder.PointerPressed += (s, e) => addMemberBorder.Opacity = 0.6;
            addMemberBorder.PointerReleased += (s, e) => addMemberBorder.Opacity = 1.0;
            addMemberBorder.PointerCanceled += (s, e) => addMemberBorder.Opacity = 1.0;
            addMemberBorder.Tapped += (s, e) => ShowAddMembersDialog();
            stack.Children.Add(addMemberBorder);

            // Members List Card
            var membersCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14, 16, 16),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var membersCardStack = new StackPanel();
            membersCardStack.Children.Add(new TextBlock
            {
                Text = "THÀNH VIÊN TRONG NHÓM",
                FontSize = 11.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textSub,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _membersStack = new StackPanel();
            membersCardStack.Children.Add(_membersStack);

            membersCard.Child = membersCardStack;
            stack.Children.Add(membersCard);

            // Danger Zone: Leave Group / Disband
            var dangerCard = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var dangerStack = new StackPanel();

            var leaveBorder = new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var leaveText = new TextBlock { Text = "Rời khỏi nhóm này", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x4B, 0x4B)) };
            leaveBorder.Child = leaveText;
            leaveBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Bạn có chắc muốn rời khỏi nhóm trò chuyện này?", "Rời nhóm");
                md.Commands.Add(new UICommand("Rời nhóm", cmd =>
                {
                    if (_conversation != null) ZaloApp.Conversations.Remove(_conversation);
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(leaveBorder);

            var disbandBorder = new Border { Padding = new Thickness(16, 14, 16, 14) };
            var disbandText = new TextBlock { Text = "Giải tán nhóm", FontSize = 14.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)) };
            disbandBorder.Child = disbandText;
            disbandBorder.Tapped += async (s, e) =>
            {
                var md = new MessageDialog("Giải tán nhóm sẽ xóa cuộc trò chuyện và mời tất cả thành viên rời khỏi nhóm.", "Giải tán nhóm");
                md.Commands.Add(new UICommand("Giải tán", cmd =>
                {
                    if (_conversation != null) ZaloApp.Conversations.Remove(_conversation);
                    NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                }));
                md.Commands.Add(new UICommand("Hủy"));
                await md.ShowAsync();
            };
            dangerStack.Children.Add(disbandBorder);

            dangerCard.Child = dangerStack;
            stack.Children.Add(dangerCard);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private async void LoadGroupDataAsync()
        {
            if (_conversation == null) return;

            _membersStack.Children.Clear();

            // Populate sample/loaded members
            var myInfo = ZaloApp.Session;
            _membersStack.Children.Add(CreateMemberRow(myInfo?.DisplayName ?? "Tôi (Bạn)", "Trưởng nhóm", myInfo?.AvatarUrl));

            foreach (var friend in ZaloApp.Friends.Take(6))
            {
                _membersStack.Children.Add(CreateMemberRow(friend.Name, "Thành viên", friend.Avatar));
            }
        }

        private FrameworkElement CreateMemberRow(string name, string role, string avatarUrl)
        {
            var border = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(0, 0, 0, 1)
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
                Foreground = role == "Trưởng nhóm" ? _activeBlue : _textSub,
                FontWeight = role == "Trưởng nhóm" ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(roleText, 2);
            grid.Children.Add(roleText);

            border.Child = grid;
            return border;
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

        private void ShowRenameDialog()
        {
            var md = new MessageDialog("Nhập tên mới cho nhóm:", "Đổi tên nhóm");
            md.Commands.Add(new UICommand("Đổi tên", cmd =>
            {
                if (_conversation != null)
                {
                    _conversation.Name = _conversation.Name + " (Đã đổi)";
                    _nameTextBlock.Text = _conversation.Name;
                }
            }));
            md.Commands.Add(new UICommand("Hủy"));
            var t = md.ShowAsync();
        }

        private void ShowAddMembersDialog()
        {
            var md = new MessageDialog("Chọn thành viên từ danh bạ để thêm vào nhóm:", "Thêm thành viên");
            foreach (var friend in ZaloApp.Friends.Take(3))
            {
                md.Commands.Add(new UICommand(friend.Name, cmd =>
                {
                    if (_conversation != null)
                    {
                        _conversation.GroupMemberCount++;
                        _memberCountTextBlock.Text = $"{_conversation.GroupMemberCount} thành viên";
                        _membersStack.Children.Add(CreateMemberRow(friend.Name, "Thành viên", friend.Avatar));
                    }
                }));
            }
            md.Commands.Add(new UICommand("Đóng"));
            var t = md.ShowAsync();
        }

        private void ShowWallpaperPicker()
        {
            var md = new MessageDialog("Chọn hình nền cho nhóm:", "Hình nền nhóm");
            md.Commands.Add(new UICommand("Hình nền 1", cmd => SetGroupWp("ms-appx:///Assets/Backgrounds/bg_01.png")));
            md.Commands.Add(new UICommand("Hình nền 2", cmd => SetGroupWp("ms-appx:///Assets/Backgrounds/bg_02.png")));
            md.Commands.Add(new UICommand("Hình nền 3", cmd => SetGroupWp("ms-appx:///Assets/Backgrounds/bg_03.png")));
            md.Commands.Add(new UICommand("Đóng"));
            var t = md.ShowAsync();
        }

        private void SetGroupWp(string uri)
        {
            if (_conversation != null)
            {
                ZaloSettingsService.Current.SetConversationWallpaper(ZaloApp.Session?.Uid, _conversation.Key, uri);
                var md = new MessageDialog("Đã đổi hình nền nhóm thành công!", "Thông báo");
                var t = md.ShowAsync();
            }
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
