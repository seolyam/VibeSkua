using System;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.ViewModels;

namespace Skua.App.WPF
{
    public partial class EmbeddedMainWindow : Window
    {
        private const int WM_SKUA_GRIDVIEW = 0x0400 + 444;

        public EmbeddedMainWindow()
        {
            InitializeComponent();
            DataContext = Ioc.Default.GetRequiredService<MainViewModel>();
            Loaded += (s, e) =>
            {
                System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                source?.AddHook(WndProc);
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SKUA_GRIDVIEW)
            {
                bool isGrid = wParam.ToInt32() == 1;
                MainMenuCtrl.Visibility = isGrid ? Visibility.Collapsed : Visibility.Visible;
                GameContainerCtrl.SetGridView(isGrid);
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
