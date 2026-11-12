using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using ZaloLite;
using ZaloUWP.Common;
using ZaloWP81.Views;

namespace ZaloWP81
{
    public sealed class App : Application, IXamlMetadataProvider
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
            LoadGlobalStyles();
        }

        private void LoadGlobalStyles()
        {
            try
            {
                string xaml = @"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

    <Style x:Key=""SearchTextBoxStyle"" TargetType=""TextBox"">
        <Setter Property=""Foreground"" Value=""White"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderBrush"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""Padding"" Value=""0"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""TextBox"">
                    <Grid Background=""Transparent"">
                        <ScrollViewer x:Name=""ContentElement""
                                      HorizontalScrollBarVisibility=""Hidden""
                                      VerticalScrollBarVisibility=""Hidden""
                                      VerticalAlignment=""Center"" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""ChatInputTextBoxStyle"" TargetType=""TextBox"">
        <Setter Property=""Foreground"" Value=""#111827"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderBrush"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""Padding"" Value=""0"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""TextBox"">
                    <Grid Background=""Transparent"">
                        <ScrollViewer x:Name=""ContentElement""
                                      HorizontalScrollBarVisibility=""Hidden""
                                      VerticalScrollBarVisibility=""Hidden""
                                      VerticalAlignment=""Center"" />
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";

                var dict = (ResourceDictionary)XamlReader.Load(xaml);
                foreach (var key in dict.Keys)
                {
                    this.Resources[key] = dict[key];
                }
                this.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                ZaloLog.Add("LoadGlobalStyles error: " + ex.Message);
            }
        }

        public static void MakeTextBoxTransparent(TextBox tb, bool isSearch = false)
        {
            if (tb == null) return;
            try
            {
                string styleKey = isSearch ? "SearchTextBoxStyle" : "ChatInputTextBoxStyle";
                if (Application.Current != null && Application.Current.Resources != null && Application.Current.Resources.ContainsKey(styleKey))
                {
                    tb.Style = (Style)Application.Current.Resources[styleKey];
                    return;
                }
            }
            catch { }

            try
            {
                string xaml = @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                    <Style TargetType=""TextBox"">
                        <Setter Property=""Background"" Value=""Transparent"" />
                        <Setter Property=""BorderBrush"" Value=""Transparent"" />
                        <Setter Property=""BorderThickness"" Value=""0"" />
                        <Setter Property=""Template"">
                            <Setter.Value>
                                <ControlTemplate TargetType=""TextBox"">
                                    <Grid Background=""Transparent"">
                                        <ScrollViewer x:Name=""ContentElement""
                                                      HorizontalScrollBarVisibility=""Hidden""
                                                      VerticalScrollBarVisibility=""Hidden""
                                                      VerticalAlignment=""Center"" />
                                    </Grid>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>
                </ResourceDictionary>";
                var dict = (ResourceDictionary)XamlReader.Load(xaml);
                foreach (var v in dict.Values)
                {
                    if (v is Style st) { tb.Style = st; break; }
                }
            }
            catch { }

            tb.Background = new SolidColorBrush(Colors.Transparent);
            tb.BorderThickness = new Thickness(0);
        }

        public IXamlType GetXamlType(Type type) => null;
        public IXamlType GetXamlType(string fullName) => null;
        public XmlnsDefinition[] GetXmlnsDefinitions() => new XmlnsDefinition[0];

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
                try
                {
                    ApiInfo.ConfigureMobileStatusBar(Windows.UI.Color.FromArgb(255, 0, 104, 255), Windows.UI.Colors.White);
                }
                catch { }

                try
                {
                    var appView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
                    appView.SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseVisible);
                }
                catch { }

                NavigationManager.Initialize();
                NavigationManager.NavigateTo(new MainPage(), clearStack: true);
                Window.Current.Activate();
            }
            catch (Exception ex)
            {
                try
                {
                    var md = new MessageDialog("Lỗi khởi động: " + ex.Message, "Sự cố");
                    var t = md.ShowAsync();
                }
                catch { }
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);
            try
            {
                NavigationManager.Initialize();
                if (args is FileOpenPickerContinuationEventArgs fileArgs)
                {
                    if (NavigationManager.CurrentPage is IFileOpenPickerContinuable continuable)
                    {
                        continuable.ContinueWithFileOpenPicker(fileArgs);
                    }
                    else
                    {
                        string convKey = (fileArgs.ContinuationData != null && fileArgs.ContinuationData.ContainsKey("ConversationKey"))
                            ? fileArgs.ContinuationData["ConversationKey"] as string : null;
                        if (!string.IsNullOrEmpty(convKey))
                        {
                            var chat = new Views.ChatPage(convKey);
                            NavigationManager.NavigateTo(chat);
                            chat.ContinueWithFileOpenPicker(fileArgs);
                        }
                    }
                }
                Window.Current?.Activate();
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
