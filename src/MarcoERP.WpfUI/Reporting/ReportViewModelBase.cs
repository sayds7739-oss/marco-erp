using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Reporting.Interfaces;
using MarcoERP.Application.Reporting.Models;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.WpfUI.ViewModels;
using MarcoERP.WpfUI.Common;

namespace MarcoERP.WpfUI.Reporting
{
    /// <summary>
    /// Enterprise-grade base ViewModel for all interactive reports.
    /// Provides: pagination, server-side sort/filter, KPI cards, drill-down,
    /// expandable rows, complexity modes, PDF/Excel export — all out of the box.
    /// 
    /// Subclass must override:
    ///   - <see cref="BuildReportDefinition"/> — define columns, filters, settings
    ///   - <see cref="LoadPageCoreAsync"/> — fetch a page of data
    /// 
    /// Optional overrides:
    ///   - <see cref="LoadKpisCoreAsync"/> — compute KPI summary cards
    ///   - <see cref="LoadChildRowsCoreAsync"/> — load expandable row children
    ///   - <see cref="BuildExportRequest"/> — customize PDF/Excel export content
    /// </summary>
    public abstract class ReportViewModelBase<TRow> : BaseViewModel
        where TRow : ReportRowBase
    {
        private readonly IReportExportService _exportService;
        private readonly DrillDownEngine _drillDownEngine;
        private CancellationTokenSource _cts;

        protected ReportViewModelBase(
            IReportExportService exportService,
            DrillDownEngine drillDownEngine)
        {
            _exportService = exportService;
            _drillDownEngine = drillDownEngine;

            Rows = new ObservableCollection<TRow>();
            KpiCards = new ObservableCollection<KpiCard>();
            VisibleColumns = new ObservableCollection<ReportColumnDefinition>();
            FilterEngine = new SmartFilterEngine();

            // Commands
            GenerateCommand = new AsyncRelayCommand(GenerateAsync);
            NextPageCommand = new AsyncRelayCommand(NextPageAsync);
            PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync);
            FirstPageCommand = new AsyncRelayCommand(FirstPageAsync);
            LastPageCommand = new AsyncRelayCommand(LastPageAsync);
            SortCommand = new RelayCommand(SortByColumn);
            DrillDownCommand = new RelayCommand(ExecuteDrillDown);
            ToggleExpandCommand = new AsyncRelayCommand(ToggleExpandAsync);
            ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync);
            ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync);
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            SetComplexityCommand = new RelayCommand(SetComplexity);
            ChangePageSizeCommand = new RelayCommand(ChangePageSize);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        // ══════════════════════════════════════════════════════════
        // ABSTRACT / VIRTUAL — Subclass contract
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Creates the report metadata: columns, filters, default sort, page size.
        /// Called once during initialization.
        /// </summary>
        protected abstract ReportDefinition BuildReportDefinition();

        /// <summary>
        /// Fetches a page of data from the server.
        /// Must apply filters, sort, and pagination.
        /// </summary>
        protected abstract Task<PagedResult<TRow>> LoadPageCoreAsync(
            IReadOnlyList<ActiveFilter> filters,
            SortDefinition sort,
            int pageIndex,
            int pageSize,
            CancellationToken ct);

        /// <summary>
        /// Computes KPI summary cards for the current filters.
        /// Override to provide report-specific KPIs.
        /// Default returns empty list.
        /// </summary>
        protected virtual Task<IReadOnlyList<KpiCard>> LoadKpisCoreAsync(
            IReadOnlyList<ActiveFilter> filters, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<KpiCard>>(Array.Empty<KpiCard>());

        /// <summary>
        /// Loads child rows for an expandable parent row.
        /// Override in reports that support row expansion.
        /// </summary>
        protected virtual Task<IReadOnlyList<TRow>> LoadChildRowsCoreAsync(
            int parentRowId,
            IReadOnlyList<ActiveFilter> filters,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TRow>>(Array.Empty<TRow>());

        /// <summary>
        /// Builds the export request for PDF/Excel.
        /// Default implementation uses report definition columns + current rows.
        /// Override for custom export formatting.
        /// </summary>
        protected virtual ReportExportRequest BuildExportRequest()
        {
            var def = ReportDefinition;
            var req = new ReportExportRequest
            {
                Title = def?.Title ?? "تقرير",
                Subtitle = BuildExportSubtitle(),
                FooterSummary = BuildExportFooter()
            };

            // Columns
            var visibleCols = GetColumnsForComplexity(ComplexityMode);
            foreach (var col in visibleCols)
            {
                req.Columns.Add(new ReportColumn(col.Header, (float)col.WidthRatio, col.IsNumeric));
            }

            // Rows
            foreach (var row in Rows)
            {
                var cells = new List<string>();
                foreach (var col in visibleCols)
                {
                    var prop = typeof(TRow).GetProperty(col.BindingPath);
                    var value = prop?.GetValue(row);
                    cells.Add(FormatCellValue(value, col));
                }
                req.Rows.Add(cells);
            }

            return req;
        }

        /// <summary>Optional: override to provide a subtitle for export.</summary>
        protected virtual string BuildExportSubtitle() => null;

        /// <summary>Optional: override to provide a footer summary for export.</summary>
        protected virtual string BuildExportFooter() => null;

        // ══════════════════════════════════════════════════════════
        // PROPERTIES
        // ══════════════════════════════════════════════════════════

        // ── Report Definition ──
        private ReportDefinition _reportDefinition;
        public ReportDefinition ReportDefinition
        {
            get => _reportDefinition;
            private set => SetProperty(ref _reportDefinition, value);
        }

        // ── Data ──
        public ObservableCollection<TRow> Rows { get; }
        public ObservableCollection<KpiCard> KpiCards { get; }
        public ObservableCollection<ReportColumnDefinition> VisibleColumns { get; }

        // ── Filter Engine ──
        public SmartFilterEngine FilterEngine { get; }

        private ObservableCollection<FilterDefinition> _visibleFilters;
        public ObservableCollection<FilterDefinition> VisibleFilters
        {
            get => _visibleFilters;
            private set => SetProperty(ref _visibleFilters, value);
        }

        // ── Pagination ──
        private int _currentPage;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            private set => SetProperty(ref _totalPages, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set => SetProperty(ref _pageSize, value);
        }

        public bool HasPreviousPage => CurrentPage > 0;
        public bool HasNextPage => CurrentPage < TotalPages - 1;

        // ── Sorting ──
        private SortDefinition _currentSort;
        public SortDefinition CurrentSort
        {
            get => _currentSort;
            set => SetProperty(ref _currentSort, value);
        }

        // ── Complexity Mode ──
        private ReportComplexityMode _complexityMode = ReportComplexityMode.Simple;
        public ReportComplexityMode ComplexityMode
        {
            get => _complexityMode;
            set
            {
                if (SetProperty(ref _complexityMode, value))
                    ApplyComplexityMode();
            }
        }

        // ── UI State ──
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            private set => SetProperty(ref _hasData, value);
        }

        private string _emptyMessage = "اضغط 'عرض التقرير' لتوليد البيانات";
        public string EmptyMessage
        {
            get => _emptyMessage;
            set => SetProperty(ref _emptyMessage, value);
        }

        private string _paginationInfo;
        public string PaginationInfo
        {
            get => _paginationInfo;
            private set => SetProperty(ref _paginationInfo, value);
        }

        // ── Selected Row (for drill-down) ──
        private TRow _selectedRow;
        public TRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                    OnPropertyChanged(nameof(CanDrillDown));
            }
        }

        public bool CanDrillDown => SelectedRow?.SourceType != null && SelectedRow?.SourceId != null;

        // ══════════════════════════════════════════════════════════
        // COMMANDS
        // ══════════════════════════════════════════════════════════

        public ICommand GenerateCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand DrillDownCommand { get; }
        public ICommand ToggleExpandCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand SetComplexityCommand { get; }
        public ICommand ChangePageSizeCommand { get; }
        public ICommand CancelCommand { get; }

        // ══════════════════════════════════════════════════════════
        // INITIALIZATION
        // ══════════════════════════════════════════════════════════

        private bool _isInitialized;

        /// <summary>
        /// Call from the constructor of the derived class AFTER DI injection,
        /// or from OnNavigatedToAsync if the VM implements INavigationAware.
        /// </summary>
        protected void InitializeReport()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            ReportDefinition = BuildReportDefinition();
            if (ReportDefinition == null)
                throw new InvalidOperationException("يجب أن تُرجع BuildReportDefinition() تعريف تقرير صالح.");

            PageSize = ReportDefinition.DefaultPageSize;
            CurrentSort = ReportDefinition.DefaultSort;

            FilterEngine.Initialize(ReportDefinition);
            ApplyComplexityMode();
        }

        // ══════════════════════════════════════════════════════════
        // GENERATE
        // ══════════════════════════════════════════════════════════

        private async Task GenerateAsync()
        {
            if (IsLoading) return;
            ClearError();
            IsLoading = true;
            IsBusy = true;
            CurrentPage = 0;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // Load KPIs and first page in parallel
                var kpiTask = LoadKpisCoreAsync(FilterEngine.ActiveFilters, ct);
                var pageTask = LoadPageCoreAsync(
                    FilterEngine.ActiveFilters, CurrentSort, 0, PageSize, ct);

                await Task.WhenAll(kpiTask, pageTask).ConfigureAwait(false);

                ct.ThrowIfCancellationRequested();

                var kpis = await kpiTask;
                var page = await pageTask;

                // Update on UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // KPIs
                    KpiCards.Clear();
                    foreach (var kpi in kpis.Where(k => k.MinComplexity <= ComplexityMode))
                        KpiCards.Add(kpi);

                    // Data
                    ApplyPageResult(page);
                    StatusMessage = $"تم عرض {TotalCount} سجل";
                });
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "تم إلغاء العملية.";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل التقرير", ex);
            }
            finally
            {
                IsLoading = false;
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // PAGINATION
        // ══════════════════════════════════════════════════════════

        private async Task NextPageAsync()
        {
            if (!HasNextPage || IsLoading) return;
            await LoadPageByIndexAsync(CurrentPage + 1);
        }

        private async Task PreviousPageAsync()
        {
            if (!HasPreviousPage || IsLoading) return;
            await LoadPageByIndexAsync(CurrentPage - 1);
        }

        private async Task FirstPageAsync()
        {
            if (CurrentPage == 0 || IsLoading) return;
            await LoadPageByIndexAsync(0);
        }

        private async Task LastPageAsync()
        {
            if (CurrentPage >= TotalPages - 1 || IsLoading) return;
            await LoadPageByIndexAsync(TotalPages - 1);
        }

        private async Task LoadPageByIndexAsync(int pageIndex)
        {
            ClearError();
            IsLoading = true;
            IsBusy = true;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                var page = await LoadPageCoreAsync(
                    FilterEngine.ActiveFilters, CurrentSort, pageIndex, PageSize, ct);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    CurrentPage = pageIndex;
                    ApplyPageResult(page);
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل الصفحة", ex);
            }
            finally
            {
                IsLoading = false;
                IsBusy = false;
            }
        }

        private void ApplyPageResult(PagedResult<TRow> page)
        {
            Rows.Clear();
            foreach (var row in page.Items)
                Rows.Add(row);

            TotalCount = page.TotalCount;
            TotalPages = page.TotalPages;
            HasData = page.TotalCount > 0;
            EmptyMessage = HasData ? null : "لا توجد نتائج للعرض";

            UpdatePaginationInfo();
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
        }

        private void UpdatePaginationInfo()
        {
            if (TotalCount == 0)
            {
                PaginationInfo = null;
                return;
            }

            var from = CurrentPage * PageSize + 1;
            var to = Math.Min(from + PageSize - 1, TotalCount);
            PaginationInfo = $"عرض {from}-{to} من {TotalCount}";
        }

        // ══════════════════════════════════════════════════════════
        // SORTING
        // ══════════════════════════════════════════════════════════

        private void SortByColumn(object param)
        {
            if (param is not string columnName) return;

            var col = ReportDefinition?.Columns.FirstOrDefault(c => c.BindingPath == columnName);
            if (col == null || !col.IsSortable) return;

            if (CurrentSort?.PropertyName == columnName)
            {
                // Toggle direction
                CurrentSort = new SortDefinition(columnName,
                    CurrentSort.Direction == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending);
            }
            else
            {
                CurrentSort = new SortDefinition(columnName, ListSortDirection.Ascending);
            }

            // Re-fetch current page with new sort
            _ = GenerateAsync();
        }

        // ══════════════════════════════════════════════════════════
        // DRILL-DOWN
        // ══════════════════════════════════════════════════════════

        private void ExecuteDrillDown(object param)
        {
            var row = param as TRow ?? SelectedRow;
            if (row == null) return;

            if (!_drillDownEngine.Navigate(row))
            {
                StatusMessage = "هذا السجل لا يدعم الانتقال المباشر.";
            }
        }

        // ══════════════════════════════════════════════════════════
        // EXPANDABLE ROWS
        // ══════════════════════════════════════════════════════════

        private async Task ToggleExpandAsync()
        {
            if (SelectedRow is not ExpandableReportRow<TRow> expandable) return;

            if (expandable.IsExpanded)
            {
                // Collapse: remove child rows from display
                expandable.IsExpanded = false;
                CollapseChildren(expandable);
            }
            else
            {
                // Expand: load children if needed, then insert after parent
                if (!expandable.IsChildrenLoaded)
                {
                    expandable.IsLoadingChildren = true;
                    try
                    {
                        _cts?.Cancel();
                        _cts = new CancellationTokenSource();
                        var children = await LoadChildRowsCoreAsync(
                            expandable.RowId, FilterEngine.ActiveFilters, _cts.Token);

                        expandable.Children.Clear();
                        expandable.Children.AddRange(children);
                        expandable.IsChildrenLoaded = true;
                    }
                    finally
                    {
                        expandable.IsLoadingChildren = false;
                    }
                }

                expandable.IsExpanded = true;
                ExpandChildren(expandable);
            }
        }

        private void ExpandChildren(ExpandableReportRow<TRow> parent)
        {
            var parentIndex = Rows.IndexOf(parent as TRow);
            if (parentIndex < 0) return;

            for (var i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                child.Level = parent.Level + 1;
                Rows.Insert(parentIndex + 1 + i, child);
            }
        }

        private void CollapseChildren(ExpandableReportRow<TRow> parent)
        {
            var parentIndex = Rows.IndexOf(parent as TRow);
            if (parentIndex < 0) return;

            // Remove all children (those with Level > parent.Level immediately following)
            while (parentIndex + 1 < Rows.Count && Rows[parentIndex + 1].Level > parent.Level)
            {
                Rows.RemoveAt(parentIndex + 1);
            }
        }

        // ══════════════════════════════════════════════════════════
        // COMPLEXITY MODE
        // ══════════════════════════════════════════════════════════

        private void SetComplexity(object param)
        {
            if (param is ReportComplexityMode mode)
                ComplexityMode = mode;
            else if (param is string str && Enum.TryParse<ReportComplexityMode>(str, out var parsed))
                ComplexityMode = parsed;
        }

        private void ApplyComplexityMode()
        {
            if (ReportDefinition == null) return;

            // Update visible columns
            VisibleColumns.Clear();
            foreach (var col in GetColumnsForComplexity(ComplexityMode))
                VisibleColumns.Add(col);

            // Update visible filters
            VisibleFilters = new ObservableCollection<FilterDefinition>(
                FilterEngine.GetVisibleFilters(ComplexityMode));

            // Update visible KPIs
            var toRemove = KpiCards.Where(k => k.MinComplexity > ComplexityMode).ToList();
            foreach (var kpi in toRemove)
                KpiCards.Remove(kpi);
        }

        private IReadOnlyList<ReportColumnDefinition> GetColumnsForComplexity(ReportComplexityMode mode)
            => ReportDefinition?.Columns?.Where(c => c.MinComplexity <= mode).ToList()
               ?? new List<ReportColumnDefinition>();

        // ══════════════════════════════════════════════════════════
        // EXPORT
        // ══════════════════════════════════════════════════════════

        private async Task ExportPdfAsync()
        {
            if (Rows.Count == 0) { ErrorMessage = "لا توجد بيانات للتصدير."; return; }
            var result = await ReportExportHelper.ExportPdfAsync(_exportService, BuildExportRequest());
            if (result != null)
                StatusMessage = result.EndsWith(".pdf") ? "تم التصدير بنجاح" : result;
        }

        private async Task ExportExcelAsync()
        {
            if (Rows.Count == 0) { ErrorMessage = "لا توجد بيانات للتصدير."; return; }
            var result = await ReportExportHelper.ExportExcelAsync(_exportService, BuildExportRequest());
            if (result != null)
                StatusMessage = result.EndsWith(".xlsx") ? "تم التصدير بنجاح" : result;
        }

        // ══════════════════════════════════════════════════════════
        // FILTERS
        // ══════════════════════════════════════════════════════════

        private void ClearFilters()
        {
            FilterEngine.ClearAll();
        }

        /// <summary>Helper: set a filter value from the ViewModel.</summary>
        protected void SetFilter(string key, object value, object valueTo = null)
            => FilterEngine.SetFilter(key, value, valueTo);

        /// <summary>Helper: get a filter value.</summary>
        protected T GetFilterValue<T>(string key)
        {
            var value = FilterEngine.GetFilterValue(key);
            if (value is T typed) return typed;
            return default;
        }

        // ══════════════════════════════════════════════════════════
        // PAGE SIZE CHANGE
        // ══════════════════════════════════════════════════════════

        private void ChangePageSize(object param)
        {
            if (param is int size)
                PageSize = size;
            else if (param is string str && int.TryParse(str, out var parsed))
                PageSize = parsed;

            if (HasData)
                _ = GenerateAsync();
        }

        // ══════════════════════════════════════════════════════════
        // CANCEL
        // ══════════════════════════════════════════════════════════

        private void Cancel()
        {
            _cts?.Cancel();
        }

        // ══════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════

        private static string FormatCellValue(object value, ReportColumnDefinition col)
        {
            if (value == null) return string.Empty;

            if (!string.IsNullOrEmpty(col.StringFormat) && value is IFormattable formattable)
                return formattable.ToString(col.StringFormat, null);

            return col.DataType switch
            {
                ColumnDataType.Currency => ((decimal)value).ToString("N2"),
                ColumnDataType.Decimal => ((decimal)value).ToString("N4"),
                ColumnDataType.Percentage => ((decimal)value).ToString("N2") + "%",
                ColumnDataType.Date when value is DateTime dt => dt.ToString("yyyy/MM/dd"),
                ColumnDataType.DateTime when value is DateTime dt => dt.ToString("yyyy/MM/dd HH:mm"),
                _ => value.ToString()
            };
        }

        // ══════════════════════════════════════════════════════════
        // DISPOSE
        // ══════════════════════════════════════════════════════════

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts?.Cancel();
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
