using System;
using System.Collections.Generic;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ZaloWP81
{
    public static class NavigationManager
    {
        private static readonly Stack<Page> _backStack = new Stack<Page>();

        public static void Initialize()
        {
            try
            {
                HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
                HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            }
            catch { }
        }

        private static void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            if (CanGoBack)
            {
                e.Handled = true;
                GoBack();
            }
        }

        public static bool CanGoBack
        {
            get { return _backStack.Count > 0; }
        }

        public static void NavigateTo(Page page, bool clearStack = false)
        {
            if (clearStack)
            {
                _backStack.Clear();
            }
            else if (Window.Current.Content is Page current && !(current is Views.LoginPage))
            {
                _backStack.Push(current);
            }

            Window.Current.Content = page;
        }

        public static void GoBack()
        {
            if (_backStack.Count > 0)
            {
                var prevPage = _backStack.Pop();
                Window.Current.Content = prevPage;
            }
        }
    }
}
