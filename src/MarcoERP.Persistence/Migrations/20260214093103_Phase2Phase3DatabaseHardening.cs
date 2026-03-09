using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Phase3DatabaseHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_ReturnNumber",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_InvoiceNumber",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_ReturnNumber",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_JournalNumber",
                table: "JournalEntries");

            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Cashboxes",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_Company_ReturnNumber",
                table: "SalesReturns",
                columns: new[] { "CompanyId", "ReturnNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesReturnLines_Quantity",
                table: "SalesReturnLines",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesReturnLines_UnitPrice",
                table: "SalesReturnLines",
                sql: "[UnitPrice] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_Company_InvoiceNumber",
                table: "SalesInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesInvoices_PaidAmount",
                table: "SalesInvoices",
                sql: "[PaidAmount] >= 0 AND [PaidAmount] <= [NetTotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesInvoiceLines_Quantity",
                table: "SalesInvoiceLines",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesInvoiceLines_UnitPrice",
                table: "SalesInvoiceLines",
                sql: "[UnitPrice] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_Company_ReturnNumber",
                table: "PurchaseReturns",
                columns: new[] { "CompanyId", "ReturnNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReturnLines_Quantity",
                table: "PurchaseReturnLines",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReturnLines_UnitPrice",
                table: "PurchaseReturnLines",
                sql: "[UnitPrice] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_Company_InvoiceNumber",
                table: "PurchaseInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoices_PaidAmount",
                table: "PurchaseInvoices",
                sql: "[PaidAmount] >= 0 AND [PaidAmount] <= [NetTotal]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLines_Quantity",
                table: "PurchaseInvoiceLines",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseInvoiceLines_UnitPrice",
                table: "PurchaseInvoiceLines",
                sql: "[UnitPrice] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_NonNegative",
                table: "JournalEntryLines",
                sql: "[DebitAmount] >= 0 AND [CreditAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalEntryLines_SingleSide",
                table: "JournalEntryLines",
                sql: "NOT ([DebitAmount] > 0 AND [CreditAmount] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_JournalNumber",
                table: "JournalEntries",
                column: "JournalNumber",
                unique: true,
                filter: "[JournalNumber] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_BaseQuantity",
                table: "InventoryMovements",
                sql: "[QuantityInBaseUnit] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_TotalCost",
                table: "InventoryMovements",
                sql: "[TotalCost] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_Company_ReturnNumber",
                table: "SalesReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesReturnLines_Quantity",
                table: "SalesReturnLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesReturnLines_UnitPrice",
                table: "SalesReturnLines");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_Company_InvoiceNumber",
                table: "SalesInvoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesInvoices_PaidAmount",
                table: "SalesInvoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesInvoiceLines_Quantity",
                table: "SalesInvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesInvoiceLines_UnitPrice",
                table: "SalesInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_Company_ReturnNumber",
                table: "PurchaseReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReturnLines_Quantity",
                table: "PurchaseReturnLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReturnLines_UnitPrice",
                table: "PurchaseReturnLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_Company_InvoiceNumber",
                table: "PurchaseInvoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoices_PaidAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLines_Quantity",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseInvoiceLines_UnitPrice",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_NonNegative",
                table: "JournalEntryLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalEntryLines_SingleSide",
                table: "JournalEntryLines");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_JournalNumber",
                table: "JournalEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_BaseQuantity",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_TotalCost",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Cashboxes");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ReturnNumber",
                table: "SalesReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_InvoiceNumber",
                table: "SalesInvoices",
                column: "InvoiceNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ReturnNumber",
                table: "PurchaseReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_InvoiceNumber",
                table: "PurchaseInvoices",
                column: "InvoiceNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_JournalNumber",
                table: "JournalEntries",
                column: "JournalNumber",
                unique: true,
                filter: "[JournalNumber] IS NOT NULL");
        }
    }
}
