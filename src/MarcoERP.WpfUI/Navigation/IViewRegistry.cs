using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace MarcoERP.WpfUI.Navigation
{
    public interface IViewRegistry
    {
        void Register<TView, TViewModel>(string key, string title)
            where TView : UserControl
            where TViewModel : class;

        void Register<TView, TViewModel>(string key, string title, PackIconKind iconKind, Brush iconBrush = null)
            where TView : UserControl
            where TViewModel : class;

        bool TryGet(string key, out NavigationRoute route);

        Task<UserControl> CreateViewAsync(string key, IServiceProvider serviceProvider);
    }
}
