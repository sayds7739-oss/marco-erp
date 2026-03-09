using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MarcoERP.WpfUI.Views.Common
{
    public partial class SendEmailDialog : Window, INotifyPropertyChanged
    {
        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _message;
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        public bool WasSent { get; private set; }

        public SendEmailDialog(string defaultEmail = null)
        {
            InitializeComponent();
            DataContext = this;
            Email = defaultEmail ?? "";
            EmailTextBox.Focus();
        }

        private void OnSendClick(object sender, RoutedEventArgs e)
        {
            ErrorMessage = null;
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
            {
                ErrorMessage = "يرجى إدخال بريد إلكتروني صحيح.";
                return;
            }
            WasSent = true;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
