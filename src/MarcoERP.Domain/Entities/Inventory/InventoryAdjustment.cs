using System;
using System.Collections.Generic;
using System.Linq;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Enums;
using MarcoERP.Domain.Exceptions.Inventory;

namespace MarcoERP.Domain.Entities.Inventory
{
    /// <summary>
    /// Represents an inventory adjustment document (تسوية مخزنية).
    /// Lifecycle: Draft → Posted → (optionally) Cancelled.
    /// On posting: creates inventory movements and auto-generates journal entry
    /// for the cost difference (adjustment account).
    /// </summary>
    public sealed class InventoryAdjustment : CompanyAwareEntity
    {
        private readonly List<InventoryAdjustmentLine> _lines = new();

        // ── Constructors ────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private InventoryAdjustment() { }

        /// <summary>
        /// Creates a new inventory adjustment in Draft status.
        /// </summary>
        public InventoryAdjustment(
            string adjustmentNumber,
            DateTime adjustmentDate,
            int warehouseId,
            string reason,
            string notes = null)
        {
            if (string.IsNullOrWhiteSpace(adjustmentNumber))
                throw new InventoryDomainException("رقم التسوية مطلوب.");

            if (warehouseId <= 0)
                throw new InventoryDomainException("المخزن مطلوب.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InventoryDomainException("سبب التسوية مطلوب.");

            AdjustmentNumber = adjustmentNumber.Trim();
            AdjustmentDate = adjustmentDate;
            WarehouseId = warehouseId;
            Reason = reason.Trim();
            Notes = notes?.Trim();
            Status = InvoiceStatus.Draft;
            TotalCostDifference = 0;
        }

        // ── Properties ──────────────────────────────────────────

        /// <summary>Unique auto-generated adjustment number (ADJ-YYYYMM-####).</summary>
        public string AdjustmentNumber { get; private set; }

        /// <summary>Adjustment date.</summary>
        public DateTime AdjustmentDate { get; private set; }

        /// <summary>FK to Warehouse being adjusted.</summary>
        public int WarehouseId { get; private set; }

        /// <summary>Navigation property.</summary>
        public Warehouse Warehouse { get; private set; }

        /// <summary>Reason for adjustment (e.g., جرد فعلي، تالف، سرقة).</summary>
        public string Reason { get; private set; }

        /// <summary>Optional notes.</summary>
        public string Notes { get; private set; }

        /// <summary>Current lifecycle status.</summary>
        public InvoiceStatus Status { get; private set; }

        /// <summary>Total cost difference (positive = surplus, negative = shortage).</summary>
        public decimal TotalCostDifference { get; private set; }

        /// <summary>FK to auto-generated journal entry (set on posting).</summary>
        public int? JournalEntryId { get; private set; }

        /// <summary>Adjustment line items.</summary>
        public IReadOnlyCollection<InventoryAdjustmentLine> Lines => _lines.AsReadOnly();

        // ── Domain Methods ──────────────────────────────────────

        /// <summary>Adds a line item to the adjustment.</summary>
        public InventoryAdjustmentLine AddLine(
            int productId,
            int unitId,
            decimal systemQuantity,
            decimal actualQuantity,
            decimal conversionFactor,
            decimal unitCost)
        {
            EnsureDraft("لا يمكن إضافة بنود لتسوية مرحّلة أو ملغاة.");

            if (productId <= 0)
                throw new InventoryDomainException("الصنف مطلوب.");

            if (unitId <= 0)
                throw new InventoryDomainException("الوحدة مطلوبة.");

            if (conversionFactor <= 0)
                throw new InventoryDomainException("معامل التحويل يجب أن يكون أكبر من صفر.");

            if (actualQuantity < 0)
                throw new InventoryDomainException("الكمية الفعلية لا يمكن أن تكون سالبة.");

            // Check for duplicate product in lines
            if (_lines.Any(l => l.ProductId == productId))
                throw new InventoryDomainException("الصنف مضاف مسبقاً في هذه التسوية.");

            var line = new InventoryAdjustmentLine(
                productId, unitId, systemQuantity, actualQuantity, conversionFactor, unitCost);

            _lines.Add(line);
            RecalculateTotalCostDifference();
            return line;
        }

        /// <summary>Removes a line item.</summary>
        public void RemoveLine(InventoryAdjustmentLine line)
        {
            EnsureDraft("لا يمكن حذف بنود من تسوية مرحّلة أو ملغاة.");

            if (!_lines.Remove(line))
                throw new InventoryDomainException("البند غير موجود في التسوية.");

            RecalculateTotalCostDifference();
        }

        /// <summary>Posts the adjustment with the generated journal entry ID.</summary>
        public void Post(int journalEntryId)
        {
            EnsureDraft("لا يمكن ترحيل تسوية مرحّلة بالفعل أو ملغاة.");

            if (!_lines.Any())
                throw new InventoryDomainException("لا يمكن ترحيل تسوية بدون بنود.");

            if (journalEntryId <= 0)
                throw new InventoryDomainException("معرف القيد المحاسبي غير صالح.");

            Status = InvoiceStatus.Posted;
            JournalEntryId = journalEntryId;
        }

        /// <summary>Cancels a posted adjustment.</summary>
        public void Cancel()
        {
            if (Status != InvoiceStatus.Posted)
                throw new InventoryDomainException("لا يمكن إلغاء إلا التسويات المرحّلة.");

            Status = InvoiceStatus.Cancelled;
        }

        /// <summary>Replaces all lines at once (used during draft editing).</summary>
        public void ReplaceLines(IEnumerable<InventoryAdjustmentLine> newLines)
        {
            EnsureDraft("لا يمكن تعديل بنود تسوية مرحّلة أو ملغاة.");

            var incomingLines = (newLines ?? Enumerable.Empty<InventoryAdjustmentLine>()).ToList();
            var existingById = _lines
                .Where(l => l.Id > 0)
                .ToDictionary(l => l.Id);

            var incomingIds = new HashSet<int>();
            var newIncomingLines = new List<InventoryAdjustmentLine>();

            foreach (var incoming in incomingLines)
            {
                if (incoming.Id > 0)
                {
                    if (!incomingIds.Add(incoming.Id))
                        throw new InventoryDomainException("تكرار معرف بند التسوية غير مسموح.");

                    if (!existingById.TryGetValue(incoming.Id, out var existingLine))
                        throw new InventoryDomainException("لا يمكن تحديث بند غير موجود في التسوية.");

                    existingLine.UpdateDetails(
                        incoming.ProductId,
                        incoming.UnitId,
                        incoming.SystemQuantity,
                        incoming.ActualQuantity,
                        incoming.ConversionFactor,
                        incoming.UnitCost);
                }
                else
                {
                    newIncomingLines.Add(incoming);
                }
            }

            var linesToRemove = existingById.Values
                .Where(l => !incomingIds.Contains(l.Id))
                .ToList();

            foreach (var line in linesToRemove)
                _lines.Remove(line);

            _lines.RemoveAll(l => l.Id == 0);
            _lines.AddRange(newIncomingLines);

            RecalculateTotalCostDifference();
        }

        /// <summary>Soft delete — only drafts.</summary>
        public override void SoftDelete(string deletedBy, DateTime deletedAt)
        {
            if (Status != InvoiceStatus.Draft)
                throw new InventoryDomainException("لا يمكن حذف تسوية مرحّلة أو ملغاة.");

            base.SoftDelete(deletedBy, deletedAt);
        }

        // ── Private Helpers ─────────────────────────────────────

        private void EnsureDraft(string errorMessage)
        {
            if (Status != InvoiceStatus.Draft)
                throw new InventoryDomainException(errorMessage);
        }

        private void RecalculateTotalCostDifference()
        {
            TotalCostDifference = _lines.Sum(l => l.CostDifference);
        }
    }
}
