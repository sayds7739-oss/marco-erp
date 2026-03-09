using System.Windows;
using System.Windows.Controls;
using MarcoERP.WpfUI.ViewModels.Settings;

namespace MarcoERP.WpfUI.Views.Settings
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserManagementViewModel vm)
                vm.FormPassword = ((PasswordBox)sender).Password;
        }

        private void ConfirmPasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserManagementViewModel vm)
                vm.FormConfirmPassword = ((PasswordBox)sender).Password;
        }
    }
}
