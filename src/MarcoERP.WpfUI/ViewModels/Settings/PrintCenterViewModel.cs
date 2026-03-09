using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// Print Center ViewModel — lets users configure print profile (colors, fonts, layout)
    /// and preview a sample document with current settings.
    /// </summary>
    public sealed class PrintCenterViewModel : BaseViewModel
    {
        private readonly IPrintProfileProvider _profileProvider;
        private readonly IDocumentHtmlBuilder _htmlBuilder;
        private readonly IInvoicePdfPreviewService _previewService;

        private PrintProfile _profile;
        public PrintProfile Profile
        {
            get => _profile;
            set => SetProperty(ref _profile, value);
        }

        private string _successMessage;
        public string SuccessMessage
        {
            get => _successMessage;
            set { SetProperty(ref _successMessage, value); OnPropertyChanged(nameof(HasSuccess)); }
        }
        public bool HasSuccess => !string.IsNullOrEmpty(_successMessage);

        // ── Bindable Profile Properties ──
        private string _primaryColor;
        public string PrimaryColor { get => _primaryColor; set { if (SetProperty(ref _primaryColor, value) && _profile != null) _profile.PrimaryColor = value; } }

        private string _headerBgColor;
        public string HeaderBgColor { get => _headerBgColor; set { if (SetProperty(ref _headerBgColor, value) && _profile != null) _profile.HeaderBgColor = value; } }

        private string _borderColor;
        public string BorderColor { get => _borderColor; set { if (SetProperty(ref _borderColor, value) && _profile != null) _profile.BorderColor = value; } }

        private string _textColor;
        public string TextColor { get => _textColor; set { if (SetProperty(ref _textColor, value) && _profile != null) _profile.TextColor = value; } }

        private string _subtitleColor;
        public string SubtitleColor { get => _subtitleColor; set { if (SetProperty(ref _subtitleColor, value) && _profile != null) _profile.SubtitleColor = value; } }

        private string _fontFamily;
        public string FontFamily { get => _fontFamily; set { if (SetProperty(ref _fontFamily, value) && _profile != null) _profile.FontFamily = value; } }

        private int _titleFontSize;
        public int TitleFontSize { get => _titleFontSize; set { if (SetProperty(ref _titleFontSize, value) && _profile != null) _profile.TitleFontSize = value; } }

        private int _bodyFontSize;
        public int BodyFontSize { get => _bodyFontSize; set { if (SetProperty(ref _bodyFontSize, value) && _profile != null) _profile.BodyFontSize = value; } }

        private bool _showLogo;
        public bool ShowLogo { get => _showLogo; set { if (SetProperty(ref _showLogo, value) && _profile != null) _profile.ShowLogo = value; } }

        private bool _showCompanyName;
        public bool ShowCompanyName { get => _showCompanyName; set { if (SetProperty(ref _showCompanyName, value) && _profile != null) _profile.ShowCompanyName = value; } }

        private bool _showAddress;
        public bool ShowAddress { get => _showAddress; set { if (SetProperty(ref _showAddress, value) && _profile != null) _profile.ShowAddress = value; } }

        private bool _showContact;
        public bool ShowContact { get => _showContact; set { if (SetProperty(ref _showContact, value) && _profile != null) _profile.ShowContact = value; } }

        private bool _showTaxNumber;
        public bool ShowTaxNumber { get => _showTaxNumber; set { if (SetProperty(ref _showTaxNumber, value) && _profile != null) _profile.ShowTaxNumber = value; } }

        private bool _showFooter;
        public bool ShowFooter { get => _showFooter; set { if (SetProperty(ref _showFooter, value) && _profile != null) _profile.ShowFooter = value; } }

        private string _footerText;
        public string FooterText { get => _footerText; set { if (SetProperty(ref _footerText, value) && _profile != null) _profile.FooterText = value; } }

        // ── Section Ordering ──
        public static readonly string[] AllSectionKeys = { "CompanyHeader", "DocumentTitle", "MetaFields", "LinesTable", "Summary", "Notes", "Footer" };
        public static readonly Dictionary<string, string> SectionDisplayNames = new()
        {
            ["CompanyHeader"] = "رأس الشركة",
            ["DocumentTitle"] = "عنوان المستند",
            ["MetaFields"] = "بيانات المستند",
            ["LinesTable"] = "جدول البنود",
            ["Summary"] = "الملخص",
            ["Notes"] = "الملاحظات",
            ["Footer"] = "التذييل"
        };

        private ObservableCollection<PrintSectionItem> _sections = new();
        public ObservableCollection<PrintSectionItem> Sections { get => _sections; set => SetProperty(ref _sections, value); }

        private PrintSectionItem _selectedSection;
        public PrintSectionItem SelectedSection { get => _selectedSection; set => SetProperty(ref _selectedSection, value); }

        // ── Column Visibility ──
        private ObservableCollection<ColumnVisibilityItem> _columnVisibility = new();
        public ObservableCollection<ColumnVisibilityItem> ColumnVisibility { get => _columnVisibility; set => SetProperty(ref _columnVisibility, value); }

        // ── Custom HTML ──
        private string _customHeaderHtml;
        public string CustomHeaderHtml { get => _customHeaderHtml; set { if (SetProperty(ref _customHeaderHtml, value) && _profile != null) _profile.CustomHeaderHtml = value; } }

        private string _customFooterHtml;
        public string CustomFooterHtml { get => _customFooterHtml; set { if (SetProperty(ref _customFooterHtml, value) && _profile != null) _profile.CustomFooterHtml = value; } }

        public ICommand MoveSectionUpCommand { get; }
        public ICommand MoveSectionDownCommand { get; }


        public ObservableCollection<string> AvailableFonts { get; } = new()
        {
            "Segoe UI, Tahoma, Arial",
            "Cairo, Tahoma, Arial",
            "Amiri, serif",
            "Tajawal, sans-serif",
            "Noto Sans Arabic, sans-serif",
            "Arial, Tahoma",
            "Times New Roman, serif"
        };

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand PreviewCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        public PrintCenterViewModel(
            IPrintProfileProvider profileProvider,
            IDocumentHtmlBuilder htmlBuilder,
            IInvoicePdfPreviewService previewService)
        {
            _profileProvider = profileProvider ?? throw new ArgumentNullException(nameof(profileProvider));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            PreviewCommand = new AsyncRelayCommand(PreviewAsync);
            ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
            MoveSectionUpCommand = new RelayCommand(_ => MoveSectionUp(), _ => SelectedSection != null && Sections.IndexOf(SelectedSection) > 0);
            MoveSectionDownCommand = new RelayCommand(_ => MoveSectionDown(), _ => SelectedSection != null && Sections.IndexOf(SelectedSection) < Sections.Count - 1);
        }

        private async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                var profile = await _profileProvider.GetProfileAsync();
                Profile = profile;
                SyncFromProfile(profile);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل إعدادات الطباعة", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveAsync()
        {
            if (_profile == null) return;
            IsBusy = true;
            try
            {
                // Sync section order and hidden columns back to profile
                _profile.SectionOrder = Sections.Select(s => s.Key).ToList();
                _profile.HiddenColumns = ColumnVisibility.Where(c => !c.IsVisible).Select(c => c.ColumnHeader).ToList();

                await _profileProvider.SaveProfileAsync(_profile);
                SuccessMessage = "تم حفظ إعدادات الطباعة بنجاح.";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("حفظ الإعدادات", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PreviewAsync()
        {
            if (_profile == null) return;
            try
            {
                var sampleData = BuildSampleDocument();
                var html = await _htmlBuilder.BuildAsync(sampleData);
                var request = new InvoicePdfPreviewRequest
                {
                    Title = "معاينة قالب الطباعة",
                    FilePrefix = "print_preview",
                    HtmlContent = html,
                    StartInHtmlMode = false
                };
                await _previewService.ShowHtmlPreviewAsync(request);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("المعاينة", ex);
            }
        }

        private void ResetDefaults()
        {
            var defaults = new PrintProfile();
            Profile = defaults;
            SyncFromProfile(defaults);
        }

        private void SyncFromProfile(PrintProfile p)
        {
            PrimaryColor = p.PrimaryColor;
            HeaderBgColor = p.HeaderBgColor;
            BorderColor = p.BorderColor;
            TextColor = p.TextColor;
            SubtitleColor = p.SubtitleColor;
            FontFamily = p.FontFamily;
            TitleFontSize = p.TitleFontSize;
            BodyFontSize = p.BodyFontSize;
            ShowLogo = p.ShowLogo;
            ShowCompanyName = p.ShowCompanyName;
            ShowAddress = p.ShowAddress;
            ShowContact = p.ShowContact;
            ShowTaxNumber = p.ShowTaxNumber;
            ShowFooter = p.ShowFooter;
            FooterText = p.FooterText;
            CustomHeaderHtml = p.CustomHeaderHtml;
            CustomFooterHtml = p.CustomFooterHtml;

            // Build section ordering
            Sections.Clear();
            var order = p.SectionOrder != null && p.SectionOrder.Count > 0 ? p.SectionOrder : new List<string>(AllSectionKeys);
            // Add any missing sections at the end
            foreach (var key in AllSectionKeys)
            {
                if (!order.Contains(key)) order.Add(key);
            }
            foreach (var key in order)
            {
                if (SectionDisplayNames.ContainsKey(key))
                    Sections.Add(new PrintSectionItem { Key = key, DisplayName = SectionDisplayNames[key] });
            }

            // Build column visibility from sample document columns
            ColumnVisibility.Clear();
            var sampleColumns = new[] { "#", "الصنف", "الوحدة", "الكمية", "السعر", "خصم %", "الإجمالي" };
            var hidden = p.HiddenColumns ?? new List<string>();
            foreach (var col in sampleColumns)
                ColumnVisibility.Add(new ColumnVisibilityItem { ColumnHeader = col, IsVisible = !hidden.Contains(col) });
        }

        private void MoveSectionUp()
        {
            if (SelectedSection == null) return;
            var idx = Sections.IndexOf(SelectedSection);
            if (idx <= 0) return;
            Sections.Move(idx, idx - 1);
        }

        private void MoveSectionDown()
        {
            if (SelectedSection == null) return;
            var idx = Sections.IndexOf(SelectedSection);
            if (idx < 0 || idx >= Sections.Count - 1) return;
            Sections.Move(idx, idx + 1);
        }

        private static DocumentData BuildSampleDocument()
        {
            var culture = CultureInfo.GetCultureInfo("ar-EG");
            return new DocumentData
            {
                Title = "فاتورة بيع رقم INV-202602-0001 (معاينة)",
                DocumentType = PrintableDocumentType.SalesInvoice,
                MetaFields = new()
                {
                    // TODO: Replace with IDateTimeProvider when refactored — static method cannot use DI
                    new DocumentField("التاريخ", DateTime.Now.ToString("yyyy-MM-dd", culture)),
                    new DocumentField("العميل", "شركة المعاينة التجارية"),
                    new DocumentField("المستودع", "المستودع الرئيسي"),
                    new DocumentField("الحالة", "مسودة")
                },
                Columns = new()
                {
                    new TableColumn("#"),
                    new TableColumn("الصنف"),
                    new TableColumn("الوحدة"),
                    new TableColumn("الكمية", true),
                    new TableColumn("السعر", true),
                    new TableColumn("خصم %", true),
                    new TableColumn("الإجمالي", true)
                },
                Rows = new()
                {
                    new() { "1", "لابتوب ديل", "قطعة", "2", "3,500.00", "5.00", "6,650.00" },
                    new() { "2", "ماوس لاسلكي", "قطعة", "10", "150.00", "0.00", "1,500.00" },
                    new() { "3", "شاشة سامسونج 27\"", "قطعة", "3", "2,800.00", "10.00", "7,560.00" }
                },
                SummaryFields = new()
                {
                    new DocumentField("الإجمالي", "15,710.00"),
                    new DocumentField("الخصم", "1,175.00"),
                    new DocumentField("الضريبة (14%)", "2,034.90"),
                    new DocumentField("الصافي", "16,569.90", true)
                },
                Notes = "هذه معاينة تجريبية لقالب الطباعة. يمكنك تعديل الألوان والخطوط والعرض من إعدادات مركز الطباعة."
            };
        }
    }

    public sealed class PrintSectionItem : BaseViewModel
    {
        public string Key { get; set; }

        private string _displayName;
        public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
    }

    public sealed class ColumnVisibilityItem : BaseViewModel
    {
        public string ColumnHeader { get; set; }

        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
    }
}
