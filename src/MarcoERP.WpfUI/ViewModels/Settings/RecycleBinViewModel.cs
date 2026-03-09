using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Settings;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Settings;

namespace MarcoERP.WpfUI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for the Recycle Bin screen — view and restore soft-deleted records.
    /// </summary>
    public sealed class RecycleBinViewModel : BaseViewModel
    {
        private readonly IRecycleBinService _recycleBinService;
        private readonly IDialogService _dialog;
        private readonly ICurrentUserService _currentUser;

        public RecycleBinViewModel(
            IRecycleBinService recycleBinService,
            IDialogService dialog,
            ICurrentUserService currentUser)
        {
            _recycleBinService = recycleBinService ?? throw new ArgumentNullException(nameof(recycleBinService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            DeletedRecords = new ObservableCollection<DeletedRecordDto>();

            // Initialize entity type filter
            var types = _recycleBinService.GetSupportedEntityTypes();
            EntityTypes = new ObservableCollection<EntityTypeFilter>(
                new[] { new EntityTypeFilter { Key = "All", ArabicName = "الكل" } }
                    .Concat(types.Select(t => new EntityTypeFilter { Key = t.Key, ArabicName = t.ArabicName })));
            SelectedEntityType = EntityTypes.FirstOrDefault();

            LoadCommand = new AsyncRelayCommand(LoadAsync);
            RestoreCommand = new AsyncRelayCommand(RestoreSelectedAsync, () => SelectedRecord != null && CanRestore);
            RefreshCommand = new AsyncRelayCommand(LoadAsync);
        }

        // ── Collections ───────────────────────────────────────

        public ObservableCollection<DeletedRecordDto> DeletedRecords { get; }
        public ObservableCollection<EntityTypeFilter> EntityTypes { get; }

        // ── Selected Item ─────────────────────────────────────

        private DeletedRecordDto _selectedRecord;
        public DeletedRecordDto SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                {
                    OnPropertyChanged(nameof(CanRestore));
                }
            }
        }

        // ── Filter ────────────────────────────────────────────

        private EntityTypeFilter _selectedEntityType;
        public EntityTypeFilter SelectedEntityType
        {
            get => _selectedEntityType;
            set
            {
                if (SetProperty(ref _selectedEntityType, value))
                {
                    _ = LoadAsync();
                }
            }
        }

        // ── Permissions ───────────────────────────────────────

        public bool CanRestore => SelectedRecord?.CanRestore == true
            && _currentUser.HasPermission(PermissionKeys.RecycleBinRestore);

        // ── Commands ──────────────────────────────────────────

        public ICommand LoadCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Load ──────────────────────────────────────────────

        private async Task LoadAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                ServiceResult<IReadOnlyList<DeletedRecordDto>> result;
                if (SelectedEntityType == null || SelectedEntityType.Key == "All")
                {
                    result = await _recycleBinService.GetAllDeletedAsync(cts.Token);
                }
                else
                {
                    result = await _recycleBinService.GetByEntityTypeAsync(SelectedEntityType.Key, cts.Token);
                }

                DeletedRecords.Clear();
                if (result.IsSuccess)
                {
                    foreach (var item in result.Data)
                        DeletedRecords.Add(item);

                    StatusMessage = $"تم تحميل {DeletedRecords.Count} سجل محذوف";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (OperationCanceledException)
            {
                ErrorMessage = "انتهت مهلة تحميل البيانات.";
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل سلة المحذوفات", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Restore ───────────────────────────────────────────

        private async Task RestoreSelectedAsync()
        {
            if (SelectedRecord == null)
                return;

            if (!_dialog.Confirm(
                $"هل تريد استعادة «{SelectedRecord.DisplayName}»؟",
                "تأكيد الاستعادة"))
                return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _recycleBinService.RestoreAsync(
                    SelectedRecord.EntityType,
                    SelectedRecord.Id);

                if (result.IsSuccess)
                {
                    _dialog.ShowInfo($"تم استعادة «{SelectedRecord.DisplayName}» بنجاح.", "تمت الاستعادة");
                    DeletedRecords.Remove(SelectedRecord);
                    SelectedRecord = null;
                    StatusMessage = "تم استعادة السجل بنجاح";
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("استعادة السجل", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// Entity type filter item.
    /// </summary>
    public sealed class EntityTypeFilter
    {
        public string Key { get; set; }
        public string ArabicName { get; set; }
    }
}
