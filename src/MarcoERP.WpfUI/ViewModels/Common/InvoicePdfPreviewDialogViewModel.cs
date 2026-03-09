using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using MarcoERP.WpfUI.ViewModels;

namespace MarcoERP.WpfUI.ViewModels.Common
{
    public sealed class InvoicePdfPreviewDialogViewModel : BaseViewModel
    {
        private string _titleText;
        public string TitleText { get => _titleText; set => SetProperty(ref _titleText, value); }

        private string _statusText;
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _pdfPath;
        public string PdfPath
        {
            get => _pdfPath;
            set
            {
                if (SetProperty(ref _pdfPath, value))
                    RelayCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isPdfMode = true;
        /// <summary>True = PDF view (default), False = HTML view.</summary>
        public bool IsPdfMode
        {
            get => _isPdfMode;
            set
            {
                if (SetProperty(ref _isPdfMode, value))
                {
                    OnPropertyChanged(nameof(IsHtmlMode));
                    OnPropertyChanged(nameof(ViewModeText));
                    RequestViewModeChange?.Invoke(value);
                }
            }
        }

        public bool IsHtmlMode => !_isPdfMode;
        public string ViewModeText => _isPdfMode ? "PDF" : "HTML";

        private bool _canToggleViewMode = true;
        public bool CanToggleViewMode
        {
            get => _canToggleViewMode;
            set => SetProperty(ref _canToggleViewMode, value);
        }

        public bool CanOpenPdf => !string.IsNullOrWhiteSpace(PdfPath) && File.Exists(PdfPath);

        public ICommand CloseCommand { get; }
        public ICommand OpenPdfCommand { get; }
        public ICommand ToggleViewModeCommand { get; }

        public event Action RequestClose;
        /// <summary>Fired when user toggles between PDF/HTML. True = PDF, False = HTML.</summary>
        public event Action<bool> RequestViewModeChange;

        public InvoicePdfPreviewDialogViewModel()
        {
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
            OpenPdfCommand = new RelayCommand(_ => OpenPdf(), _ => CanOpenPdf);
            ToggleViewModeCommand = new RelayCommand(_ => IsPdfMode = !IsPdfMode);
        }

        private void OpenPdf()
        {
            try
            {
                if (!CanOpenPdf)
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = PdfPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                StatusText = "تعذر فتح ملف PDF.";
            }
        }
    }
}
