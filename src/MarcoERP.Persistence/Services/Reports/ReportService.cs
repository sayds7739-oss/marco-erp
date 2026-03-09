using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Interfaces.Reports;
using MarcoERP.Domain.Enums;
using MarcoERP.Persistence;

namespace MarcoERP.Persistence.Services.Reports
{
    /// <summary>
    /// Implementation of IReportService using direct EF Core queries
    /// against the MarcoDbContext for efficient aggregation.
    /// </summary>
    public sealed partial class ReportService : IReportService
    {
        private readonly MarcoDbContext _db;
        private readonly IDateTimeProvider _dateTime;

        public ReportService(MarcoDbContext db, IDateTimeProvider dateTime)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _dateTime = dateTime ?? throw new ArgumentNullException(nameof(dateTime));
        }

        // ════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════

        private static string GetAccountTypeName(AccountType type) => type switch
        {
            AccountType.Asset => "أصول",
            AccountType.Liability => "التزامات",
            AccountType.Equity => "حقوق ملكية",
            AccountType.Revenue => "إيرادات",
            AccountType.COGS => "تكلفة المبيعات",
            AccountType.Expense => "مصروفات",
            AccountType.OtherIncome => "إيرادات أخرى",
            AccountType.OtherExpense => "مصروفات أخرى",
            _ => type.ToString()
        };

        private static string GetSourceTypeName(SourceType type) => type switch
        {
            SourceType.Manual => "يدوي",
            SourceType.SalesInvoice => "فاتورة بيع",
            SourceType.PurchaseInvoice => "فاتورة شراء",
            SourceType.CashReceipt => "سند قبض",
            SourceType.CashPayment => "سند صرف",
            SourceType.Inventory => "مخزون",
            SourceType.Adjustment => "تسوية",
            SourceType.Opening => "رصيد افتتاحي",
            SourceType.Closing => "إقفال",
            SourceType.PurchaseReturn => "مرتجع شراء",
            SourceType.SalesReturn => "مرتجع بيع",
            SourceType.CashTransfer => "تحويل بين خزن",
            _ => type.ToString()
        };

        private static string GetMovementTypeName(MovementType type) => type switch
        {
            MovementType.PurchaseIn => "شراء",
            MovementType.SalesOut => "بيع",
            MovementType.SalesReturn => "مرتجع بيع",
            MovementType.PurchaseReturn => "مرتجع شراء",
            MovementType.AdjustmentIn => "تسوية +",
            MovementType.AdjustmentOut => "تسوية -",
            MovementType.TransferOut => "تحويل صادر",
            MovementType.TransferIn => "تحويل وارد",
            MovementType.OpeningBalance => "رصيد افتتاحي",
            _ => type.ToString()
        };
    }
}
