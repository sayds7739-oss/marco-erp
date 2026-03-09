using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Purchases;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Purchases;
using MarcoERP.Application.Interfaces.Treasury;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for QuickCashPaymentWindow — سند صرف سريع.
    /// Creates (and optionally posts) a cash payment linked to a supplier.
    /// </summary>
    public sealed class QuickCashPaymentViewModel : BaseViewModel
    {
        private readonly ICashPaymentService _cashPaymentService;
        private readonly ICashboxService _cashboxService;
        private readonly ISupplierService _supplierService;
        private readonly IDateTimeProvider _dateTime;

        public ObservableCollection<CashboxDto> Cashboxes { get; } = new();
        public ObservableCollection<SupplierDto> Suppliers { get; } = new();

        // ── Properties ──

        private string _paymentNumber;
        public string PaymentNumber
        {
            get => _paymentNumber;
            set => SetProperty(ref _paymentNumber, value);
        }

        private DateTime _paymentDate;
        public DateTime PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
        }

        private int? _selectedCashboxId;
        public int? SelectedCashboxId
        {
            get => _selectedCashboxId;
            set => SetProperty(ref _selectedCashboxId, value);
        }

        private int? _selectedSupplierId;
        public int? SelectedSupplierId
        {
            get => _selectedSupplierId;
            set
            {
                if (SetProperty(ref _selectedSupplierId, value))
                    OnSupplierChanged();
            }
        }

        private decimal _supplierBalance;
        public decimal SupplierBalance
        {
            get => _supplierBalance;
            private set
            {
                if (SetProperty(ref _supplierBalance, value))
                    OnPropertyChanged(nameof(BalanceAfter));
            }
        }

        private decimal _amount;
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                    OnPropertyChanged(nameof(BalanceAfter));
            }
        }

        /// <summary>Balance after payment = SupplierBalance − Amount.</summary>
        public decimal BalanceAfter => SupplierBalance - Amount;

        private string _beneficiaryName;
        public string BeneficiaryName
        {
            get => _beneficiaryName;
            set => SetProperty(ref _beneficiaryName, value);
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        /// <summary>Optional: links this payment to a specific purchase invoice.</summary>
        public int? PurchaseInvoiceId { get; set; }

        // ── Commands ──

        public ICommand LoadedCommand { get; }
        public ICommand SaveAndPostCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>Raised to close the dialog. True = success, False = cancelled.</summary>
        public event Action<bool> RequestClose;

        // ── Constructor ──

        public QuickCashPaymentViewModel(
            ICashPaymentService cashPaymentService,
            ICashboxService cashboxService,
            ISupplierService supplierService,
            IDateTimeProvider dateTime)
        {
            _cashPaymentService = cashPaymentService ?? throw new ArgumentNullException(nameof(cashPaymentService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _paymentDate = _dateTime.Today;

            LoadedCommand = new AsyncRelayCommand(LoadAsync);
            SaveAndPostCommand = new AsyncRelayCommand(SaveAndPostAsync);
            SaveDraftCommand = new AsyncRelayCommand(SaveDraftAsync);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        // ── Load ──

        private async Task LoadAsync()
        {
            ClearError();
            IsBusy = true;
            try
            {
                // Load next number
                var numResult = await _cashPaymentService.GetNextNumberAsync();
                if (numResult.IsSuccess)
                    PaymentNumber = numResult.Data;

                // Load cashboxes
                var cbResult = await _cashboxService.GetAllAsync();
                Cashboxes.Clear();
                if (cbResult.IsSuccess)
                {
                    foreach (var c in cbResult.Data.Where(x => x.IsActive))
                        Cashboxes.Add(c);
                }
                if (SelectedCashboxId == null)
                {
                    var def = Cashboxes.FirstOrDefault(x => x.IsDefault) ?? Cashboxes.FirstOrDefault();
                    if (def != null) SelectedCashboxId = def.Id;
                }

                // Load suppliers
                var suppResult = await _supplierService.GetAllAsync();
                Suppliers.Clear();
                if (suppResult.IsSuccess)
                {
                    foreach (var s in suppResult.Data)
                        Suppliers.Add(s);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل بيانات سند الصرف", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Supplier changed ──

        private void OnSupplierChanged()
        {
            var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId);
            SupplierBalance = supplier?.PreviousBalance ?? 0m;
        }

        // ── Save helpers ──

        private bool Validate()
        {
            ClearError();

            if (SelectedCashboxId is null or <= 0)
            {
                ErrorMessage = "يجب اختيار الخزنة.";
                return false;
            }

            if (SelectedSupplierId is null or <= 0)
            {
                ErrorMessage = "يجب اختيار المورد.";
                return false;
            }

            var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId);
            if (supplier?.AccountId is null or <= 0)
            {
                ErrorMessage = "المورد المحدد ليس لديه حساب مرتبط. يرجى تعديل بيانات المورد أولاً.";
                return false;
            }

            if (Amount <= 0)
            {
                ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر.";
                return false;
            }

            return true;
        }

        private CreateCashPaymentDto BuildDto()
        {
            var supplier = Suppliers.FirstOrDefault(s => s.Id == SelectedSupplierId);
            if (supplier == null)
                throw new InvalidOperationException("المورد المحدد غير موجود.");

            return new CreateCashPaymentDto
            {
                PaymentDate = PaymentDate,
                CashboxId = SelectedCashboxId!.Value,
                AccountId = supplier.AccountId!.Value,
                SupplierId = SelectedSupplierId,
                Amount = Amount,
                PurchaseInvoiceId = PurchaseInvoiceId,
                Description = string.IsNullOrWhiteSpace(BeneficiaryName)
                    ? Description
                    : $"{Description} — المستفيد: {BeneficiaryName}".Trim(' ', '—', ' '),
                Notes = Notes
            };
        }

        private async Task SaveAndPostAsync()
        {
            if (!Validate()) return;

            IsBusy = true;
            try
            {
                var dto = BuildDto();
                var createResult = await _cashPaymentService.CreateAsync(dto);
                if (createResult.IsFailure)
                {
                    ErrorMessage = createResult.ErrorMessage ?? "فشل إنشاء سند الصرف.";
                    return;
                }

                var postResult = await _cashPaymentService.PostAsync(createResult.Data.Id);
                if (postResult.IsFailure)
                {
                    ErrorMessage = postResult.ErrorMessage ?? "تم الحفظ لكن فشل الترحيل.";
                    return;
                }

                StatusMessage = $"تم حفظ وترحيل سند الصرف رقم {PaymentNumber} بنجاح.";
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("حفظ وترحيل سند الصرف", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveDraftAsync()
        {
            if (!Validate()) return;

            IsBusy = true;
            try
            {
                var dto = BuildDto();
                var createResult = await _cashPaymentService.CreateAsync(dto);
                if (createResult.IsFailure)
                {
                    ErrorMessage = createResult.ErrorMessage ?? "فشل إنشاء سند الصرف.";
                    return;
                }

                StatusMessage = $"تم حفظ سند الصرف رقم {PaymentNumber} كمسودة.";
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("حفظ سند الصرف", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
