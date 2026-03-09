namespace MarcoERP.Application.Interfaces
{
    /// <summary>
    /// Result of a confirmation dialog with three options.
    /// </summary>
    public enum ConfirmResult
    {
        Yes,
        No,
        Cancel
    }

    /// <summary>
    /// Abstraction over UI message dialogs.
    /// Defined in Application layer so ViewModels can depend on it without
    /// referencing System.Windows.MessageBox directly, enabling unit-testing.
    /// Implemented in WpfUI layer.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a yes/no confirmation dialog and returns true if the user chose Yes.
        /// </summary>
        bool Confirm(string message, string title);

        /// <summary>
        /// Shows a yes/no/cancel confirmation dialog.
        /// </summary>
        ConfirmResult ConfirmWithCancel(string message, string title);

        /// <summary>
        /// Shows an informational message (OK button only).
        /// </summary>
        void ShowInfo(string message, string title);

        /// <summary>
        /// Shows a warning message (OK button only).
        /// </summary>
        void ShowWarning(string message, string title);

        /// <summary>
        /// Shows an error message (OK button only).
        /// </summary>
        void ShowError(string message, string title);
    }
}
