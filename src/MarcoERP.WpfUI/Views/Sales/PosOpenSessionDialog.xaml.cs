using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.Views.Sales
{
    public partial class PosOpenSessionDialog : Window
    {
        private readonly IDialogService _dialog;

        public int SelectedCashboxId { get; private set; }
        public int SelectedWarehouseId { get; private set; }
        public decimal OpeningBalance { get; private set; }

        public PosOpenSessionDialog(
            IReadOnlyList<CashboxDto> cashboxes,
            IReadOnlyList<WarehouseDto> warehouses,
            IDialogService dialogService = null)
        {
            InitializeComponent();
            _dialog = dialogService;

            CashboxCombo.ItemsSource = cashboxes;
            WarehouseCombo.ItemsSource = warehouses;

            // Select defaults
            foreach (var cb in cashboxes)
            {
                if (cb.IsDefault) { CashboxCombo.SelectedItem = cb; break; }
            }
            if (CashboxCombo.SelectedItem == null && cashboxes.Count > 0)
                CashboxCombo.SelectedIndex = 0;

            foreach (var wh in warehouses)
            {
                if (wh.IsDefault) { WarehouseCombo.SelectedItem = wh; break; }
            }
            if (WarehouseCombo.SelectedItem == null && warehouses.Count > 0)
                WarehouseCombo.SelectedIndex = 0;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (CashboxCombo.SelectedItem is not CashboxDto cashbox)
            {
                ShowWarning("يجب اختيار الخزنة.");
                return;
            }

            if (WarehouseCombo.SelectedItem is not WarehouseDto warehouse)
            {
                ShowWarning("يجب اختيار المستودع.");
                return;
            }

            if (!decimal.TryParse(OpeningBalanceBox.Text, out var balance) || balance < 0)
            {
                ShowWarning("رصيد الافتتاح يجب أن يكون قيمة صحيحة غير سالبة.");
                return;
            }

            SelectedCashboxId = cashbox.Id;
            SelectedWarehouseId = warehouse.Id;
            OpeningBalance = balance;
            DialogResult = true;
        }

        private void ShowWarning(string message)
        {
            if (_dialog != null)
                _dialog.ShowWarning(message, "تنبيه");
            else
                MessageBox.Show(message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void NumericBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }
    }
}
