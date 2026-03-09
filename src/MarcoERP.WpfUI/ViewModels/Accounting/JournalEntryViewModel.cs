using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Accounting;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Accounting;
using MarcoERP.Application.DTOs.Printing;
using MarcoERP.Application.Interfaces.Printing;
using MarcoERP.Domain.Enums;
using MarcoERP.WpfUI.Common;
using MarcoERP.WpfUI.Services;

namespace MarcoERP.WpfUI.ViewModels.Accounting
{
    /// <summary>
    /// ViewModel for Journal Entry management screen.
    /// Supports creating, posting, reversing, and deleting drafts.
    /// </summary>
    public sealed class JournalEntryViewModel : BaseViewModel
    {
        private readonly IJournalEntryService _journalService;
        private readonly IAccountService _accountService;
        private readonly IDialogService _dialog;
        private readonly IInvoicePdfPreviewService _previewService;
        private readonly IDocumentHtmlBuilder _htmlBuilder;
        public JournalEntryViewModel(
            IJournalEntryService journalService,
            IAccountService accountService,
            IDialogService dialog,
            IInvoicePdfPreviewService previewService,
            IDocumentHtmlBuilder htmlBuilder)
        {
            _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
            _htmlBuilder = htmlBuilder ?? throw new ArgumentNullException(nameof(htmlBuilder));

            JournalEntries = new ObservableCollection<JournalEntryDto>();
            PostableAccounts = new ObservableCollection<AccountDto>();
            FormLines = new ObservableCollection<JournalLineFormItem>();

            LoadCommand = new AsyncRelayCommand(LoadJournalEntriesAsync);
            NewDraftCommand = new RelayCommand(PrepareNewDraft);
            AddLineCommand = new RelayCommand(AddLine);
            RemoveLineCommand = new RelayCommand(RemoveLine);
            SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync, () => CanSaveDraft);
            PostCommand = new AsyncRelayCommand(PostEntryAsync, () => CanPost);
            ReverseCommand = new AsyncRelayCommand(ReverseEntryAsync, () => CanReverse);
            DeleteDraftCommand = new AsyncRelayCommand(DeleteDraftAsync, () => CanDeleteDraft);
            CancelCommand = new RelayCommand(CancelEditing);
            PrintCommand = new AsyncRelayCommand(PrintAsync);

            // Default filter
            FilterStatus = null; // All
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<JournalEntryDto> JournalEntries { get; }
        public ObservableCollection<AccountDto> PostableAccounts { get; }
        public ObservableCollection<JournalLineFormItem> FormLines { get; }

        // ── Selected entry ───────────────────────────────────────

