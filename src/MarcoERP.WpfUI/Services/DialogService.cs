using System.Windows;
using MarcoERP.Application.Interfaces;

namespace MarcoERP.WpfUI.Services
{
    /// <summary>
    /// WPF implementation of <see cref="IDialogService"/> using MessageBox.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        /// <inheritdoc/>
        public bool Confirm(string message, string title)
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            return result == MessageBoxResult.Yes;
        }

        /// <inheritdoc/>
        public ConfirmResult ConfirmWithCancel(string message, string title)
        {
            var result = MessageBox.Show(
                message,
                title,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel,
                MessageBoxOptions.RtlReading);

            return result switch
            {
                MessageBoxResult.Yes => ConfirmResult.Yes,
                MessageBoxResult.No => ConfirmResult.No,
                _ => ConfirmResult.Cancel
            };
        }

        /// <inheritdoc/>
        public void ShowInfo(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <inheritdoc/>
        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <inheritdoc/>
        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
