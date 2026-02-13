using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Inventory;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Inventory;
using MarcoERP.Application.Interfaces.SmartEntry;
using MarcoERP.WpfUI.Navigation;

namespace MarcoERP.WpfUI.ViewModels.Inventory
{
    /// <summary>
    /// ViewModel for Inventory Adjustment detail screen — create / edit / post / cancel.
    /// </summary>
    public sealed class InventoryAdjustmentDetailViewModel : BaseViewModel, INavigationAware
    {
        private readonly IInventoryAdjustmentService _adjustmentService;
        private readonly IWarehouseService _warehouseService;
        private readonly IProductService _productService;
        private readonly ISmartEntryQueryService _smartEntryQueryService;
        private readonly INavigationService _navigationService;
        private readonly ILineCalculationService _lineCalculationService;

        public InventoryAdjustmentDetailViewModel(
            IInventoryAdjustmentService adjustmentService,
            IWarehouseService warehouseService,
            IProductService productService,
            ISmartEntryQueryService smartEntryQueryService,
            INavigationService navigationService,
            ILineCalculationService lineCalculationService)
        {
            _adjustmentService = adjustmentService ?? throw new ArgumentNullException(nameof(adjustmentService));
            _warehouseService = warehouseService ?? throw new ArgumentNullException(nameof(warehouseService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _smartEntryQueryService = smartEntryQueryService ?? throw new ArgumentNullException(nameof(smartEntryQueryService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _lineCalculationService = lineCalculationService ?? throw new ArgumentNullException(nameof(lineCalculationService));

            Lines = new ObservableCollection<InventoryAdjustmentLineDto>();
            Warehouses = new ObservableCollection<WarehouseDto>();

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
            PostCommand = new AsyncRelayCommand(PostAsync, () => CanPost);
            CancelAdjustmentCommand = new AsyncRelayCommand(CancelAdjustmentAsync, () => CanCancelAdjustment);
            DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => CanDelete);
            BackCommand = new RelayCommand(_ => _navigationService.NavigateTo("InventoryAdjustments"));
            AddLineCommand = new RelayCommand(_ => AddLine());
            RemoveLineCommand = new RelayCommand(RemoveLine);
        }

        // ── State ────────────────────────────────────────────────

        private int? _editingId;
        private bool _isNew;
        public bool IsNew
        {
            get => _isNew;
            set => SetProperty(ref _isNew, value);
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private bool _isEditable;
        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value);
        }

        // ── Form Fields ─────────────────────────────────────────

        private string _adjustmentNumber;
        public string AdjustmentNumber
        {
            get => _adjustmentNumber;
            set => SetProperty(ref _adjustmentNumber, value);
        }

        private DateTime _adjustmentDate = DateTime.Today;
        public DateTime AdjustmentDate
        {
            get => _adjustmentDate;
            set => SetProperty(ref _adjustmentDate, value);
        }

        private int _warehouseId;
        public int WarehouseId
        {
            get => _warehouseId;
            set
            {
                if (SetProperty(ref _warehouseId, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    EnqueueDbWork(RefreshLineMetaAsync);
                }
            }
        }

        private string _reason;
        public string Reason
        {
            get => _reason;
            set { SetProperty(ref _reason, value); OnPropertyChanged(nameof(CanSave)); }
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public ObservableCollection<InventoryAdjustmentLineDto> Lines { get; }
        public ObservableCollection<WarehouseDto> Warehouses { get; }

        // ── Line Input Fields ───────────────────────────────────

        private int _lineProductId;
        public int LineProductId
        {
            get => _lineProductId;
            set
            {
                if (SetProperty(ref _lineProductId, value))
                    EnqueueDbWork(RefreshLineMetaAsync);
            }
        }

        private int _lineUnitId;
        public int LineUnitId
        {
            get => _lineUnitId;
            set
            {
                if (SetProperty(ref _lineUnitId, value))
                    EnqueueDbWork(RefreshLineMetaAsync);
            }
        }

        private decimal _lineSystemQty;
        public decimal LineSystemQty
        {
            get => _lineSystemQty;
            set => SetProperty(ref _lineSystemQty, value);
        }

        private decimal _lineActualQty;
        public decimal LineActualQty
        {
            get => _lineActualQty;
            set => SetProperty(ref _lineActualQty, value);
        }

        private decimal _lineConversion = 1;
        public decimal LineConversion
        {
            get => _lineConversion;
            set => SetProperty(ref _lineConversion, value);
        }

        private decimal _lineUnitCost;
        public decimal LineUnitCost
        {
            get => _lineUnitCost;
            set => SetProperty(ref _lineUnitCost, value);
        }

        // ── Commands ─────────────────────────────────────────────

        public ICommand SaveCommand { get; }
        public ICommand PostCommand { get; }
        public ICommand CancelAdjustmentCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }

        // ── Can Execute ──────────────────────────────────────────

        public bool CanSave => IsEditable
                      && WarehouseId > 0
                      && !string.IsNullOrWhiteSpace(Reason)
                      && Lines.Count > 0
                      && Lines.All(l => l.ProductId > 0 && l.UnitId > 0 && l.ActualQuantity >= 0);
        public bool CanPost => !IsNew && Status == "Draft";
        public bool CanCancelAdjustment => !IsNew && Status == "Posted";
        public bool CanDelete => !IsNew && Status == "Draft";

        // ── Navigation ───────────────────────────────────────────

        public async Task OnNavigatedToAsync(object parameter)
        {
            await LoadWarehousesAsync();

            if (parameter is int id)
            {
                await LoadAsync(id);
            }
            else
            {
                IsNew = true;
                IsEditable = true;
                Status = "Draft";
                Lines.Clear();

                try
                {
                    var numberResult = await _adjustmentService.GetNextNumberAsync();
                    AdjustmentNumber = numberResult.IsSuccess ? numberResult.Data : "";
                }
                catch
                {
                    AdjustmentNumber = "";
                }
            }
        }

        public Task<bool> OnNavigatingFromAsync()
        {
            return Task.FromResult(true);
        }

        // ── Load ─────────────────────────────────────────────────

        private async Task LoadWarehousesAsync()
        {
            try
            {
                var result = await _warehouseService.GetAllAsync();
                if (result.IsSuccess)
                {
                    Warehouses.Clear();
                    foreach (var w in result.Data)
                        Warehouses.Add(w);
                }
            }
            catch { /* silently fail */ }
        }

        private async Task LoadAsync(int id)
        {
            IsBusy = true;
            ClearError();
            try
            {
                var result = await _adjustmentService.GetByIdAsync(id);
                if (result.IsSuccess)
                {
                    var dto = result.Data;
                    _editingId = dto.Id;
                    IsNew = false;
                    AdjustmentNumber = dto.AdjustmentNumber;
                    AdjustmentDate = dto.AdjustmentDate;
                    WarehouseId = dto.WarehouseId;
                    Reason = dto.Reason;
                    Notes = dto.Notes;
                    Status = dto.Status;
                    IsEditable = dto.Status == "Draft";

                    Lines.Clear();
                    foreach (var line in dto.Lines)
                        Lines.Add(line);
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Save ─────────────────────────────────────────────────

        private async Task SaveAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                if (WarehouseId <= 0)
                {
                    ErrorMessage = "يجب اختيار المخزن.";
                    return;
                }

                if (Lines.Count == 0)
                {
                    ErrorMessage = "لا يمكن حفظ تسوية بدون بنود.";
                    return;
                }

                var invalidLines = Lines.Where(l => l.ProductId <= 0 || l.UnitId <= 0 || l.ActualQuantity < 0).ToList();
                if (invalidLines.Any())
                {
                    ErrorMessage = "يوجد بنود غير مكتملة (صنف أو وحدة أو كمية غير صحيحة).";
                    return;
                }

                if (IsNew)
                {
                    var dto = new CreateInventoryAdjustmentDto
                    {
                        AdjustmentDate = AdjustmentDate,
                        WarehouseId = WarehouseId,
                        Reason = Reason,
                        Notes = Notes,
                        Lines = Lines.Select(l => new CreateInventoryAdjustmentLineDto
                        {
                            ProductId = l.ProductId,
                            UnitId = l.UnitId,
                            ActualQuantity = l.ActualQuantity
                        }).ToList()
                    };
                    var result = await _adjustmentService.CreateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = $"تم إنشاء التسوية: {result.Data.AdjustmentNumber}";
                        _editingId = result.Data.Id;
                        IsNew = false;
                        AdjustmentNumber = result.Data.AdjustmentNumber;
                        StatusMessage = "تم الحفظ بنجاح";
                        OnPropertyChanged(nameof(CanPost));
                        OnPropertyChanged(nameof(CanDelete));
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }
                else
                {
                    var dto = new UpdateInventoryAdjustmentDto
                    {
                        Id = _editingId!.Value,
                        AdjustmentDate = AdjustmentDate,
                        WarehouseId = WarehouseId,
                        Reason = Reason,
                        Notes = Notes,
                        Lines = Lines.Select(l => new CreateInventoryAdjustmentLineDto
                        {
                            ProductId = l.ProductId,
                            UnitId = l.UnitId,
                            ActualQuantity = l.ActualQuantity
                        }).ToList()
                    };
                    var result = await _adjustmentService.UpdateAsync(dto);
                    if (result.IsSuccess)
                    {
                        StatusMessage = "تم تحديث التسوية بنجاح";
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
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

        private async Task PostAsync()
        {
            if (_editingId == null) return;

            var confirm = MessageBox.Show(
                "هل أنت متأكد من ترحيل هذه التسوية؟ لا يمكن التراجع.",
                "تأكيد الترحيل",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _adjustmentService.PostAsync(_editingId.Value);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم ترحيل التسوية بنجاح";
                    Status = "Posted";
                    IsEditable = false;
                    OnPropertyChanged(nameof(CanPost));
                    OnPropertyChanged(nameof(CanCancelAdjustment));
                    OnPropertyChanged(nameof(CanDelete));
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Cancel Adjustment ────────────────────────────────────

        private async Task CancelAdjustmentAsync()
        {
            if (_editingId == null) return;

            var confirm = MessageBox.Show(
                "هل أنت متأكد من إلغاء هذه التسوية؟",
                "تأكيد الإلغاء",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _adjustmentService.CancelAsync(_editingId.Value);
                if (result.IsSuccess)
                {
                    StatusMessage = "تم إلغاء التسوية";
                    Status = "Cancelled";
                    IsEditable = false;
                    OnPropertyChanged(nameof(CanPost));
                    OnPropertyChanged(nameof(CanCancelAdjustment));
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Delete ───────────────────────────────────────────────

        private async Task DeleteAsync()
        {
            if (_editingId == null) return;

            var confirm = MessageBox.Show(
                "هل أنت متأكد من حذف هذه التسوية؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            IsBusy = true;
            ClearError();
            try
            {
                var result = await _adjustmentService.DeleteDraftAsync(_editingId.Value);
                if (result.IsSuccess)
                {
                    _navigationService.NavigateTo("InventoryAdjustments");
                }
                else
                {
                    ErrorMessage = result.ErrorMessage;
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

        // ── Line Management ──────────────────────────────────────

        private void AddLine()
        {
            if (LineProductId <= 0 || LineUnitId <= 0) return;

            var diff = LineActualQty - LineSystemQty;
            Lines.Add(new InventoryAdjustmentLineDto
            {
                ProductId = LineProductId,
                UnitId = LineUnitId,
                SystemQuantity = LineSystemQty,
                ActualQuantity = LineActualQty,
                DifferenceQuantity = diff,
                ConversionFactor = LineConversion,
                DifferenceInBaseUnit = _lineCalculationService.ConvertQuantity(diff, LineConversion),
                UnitCost = LineUnitCost,
                CostDifference = _lineCalculationService.ConvertQuantity(diff, LineConversion) * LineUnitCost
            });

            LineProductId = 0;
            LineUnitId = 0;
            LineSystemQty = 0;
            LineActualQty = 0;
            LineConversion = 1;
            LineUnitCost = 0;

            OnPropertyChanged(nameof(CanSave));
        }

        private void RemoveLine(object parameter)
        {
            if (parameter is InventoryAdjustmentLineDto line)
                Lines.Remove(line);
            OnPropertyChanged(nameof(CanSave));
        }

        private async Task RefreshLineMetaAsync()
        {
            if (WarehouseId <= 0 || LineProductId <= 0 || LineUnitId <= 0)
                return;

            try
            {
                var productResult = await _productService.GetByIdAsync(LineProductId);
                if (!productResult.IsSuccess || productResult.Data == null)
                    return;

                var product = productResult.Data;
                var unit = product.Units?.FirstOrDefault(u => u.UnitId == LineUnitId);
                var factor = unit?.ConversionFactor ?? 1m;
                if (factor <= 0) factor = 1m;

                LineConversion = factor;
                LineUnitCost = product.WeightedAverageCost;

                var stockBase = await _smartEntryQueryService.GetStockBaseQtyAsync(WarehouseId, LineProductId);
                LineSystemQty = Math.Round(stockBase / factor, 4);
            }
            catch
            {
                // Non-critical; keep existing values.
            }
        }
    }
}
