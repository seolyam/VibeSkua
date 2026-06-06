using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.ViewModels;

namespace Skua.App.WPF
{
    public partial class EmbeddedMainWindow : Window
    {
        public EmbeddedMainWindow()
        {
            InitializeComponent();
            DataContext = Ioc.Default.GetRequiredService<MainViewModel>();
        }
    }
}
