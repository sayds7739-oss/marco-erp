using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Common;
using MarcoERP.WpfUI.ViewModels.Common;

namespace MarcoERP.WpfUI.Views.Common
{
    public partial class PriceHistoryDialog : Window
    {
        public decimal? SelectedPrice { get; private set; }

        public PriceHistoryDialog()
        {
            InitializeComponent();
            DataContext = new PriceHistoryDialogState();
        }

        public static decimal? ShowDialog(
            Window owner,
            string title,
            string counterpartyLabel,
            decimal? counterpartyPrice,
            IReadOnlyList<PriceHistoryRowDto> rows)
        {
            var dialog = new PriceHistoryDialog
            {
                Owner = owner,
                Title = title
            };

            if (dialog.DataContext is PriceHistoryDialogState state)
            {
                state.CounterpartyLabel = counterpartyLabel;
                state.CounterpartyPrice = counterpartyPrice;
                state.RecentPrices.Clear();
                foreach (var row in rows ?? Enumerable.Empty<PriceHistoryRowDto>())
                    state.RecentPrices.Add(row);
            }

            return dialog.ShowDialog() == true ? dialog.SelectedPrice : null;
        }

        private void ApplySelected_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PriceHistoryDialogState state || state.SelectedRow == null)
                return;

            SelectedPrice = state.SelectedRow.UnitPrice;
            DialogResult = true;
        }

        private void ApplyCounterparty_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PriceHistoryDialogState state || !state.CounterpartyPrice.HasValue)
                return;

            SelectedPrice = state.CounterpartyPrice.Value;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PricesGrid_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGrid grid || grid.SelectedItem is not PriceHistoryRowDto row)
                return;

            SelectedPrice = row.UnitPrice;
            DialogResult = true;
        }
    }
}
