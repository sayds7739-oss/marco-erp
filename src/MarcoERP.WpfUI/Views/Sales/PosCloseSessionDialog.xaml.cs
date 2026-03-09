using System;
using System.Windows;
using System.Windows.Controls;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.Views.Sales
{
    public partial class PosCloseSessionDialog : Window
    {
        private readonly IDialogService _dialog;

        public decimal ActualClosingBalance { get; private set; }
        public string Notes { get; private set; }

        public PosCloseSessionDialog(PosSessionDto session, IDialogService dialogService = null)
        {
            InitializeComponent();
            _dialog = dialogService;

            var expectedBalance = session.OpeningBalance + session.TotalCashReceived;

            SessionInfoText.Text = $"الجلسة: {session.SessionNumber}  —  المعاملات: {session.TransactionCount}";
            OpeningBalText.Text = session.OpeningBalance.ToString("N2");
            CashReceivedText.Text = session.TotalCashReceived.ToString("N2");
            ExpectedBalText.Text = expectedBalance.ToString("N2");
            ActualBalanceBox.Text = expectedBalance.ToString("N2");
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(ActualBalanceBox.Text, out var balance) || balance < 0)
            {
                if (_dialog != null)
                    _dialog.ShowWarning("الرصيد الفعلي يجب أن يكون قيمة صحيحة غير سالبة.", "تنبيه");
                else
                    MessageBox.Show("الرصيد الفعلي يجب أن يكون قيمة صحيحة غير سالبة.", "تنبيه",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ActualClosingBalance = balance;
            Notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? "إغلاق نقطة البيع" : NotesBox.Text.Trim();
            DialogResult = true;
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
