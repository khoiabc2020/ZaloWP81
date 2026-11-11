using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;
using ZaloUWP.Services;

namespace ZaloWP81.Views
{
    public sealed class MutualGroupsPage : Page
    {
        private readonly string _targetName;
        private readonly List<ZaloConversation> _groups;
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0x86, 0xF9));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x18, 0x27));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));

        public MutualGroupsPage(string targetName, List<ZaloConversation> groups)
        {
            _targetName = targetName ?? "Bạn bè";
            _groups = groups ?? new List<ZaloConversation>();
            BuildUI();
        }

        public MutualGroupsPage(string targetName, List<CommonGroupItem> mutualItems)
        {
            _targetName = targetName ?? "Bạn bè";
            _groups = new List<ZaloConversation>();
            if (mutualItems != null)
            {
                foreach (var item in mutualItems)
                {
                    var existing = ZaloApp.Conversations.FirstOrDefault(c => c.IsGroup && c.ThreadId == item.ThreadId);
                    if (existing != null)
                    {
                        _groups.Add(existing);
                    }
                    else
                    {
                        _groups.Add(new ZaloConversation
                        {
                            Key = "group_" + item.ThreadId,
                            ThreadId = item.ThreadId,
                            IsGroup = true,
                            Name = item.Name
                        });
                    }
                }
            }
            BuildUI();
        }

        public MutualGroupsPage(string targetName, List<Tuple<string, string>> mutualTuple)
        {
            _targetName = targetName ?? "Bạn bè";
            _groups = new List<ZaloConversation>();
            if (mutualTuple != null)
            {
                foreach (var item in mutualTuple)
                {
                    var existing = ZaloApp.Conversations.FirstOrDefault(c => c.IsGroup && c.ThreadId == item.Item1);
                    if (existing != null)
                    {
                        _groups.Add(existing);
                    }
                    else
                    {
                        _groups.Add(new ZaloConversation
                        {
                            Key = "group_" + item.Item1,
                            ThreadId = item.Item1,
                            IsGroup = true,
                            Name = item.Item2
                        });
                    }
                }
            }
            BuildUI();
        }

        private void BuildUI()
        {
            double scale = ZaloSettingsService.Current.UiScaleFactor;
            var rootGrid = new Grid { Background = new SolidColorBrush(Colors.White) };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Math.Round(60 * scale)) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. TOP HEADER (Vibrant Blue Gradient)
            var headerBorder = new Border { Padding = new Thickness(6, 0, 12, 0) };
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0)
            };
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x28, 0x78, 0xF0), Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x08, 0xA9, 0xE8), Offset = 1 });
            headerBorder.Background = gradient;

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var backBtn = CreateTouchBorder(42, 44);
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
                else NavigationManager.NavigateTo(new MainPage());
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var titleBlock = new TextBlock
            {
                Text = $"Nhóm chung ({_groups.Count})",
                FontSize = Math.Round(18 * scale, 1),
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleBlock, 1);
            header.Children.Add(titleBlock);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. BODY LIST
            var scroll = new ScrollViewer { Padding = new Thickness(0, 8, 0, 20) };
            var stack = new StackPanel();

            if (_groups.Count == 0)
            {
                var emptyStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(20, 60, 20, 0) };
                emptyStack.Children.Add(new TextBlock
                {
                    Text = "Không có nhóm chung",
                    FontSize = 17,
                    FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                    Foreground = _textDark,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyStack.Children.Add(new TextBlock
                {
                    Text = $"Bạn và {_targetName} chưa cùng tham gia nhóm nào.",
                    FontSize = 13.5,
                    Foreground = _textSub,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                stack.Children.Add(emptyStack);
            }
            else
            {
                double rowHeight = Math.Round(68 * scale);
                double avatarSize = Math.Round(48 * scale);

                foreach (var g in _groups)
                {
                    var groupItem = g;
                    var itemBorder = new Border
                    {
                        Height = rowHeight,
                        Padding = new Thickness(16, 0, 16, 0),
                        Background = new SolidColorBrush(Colors.White),
                        IsTapEnabled = true
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(avatarSize + 12) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Avatar
                    var avaGrid = new Grid { Width = avatarSize, Height = avatarSize, VerticalAlignment = VerticalAlignment.Center };
                    avaGrid.Children.Add(new Ellipse { Width = avatarSize, Height = avatarSize, Fill = groupItem.AvatarColor ?? _activeBlue });
                    avaGrid.Children.Add(new TextBlock
                    {
                        Text = groupItem.Initial ?? "N",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = Math.Round(18 * scale, 1),
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    if (groupItem.AvatarImageBrush != null)
                    {
                        avaGrid.Children.Add(new Ellipse { Width = avatarSize, Height = avatarSize, Fill = groupItem.AvatarImageBrush });
                    }
                    Grid.SetColumn(avaGrid, 0);
                    grid.Children.Add(avaGrid);

                    // Name and info
                    var txtStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 8, 0) };
                    txtStack.Children.Add(new TextBlock
                    {
                        Text = groupItem.Name ?? "Nhóm",
                        FontSize = Math.Round(16 * scale, 1),
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = _textDark,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    string subtitle = groupItem.GroupMemberCount > 0 ? $"{groupItem.GroupMemberCount} thành viên" : (groupItem.LastText ?? "Nhóm trò chuyện");
                    txtStack.Children.Add(new TextBlock
                    {
                        Text = subtitle,
                        FontSize = Math.Round(13 * scale, 1),
                        Foreground = _textSub,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                    Grid.SetColumn(txtStack, 1);
                    grid.Children.Add(txtStack);

                    // Chevron right
                    var chevron = new Image
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_chevron_right_clean_gray.png")),
                        Width = 20,
                        Height = 20,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(chevron, 2);
                    grid.Children.Add(chevron);

                    // Bottom divider
                    var div = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Margin = new Thickness(avatarSize + 12, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Bottom
                    };
                    Grid.SetColumnSpan(div, 3);
                    grid.Children.Add(div);

                    itemBorder.Child = grid;
                    itemBorder.PointerPressed += (s, e) => itemBorder.Opacity = 0.6;
                    itemBorder.PointerReleased += (s, e) => itemBorder.Opacity = 1.0;
                    itemBorder.PointerCanceled += (s, e) => itemBorder.Opacity = 1.0;
                    itemBorder.Tapped += (s, e) =>
                    {
                        var conv = ZaloApp.GetOrCreate(groupItem.ThreadId, true, true);
                        NavigationManager.NavigateTo(new ChatPage(conv.Key));
                    };

                    stack.Children.Add(itemBorder);
                }
            }

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
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
