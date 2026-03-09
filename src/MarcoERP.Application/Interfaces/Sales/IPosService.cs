using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarcoERP.Application.Common;
using MarcoERP.Application.DTOs.Sales;

namespace MarcoERP.Application.Interfaces.Sales
{
    /// <summary>
    /// Application service contract for POS operations.
    /// Handles session management, sale completion, cancellation, and reporting.
    /// </summary>
    public interface IPosService
    {
        // ── Session Management ──────────────────────────────────

        /// <summary>Opens a new POS session for the current user.</summary>
        [RequiresPermission(PermissionKeys.PosAccess)]
        Task<ServiceResult<PosSessionDto>> OpenSessionAsync(OpenPosSessionDto dto, CancellationToken ct = default);

        /// <summary>Gets the current open session for the current user.</summary>
        Task<ServiceResult<PosSessionDto>> GetCurrentSessionAsync(CancellationToken ct = default);

        /// <summary>Closes a POS session with reconciliation.</summary>
        [RequiresPermission(PermissionKeys.PosAccess)]
        Task<ServiceResult<PosSessionDto>> CloseSessionAsync(ClosePosSessionDto dto, CancellationToken ct = default);

        /// <summary>Gets a session by ID.</summary>
        Task<ServiceResult<PosSessionDto>> GetSessionByIdAsync(int id, CancellationToken ct = default);

        /// <summary>Gets all sessions (for admin view).</summary>
        Task<ServiceResult<IReadOnlyList<PosSessionListDto>>> GetAllSessionsAsync(CancellationToken ct = default);

        // ── Product Lookup ──────────────────────────────────────

        /// <summary>Loads all active products for POS cache (lightweight projections).</summary>
        Task<ServiceResult<IReadOnlyList<PosProductLookupDto>>> LoadProductCacheAsync(CancellationToken ct = default);

        /// <summary>Finds a product by barcode (searches both product and unit barcodes).</summary>
        Task<ServiceResult<PosProductLookupDto>> FindByBarcodeAsync(string barcode, CancellationToken ct = default);

        /// <summary>Searches products by name or code.</summary>
        Task<ServiceResult<IReadOnlyList<PosProductLookupDto>>> SearchProductsAsync(string term, CancellationToken ct = default);

        /// <summary>Gets available stock for a product in the session warehouse.</summary>
        Task<ServiceResult<decimal>> GetAvailableStockAsync(int productId, int warehouseId, CancellationToken ct = default);

        // ── Sale Completion ─────────────────────────────────────

        /// <summary>
        /// Completes a POS sale: creates invoice, posts it, deducts stock,
        /// generates journals, records payments. All in one atomic transaction.
        /// </summary>
        [RequiresPermission(PermissionKeys.PosAccess)]
        Task<ServiceResult<SalesInvoiceDto>> CompleteSaleAsync(CompletePoseSaleDto dto, CancellationToken ct = default);

        /// <summary>Cancels a posted POS invoice (reversal).</summary>
        [RequiresPermission(PermissionKeys.PosAccess)]
        Task<ServiceResult> CancelSaleAsync(int salesInvoiceId, int sessionId, CancellationToken ct = default);

        // ── Reports ─────────────────────────────────────────────

        /// <summary>Gets POS daily report.</summary>
        Task<ServiceResult<PosDailyReportDto>> GetDailyReportAsync(System.DateTime date, CancellationToken ct = default);

        /// <summary>Gets POS session report.</summary>
        Task<ServiceResult<PosSessionReportDto>> GetSessionReportAsync(int sessionId, CancellationToken ct = default);

        /// <summary>Gets POS profit report for a date range.</summary>
        Task<ServiceResult<PosProfitReportDto>> GetProfitReportAsync(System.DateTime fromDate, System.DateTime toDate, CancellationToken ct = default);

        /// <summary>Gets cash variance report for a date range.</summary>
        Task<ServiceResult<CashVarianceReportDto>> GetCashVarianceReportAsync(System.DateTime fromDate, System.DateTime toDate, CancellationToken ct = default);
    }
}
