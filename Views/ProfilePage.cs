using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;
using ZaloLite;

namespace ZaloWP81.Views
{
    public sealed class ProfilePage : Page, IFileOpenPickerContinuable
    {
        private readonly SolidColorBrush _activeBlue = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x88, 0xFF));
        private readonly SolidColorBrush _textDark = new SolidColorBrush(Color.FromArgb(0xFF, 0x17, 0x21, 0x2B));
        private readonly SolidColorBrush _textSub = new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0x84, 0x90));

        private TextBlock _profileName;
        private TextBlock _profileUid;
        private TextBlock _profilePhone;
        private TextBlock _profileDobText;
        private TextBlock _profileGenderText;
        private TextBlock _profileStatus;
        private TextBlock _profileInitial;
        private Ellipse _profileAvatar;
        private TextBlock _profileFeedback;

        public ProfilePage()
        {
            BuildUI();
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            ShowProfile();
            var session = ZaloApp.Session;
            if (session == null) return;
            try
            {
                await ZaloAuth.LoadMyProfileAsync(session, CancellationToken.None);
                ShowProfile();
            }
            catch
            {
                _profileFeedback.Text = "Không làm mới được hồ sơ. Đang hiển thị thông tin đã lưu trong phiên.";
            }
        }

        private void BuildUI()
        {
            var rootGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF4, 0xF6, 0xF8))
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
                Text = "Trang cá nhân",
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

            // 2. ScrollViewer Body
            var scroll = new ScrollViewer { Padding = new Thickness(14, 16, 14, 20) };
            var stack = new StackPanel();

            // Card 1: Avatar & Main Profile Header
            var card1 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card1Stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var avatarGrid = new Grid { Width = 92, Height = 92, Margin = new Thickness(0, 2, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            var avatarBg = new Ellipse { Width = 92, Height = 92, Fill = _activeBlue };
            _profileInitial = new TextBlock
            {
                Text = "Z",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 34,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _profileAvatar = new Ellipse { Width = 92, Height = 92 };
            avatarGrid.Children.Add(avatarBg);
            avatarGrid.Children.Add(_profileInitial);
            avatarGrid.Children.Add(_profileAvatar);
            card1Stack.Children.Add(avatarGrid);

            _profileName = new TextBlock
            {
                Text = "Người dùng Zalo",
                FontSize = 21,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = _textDark,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            card1Stack.Children.Add(_profileName);

            var subHeader = new TextBlock
            {
                Text = "Thông tin hồ sơ của bạn",
                FontSize = 13,
                Foreground = _textSub,
                Margin = new Thickness(0, 4, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            card1Stack.Children.Add(subHeader);

            // Change Avatar Button
            var changeAvatarBtn = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(14, 6, 14, 6),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            changeAvatarBtn.Child = new TextBlock { Text = "Đổi ảnh đại diện", FontSize = 13, Foreground = _activeBlue, FontWeight = Windows.UI.Text.FontWeights.SemiBold };
            changeAvatarBtn.PointerPressed += (s, e) => changeAvatarBtn.Opacity = 0.6;
            changeAvatarBtn.PointerReleased += (s, e) => changeAvatarBtn.Opacity = 1.0;
            changeAvatarBtn.PointerCanceled += (s, e) => changeAvatarBtn.Opacity = 1.0;
            changeAvatarBtn.Tapped += (s, e) => ShowChangeAvatarDialog();
            card1Stack.Children.Add(changeAvatarBtn);

            // Edit Profile Button
            var editProfileBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18, 8, 18, 8),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            editProfileBtn.Child = new TextBlock { Text = "Chỉnh sửa hồ sơ", FontSize = 13.5, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue };
            editProfileBtn.PointerPressed += (s, e) => editProfileBtn.Opacity = 0.6;
            editProfileBtn.PointerReleased += (s, e) => editProfileBtn.Opacity = 1.0;
            editProfileBtn.PointerCanceled += (s, e) => editProfileBtn.Opacity = 1.0;
            editProfileBtn.Tapped += (s, e) => ShowEditProfileDialog();
            card1Stack.Children.Add(editProfileBtn);

            card1.Child = card1Stack;
            stack.Children.Add(card1);

            // Card 2: Account Details
            var card2 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card2Stack = new StackPanel();

            _profileUid = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Zalo ID", _profileUid));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profilePhone = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Số điện thoại", _profilePhone));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profileDobText = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Ngày sinh", _profileDobText));
            card2Stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF0, 0xF2)) });

            _profileGenderText = new TextBlock { FontSize = 14, Foreground = _textDark, VerticalAlignment = VerticalAlignment.Center };
            card2Stack.Children.Add(CreateFieldRow("Giới tính", _profileGenderText));

            card2.Child = card2Stack;
            stack.Children.Add(card2);

            // Card 3: Status (Bio)
            var card3 = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var card3Stack = new StackPanel();
            card3Stack.Children.Add(new TextBlock { Text = "Trạng thái", FontSize = 14, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _textDark });

            _profileStatus = new TextBlock
            {
                Text = "Chưa đặt trạng thái",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x80, 0x96)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 10)
            };
            card3Stack.Children.Add(_profileStatus);

            var editStatusBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xF3, 0xFF)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            editStatusBtn.Child = new TextBlock { Text = "Chỉnh sửa trạng thái", FontSize = 13, FontWeight = Windows.UI.Text.FontWeights.SemiBold, Foreground = _activeBlue, HorizontalAlignment = HorizontalAlignment.Center };
            editStatusBtn.PointerPressed += (s, e) => editStatusBtn.Opacity = 0.6;
            editStatusBtn.PointerReleased += (s, e) => editStatusBtn.Opacity = 1.0;
            editStatusBtn.PointerCanceled += (s, e) => editStatusBtn.Opacity = 1.0;
            editStatusBtn.Tapped += (s, e) => ShowEditStatusDialog();
            card3Stack.Children.Add(editStatusBtn);

            card3.Child = card3Stack;
            stack.Children.Add(card3);

            _profileFeedback = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x80, 0x96)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 4, 4, 0)
            };
            stack.Children.Add(_profileFeedback);

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            rootGrid.Children.Add(scroll);

            this.Content = rootGrid;
        }

        private Grid CreateFieldRow(string label, TextBlock valBlock)
        {
            var grid = new Grid { MinHeight = 52 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 14,
                Foreground = _textSub,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);

            return grid;
        }

        private void ShowProfile()
        {
            var session = ZaloApp.Session;
            if (session == null)
            {
                _profileFeedback.Text = "Chưa có tài khoản đang đăng nhập.";
                return;
            }

            string name = string.IsNullOrWhiteSpace(session.DisplayName) ? "Người dùng Zalo" : session.DisplayName;
            _profileName.Text = name;
            _profileInitial.Text = ZaloAvatar.InitialOf(name);
            _profileUid.Text = string.IsNullOrWhiteSpace(session.Uid) ? "Chưa có" : session.Uid;
            _profilePhone.Text = string.IsNullOrWhiteSpace(session.PhoneNumber) ? "Chưa công khai" : session.PhoneNumber;
            _profileStatus.Text = string.IsNullOrWhiteSpace(session.ProfileStatus) ? "Chưa đặt trạng thái" : session.ProfileStatus;

            DateTime dob;
            _profileDobText.Text = DateTime.TryParseExact(session.ProfileDob, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dob) ? dob.ToString("dd/MM/yyyy") : "Chưa công khai";
            _profileGenderText.Text = session.ProfileGender == 0 ? "Nam" : session.ProfileGender == 1 ? "Nữ" : "Chưa công khai";

            if (!string.IsNullOrWhiteSpace(session.AvatarUrl))
            {
                try
                {
                    _profileAvatar.Fill = new ImageBrush { ImageSource = new BitmapImage(new Uri(session.AvatarUrl)), Stretch = Stretch.UniformToFill };
                    _profileInitial.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    _profileAvatar.Fill = new SolidColorBrush(Colors.Transparent);
                    _profileInitial.Visibility = Visibility.Visible;
                }
            }
            else
            {
                _profileAvatar.Fill = new SolidColorBrush(Colors.Transparent);
                _profileInitial.Visibility = Visibility.Visible;
            }
        }

        private async void ShowChangeAvatarDialog()
        {
            var session = ZaloApp.Session;
            if (session == null) return;

            var md = new MessageDialog("Chọn phương thức đổi ảnh đại diện:", "Đổi ảnh đại diện");
            md.Commands.Add(new UICommand("Chọn từ thư viện máy", cmd =>
            {
                try
                {
                    var picker = new Windows.Storage.Pickers.FileOpenPicker
                    {
                        SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary,
                        ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail
                    };
                    picker.FileTypeFilter.Add(".jpg");
                    picker.FileTypeFilter.Add(".jpeg");
                    picker.FileTypeFilter.Add(".png");
                    picker.PickSingleFileAndContinue();
                }
                catch (Exception ex)
                {
                    _profileFeedback.Text = "Lỗi mở thư viện: " + ex.Message;
                }
            }));
            md.Commands.Add(new UICommand("Nhập liên kết ảnh (URL)", async cmd =>
            {
                await ShowAvatarUrlDialog();
            }));
            md.Commands.Add(new UICommand("Hủy"));
            await md.ShowAsync();
        }

        public async void ContinueWithFileOpenPicker(Windows.ApplicationModel.Activation.FileOpenPickerContinuationEventArgs args)
        {
            if (args == null || args.Files == null || args.Files.Count == 0) return;
            var file = args.Files[0];
            var session = ZaloApp.Session;
            if (session == null) return;

            _profileFeedback.Text = "Đang xử lý và tải lên ảnh đại diện từ máy…";
            try
            {
                using (var stream = await file.OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                    uint orientedWidth = decoder.OrientedPixelWidth;
                    uint orientedHeight = decoder.OrientedPixelHeight;
                    double scale = Math.Min(1.0, 1600.0 / Math.Max(orientedWidth, orientedHeight));
                    uint width = Math.Max(1U, (uint)Math.Round(orientedWidth * scale));
                    uint height = Math.Max(1U, (uint)Math.Round(orientedHeight * scale));
                    var transform = new BitmapTransform
                    {
                        ScaledWidth = width,
                        ScaledHeight = height,
                        InterpolationMode = BitmapInterpolationMode.Fant
                    };
                    PixelDataProvider pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,
                        BitmapAlphaMode.Ignore, transform, ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    byte[] jpegBytes;
                    using (var output = new InMemoryRandomAccessStream())
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
                        encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore, width, height, 96, 96, pixels.DetachPixelData());
                        await encoder.FlushAsync();
                        output.Seek(0);
                        using (var reader = new DataReader(output.GetInputStreamAt(0)))
                        {
                            await reader.LoadAsync((uint)output.Size);
                            jpegBytes = new byte[(int)output.Size];
                            reader.ReadBytes(jpegBytes);
                        }
                    }

                    _profileFeedback.Text = "Đang gửi lên Zalo…";
                    await ZaloAuth.UpdateMyAvatarAsync(session, jpegBytes, width, height, CancellationToken.None);
                    await ZaloAuth.LoadMyProfileAsync(session, CancellationToken.None);
                    ShowProfile();
                    _profileFeedback.Text = "Đã cập nhật ảnh đại diện thành công.";
                }
            }
            catch (Exception ex)
            {
                ZaloLog.Add("Profile avatar picker upload failed: " + ex.Message);
                _profileFeedback.Text = "Không đổi được ảnh đại diện: " + ex.Message;
            }
        }

        private async Task ShowAvatarUrlDialog()
        {
            var session = ZaloApp.Session;
            if (session == null) return;

            var input = new TextBox
            {
                PlaceholderText = "Nhập URL ảnh (https://...)",
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 50
            };

            var content = new StackPanel { Width = 300 };
            content.Children.Add(new TextBlock
            {
                Text = "Dán link ảnh trực tuyến để cập nhật ảnh đại diện Zalo:",
                FontSize = 13,
                Foreground = _textDark,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(input);

            var dialog = new ContentDialog
            {
                Title = "Đổi ảnh từ liên kết URL",
                Content = content,
                PrimaryButtonText = "Tải lên",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string url = (input.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(url)) return;

            _profileFeedback.Text = "Đang tải và xử lý ảnh đại diện…";
            try
            {
                byte[] jpegBytes;
                uint width = 640;
                uint height = 640;

                using (var http = new HttpClient())
                {
                    byte[] raw = await http.GetByteArrayAsync(url);
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteBytes(raw);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                            writer.DetachStream();
                        }
                        stream.Seek(0);
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                        uint orientedWidth = decoder.OrientedPixelWidth;
                        uint orientedHeight = decoder.OrientedPixelHeight;
                        double scale = Math.Min(1.0, 1600.0 / Math.Max(orientedWidth, orientedHeight));
                        width = Math.Max(1U, (uint)Math.Round(orientedWidth * scale));
                        height = Math.Max(1U, (uint)Math.Round(orientedHeight * scale));
                        var transform = new BitmapTransform
                        {
                            ScaledWidth = width,
                            ScaledHeight = height,
                            InterpolationMode = BitmapInterpolationMode.Fant
                        };
                        PixelDataProvider pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,
                            BitmapAlphaMode.Ignore, transform, ExifOrientationMode.RespectExifOrientation,
                            ColorManagementMode.DoNotColorManage);

                        using (var output = new InMemoryRandomAccessStream())
                        {
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
                            encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore, width, height, 96, 96, pixels.DetachPixelData());
                            await encoder.FlushAsync();
                            output.Seek(0);
                            using (var reader = new DataReader(output.GetInputStreamAt(0)))
                            {
                                await reader.LoadAsync((uint)output.Size);
                                jpegBytes = new byte[(int)output.Size];
                                reader.ReadBytes(jpegBytes);
                            }
                        }
                    }
                }

                _profileFeedback.Text = "Đang gửi lên Zalo…";
                await ZaloAuth.UpdateMyAvatarAsync(session, jpegBytes, width, height, CancellationToken.None);
                await ZaloAuth.LoadMyProfileAsync(session, CancellationToken.None);
                ShowProfile();
                _profileFeedback.Text = "Đã cập nhật ảnh đại diện thành công.";
            }
            catch (Exception ex)
            {
                ZaloLog.Add("Profile avatar update failed: " + ex.Message);
                _profileFeedback.Text = "Không đổi được ảnh đại diện: " + ex.Message;
            }
        }

        private async void ShowEditProfileDialog()
        {
            var session = ZaloApp.Session;
            if (session == null) return;
            if (session.HasBusinessCategory)
            {
                _profileFeedback.Text = "Tài khoản doanh nghiệp cần gửi lại danh mục biz.cate để tránh làm mất cấu hình. Tạm thời không bật sửa hồ sơ.";
                return;
            }

            var nameInput = new TextBox
            {
                Header = "Tên hiển thị",
                Text = session.DisplayName ?? string.Empty,
                MaxLength = 40,
                PlaceholderText = "Tên của bạn"
            };
            var dobInput = new TextBox
            {
                Header = "Ngày sinh (YYYY-MM-DD)",
                Text = session.ProfileDob ?? string.Empty,
                PlaceholderText = "Ví dụ: 1995-08-24"
            };
            var genderInput = new ComboBox { Header = "Giới tính", HorizontalAlignment = HorizontalAlignment.Stretch };
            genderInput.Items.Add(new ComboBoxItem { Content = "Nam" });
            genderInput.Items.Add(new ComboBoxItem { Content = "Nữ" });
            if (session.ProfileGender == 0 || session.ProfileGender == 1) genderInput.SelectedIndex = session.ProfileGender;

            var content = new StackPanel { Width = 300 };
            content.Children.Add(nameInput);
            content.Children.Add(dobInput);
            content.Children.Add(genderInput);

            var dialog = new ContentDialog
            {
                Title = "Chỉnh sửa hồ sơ",
                Content = content,
                PrimaryButtonText = "Lưu",
                SecondaryButtonText = "Hủy"
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string name = (nameInput.Text ?? string.Empty).Trim();
            string dobText = (dobInput.Text ?? string.Empty).Trim();
            DateTime parsedDob;
            if (name.Length == 0 || name.Length > 40)
            {
                _profileFeedback.Text = "Tên hiển thị cần có từ 1 đến 40 ký tự.";
                return;
            }
            if (!DateTime.TryParseExact(dobText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsedDob) || parsedDob.Date > DateTime.Today || parsedDob.Year < 1900)
            {
                _profileFeedback.Text = "Ngày sinh cần đúng dạng YYYY-MM-DD và không ở tương lai.";
                return;
            }
            if (genderInput.SelectedIndex < 0)
            {
                _profileFeedback.Text = "Hãy chọn giới tính trước khi lưu.";
                return;
            }

            _profileFeedback.Text = "Đang cập nhật hồ sơ…";
            try
            {
                await ZaloAuth.UpdateMyProfileAsync(session, name, parsedDob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    genderInput.SelectedIndex, CancellationToken.None);
                session.DisplayName = name;
                session.ProfileDob = parsedDob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                session.ProfileGender = genderInput.SelectedIndex;
                ShowProfile();
                _profileFeedback.Text = "Đã cập nhật hồ sơ thành công.";
                _ = ZaloStore.SaveAsync(session);
            }
            catch (Exception ex)
            {
                _profileFeedback.Text = "Không cập nhật được hồ sơ: " + ex.Message;
            }
        }

        private async void ShowEditStatusDialog()
        {
            var session = ZaloApp.Session;
            if (session == null) return;

            var input = new TextBox
            {
                Text = session.ProfileStatus ?? string.Empty,
                PlaceholderText = "Nhập trạng thái của bạn",
                MaxLength = 250,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 72
            };
            var dialog = new ContentDialog
            {
                Title = "Cập nhật trạng thái",
                Content = input,
                PrimaryButtonText = "Lưu",
                SecondaryButtonText = "Hủy"
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await ZaloAuth.UpdateMyStatusAsync(session, input.Text.Trim(), CancellationToken.None);
                session.ProfileStatus = input.Text.Trim();
                _profileStatus.Text = string.IsNullOrWhiteSpace(session.ProfileStatus) ? "Chưa đặt trạng thái" : session.ProfileStatus;
                _profileFeedback.Text = "Đã cập nhật trạng thái.";
                _ = ZaloStore.SaveAsync(session);
            }
            catch (Exception ex)
            {
                _profileFeedback.Text = "Không cập nhật được trạng thái: " + ex.Message;
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
