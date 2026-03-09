using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using MarcoERP.WpfUI.Services;
using MarcoERP.WpfUI.ViewModels.Common;

namespace MarcoERP.WpfUI.Views.Common
{
    public partial class InvoicePdfPreviewDialog : Window
    {
        private readonly InvoicePdfPreviewDialogViewModel _viewModel;
        private string _htmlContent;
        private string _pdfPath;
        private PdfPaperSize _paperSize = PdfPaperSize.A4;
        private bool _startInHtmlMode;

        public InvoicePdfPreviewDialog(InvoicePdfPreviewDialogViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.RequestClose += () => Close();
            _viewModel.RequestViewModeChange += OnViewModeChange;

            Loaded += OnLoaded;
        }

        public void Initialize(InvoicePdfPreviewRequest request)
        {
            _viewModel.TitleText = request?.Title ?? "PDF Preview";
            _viewModel.PdfPath = null;
            _viewModel.CanToggleViewMode = !string.IsNullOrWhiteSpace(request?.HtmlContent);

            _pdfPath = request?.PdfPath;
            _htmlContent = request?.HtmlContent;

            if (string.IsNullOrWhiteSpace(_pdfPath))
            {
                var safePrefix = string.IsNullOrWhiteSpace(request?.FilePrefix) ? "invoice" : request.FilePrefix;
                foreach (var ch in Path.GetInvalidFileNameChars())
                    safePrefix = safePrefix.Replace(ch, '_');
                var folder = Path.Combine(Path.GetTempPath(), "MarcoERP", "pdf-preview");
                Directory.CreateDirectory(folder);

                CleanupPreviewFiles(folder, safePrefix);
                _pdfPath = Path.Combine(folder, $"{safePrefix}.pdf");
            }

            _paperSize = request?.PaperSize ?? PdfPaperSize.A4;
            _startInHtmlMode = (request?.StartInHtmlMode ?? false) && !string.IsNullOrWhiteSpace(_htmlContent);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath))
            {
                _viewModel.IsPdfMode = true;
                _viewModel.PdfPath = _pdfPath;
                PdfWebView.Source = new Uri(_pdfPath);
                _viewModel.StatusText = string.Empty;
                return;
            }

            if (_startInHtmlMode)
            {
                _viewModel.IsPdfMode = false;
                await PdfWebView.EnsureCoreWebView2Async();
                PdfWebView.CoreWebView2.NavigateToString(_htmlContent);
                _viewModel.StatusText = "عرض HTML";
            }
            else
            {
                await RenderPdfAsync();
            }
        }

        private async Task RenderPdfAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_htmlContent))
                {
                    _viewModel.StatusText = "لا توجد محتويات قابلة للمعاينة.";
                    return;
                }

                _viewModel.IsBusy = true;
                _viewModel.StatusText = "جاري إنشاء PDF...";

                await PdfWebView.EnsureCoreWebView2Async();
                PdfWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PdfWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                var tcs = new TaskCompletionSource<bool>();
                void Handler(object s, CoreWebView2NavigationCompletedEventArgs args)
                {
                    PdfWebView.NavigationCompleted -= Handler;
                    tcs.TrySetResult(args.IsSuccess);
                }

                PdfWebView.NavigationCompleted += Handler;
                PdfWebView.CoreWebView2.NavigateToString(_htmlContent);

                await tcs.Task;

                // Configure paper size (dimensions in inches)
                var printSettings = PdfWebView.CoreWebView2.Environment.CreatePrintSettings();
                switch (_paperSize)
                {
                    case PdfPaperSize.A5:
                        printSettings.PageWidth = 5.83;   // 148 mm
                        printSettings.PageHeight = 8.27;  // 210 mm
                        printSettings.MarginTop = 0.3;
                        printSettings.MarginBottom = 0.3;
                        printSettings.MarginLeft = 0.3;
                        printSettings.MarginRight = 0.3;
                        break;
                    case PdfPaperSize.Receipt:
                        printSettings.PageWidth = 3.15;   // 80 mm
                        printSettings.PageHeight = 11.69;  // long roll
                        printSettings.MarginTop = 0.1;
                        printSettings.MarginBottom = 0.1;
                        printSettings.MarginLeft = 0.1;
                        printSettings.MarginRight = 0.1;
                        break;
                    default: // A4
                        printSettings.PageWidth = 8.27;   // 210 mm
                        printSettings.PageHeight = 11.69;  // 297 mm
                        printSettings.MarginTop = 0.4;
                        printSettings.MarginBottom = 0.4;
                        printSettings.MarginLeft = 0.4;
                        printSettings.MarginRight = 0.4;
                        break;
                }

                var success = await PdfWebView.CoreWebView2.PrintToPdfAsync(_pdfPath, printSettings);
                if (success)
                {
                    _viewModel.StatusText = "";
                    _viewModel.PdfPath = _pdfPath;
                    PdfWebView.Source = new Uri(_pdfPath);
                }
                else
                {
                    _viewModel.StatusText = "تعذر إنشاء PDF، يتم عرض نسخة HTML.";
                }
            }
            catch
            {
                _viewModel.StatusText = "تعذر إنشاء PDF، يتم عرض نسخة HTML.";
            }
            finally
            {
                _viewModel.IsBusy = false;
            }
        }

        private static void CleanupPreviewFiles(string folder, string safePrefix)
        {
            try
            {
                foreach (var file in Directory.GetFiles(folder, $"{safePrefix}*.pdf"))
                    File.Delete(file);

                foreach (var file in Directory.GetFiles(folder, $"{safePrefix}*.html"))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private async void OnViewModeChange(bool isPdfMode)
        {
            try
            {
                if (isPdfMode)
                {
                    // Switch to PDF view
                    if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath) && string.IsNullOrWhiteSpace(_htmlContent))
                    {
                        _viewModel.PdfPath = _pdfPath;
                        PdfWebView.Source = new Uri(_pdfPath);
                        _viewModel.StatusText = string.Empty;
                        return;
                    }

                    await RenderPdfAsync();
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(_htmlContent))
                        return;

                    // Switch to HTML view — show raw HTML in WebView2
                    _viewModel.IsBusy = true;
                    _viewModel.StatusText = "جاري عرض HTML...";

                    await PdfWebView.EnsureCoreWebView2Async();
                    PdfWebView.CoreWebView2.NavigateToString(_htmlContent);

                    _viewModel.StatusText = "عرض HTML";
                    _viewModel.IsBusy = false;
                }
            }
            catch
            {
                _viewModel.StatusText = "تعذر التبديل بين أوضاع العرض.";
                _viewModel.IsBusy = false;
            }
        }
    }
}
