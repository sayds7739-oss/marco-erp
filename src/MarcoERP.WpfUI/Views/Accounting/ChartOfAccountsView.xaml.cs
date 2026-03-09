using System.Windows;
using System.Windows.Controls;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.WpfUI.ViewModels.Accounting;

namespace MarcoERP.WpfUI.Views.Accounting
{
    /// <summary>
    /// Chart of Accounts management view.
    /// Handles TreeView selection changed (WPF TreeView does not support SelectedItem binding)
    /// and expand/collapse all via ViewModel event.
    /// </summary>
    public partial class ChartOfAccountsView : UserControl
    {
        public ChartOfAccountsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChartOfAccountsViewModel oldVm)
                oldVm.RequestExpandAll -= OnRequestExpandAll;

            if (e.NewValue is ChartOfAccountsViewModel newVm)
                newVm.RequestExpandAll += OnRequestExpandAll;
        }

        /// <summary>
        /// Syncs the selected TreeView node to the ViewModel's SelectedTreeNode property.
        /// </summary>
        private void AccountTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ChartOfAccountsViewModel vm && e.NewValue is AccountTreeNodeDto node)
            {
                vm.SelectedTreeNode = node;
            }
        }

        /// <summary>
        /// Expands or collapses all TreeViewItem containers.
        /// </summary>
        private void OnRequestExpandAll(bool expand)
        {
            SetAllTreeViewItemsExpanded(AccountTreeView, expand);
        }

        private static void SetAllTreeViewItemsExpanded(ItemsControl parent, bool isExpanded)
        {
            if (parent == null) return;

            foreach (var item in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                {
                    tvi.IsExpanded = isExpanded;
                    SetAllTreeViewItemsExpanded(tvi, isExpanded);
                }
            }
        }
    }
}