        private JournalEntryDto _selectedEntry;
        public JournalEntryDto SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (SetProperty(ref _selectedEntry, value))
                {
                    if (value != null && !IsEditing)
                    {
                        PopulateFormFromEntry(value);
                    }
                    OnPropertyChanged(nameof(CanPost));
                    OnPropertyChanged(nameof(CanReverse));
                    OnPropertyChanged(nameof(CanDeleteDraft));
                    OnPropertyChanged(nameof(SelectedEntryStatus));
                }
            }
        }

        public string SelectedEntryStatus
        {
            get
            {
                if (SelectedEntry == null) return "";
                switch (SelectedEntry.Status)
                {
                    case JournalEntryStatus.Draft: return "مسودة";
                    case JournalEntryStatus.Posted: return "مُرحل";
                    case JournalEntryStatus.Reversed: return "مُعكوس";
                    default: return SelectedEntry.Status.ToString();
                }
            }
        }

        // ── Filter ──────────────────────────────────────────────

        private JournalEntryStatus? _filterStatus;
        public JournalEntryStatus? FilterStatus
        {
            get => _filterStatus;
            set => SetProperty(ref _filterStatus, value);
        }

        // ── Form Fields ─────────────────────────────────────────

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        private DateTime _formDate = DateTime.Today;
        public DateTime FormDate
        {
            get => _formDate;
            set => SetProperty(ref _formDate, value);
        }

        private string _formDescription;
        public string FormDescription
        {
            get => _formDescription;
            set { SetProperty(ref _formDescription, value); OnPropertyChanged(nameof(CanSaveDraft)); }
        }

        private string _formReferenceNumber;
        public string FormReferenceNumber
        {
            get => _formReferenceNumber;
            set => SetProperty(ref _formReferenceNumber, value);
        }

        private string _reversalReason;
        public string ReversalReason
        {
            get => _reversalReason;
            set => SetProperty(ref _reversalReason, value);
        }

        // ── Totals (computed) ────────────────────────────────────

        public decimal TotalDebit => FormLines.Sum(l => l.DebitAmount);
        public decimal TotalCredit => FormLines.Sum(l => l.CreditAmount);
        public decimal Difference => TotalDebit - TotalCredit;
        public bool IsBalanced => TotalDebit == TotalCredit && TotalDebit > 0;

        public void RefreshTotals()
        {
            OnPropertyChanged(nameof(TotalDebit));
            OnPropertyChanged(nameof(TotalCredit));
            OnPropertyChanged(nameof(Difference));
            OnPropertyChanged(nameof(IsBalanced));
            OnPropertyChanged(nameof(CanSaveDraft));
        }

        // ── Source Types for display ────────────────────────────

        public IReadOnlyList<JournalEntryStatus> StatusFilters { get; } = new List<JournalEntryStatus>
        {
            JournalEntryStatus.Draft,
            JournalEntryStatus.Posted,
            JournalEntryStatus.Reversed
        };

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand NewDraftCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand ReverseCommand { get; }
        public ICommand DeleteDraftCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PrintCommand { get; }

        // ── Can Execute ─────────────────────────────────────────

        public bool CanSaveDraft => IsEditing
                                  && !string.IsNullOrWhiteSpace(FormDescription)
                                  && FormLines.Count >= 2
                                  && IsBalanced;

        public bool CanPost => SelectedEntry != null
                             && SelectedEntry.Status == JournalEntryStatus.Draft;

        public bool CanReverse => SelectedEntry != null
                               && SelectedEntry.Status == JournalEntryStatus.Posted;

        public bool CanDeleteDraft => SelectedEntry != null
                                   && SelectedEntry.Status == JournalEntryStatus.Draft;

        // ── Print ─────────────────────────────────────────────────

        private async Task PrintAsync(object _)
        {
            if (SelectedEntry == null) return;
            IsBusy = true;
            ClearError();
            try
            {
                var data = new DocumentData
                {
                    Title = $"قيد يومية رقم {SelectedEntry.JournalNumber}",
                    DocumentType = PrintableDocumentType.JournalEntry,
                    MetaFields = new()
                    {
                        new("رقم القيد", SelectedEntry.JournalNumber),
                        new("التاريخ", SelectedEntry.JournalDate.ToString("yyyy-MM-dd")),
                        new("الوصف", SelectedEntry.Description ?? "—"),
                        new("المرجع", SelectedEntry.ReferenceNumber ?? "—"),
                        new("الحالة", SelectedEntry.StatusName, true),
                        new("المصدر", SelectedEntry.SourceTypeName ?? "—")
                    },
                    Columns = new()
                    {
                        new("#"), new("كود الحساب"), new("اسم الحساب"),
                        new("مدين", true), new("دائن", true), new("البيان")
                    },
                    Notes = SelectedEntry.Description
                };
                int row = 1;
                foreach (var l in SelectedEntry.Lines)
                    data.Rows.Add(new() { (row++).ToString(), l.AccountCode, l.AccountNameAr,
                        l.DebitAmount.ToString("N2"), l.CreditAmount.ToString("N2"), l.Description ?? "" });
                data.SummaryFields = new()
                {
                    new("إجمالي المدين", SelectedEntry.TotalDebit.ToString("N2"), true),
                    new("إجمالي الدائن", SelectedEntry.TotalCredit.ToString("N2"), true)
                };
                var html = await _htmlBuilder.BuildAsync(data);
                await _previewService.ShowHtmlPreviewAsync(new InvoicePdfPreviewRequest
                {
                    Title = data.Title, FilePrefix = "journal_entry", HtmlContent = html
                });
            }
            catch (Exception ex) { ErrorMessage = FriendlyErrorMessage("الطباعة", ex); }
            finally { IsBusy = false; }
        }

        // ── Load ─────────────────────────────────────────────────

        public async Task LoadJournalEntriesAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Load postable accounts for the lines combo
                var accsResult = await _accountService.GetPostableAccountsAsync();
                if (accsResult.IsSuccess)
                {
                    PostableAccounts.Clear();
                    foreach (var acc in accsResult.Data)
                        PostableAccounts.Add(acc);
                }

                // Load journal entries by filter
                IReadOnlyList<JournalEntryDto> entries;
                if (FilterStatus.HasValue)
                {
                    var result = await _journalService.GetByStatusAsync(FilterStatus.Value);
                    entries = result.IsSuccess ? result.Data : new List<JournalEntryDto>();
                }
                else
                {
                    // Load last 30 days
                    var result = await _journalService.GetByDateRangeAsync(
                        DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1));
                    entries = result.IsSuccess ? result.Data : new List<JournalEntryDto>();
                }

                JournalEntries.Clear();
                foreach (var entry in entries)
                    JournalEntries.Add(entry);

                StatusMessage = $"تم تحميل {JournalEntries.Count} قيد";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("التحميل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── New Draft ────────────────────────────────────────────

        private void PrepareNewDraft(object parameter)
        {
            IsEditing = true;
            ClearError();
            FormDate = DateTime.Today;
            FormDescription = "";
            FormReferenceNumber = "";
            FormLines.Clear();

            // Add 2 empty lines by default
            FormLines.Add(new JournalLineFormItem(this));
            FormLines.Add(new JournalLineFormItem(this));

            StatusMessage = "إنشاء قيد يومي جديد...";
        }

        // ── Add/Remove Lines ────────────────────────────────────

        private void AddLine(object parameter)
        {
            FormLines.Add(new JournalLineFormItem(this));
            RefreshTotals();
        }

        private void RemoveLine(object parameter)
        {
            if (parameter is JournalLineFormItem line && FormLines.Count > 2)
            {
                FormLines.Remove(line);
                RefreshTotals();
            }
        }

        // ── Save Draft ──────────────────────────────────────────

        private async Task SaveDraftAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var dto = new CreateJournalEntryDto
                {
                    JournalDate = FormDate,
                    Description = FormDescription,
                    ReferenceNumber = FormReferenceNumber,
                    SourceType = SourceType.Manual,
                    Lines = FormLines.Select(l => new CreateJournalEntryLineDto
                    {
                        AccountId = l.SelectedAccountId,
                        DebitAmount = l.DebitAmount,
                        CreditAmount = l.CreditAmount,
                        Description = l.Description
                    }).ToList()
                };

                var result = await _journalService.CreateDraftAsync(dto);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم إنشاء المسودة: {result.Data.DraftCode}";
                    IsEditing = false;
                    await LoadJournalEntriesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    await RefreshAfterConcurrencyFailureAsync(result.ErrorMessage, "تعارض في التزامن");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الحفظ", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Post ─────────────────────────────────────────────────

        private async Task PostEntryAsync()
        {
            if (SelectedEntry == null) return;

            if (!_dialog.Confirm(
                $"هل تريد ترحيل القيد «{SelectedEntry.DraftCode}»؟\nبعد الترحيل لا يمكن التعديل.",
                "تأكيد الترحيل")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _journalService.PostAsync(SelectedEntry.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم الترحيل بنجاح — رقم القيد: {result.Data.JournalNumber}";
                    await LoadJournalEntriesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    await RefreshAfterConcurrencyFailureAsync(result.ErrorMessage, "تعارض في التزامن");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("الترحيل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Reverse ──────────────────────────────────────────────

        private async Task ReverseEntryAsync()
        {
            if (SelectedEntry == null) return;

            if (!_dialog.Confirm(
                $"هل تريد عكس القيد «{SelectedEntry.JournalNumber}»؟\nسيتم إنشاء قيد عكسي.",
                "تأكيد العكس")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var dto = new ReverseJournalEntryDto
                {
                    JournalEntryId = SelectedEntry.Id,
                    ReversalReason = string.IsNullOrWhiteSpace(ReversalReason) ? "عكس قيد" : ReversalReason,
                    ReversalDate = SelectedEntry.JournalDate // Original date per governance
                };

                var result = await _journalService.ReverseAsync(dto);
                if (result.IsSuccess)
                {
                    StatusMessage = $"تم العكس بنجاح — قيد العكس: {result.Data.JournalNumber}";
                    await LoadJournalEntriesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    await RefreshAfterConcurrencyFailureAsync(result.ErrorMessage, "تعارض في التزامن");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العكس", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Delete Draft ────────────────────────────────────────

        private async Task DeleteDraftAsync()
        {
            if (SelectedEntry == null) return;

            if (!_dialog.Confirm(
                $"هل تريد حذف المسودة «{SelectedEntry.DraftCode}»؟",
                "تأكيد الحذف")) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _journalService.DeleteDraftAsync(SelectedEntry.Id);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم حذف المسودة";
                    await LoadJournalEntriesAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                    await RefreshAfterConcurrencyFailureAsync(result.ErrorMessage, "تعارض في التزامن");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("العملية", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Cancel ──────────────────────────────────────────────

        private void CancelEditing(object parameter)
        {
            IsEditing = false;
            FormLines.Clear();
            ClearError();
            StatusMessage = "تم الإلغاء";
        }

        // ── Helpers ─────────────────────────────────────────────

        private async Task RefreshAfterConcurrencyFailureAsync(string errorMessage, string title)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)
                || !errorMessage.Contains("تعارض تزامن", StringComparison.Ordinal))
            {
                return;
            }

            if (_dialog.Confirm("تم تعديل هذا القيد بواسطة مستخدم آخر. هل تريد إعادة تحميل البيانات الآن؟", title))
            {
                await LoadJournalEntriesAsync();
            }
        }

        private void PopulateFormFromEntry(JournalEntryDto entry)
        {
            FormDate = entry.JournalDate;
            FormDescription = entry.Description;
            FormReferenceNumber = entry.ReferenceNumber;

            FormLines.Clear();
            if (entry.Lines != null)
            {
                foreach (var line in entry.Lines)
                {
                    FormLines.Add(new JournalLineFormItem(this)
                    {
                        SelectedAccountId = line.AccountId,
                        DebitAmount = line.DebitAmount,
                        CreditAmount = line.CreditAmount,
                        Description = line.Description
                    });
                }
            }
            RefreshTotals();
        }
    }

    /// <summary>
    /// Represents a single journal entry line in the form.
    /// </summary>
    public sealed class JournalLineFormItem : BaseViewModel
    {
        private readonly JournalEntryViewModel _parent;

        public JournalLineFormItem(JournalEntryViewModel parent)
        {
            _parent = parent;
        }

        private int _selectedAccountId;
        public int SelectedAccountId
        {
            get => _selectedAccountId;
            set => SetProperty(ref _selectedAccountId, value);
        }

        private decimal _debitAmount;
        public decimal DebitAmount
        {
            get => _debitAmount;
            set
            {
                if (SetProperty(ref _debitAmount, value))
                {
                    // JNL-03: If debit > 0, credit must be 0
                    if (value > 0) CreditAmount = 0;
                    _parent?.RefreshTotals();
                }
            }
        }

        private decimal _creditAmount;
        public decimal CreditAmount
        {
            get => _creditAmount;
            set
            {
                if (SetProperty(ref _creditAmount, value))
                {
                    // JNL-03: If credit > 0, debit must be 0
                    if (value > 0) DebitAmount = 0;
                    _parent?.RefreshTotals();
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
}
