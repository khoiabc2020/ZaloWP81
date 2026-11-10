using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ZaloLite;
using ZaloWP81.Views;

namespace ZaloWP81
{
    public sealed class App : Application
    {
        static void Main(string[] args)
        {
            try
            {
                Application.Start((p) => new App());
            }
            catch (Exception ex)
            {
                try
                {
                    var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    var file = folder.CreateFileAsync("main_crash.log", Windows.Storage.CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();
                    Windows.Storage.FileIO.WriteTextAsync(file, ex.ToString()).AsTask().GetAwaiter().GetResult();
                }
                catch { }
            }
        }

        public App()
        {
            this.UnhandledException += App_UnhandledException;
            this.Suspending += OnSuspending;
        }

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            try
            {
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync("unhandled_crash.log", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                await Windows.Storage.FileIO.WriteTextAsync(file, e.Message + "\n" + e.Exception?.ToString());
            }
            catch { }

            try
            {
                var md = new MessageDialog("Lỗi ứng dụng: " + e.Message + "\n\n" + e.Exception?.ToString(), "Sự cố Zalo");
                await md.ShowAsync();
            }
            catch { }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                Frame rootFrame = Window.Current.Content as Frame;
                if (rootFrame == null)
                {
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;
                }

                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(LoginPage), e?.Arguments);
                }

                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                try
                {
                    var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    var file = folder.CreateFileAsync("launch_crash.log", Windows.Storage.CreationCollisionOption.ReplaceExisting).AsTask().GetAwaiter().GetResult();
                    Windows.Storage.FileIO.WriteTextAsync(file, ex.ToString()).AsTask().GetAwaiter().GetResult();
                }
                catch { }
            }
        }

        private async void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            e.Handled = true;
            try
            {
                var md = new MessageDialog("Không thể mở trang: " + e.Exception?.Message, "Lỗi điều hướng");
                await md.ShowAsync();
            }
            catch { }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
