using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
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
    public sealed partial class ChatPage : Page, IFileOpenPickerContinuable
    {
        private void ResetActiveVoicePlayer()
        {
            if (_activeVoicePlayIcon != null)
            {
                try
                {
                    _activeVoicePlayIcon.Source = SafeAppxImage("ms-appx:///Assets/Icons/ic_play_clean_white.png");
                }
                catch { }
            }
            if (_activeVoiceStatusText != null && _activeVoiceStatusText.Text == "Đang phát...")
            {
                _activeVoiceStatusText.Text = "Chạm để nghe";
            }
            _activeVoiceMsg = null;
            _activeVoicePlayIcon = null;
            _activeVoiceStatusText = null;
        }

        private async void StartVoiceRecording()
        {
            if (_isRecordingVoice || _conversation == null) return;
            try
            {
                bool started = await ZaloVoiceService.Current.StartRecordingAsync();
                if (!started)
                {
                    var md = new MessageDialog("Không thể truy cập microphone. Vui lòng vào Cài đặt -> Quyền riêng tư -> Micrô (Settings -> Privacy -> Microphone) và cho phép Zalo.\n\nChi tiết: " + ZaloVoiceService.Current.LastError, "Lỗi Micro");
                    await md.ShowAsync();
                    return;
                }

                _isRecordingVoice = true;
                _recordingSeconds = 0;
                _recordingTimeText.Text = "00:00 · Đang ghi âm...";
                _recordingTimer.Start();
                _inputBarGrid.Visibility = Visibility.Collapsed;
                _recordingBarGrid.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi mở micro: " + ex.Message, "Lỗi");
                await md.ShowAsync();
            }
        }

        private async void CancelVoiceRecording()
        {
            if (!_isRecordingVoice) return;
            _recordingTimer.Stop();
            _isRecordingVoice = false;
            _recordingBarGrid.Visibility = Visibility.Collapsed;
            _inputBarGrid.Visibility = Visibility.Visible;
            try
            {
                var file = await ZaloVoiceService.Current.StopRecordingAsync();
                if (file != null) await file.DeleteAsync();
            }
            catch { }
        }

        private async void FinishAndSendVoiceRecording()
        {
            if (!_isRecordingVoice || _conversation == null) return;
            _recordingTimer.Stop();
            _isRecordingVoice = false;
            _recordingBarGrid.Visibility = Visibility.Collapsed;
            _inputBarGrid.Visibility = Visibility.Visible;

            try
            {
                var file = await ZaloVoiceService.Current.StopRecordingAsync();
                if (file != null)
                {
                    var props = await file.GetBasicPropertiesAsync();
                    if (props.Size > 400)
                    {
                        using (var stream = await file.OpenReadAsync())
                        {
                            await ZaloApp.SendVoiceAsync(_conversation, file.Name, stream, (long)props.Size);
                        }
                        RenderMessages();
                        ScrollToBottom();
                    }
                    try { await file.DeleteAsync(); } catch { }
                }
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi tin nhắn thoại: " + ex.Message, "Lỗi");
                await md.ShowAsync();
            }
        }

        private void PickPhoto()
        {
            if (_conversation == null) return;
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                    ViewMode = PickerViewMode.Thumbnail
                };
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".bmp");
                picker.ContinuationData["ConversationKey"] = _conversation.Key;
                picker.ContinuationData["Action"] = "SendPhoto";
                picker.PickSingleFileAndContinue();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể mở bộ chọn ảnh: " + ex.Message + "\nHãy kiểm tra quyền Thư viện ảnh trong Cài đặt.", "Lỗi ảnh");
                var ignore = md.ShowAsync();
            }
        }

        private void PickDocumentFile()
        {
            if (_conversation == null) return;
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    ViewMode = PickerViewMode.List
                };
                picker.FileTypeFilter.Add(".pdf");
                picker.FileTypeFilter.Add(".doc");
                picker.FileTypeFilter.Add(".docx");
                picker.FileTypeFilter.Add(".xls");
                picker.FileTypeFilter.Add(".xlsx");
                picker.FileTypeFilter.Add(".ppt");
                picker.FileTypeFilter.Add(".pptx");
                picker.FileTypeFilter.Add(".zip");
                picker.FileTypeFilter.Add(".rar");
                picker.FileTypeFilter.Add(".7z");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".mp4");
                picker.ContinuationData["ConversationKey"] = _conversation.Key;
                picker.ContinuationData["Action"] = "SendFile";
                picker.PickSingleFileAndContinue();
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể mở bộ chọn tệp: " + ex.Message, "Lỗi tệp");
                var ignore = md.ShowAsync();
            }
        }

        public async void ContinueWithFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            if (args == null || args.Files == null || args.Files.Count == 0) return;
            if (_conversation == null)
            {
                string convKey = (args.ContinuationData != null && args.ContinuationData.ContainsKey("ConversationKey"))
                    ? args.ContinuationData["ConversationKey"] as string : null;
                if (!string.IsNullOrEmpty(convKey))
                {
                    _conversation = ZaloApp.Conversations.FirstOrDefault(c => c.Key == convKey);
                }
            }
            if (_conversation == null) return;

            string action = (args.ContinuationData != null && args.ContinuationData.ContainsKey("Action"))
                ? args.ContinuationData["Action"] as string : "SendPhoto";

            if (string.Equals(action, "SendFile", StringComparison.OrdinalIgnoreCase))
            {
                await SendStorageFileDocumentAsync(args.Files[0]);
            }
            else
            {
                await SendStorageFilePhotoAsync(args.Files[0]);
            }
        }

        private async Task SendStorageFileDocumentAsync(StorageFile file)
        {
            if (file == null || _conversation == null) return;
            try
            {
                var props = await file.GetBasicPropertiesAsync();
                ulong size = props.Size;
                using (var stream = await file.OpenReadAsync())
                {
                    await ZaloApp.SendFileAsync(_conversation, file.Name, stream, (long)size);
                }
                RenderMessages();
            }
            catch (Exception ex)
            {
                await new MessageDialog("Không thể gửi tệp: " + ex.Message, "Gửi tệp thất bại").ShowAsync();
            }
        }

        private async System.Threading.Tasks.Task CapturePhotoFromCameraAsync()
        {
            if (_conversation == null) return;
            try
            {
                var capture = new Windows.Media.Capture.MediaCapture();
                var initSettings = new Windows.Media.Capture.MediaCaptureInitializationSettings
                {
                    StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Video,
                    PhotoCaptureSource = Windows.Media.Capture.PhotoCaptureSource.Photo
                };
                await capture.InitializeAsync(initSettings);

                var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                var file = await tempFolder.CreateFileAsync("zalo_cam_" + DateTime.Now.Ticks + ".jpg", CreationCollisionOption.GenerateUniqueName);
                var encProps = Windows.Media.MediaProperties.ImageEncodingProperties.CreateJpeg();
                await capture.CapturePhotoToStorageFileAsync(encProps, file);
                capture.Dispose();

                await SendStorageFilePhotoAsync(file);
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Không thể truy cập máy ảnh: " + ex.Message, "Lỗi máy ảnh");
                await md.ShowAsync();
            }
        }

        private async System.Threading.Tasks.Task SendStorageFilePhotoAsync(StorageFile file)
        {
            if (file == null || _conversation == null) return;
            try
            {
                var props = await file.GetBasicPropertiesAsync();
                using (var imageStream = await file.OpenReadAsync())
                {
                    uint width = 800;
                    uint height = 600;
                    try
                    {
                        var decoder = await BitmapDecoder.CreateAsync(imageStream);
                        width = decoder.PixelWidth;
                        height = decoder.PixelHeight;
                        imageStream.Seek(0);
                    }
                    catch
                    {
                        try { imageStream.Seek(0); } catch { }
                    }

                    string caption = _inputBox.Text?.Trim() ?? string.Empty;
                    _inputBox.Text = string.Empty;
                    await ZaloApp.SendImageAsync(_conversation, file.Name, imageStream, (long)props.Size, width, height, caption);
                }
                RenderMessages();
                ScrollToBottom(instant: true);
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi ảnh: " + ex.Message, "Gửi ảnh thất bại");
                await md.ShowAsync();
            }
        }

        private async Task ShowSendUrlPhotoDialog()
        {
            if (_conversation == null) return;
            var input = new TextBox
            {
                PlaceholderText = "Nhập URL ảnh (https://... hoặc http://...)",
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 44
            };
            var cd = new ContentDialog
            {
                Title = "Gửi ảnh từ liên kết URL",
                Content = input,
                PrimaryButtonText = "Gửi",
                SecondaryButtonText = "Hủy"
            };
            App.StyleContentDialog(cd);
            var res = await cd.ShowAsync();
            if (res == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            {
                string caption = _inputBox.Text?.Trim() ?? string.Empty;
                _inputBox.Text = string.Empty;
                await SendPhotoFromUrlAsync(input.Text.Trim(), caption);
            }
        }

        private async Task SendPhotoFromUrlAsync(string url, string caption)
        {
            if (string.IsNullOrWhiteSpace(url) || _conversation == null) return;
            try
            {
                byte[] bytes = null;
                if (ZaloApp.Session != null)
                {
                    try { bytes = await ZaloApp.Session.Http.DownloadBytesAsync(url, CancellationToken.None); }
                    catch { }
                }
                if (bytes == null)
                {
                    using (var hc = new System.Net.Http.HttpClient())
                    {
                        bytes = await hc.GetByteArrayAsync(url);
                    }
                }
                if (bytes != null && bytes.Length > 0)
                {
                    using (var stream = new InMemoryRandomAccessStream())
                    using (var writer = new DataWriter(stream))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream();
                        stream.Seek(0);
                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        uint width = decoder.PixelWidth;
                        uint height = decoder.PixelHeight;
                        stream.Seek(0);
                        string fileName = "photo_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
                        await ZaloApp.SendImageAsync(_conversation, fileName, stream, (long)bytes.Length, width, height, caption ?? string.Empty);
                    }
                    RenderMessages();
                    ScrollToBottom();
                }
                else
                {
                    throw new Exception("Không tải được dữ liệu ảnh từ URL này.");
                }
            }
            catch (Exception ex)
            {
                var md = new MessageDialog("Lỗi gửi ảnh từ liên kết: " + ex.Message, "Thất bại");
                await md.ShowAsync();
            }
        }

        private async Task ShowShareContactDialog()
        {
            if (_conversation == null) return;
            if (ZaloApp.Friends == null || ZaloApp.Friends.Count == 0)
            {
                var md = new MessageDialog("Danh bạ chưa có bạn bè để chia sẻ danh thiếp.", "Chia sẻ danh thiếp");
                await md.ShowAsync();
                return;
            }

            var scroll = new ScrollViewer { MaxHeight = 350 };
            var listStack = new StackPanel();
            var cd = new ContentDialog
            {
                Title = "Chọn liên hệ cần chia sẻ",
                PrimaryButtonText = "Đóng",
                Content = scroll
            };
            App.StyleContentDialog(cd);

            foreach (var fr in ZaloApp.Friends.Take(30))
            {
                var item = new Border
                {
                    Padding = new Thickness(10, 8, 10, 8),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var ava = new Ellipse { Width = 32, Height = 32, Fill = fr.AvatarImageBrush ?? ZaloAvatar.ColorFor(fr.DisplayName ?? "Z") };
                Grid.SetColumn(ava, 0);
                grid.Children.Add(ava);

                var nameBlock = new TextBlock
                {
                    Text = fr.DisplayName ?? "Bạn bè",
                    FontSize = 14.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    Foreground = _textDark
                };
                Grid.SetColumn(nameBlock, 1);
                grid.Children.Add(nameBlock);

                item.Child = grid;
                var currentFr = fr;
                item.Tapped += async (s, e) =>
                {
                    cd.Hide();
                    await ZaloApp.SendContactCardAsync(_conversation, currentFr);
                };
                listStack.Children.Add(item);
            }
            scroll.Content = listStack;
            await cd.ShowAsync();
        }

        private FrameworkElement CreateStickerDrawer()
        {
            var drawerBorder = new Border
            {
                Height = 210,
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xEA, 0xEC, 0xEF)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tabsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var tabEmoji = CreateDrawerTab("Emoji", true);
            var tabSticker = CreateDrawerTab("Nhãn dán", false);
            var tabGif = CreateDrawerTab("GIF", false);
            var tabZalo = CreateDrawerTab("Zalo", false);

            tabsStack.Children.Add(tabEmoji);
            tabsStack.Children.Add(tabSticker);
            tabsStack.Children.Add(tabGif);
            tabsStack.Children.Add(tabZalo);

            Grid.SetRow(tabsStack, 0);
            grid.Children.Add(tabsStack);

            var scroll = new ScrollViewer { Padding = new Thickness(10, 6, 10, 10) };
            var stack = new StackPanel();

            int count = 0;
            StackPanel currentRow = null;
            for (int i = 0; i < 35; i++)
            {
                if (count % 7 == 0)
                {
                    currentRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 3, 0, 3),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    stack.Children.Add(currentRow);
                }

                string emoIndex = i < 10 ? "0" + i : i.ToString();
                string uri = $"ms-appx:///Assets/Emoticons/Emoji/emo_{emoIndex}.png";

                var emoBtn = CreateTouchBorder(44, 44);
                emoBtn.Margin = new Thickness(2, 0, 2, 0);

                var emoImg = new Image
                {
                    Source = SafeAppxImage(uri),
                    Width = 34,
                    Height = 34,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                emoBtn.Child = emoImg;

                emoBtn.Tapped += (s, e) =>
                {
                    if (_conversation != null)
                    {
                        long nowMs = DateTime.UtcNow.Ticks / 10000;
                        var stickerMsg = new ZaloChatMessage
                        {
                            FromMe = true,
                            Text = "[Sticker]",
                            MediaUrl = uri,
                            MsgType = "chat.sticker",
                            Ts = nowMs
                        };
                        _conversation.Messages.Add(stickerMsg);
                        _conversation.LastText = "[Sticker]";
                        _conversation.LastTs = nowMs;
                        ToggleDrawer();
                    }
                };

                currentRow.Children.Add(emoBtn);
                count++;
            }

            scroll.Content = stack;
            Grid.SetRow(scroll, 1);
            grid.Children.Add(scroll);

            drawerBorder.Child = grid;
            return drawerBorder;
        }

        private FrameworkElement CreateDrawerTab(string text, bool active)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = active ? new SolidColorBrush(Color.FromArgb(0xFF, 0xEE, 0xF4, 0xFA)) : new SolidColorBrush(Colors.Transparent),
                CornerRadius = new CornerRadius(14)
            };
            var txt = new TextBlock
            {
                Text = text,
                FontSize = 13.5,
                FontWeight = active ? Windows.UI.Text.FontWeights.SemiBold : Windows.UI.Text.FontWeights.Normal,
                Foreground = active ? _activeBlue : new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B))
            };
            border.Child = txt;
            return border;
        }

        private void ToggleDrawer()
        {
            _isDrawerOpen = !_isDrawerOpen;
            _drawerContainer.Visibility = _isDrawerOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

