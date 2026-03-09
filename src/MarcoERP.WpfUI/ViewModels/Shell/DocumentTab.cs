using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using MarcoERP.WpfUI.Navigation;

namespace MarcoERP.WpfUI.ViewModels.Shell
{
    /// <summary>
    /// Represents a single open document tab in the shell.
    /// </summary>
    public sealed class DocumentTab : BaseViewModel
    {
        private readonly string _tabKey;
        private bool _isActive;
        private bool _isTabDirty;

        public DocumentTab(string tabKey, string title, UserControl view, object parameter)
        {
            _tabKey = tabKey;
            _title = title;
            View = view;
            Parameter = parameter;

            HookDirtyTracking(view);
            UpdateDirtyState(view);
        }

        /// <summary>Unique tab identity (key + parameter).</summary>
        public string TabKey => _tabKey;

        /// <summary>Navigation key for the view.</summary>
        public string ViewKey { get; init; }

        /// <summary>Original navigation parameter.</summary>
        public object Parameter { get; }

        private string _title;
        /// <summary>Tab title.</summary>
        public string Title 
        { 
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(DisplayTitle));
                    OnPropertyChanged(nameof(SafeTitle));
                }
            }
        }

        private string _statusText;
        /// <summary>Status text shown below title (e.g., invoice number).</summary>
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        /// <summary>Display title with dirty indicator when needed.</summary>
        public string DisplayTitle => IsTabDirty ? $"{Title} *" : Title;

        /// <summary>Guaranteed non-empty title for tab header rendering.</summary>
        public string SafeTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayTitle))
                    return DisplayTitle;

                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (!string.IsNullOrWhiteSpace(ViewKey))
                    return ViewKey;

                return "صفحة";
            }
        }

        /// <summary>View instance hosted by the tab.</summary>
        public UserControl View { get; }

        /// <summary>Icon kind for the tab header.</summary>
        public PackIconKind IconKind { get; init; } = PackIconKind.FileDocumentOutline;

        /// <summary>Icon brush color for the tab header.</summary>
        public Brush IconBrush { get; init; }

        /// <summary>True when this tab is currently active.</summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>True when the underlying view model has unsaved changes.</summary>
        public bool IsTabDirty
        {
            get => _isTabDirty;
            private set
            {
                if (SetProperty(ref _isTabDirty, value))
                {
                    OnPropertyChanged(nameof(DisplayTitle));
                    OnPropertyChanged(nameof(SafeTitle));
                }
            }
        }

        private void HookDirtyTracking(UserControl view)
        {
            if (view?.DataContext is INotifyPropertyChanged notifier)
                notifier.PropertyChanged += OnViewModelPropertyChanged;
        }

        /// <summary>Removes the PropertyChanged subscription to prevent memory leaks on tab close.</summary>
        public void UnhookDirtyTracking()
        {
            if (View?.DataContext is INotifyPropertyChanged notifier)
                notifier.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BaseViewModel.IsDirty))
                UpdateDirtyState(View);
        }

        private void UpdateDirtyState(UserControl view)
        {
            if (view?.DataContext is IDirtyStateAware dirty)
                IsTabDirty = dirty.IsDirty;
            else
                IsTabDirty = false;
        }
    }
}