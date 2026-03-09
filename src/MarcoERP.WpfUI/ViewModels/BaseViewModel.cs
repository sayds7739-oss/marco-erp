using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Domain.Exceptions;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels implementing INotifyPropertyChanged and INotifyDataErrorInfo.
    /// Provides SetProperty helper for clean property change notification.
    /// Includes DbGuard semaphore to prevent concurrent DbContext access.
    /// Includes validation infrastructure for real-time UI validation.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged, INotifyDataErrorInfo, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        /// <summary>
        /// Serializes all async DB operations within a single ViewModel scope,
        /// preventing concurrent access to the same scoped DbContext instance.
        /// </summary>
        protected readonly SemaphoreSlim DbGuard = new(1, 1);

        /// <summary>
        /// Dictionary holding validation errors per property.
        /// </summary>
        private readonly Dictionary<string, List<string>> _validationErrors = new();

        #region INotifyDataErrorInfo Implementation

        /// <summary>
        /// Returns true if any property has validation errors.
        /// </summary>
        public bool HasErrors => _validationErrors.Any(kv => kv.Value?.Count > 0);

        /// <summary>
        /// Gets the validation errors for a specific property or all properties.
        /// </summary>
        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _validationErrors.SelectMany(kv => kv.Value);

            return _validationErrors.TryGetValue(propertyName, out var errors) ? errors : null;
        }

        /// <summary>
        /// Adds a validation error for the specified property.
        /// </summary>
        protected void AddValidationError(string propertyName, string errorMessage)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            if (!_validationErrors.ContainsKey(propertyName))
                _validationErrors[propertyName] = new List<string>();

            if (!_validationErrors[propertyName].Contains(errorMessage))
            {
                _validationErrors[propertyName].Add(errorMessage);
                RaiseErrorsChanged(propertyName);
            }
        }

        /// <summary>
        /// Clears all validation errors for the specified property.
        /// </summary>
        protected void ClearValidationErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            if (_validationErrors.ContainsKey(propertyName) && _validationErrors[propertyName].Count > 0)
            {
                _validationErrors[propertyName].Clear();
                RaiseErrorsChanged(propertyName);
            }
        }

        /// <summary>
        /// Clears all validation errors for all properties.
        /// </summary>
        protected void ClearAllValidationErrors()
        {
            var properties = _validationErrors.Keys.ToList();
            _validationErrors.Clear();
            foreach (var prop in properties)
                RaiseErrorsChanged(prop);
        }

        /// <summary>
        /// Validates a property value using the provided validation functions.
        /// Returns true if the value is valid.
        /// </summary>
        protected bool ValidateProperty<T>(T value, string propertyName, params (Func<T, bool> IsValid, string ErrorMessage)[] validators)
        {
            ClearValidationErrors(propertyName);

            foreach (var (isValid, errorMessage) in validators)
            {
                if (!isValid(value))
                {
                    AddValidationError(propertyName, errorMessage);
                }
            }

            return !_validationErrors.ContainsKey(propertyName) || _validationErrors[propertyName].Count == 0;
        }

        /// <summary>
        /// Validates a required string property.
        /// </summary>
        protected bool ValidateRequired(string value, string propertyName, string fieldName = null)
        {
            var name = fieldName ?? propertyName;
            return ValidateProperty(value, propertyName,
                (v => !string.IsNullOrWhiteSpace(v), $"{name} مطلوب."));
        }

        /// <summary>
        /// Validates that a numeric value is greater than zero.
        /// </summary>
        protected bool ValidatePositive(decimal value, string propertyName, string fieldName = null)
        {
            var name = fieldName ?? propertyName;
            return ValidateProperty(value, propertyName,
                (v => v > 0, $"{name} يجب أن يكون أكبر من صفر."));
        }

        /// <summary>
        /// Validates that a selection is made (ID > 0).
        /// </summary>
        protected bool ValidateSelection(int value, string propertyName, string fieldName = null)
        {
            var name = fieldName ?? propertyName;
            return ValidateProperty(value, propertyName,
                (v => v > 0, $"يرجى اختيار {name}."));
        }

        private void RaiseErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }

        #endregion

        /// <summary>
        /// Enqueues an async action to run serially against the DB guard.
        /// Safe to call fire-and-forget from property setters.
        /// Surfaces exceptions to ErrorMessage via the UI dispatcher.
        /// </summary>
        protected void EnqueueDbWork(Func<Task> work)
        {
            _ = Task.Run(async () =>
            {
                await DbGuard.WaitAsync().ConfigureAwait(false);
                try
                {
                    await work().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EnqueueDbWork] Error in {GetType().Name}: {ex}");
                    var friendly = FriendlyErrorMessage("العملية", ex);
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                        () => ErrorMessage = friendly);
                }
                finally
                {
                    DbGuard.Release();
                }
            });
        }

        /// <summary>
        /// Creates an AsyncRelayCommand that surfaces exceptions to ErrorMessage automatically.
        /// Prefer this over new AsyncRelayCommand(...) to avoid silent exception swallowing.
        /// </summary>
        protected AsyncRelayCommand CreateCommand(Func<Task> execute, Func<bool> canExecute = null)
            => new AsyncRelayCommand(
                execute,
                canExecute,
                ex => ErrorMessage = FriendlyErrorMessage("العملية", ex));

        /// <summary>Raises PropertyChanged for the given property name.</summary>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changed.
        /// Returns true if the value was changed.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private bool _isBusy;
        /// <summary>Indicates whether the ViewModel is performing an async operation.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage;
        /// <summary>Status message shown in the UI.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _errorMessage;
        /// <summary>Error message shown in the UI.</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                SetProperty(ref _errorMessage, value);
                OnPropertyChanged(nameof(HasError));
            }
        }

        /// <summary>True if there is an error message.</summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>Clears the error message.</summary>
        protected void ClearError() => ErrorMessage = null;

        /// <summary>
        /// Converts a raw exception into a user-friendly Arabic error message.
        /// Never exposes stack traces or raw technical details to the user.
        /// </summary>
        protected static string FriendlyErrorMessage(string operationName, Exception ex)
        {
            if (ex is ConcurrencyConflictException)
                return "حدث تعارض في البيانات. يرجى إعادة تحميل السجل والمحاولة مجدداً.";

            if (ex is InvalidOperationException ioe)
            {
                var msg = ioe.Message ?? string.Empty;

                // EF Core entity tracking conflict — never show to user
                if (msg.Contains("cannot be tracked", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("is already being tracked", StringComparison.OrdinalIgnoreCase))
                    return $"حدث تعارض داخلي في تتبع البيانات أثناء {operationName}. يرجى إعادة المحاولة.";

                // EF Core DbContext disposed
                if (msg.Contains("disposed", StringComparison.OrdinalIgnoreCase)
                    && msg.Contains("context", StringComparison.OrdinalIgnoreCase))
                    return $"انتهت جلسة الاتصال أثناء {operationName}. يرجى إعادة المحاولة.";

                // EF Core concurrent access
                if (msg.Contains("second operation", StringComparison.OrdinalIgnoreCase)
                    && msg.Contains("previous operation", StringComparison.OrdinalIgnoreCase))
                    return $"حدث تعارض في عمليات قاعدة البيانات أثناء {operationName}. يرجى الانتظار وإعادة المحاولة.";

                // LINQ sequence errors
                if (msg.Contains("Sequence contains no", StringComparison.OrdinalIgnoreCase))
                    return $"لم يتم العثور على البيانات المطلوبة أثناء {operationName}.";

                // Arabic domain messages — pass through
                if (!string.IsNullOrEmpty(msg) && ContainsArabic(msg))
                    return $"خطأ في {operationName}: {msg}";

                // Default for unknown IOE — don't expose raw English errors
                return $"حدث خطأ أثناء {operationName}. يرجى المحاولة مرة أخرى أو التواصل مع الدعم الفني.";
            }

            if (ex?.InnerException != null)
            {
                var inner = ex.InnerException.Message;
                if (inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                    inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                    return $"خطأ في {operationName}: يوجد سجل مكرر. تأكد من عدم تكرار البيانات.";

                if (inner.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                    inner.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase))
                    return $"خطأ في {operationName}: لا يمكن الحذف لوجود سجلات مرتبطة.";

                if (inner.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    return $"انتهت مهلة الاتصال أثناء {operationName}. يرجى إعادة المحاولة.";

                if (inner.Contains("connection", StringComparison.OrdinalIgnoreCase)
                    && (inner.Contains("failed", StringComparison.OrdinalIgnoreCase)
                        || inner.Contains("refused", StringComparison.OrdinalIgnoreCase)))
                    return $"تعذر الاتصال بقاعدة البيانات أثناء {operationName}. تأكد من تشغيل الخادم.";
            }

            return $"حدث خطأ أثناء {operationName}. يرجى المحاولة مرة أخرى أو التواصل مع الدعم الفني.";
        }

        private static bool ContainsArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
            {
                if (c >= '\u0600' && c <= '\u06FF') return true;
                if (c >= '\uFE70' && c <= '\uFEFF') return true;
            }
            return false;
        }

        // ── Performance Monitoring ──

        private readonly Stopwatch _loadStopwatch = new();

        private long _lastLoadTimeMs;
        /// <summary>Milliseconds taken by the last load/DB operation.</summary>
        public long LastLoadTimeMs
        {
            get => _lastLoadTimeMs;
            protected set => SetProperty(ref _lastLoadTimeMs, value);
        }

        private int _currentRecordCount;
        /// <summary>Number of records loaded by the last operation.</summary>
        public int CurrentRecordCount
        {
            get => _currentRecordCount;
            protected set => SetProperty(ref _currentRecordCount, value);
        }

        /// <summary>Start timing a load operation.</summary>
        protected void LoadTimerStart()
        {
            _loadStopwatch.Restart();
        }

        /// <summary>Stop timing and update LastLoadTimeMs.</summary>
        protected void LoadTimerStop()
        {
            _loadStopwatch.Stop();
            LastLoadTimeMs = _loadStopwatch.ElapsedMilliseconds;
        }

        /// <summary>Stop timing, update LastLoadTimeMs and CurrentRecordCount.</summary>
        protected void LoadTimerStop(int recordCount)
        {
            _loadStopwatch.Stop();
            LastLoadTimeMs = _loadStopwatch.ElapsedMilliseconds;
            CurrentRecordCount = recordCount;
        }

        // ── Dirty State Tracking ──

        private bool _isDirty;
        /// <summary>True if the form has unsaved changes.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            protected set => SetProperty(ref _isDirty, value);
        }

        /// <summary>Marks the ViewModel as having unsaved changes.</summary>
        protected void MarkDirty() => IsDirty = true;

        /// <summary>Resets the dirty flag (call after save or load). Also clears undo history.</summary>
        protected void ResetDirtyTracking()
        {
            IsDirty = false;
            _undoManager?.Clear();
        }

        // ── Undo / Redo ──

        private UndoRedoManager _undoManager;

        /// <summary>Lazy-initialized undo/redo manager. Only created when first accessed.</summary>
        protected UndoRedoManager UndoManager => _undoManager ??= CreateUndoManager();

        private UndoRedoManager CreateUndoManager()
        {
            var mgr = new UndoRedoManager();
            mgr.StateChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            };
            return mgr;
        }

        /// <summary>True if there are actions to undo.</summary>
        public bool CanUndo => _undoManager?.CanUndo ?? false;

        /// <summary>True if there are actions to redo.</summary>
        public bool CanRedo => _undoManager?.CanRedo ?? false;

        private ICommand _undoCommand;
        public ICommand UndoCommand => _undoCommand ??= new RelayCommand(
            () => UndoManager.Undo(),
            () => CanUndo);

        private ICommand _redoCommand;
        public ICommand RedoCommand => _redoCommand ??= new RelayCommand(
            () => UndoManager.Redo(),
            () => CanRedo);

        /// <summary>
        /// Sets a property value with undo/redo support.
        /// Records the old/new value so the change can be undone.
        /// </summary>
        protected bool SetPropertyWithUndo<T>(ref T field, T value, Action<T> setter, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            var oldValue = field;
            field = value;
            OnPropertyChanged(propertyName);

            if (_undoManager != null && !_undoManager.IsSuppressed)
            {
                _undoManager.RecordChange(propertyName, oldValue, value, v => setter((T)v));
            }

            return true;
        }

        // ── IDisposable ──

        private bool _disposed;

        /// <summary>Disposes the DbGuard semaphore. Override in derived classes for additional cleanup.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    DbGuard.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
