using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.DTOs.Treasury;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Treasury;

namespace MarcoERP.WpfUI.ViewModels.Treasury
{
    /// <summary>
    /// ViewModel for QuickCashReceiptWindow — سند قبض سريع.
    /// Creates (and optionally posts) a cash receipt linked to a customer.
    /// </summary>
    public sealed class QuickCashReceiptViewModel : BaseViewModel
    {
        private readonly ICashReceiptService _cashReceiptService;
        private readonly ICashboxService _cashboxService;
        private readonly ICustomerService _customerService;
        private readonly IDateTimeProvider _dateTime;

        public ObservableCollection<CashboxDto> Cashboxes { get; } = new();
        public ObservableCollection<CustomerDto> Customers { get; } = new();

        // ── Properties ──

        private string _receiptNumber;
        public string ReceiptNumber
        {
            get => _receiptNumber;
            set => SetProperty(ref _receiptNumber, value);
        }

        private DateTime _receiptDate;
        public DateTime ReceiptDate
        {
            get => _receiptDate;
            set => SetProperty(ref _receiptDate, value);
        }

        private int? _selectedCashboxId;
        public int? SelectedCashboxId
        {
            get => _selectedCashboxId;
            set => SetProperty(ref _selectedCashboxId, value);
        }

        private int? _selectedCustomerId;
        public int? SelectedCustomerId
        {
            get => _selectedCustomerId;
            set
            {
                if (SetProperty(ref _selectedCustomerId, value))
                    OnCustomerChanged();
            }
        }

        private decimal _customerBalance;
        public decimal CustomerBalance
        {
            get => _customerBalance;
            private set
            {
                if (SetProperty(ref _customerBalance, value))
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

        /// <summary>Balance after receipt = CustomerBalance − Amount.</summary>
        public decimal BalanceAfter => CustomerBalance - Amount;

        private string _recipientName;
        public string RecipientName
        {
            get => _recipientName;
            set => SetProperty(ref _recipientName, value);
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

        /// <summary>Optional: links this receipt to a specific sales invoice.</summary>
        public int? SalesInvoiceId { get; set; }

        // ── Commands ──

        public ICommand LoadedCommand { get; }
        public ICommand SaveAndPostCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>Raised to close the dialog. True = success, False = cancelled.</summary>
        public event Action<bool> RequestClose;

        // ── Constructor ──

        public QuickCashReceiptViewModel(
            ICashReceiptService cashReceiptService,
            ICashboxService cashboxService,
            ICustomerService customerService,
            IDateTimeProvider dateTime)
        {
            _cashReceiptService = cashReceiptService ?? throw new ArgumentNullException(nameof(cashReceiptService));
            _cashboxService = cashboxService ?? throw new ArgumentNullException(nameof(cashboxService));
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            _receiptDate = _dateTime.Today;

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
                var numResult = await _cashReceiptService.GetNextNumberAsync();
                if (numResult.IsSuccess)
                    ReceiptNumber = numResult.Data;

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

                // Load customers
                var custResult = await _customerService.GetAllAsync();
                Customers.Clear();
                if (custResult.IsSuccess)
                {
                    foreach (var c in custResult.Data)
                        Customers.Add(c);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("تحميل بيانات سند القبض", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Customer changed ──

        private void OnCustomerChanged()
        {
            var customer = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId);
            CustomerBalance = customer?.PreviousBalance ?? 0m;
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

            if (SelectedCustomerId is null or <= 0)
            {
                ErrorMessage = "يجب اختيار العميل.";
                return false;
            }

            var customer = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId);
            if (customer?.AccountId is null or <= 0)
            {
                ErrorMessage = "العميل المحدد ليس لديه حساب مرتبط. يرجى تعديل بيانات العميل أولاً.";
                return false;
            }

            if (Amount <= 0)
            {
                ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر.";
                return false;
            }

            return true;
        }

        private CreateCashReceiptDto BuildDto()
        {
            var customer = Customers.FirstOrDefault(c => c.Id == SelectedCustomerId);
            if (customer == null)
                throw new InvalidOperationException("العميل المحدد غير موجود.");

            return new CreateCashReceiptDto
            {
                ReceiptDate = ReceiptDate,
                CashboxId = SelectedCashboxId!.Value,
                AccountId = customer.AccountId!.Value,
                CustomerId = SelectedCustomerId,
                Amount = Amount,
                SalesInvoiceId = SalesInvoiceId,
                Description = string.IsNullOrWhiteSpace(RecipientName)
                    ? Description
                    : $"{Description} — المستلم: {RecipientName}".Trim(' ', '—', ' '),
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
                var createResult = await _cashReceiptService.CreateAsync(dto);
                if (createResult.IsFailure)
                {
                    ErrorMessage = createResult.ErrorMessage ?? "فشل إنشاء سند القبض.";
                    return;
                }

                var postResult = await _cashReceiptService.PostAsync(createResult.Data.Id);
                if (postResult.IsFailure)
                {
                    ErrorMessage = postResult.ErrorMessage ?? "تم الحفظ لكن فشل الترحيل.";
                    return;
                }

                StatusMessage = $"تم حفظ وترحيل سند القبض رقم {ReceiptNumber} بنجاح.";
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("حفظ وترحيل سند القبض", ex);
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
                var createResult = await _cashReceiptService.CreateAsync(dto);
                if (createResult.IsFailure)
                {
                    ErrorMessage = createResult.ErrorMessage ?? "فشل إنشاء سند القبض.";
                    return;
                }

                StatusMessage = $"تم حفظ سند القبض رقم {ReceiptNumber} كمسودة.";
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = FriendlyErrorMessage("حفظ سند القبض", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
