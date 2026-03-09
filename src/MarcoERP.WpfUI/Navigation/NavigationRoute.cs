using System;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace MarcoERP.WpfUI.Navigation
{
    public sealed class NavigationRoute
    {
        public NavigationRoute(string title, Func<IServiceProvider, UserControl> factory)
        {
            Title = title;
            Factory = factory;
        }

        public string Title { get; }

        public Func<IServiceProvider, UserControl> Factory { get; }

        /// <summary>Icon kind for the tab header (set via ViewRegistry registration).</summary>
        public PackIconKind IconKind { get; init; } = PackIconKind.FileDocumentOutline;

        /// <summary>Icon brush color for the tab header.</summary>
        public Brush IconBrush { get; init; }
    }
}
