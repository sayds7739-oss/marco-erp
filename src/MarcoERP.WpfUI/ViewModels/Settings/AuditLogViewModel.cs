using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for the Audit Log Viewer screen (سجل المراجعة).
    /// Phase D.1: Admin-only read-only viewer with filters.
    /// </summary>
    public sealed class AuditLogViewModel : BaseViewModel
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IDateTimeProvider _dateTime;

        public AuditLogViewModel(IAuditLogService auditLogService, IDateTimeProvider dateTime)
        {
            _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));

            AuditLogs = new ObservableCollection<AuditLogDto>();

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            ClearFiltersCommand = new RelayCommand(ClearFilters);

            // Default date range: last 7 days
            StartDate = _dateTime.Today.AddDays(-7);
            EndDate = _dateTime.Today;
        }

        // ── Collections ──────────────────────────────────────────

        public ObservableCollection<AuditLogDto> AuditLogs { get; }

        // ── Filter Properties ────────────────────────────────────

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private string _entityTypeFilter;
        public string EntityTypeFilter
        {
            get => _entityTypeFilter;
            set => SetProperty(ref _entityTypeFilter, value);
        }

        private string _usernameFilter;
        public string UsernameFilter
        {
            get => _usernameFilter;
            set => SetProperty(ref _usernameFilter, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand LoadDataCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        // ── Load All Data ────────────────────────────────────────

        public async Task LoadDataAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                var endOfDay = EndDate.Date.AddDays(1).AddTicks(-1);
                var result = await _auditLogService.GetByDateRangeAsync(StartDate.Date, endOfDay);
                if (result.IsSuccess)
                {
                    AuditLogs.Clear();
                    foreach (var item in result.Data)
                        AuditLogs.Add(item);

                    StatusMessage = $"تم تحميل {AuditLogs.Count} سجل";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
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

        // ── Search with Filters ──────────────────────────────────

        private async Task SearchAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                // Priority: username filter → date range (always applied)
                Application.Common.ServiceResult<System.Collections.Generic.IReadOnlyList<AuditLogDto>> result;

                if (!string.IsNullOrWhiteSpace(UsernameFilter))
                {
                    result = await _auditLogService.GetByUserAsync(UsernameFilter.Trim());
                }
                else
                {
                    // Use end of day for EndDate to include the full day
                    var endOfDay = EndDate.Date.AddDays(1).AddTicks(-1);
                    result = await _auditLogService.GetByDateRangeAsync(StartDate.Date, endOfDay);
                }

                if (result.IsSuccess)
                {
                    AuditLogs.Clear();

                    foreach (var item in result.Data)
                    {
                        // Apply client-side entity type filter if specified
                        if (!string.IsNullOrWhiteSpace(EntityTypeFilter)
                            && !string.Equals(item.EntityType, EntityTypeFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                            continue;

                        AuditLogs.Add(item);
                    }

                    StatusMessage = $"تم العثور على {AuditLogs.Count} سجل";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("البحث", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Clear Filters ────────────────────────────────────────

        private void ClearFilters()
        {
            StartDate = _dateTime.Today.AddDays(-7);
            EndDate = _dateTime.Today;
            EntityTypeFilter = null;
            UsernameFilter = null;
            StatusMessage = "تم مسح الفلاتر";
        }
    }
}
