using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;

namespace MarcoERP.WpfUI.Views.Sales
{
    /// <summary>
    /// Code-behind for PriceListView.xaml — قوائم الأسعار.
    /// </summary>
    public partial class PriceListView : UserControl
    {
        public PriceListView()
        {
            InitializeComponent();
        }

        private void EditablePriceTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.SelectAll();
        }

        private void PriceGrid_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            var textBox = e.EditingElement as TextBox;
            if (textBox == null)
                textBox = FindVisualChild<TextBox>(e.EditingElement);

            if (textBox == null)
                return;

            textBox.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        private void EditablePriceTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            if (!textBox.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                textBox.Focus();
            }
        }

        private static TChild FindVisualChild<TChild>(DependencyObject parent) where TChild : DependencyObject
        {
            if (parent == null)
                return null;

            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is TChild typedChild)
                    return typedChild;

                var descendant = FindVisualChild<TChild>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }
    }
}
