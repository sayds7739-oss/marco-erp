using System;
using System.Threading.Tasks;
using System.Windows;
using MarcoERP.Application.Interfaces;
using MarcoERP.WpfUI.Resources;

namespace MarcoERP.WpfUI.Navigation
{
    /// <summary>
    /// Centralized helper for unsaved-changes prompts and save/continue decisions.
    /// </summary>
    public static class DirtyStateGuard
    {
        /// <summary>
        /// Prompts the user to save, discard, or cancel when unsaved changes exist.
        /// Returns true when navigation can proceed.
        /// </summary>
        public static Task<bool> ConfirmContinueAsync(IDirtyStateAware dirtyState)
            => ConfirmContinueAsync(dirtyState, null, null);

        /// <summary>
        /// Prompts the user with optional context (e.g., tab title) about unsaved changes.
        /// Returns true when the operation can proceed.
        /// </summary>
        public static Task<bool> ConfirmContinueAsync(IDirtyStateAware dirtyState, string contextTitle)
            => ConfirmContinueAsync(dirtyState, contextTitle, null);

        /// <summary>
        /// Prompts the user with optional context about unsaved changes using IDialogService.
        /// Returns true when the operation can proceed.
        /// </summary>
        public static async Task<bool> ConfirmContinueAsync(IDirtyStateAware dirtyState, string contextTitle, IDialogService dialogService)
        {
            if (dirtyState == null || !dirtyState.IsDirty)
                return true;

            var message = string.IsNullOrWhiteSpace(contextTitle)
                ? UiStrings.UnsavedChangesPrompt
                : $"«{contextTitle}»\n\n{UiStrings.UnsavedChangesPrompt}";

            ConfirmResult result;

            if (dialogService != null)
            {
                result = dialogService.ConfirmWithCancel(message, UiStrings.UnsavedChangesTitle);
            }
            else
            {
                // Fallback to MessageBox when IDialogService is not available
                var mbResult = MessageBox.Show(
                    message,
                    UiStrings.UnsavedChangesTitle,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel,
                    MessageBoxOptions.RtlReading);

                result = mbResult switch
                {
                    MessageBoxResult.Yes => ConfirmResult.Yes,
                    MessageBoxResult.No => ConfirmResult.No,
                    _ => ConfirmResult.Cancel
                };
            }

            if (result == ConfirmResult.Yes)
            {
                var saved = await dirtyState.SaveChangesAsync();
                if (!saved)
                {
                    if (dialogService != null)
                    {
                        dialogService.ShowError(UiStrings.UnsavedChangesSaveFailed, UiStrings.UnsavedChangesTitle);
                    }
                    else
                    {
                        MessageBox.Show(
                            UiStrings.UnsavedChangesSaveFailed,
                            UiStrings.UnsavedChangesTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error,
                            MessageBoxResult.OK,
                            MessageBoxOptions.RtlReading);
                    }
                    return false;
                }

                return true;
            }

            return result == ConfirmResult.No;
        }
    }
}
