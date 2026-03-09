using System;
using FluentValidation;
using MarcoERP.Application.DTOs.Sales;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Sales;
using MarcoERP.Application.Interfaces.Settings;
using MarcoERP.Domain.Entities.Accounting.Policies;
using MarcoERP.Domain.Interfaces;
using MarcoERP.Domain.Interfaces.Inventory;
using MarcoERP.Domain.Interfaces.Sales;
using MarcoERP.Domain.Interfaces.Settings;

namespace MarcoERP.Application.Services.Sales
{
    public sealed class PosRepositories
    {
        public PosRepositories(
            PosSalesRepositories salesRepos,
            PosInventoryRepositories inventoryRepos,
            PosAccountingRepositories accountingRepos)
        {
            if (salesRepos == null) throw new ArgumentNullException(nameof(salesRepos));
            if (inventoryRepos == null) throw new ArgumentNullException(nameof(inventoryRepos));
            if (accountingRepos == null) throw new ArgumentNullException(nameof(accountingRepos));

            SessionRepo = salesRepos.SessionRepo;
            PaymentRepo = salesRepos.PaymentRepo;
            InvoiceRepo = salesRepos.InvoiceRepo;

            ProductRepo = inventoryRepos.ProductRepo;
            WhProductRepo = inventoryRepos.WhProductRepo;
            MovementRepo = inventoryRepos.MovementRepo;

            JournalRepo = accountingRepos.JournalRepo;
            AccountRepo = accountingRepos.AccountRepo;
        }

        public IPosSessionRepository SessionRepo { get; }
        public IPosPaymentRepository PaymentRepo { get; }
        public ISalesInvoiceRepository InvoiceRepo { get; }
        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
    }

    public sealed class PosSalesRepositories
    {
        public PosSalesRepositories(
            IPosSessionRepository sessionRepo,
            IPosPaymentRepository paymentRepo,
            ISalesInvoiceRepository invoiceRepo)
        {
            SessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
            PaymentRepo = paymentRepo ?? throw new ArgumentNullException(nameof(paymentRepo));
            InvoiceRepo = invoiceRepo ?? throw new ArgumentNullException(nameof(invoiceRepo));
        }

        public IPosSessionRepository SessionRepo { get; }
        public IPosPaymentRepository PaymentRepo { get; }
        public ISalesInvoiceRepository InvoiceRepo { get; }
    }

    public sealed class PosInventoryRepositories
    {
        public PosInventoryRepositories(
            IProductRepository productRepo,
            IWarehouseProductRepository whProductRepo,
            IInventoryMovementRepository movementRepo)
        {
            ProductRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
            WhProductRepo = whProductRepo ?? throw new ArgumentNullException(nameof(whProductRepo));
            MovementRepo = movementRepo ?? throw new ArgumentNullException(nameof(movementRepo));
        }

        public IProductRepository ProductRepo { get; }
        public IWarehouseProductRepository WhProductRepo { get; }
        public IInventoryMovementRepository MovementRepo { get; }
    }

    public sealed class PosAccountingRepositories
    {
        public PosAccountingRepositories(
            IJournalEntryRepository journalRepo,
            IAccountRepository accountRepo)
        {
            JournalRepo = journalRepo ?? throw new ArgumentNullException(nameof(journalRepo));
            AccountRepo = accountRepo ?? throw new ArgumentNullException(nameof(accountRepo));
        }

        public IJournalEntryRepository JournalRepo { get; }
        public IAccountRepository AccountRepo { get; }
    }

    public sealed class PosServices
    {
        public PosServices(
            IFiscalYearRepository fiscalYearRepo,
            IJournalNumberGenerator journalNumberGen,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IDateTimeProvider dateTime,
            ISystemSettingRepository systemSettingRepo,
            IFeatureService featureService,
            IAuditLogger auditLogger,
            IReceiptPrinterService receiptPrinterService)
        {
            FiscalYearRepo = fiscalYearRepo ?? throw new ArgumentNullException(nameof(fiscalYearRepo));
            JournalNumberGen = journalNumberGen ?? throw new ArgumentNullException(nameof(journalNumberGen));
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            CurrentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            DateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
            SystemSettingRepo = systemSettingRepo;
            FeatureService = featureService;
            AuditLogger = auditLogger;
            ReceiptPrinterService = receiptPrinterService;
        }

        public IFiscalYearRepository FiscalYearRepo { get; }
        public IJournalNumberGenerator JournalNumberGen { get; }
        public IUnitOfWork UnitOfWork { get; }
        public ICurrentUserService CurrentUser { get; }
        public IDateTimeProvider DateTime { get; }
        public ISystemSettingRepository SystemSettingRepo { get; }
        public IFeatureService FeatureService { get; }
        public IAuditLogger AuditLogger { get; }
        public IReceiptPrinterService ReceiptPrinterService { get; }
    }

    public sealed class PosValidators
    {
        public PosValidators(
            IValidator<OpenPosSessionDto> openSessionValidator,
            IValidator<ClosePosSessionDto> closeSessionValidator,
            IValidator<CompletePoseSaleDto> completeSaleValidator)
        {
            OpenSessionValidator = openSessionValidator ?? throw new ArgumentNullException(nameof(openSessionValidator));
            CloseSessionValidator = closeSessionValidator ?? throw new ArgumentNullException(nameof(closeSessionValidator));
            CompleteSaleValidator = completeSaleValidator ?? throw new ArgumentNullException(nameof(completeSaleValidator));
        }

        public IValidator<OpenPosSessionDto> OpenSessionValidator { get; }
        public IValidator<ClosePosSessionDto> CloseSessionValidator { get; }
        public IValidator<CompletePoseSaleDto> CompleteSaleValidator { get; }
    }
}
