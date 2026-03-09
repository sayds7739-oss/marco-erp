using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces.Reports;
using Microsoft.AspNetCore.Mvc;

namespace MarcoERP.API.Controllers.Reports
{
    /// <summary>
    /// API controller for all reporting queries.
    /// All endpoints are read-only (GET).
    /// </summary>
    public class ReportsController : ApiControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // ── Dashboard ───────────────────────────────────────────

        /// <summary>Gets the dashboard summary with key business metrics.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        {
            var result = await _reportService.GetDashboardSummaryAsync(ct);
            return FromResult(result);
        }

        // ── Accounting Reports ──────────────────────────────────

        /// <summary>Gets the Trial Balance for a date range.</summary>
        [HttpGet("trial-balance")]
        public async Task<IActionResult> GetTrialBalance(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetTrialBalanceAsync(from, to, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Income Statement (Profit and Loss) for a date range.</summary>
        [HttpGet("income-statement")]
        public async Task<IActionResult> GetIncomeStatement(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetIncomeStatementAsync(from, to, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Balance Sheet as of a specific date.</summary>
        [HttpGet("balance-sheet")]
        public async Task<IActionResult> GetBalanceSheet(
            [FromQuery] DateTime asOf,
            CancellationToken ct)
        {
            var result = await _reportService.GetBalanceSheetAsync(asOf, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Account Statement showing all movements for a specific account.</summary>
        [HttpGet("account-statement/{accountId:int}")]
        public async Task<IActionResult> GetAccountStatement(
            int accountId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetAccountStatementAsync(accountId, from, to, ct);
            return FromResult(result);
        }

        // ── Sales & Purchase Reports ────────────────────────────

        /// <summary>Gets the Sales Report for a date range, optionally filtered by customer.</summary>
        [HttpGet("sales")]
        public async Task<IActionResult> GetSalesReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? customerId,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetSalesReportAsync(from, to, customerId, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Purchase Report for a date range, optionally filtered by supplier.</summary>
        [HttpGet("purchases")]
        public async Task<IActionResult> GetPurchaseReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? supplierId,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetPurchaseReportAsync(from, to, supplierId, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Profit Report per product for a date range.</summary>
        [HttpGet("profit")]
        public async Task<IActionResult> GetProfitReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetProfitReportAsync(from, to, ct);
            return FromResult(result);
        }

        // ── Inventory Reports ───────────────────────────────────

        /// <summary>Gets the Inventory Report showing current stock levels.</summary>
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventoryReport(
            [FromQuery] int? warehouseId,
            CancellationToken ct)
        {
            var result = await _reportService.GetInventoryReportAsync(warehouseId, ct);
            return FromResult(result);
        }

        /// <summary>Gets the Stock Card for a specific product showing all movements.</summary>
        [HttpGet("stock-card/{productId:int}")]
        public async Task<IActionResult> GetStockCard(
            int productId,
            [FromQuery] int? warehouseId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetStockCardAsync(productId, warehouseId, from, to, ct);
            return FromResult(result);
        }

        // ── Treasury Reports ────────────────────────────────────

        /// <summary>Gets the Cashbox Movement report.</summary>
        [HttpGet("cashbox-movement")]
        public async Task<IActionResult> GetCashboxMovement(
            [FromQuery] int? cashboxId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetCashboxMovementAsync(cashboxId, from, to, ct);
            return FromResult(result);
        }

        // ── Aging & VAT ─────────────────────────────────────────

        /// <summary>Gets the Aging Report for customer and supplier receivables/payables.</summary>
        [HttpGet("aging")]
        public async Task<IActionResult> GetAgingReport(CancellationToken ct)
        {
            var result = await _reportService.GetAgingReportAsync(ct);
            return FromResult(result);
        }

        /// <summary>Gets the VAT Report for a date range.</summary>
        [HttpGet("vat")]
        public async Task<IActionResult> GetVatReport(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken ct)
        {
            var err = ValidateDateRange(from, to);
            if (err is not null) return err;
            var result = await _reportService.GetVatReportAsync(from, to, ct);
            return FromResult(result);
        }

        // ── Customer & Supplier Statements ────────────────────

        /// <summary>Gets a Customer Statement for a date range.</summary>
        [HttpGet("customer-statement")]
        public async Task<IActionResult> GetCustomerStatement(
            [FromQuery] CustomerStatementRequestDto request,
            CancellationToken ct)
        {
            var result = await _reportService.GetCustomerStatementAsync(request, ct);
            return FromResult(result);
        }

        /// <summary>Gets a Supplier Statement for a date range.</summary>
        [HttpGet("supplier-statement")]
        public async Task<IActionResult> GetSupplierStatement(
            [FromQuery] SupplierStatementRequestDto request,
            CancellationToken ct)
        {
            var result = await _reportService.GetSupplierStatementAsync(request, ct);
            return FromResult(result);
        }

        // ── Journal Register ──────────────────────────────────

        /// <summary>Gets the Journal Entry Register for a date range.</summary>
        [HttpGet("journal-register")]
        public async Task<IActionResult> GetJournalRegister(
            [FromQuery] JournalRegisterRequestDto request,
            CancellationToken ct)
        {
            var result = await _reportService.GetJournalRegisterAsync(request, ct);
            return FromResult(result);
        }
    }
}
