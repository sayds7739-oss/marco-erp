using System.Windows;
using MarcoERP.WpfUI.ViewModels.Sales;

namespace MarcoERP.WpfUI.Views.Sales
{
    /// <summary>
    /// POS full-screen window. Keyboard-optimized for retail speed.
    /// </summary>
    public partial class PosWindow : Window
    {
        private readonly PosViewModel _viewModel;

        public PosWindow(PosViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestBarcodeFocus += OnRequestBarcodeFocus;
        }

        private void OnRequestBarcodeFocus()
        {
            BarcodeInput?.Focus();
        }

    }
}
