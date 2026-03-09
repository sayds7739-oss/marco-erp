using System.ComponentModel;
using System.Windows;
using MarcoERP.WpfUI.ViewModels.Setup;

namespace MarcoERP.WpfUI.Views.Setup
{
    public partial class OnboardingWizardWindow : Window
    {
        public OnboardingWizardWindow(OnboardingWizardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OnboardingWizardViewModel.IsCompleted))
            {
                if (DataContext is OnboardingWizardViewModel vm && vm.IsCompleted)
                {
                    DialogResult = true;
                    Close();
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is OnboardingWizardViewModel vm)
                vm.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
