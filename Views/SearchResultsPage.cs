using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class SearchPageRequest
    {
        public string ConversationKey { get; set; }
        public string Query { get; set; }
    }

    public sealed class SearchMessageHit
    {
        public ZaloConversation Conversation { get; private set; }
        public ZaloChatMessage Message { get; private set; }
        public long Timestamp { get { return Message == null ? 0 : Message.Ts; } }
        public string ConversationName { get { return Conversation == null ? "Trò chuyện" : Conversation.Name; } }
        public string Preview
        {
            get
            {
                if (Message == null) return string.Empty;
                string body = Message.Text ?? string.Empty;
                if (Conversation != null && Conversation.IsGroup && !string.IsNullOrWhiteSpace(Message.Sender))
                    body = Message.Sender + ": " + body;
                return body;
            }
        }
        public string TimeText { get { return Message == null ? string.Empty : ZaloFormat.Time(Message.Ts); } }

        public SearchMessageHit(ZaloConversation conversation, ZaloChatMessage message)
        {
            Conversation = conversation;
            Message = message;
        }
    }

    public sealed class SearchResultsPage : Page
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x77, 0x83, 0x92));

        private string _conversationKey;
        private string _conversationName;
        private TextBox _queryBox;
        private TextBlock _statusText;
        private StackPanel _resultsStack;
        private StackPanel _emptyPanel;
        private TextBlock _emptyTitle;
        private TextBlock _emptyDesc;

        public SearchResultsPage()
        {
            BuildUI();
        }

        public SearchResultsPage(string query) : this()
        {
            if (_queryBox != null) _queryBox.Text = query ?? string.Empty;
            SearchMessages(query);
        }

        public SearchResultsPage(SearchPageRequest request) : this()
        {
            ApplyRequest(request);
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF3, 0xF6, 0xFA))
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Top Bar
            var headerBorder = new Border { Background = _activeBlue, Padding = new Thickness(6, 0, 10, 0) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

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
                else NavigationManager.NavigateTo(new MainPage(), clearStack: true);
            };
            Grid.SetColumn(backBtn, 0);
            header.Children.Add(backBtn);

            var queryBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x35, 0x00, 0x00, 0x00)),
                CornerRadius = new CornerRadius(14),
                Height = 40,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 8, 0)
            };
            var queryGrid = new Grid();
            queryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            queryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _queryBox = new TextBox
            {
                PlaceholderText = "Tìm tin nhắn",
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                FontSize = 15.5,
                Height = 38,
                Padding = new Thickness(2, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            try
            {
                if (Application.Current.Resources.ContainsKey("SearchTextBoxStyle"))
                {
                    _queryBox.Style = (Style)Application.Current.Resources["SearchTextBoxStyle"];
                }
            }
            catch { }

            var clearBtn = CreateTouchBorder(32, 38);
            clearBtn.Visibility = Visibility.Collapsed;
            var clearText = new TextBlock
            {
                Text = "✕",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            clearBtn.Child = clearText;
            clearBtn.Tapped += (s, e) =>
            {
                _queryBox.Text = string.Empty;
                SearchMessages(string.Empty);
            };
            Grid.SetColumn(clearBtn, 1);
            queryGrid.Children.Add(clearBtn);

            _queryBox.TextChanged += (s, e) =>
            {
                clearBtn.Visibility = string.IsNullOrEmpty(_queryBox.Text) ? Visibility.Collapsed : Visibility.Visible;
                SearchMessages(_queryBox.Text);
            };
            _queryBox.KeyDown += (s, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    e.Handled = true;
                    SearchMessages(_queryBox.Text);
                }
            };
            Grid.SetColumn(_queryBox, 0);
            queryGrid.Children.Add(_queryBox);

            queryBorder.Child = queryGrid;
            Grid.SetColumn(queryBorder, 1);
            header.Children.Add(queryBorder);

            var searchBtn = CreateTouchBorder(42, 44);
            searchBtn.Child = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_search_clean_white.png")),
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            searchBtn.Tapped += (s, e) => SearchMessages(_queryBox.Text);
            Grid.SetColumn(searchBtn, 2);
            header.Children.Add(searchBtn);

            headerBorder.Child = header;
            Grid.SetRow(headerBorder, 0);
            rootGrid.Children.Add(headerBorder);

            // 2. Status Text
            _statusText = new TextBlock
            {
                Padding = new Thickness(14, 9, 14, 9),
                FontSize = 12.5,
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                Text = "Nhập từ khóa để tìm trong tin nhắn đã tải xuống."
            };
            Grid.SetRow(_statusText, 1);
            rootGrid.Children.Add(_statusText);

            // 3. Results Container & Empty State
            var contentGrid = new Grid();
            var scroll = new ScrollViewer { Padding = new Thickness(8, 4, 8, 12) };
            _resultsStack = new StackPanel();
            scroll.Content = _resultsStack;
            contentGrid.Children.Add(scroll);

            _emptyPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(24),
                MaxWidth = 320,
                Visibility = Visibility.Collapsed
            };
            var emptyIcon = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Icons/ic_search_clean_gray.png")),
                Width = 40,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _emptyPanel.Children.Add(emptyIcon);

            _emptyTitle = new TextBlock
            {
                Text = "Không tìm thấy tin nhắn",
                FontSize = 16.5,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _emptyPanel.Children.Add(_emptyTitle);

            _emptyDesc = new TextBlock
            {
                Text = "Kết quả chỉ gồm tin nhắn đã có trong lịch sử tải xuống.",
                FontSize = 13,
                Foreground = _textSub,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            _emptyPanel.Children.Add(_emptyDesc);

            contentGrid.Children.Add(_emptyPanel);

            Grid.SetRow(contentGrid, 2);
            rootGrid.Children.Add(contentGrid);

            this.Content = rootGrid;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is SearchPageRequest req)
            {
                ApplyRequest(req);
            }
            else if (e.Parameter is string q)
            {
                if (_queryBox != null) _queryBox.Text = q;
                SearchMessages(q);
            }
        }

        private void ApplyRequest(SearchPageRequest req)
        {
            if (req == null) return;
            _conversationKey = req.ConversationKey;
            _conversationName = string.IsNullOrWhiteSpace(_conversationKey) ? string.Empty
                : ZaloApp.Find(_conversationKey)?.Name ?? string.Empty;

            if (_queryBox != null)
            {
                _queryBox.PlaceholderText = string.IsNullOrWhiteSpace(_conversationName)
                    ? "Tìm tin nhắn" : "Tìm trong " + _conversationName;
                _queryBox.Text = req.Query ?? string.Empty;
            }
            SearchMessages(req.Query);
        }

        private void SearchMessages(string query)
        {
            string text = (query ?? string.Empty).Trim();
            if (_resultsStack == null) return;
            _resultsStack.Children.Clear();

            if (text.Length == 0)
            {
                _statusText.Text = "Nhập từ khóa để tìm trong tin nhắn đã tải xuống.";
                _emptyPanel.Visibility = Visibility.Visible;
                _emptyTitle.Text = "Tìm tin nhắn";
                _emptyDesc.Text = "Tìm trong các cuộc trò chuyện có lịch sử trên thiết bị này.";
                return;
            }

            string needle = Normalize(text);
            var hits = new List<SearchMessageHit>();
            IEnumerable<ZaloConversation> conversations = ZaloApp.Conversations;
            if (!string.IsNullOrWhiteSpace(_conversationKey))
                conversations = conversations.Where(c => c.Key == _conversationKey);

            foreach (var conv in conversations)
            {
                foreach (var msg in conv.Messages)
                {
                    if (msg == null || msg.IsRecallEvent || msg.IsRecalled) continue;
                    string body = msg.Text ?? string.Empty;
                    if (!Normalize(body).Contains(needle)) continue;
                    hits.Add(new SearchMessageHit(conv, msg));
                }
            }

            var ordered = hits.OrderByDescending(h => h.Timestamp).Take(200).ToList();
            if (ordered.Count == 0)
            {
                _emptyPanel.Visibility = Visibility.Visible;
                _emptyTitle.Text = "Không tìm thấy tin nhắn";
                _emptyDesc.Text = "Kết quả chỉ gồm tin nhắn đã có trong lịch sử tải xuống. Mở thêm cuộc trò chuyện rồi thử lại để tìm trong tin cũ hơn.";
            }
            else
            {
                _emptyPanel.Visibility = Visibility.Collapsed;
                foreach (var hit in ordered)
                {
                    _resultsStack.Children.Add(CreateHitCard(hit));
                }
            }

            string scopeText = string.IsNullOrWhiteSpace(_conversationName)
                ? "lịch sử hiện có trên thiết bị" : "lịch sử đã tải của " + _conversationName;
            _statusText.Text = ordered.Count == 0
                ? "Tìm trong " + scopeText + "."
                : ordered.Count == 200
                    ? "200 kết quả gần nhất · tìm trong " + scopeText + "."
                    : ordered.Count + " kết quả · tìm trong " + scopeText + ".";
        }

        private FrameworkElement CreateHitCard(SearchMessageHit hit)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(14, 11, 14, 11)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var convName = new TextBlock
            {
                Text = hit.ConversationName,
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetRow(convName, 0);
            Grid.SetColumn(convName, 0);
            grid.Children.Add(convName);

            var timeText = new TextBlock
            {
                Text = hit.TimeText,
                FontSize = 12,
                Foreground = _textSub,
                Margin = new Thickness(10, 1, 0, 0)
            };
            Grid.SetRow(timeText, 0);
            Grid.SetColumn(timeText, 1);
            grid.Children.Add(timeText);

            var preview = new TextBlock
            {
                Text = hit.Preview,
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x58, 0x67, 0x77)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(preview, 1);
            Grid.SetColumnSpan(preview, 2);
            grid.Children.Add(preview);

            card.Child = grid;

            card.PointerPressed += (s, e) => card.Opacity = 0.6;
            card.PointerReleased += (s, e) => card.Opacity = 1.0;
            card.PointerCanceled += (s, e) => card.Opacity = 1.0;
            card.Tapped += (s, e) =>
            {
                if (hit.Conversation != null)
                {
                    NavigationManager.NavigateTo(new ChatPage(hit.Conversation.Key));
                }
            };

            return card;
        }

        private static string Normalize(string value)
        {
            return ZaloFormat.NormalizeSearch(value);
        }

        private static Border CreateTouchBorder(double width, double height)
        {
            var b = new Border { Width = width, Height = height, Background = new SolidColorBrush(Colors.Transparent) };
            b.PointerPressed += (s, e) => b.Opacity = 0.6;
            b.PointerReleased += (s, e) => b.Opacity = 1.0;
            b.PointerCanceled += (s, e) => b.Opacity = 1.0;
            return b;
        }
    }
}