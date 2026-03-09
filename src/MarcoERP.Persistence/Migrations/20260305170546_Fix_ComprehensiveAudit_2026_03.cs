using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fix_ComprehensiveAudit_2026_03 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                table: "OpeningBalanceLines");

            migrationBuilder.DropIndex(
                name: "IX_SalesQuotations_QuotationNumber",
                table: "SalesQuotations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseQuotations_QuotationNumber",
                table: "PurchaseQuotations");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_DraftCode",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashTransfers_TransferNumber",
                table: "CashTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashReceipts_ReceiptNumber",
                table: "CashReceipts");

            migrationBuilder.DropIndex(
                name: "IX_CashPayments_PaymentNumber",
                table: "CashPayments");

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "SalesReturns",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "PurchaseReturns",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_CompanyId_QuotationNumber",
                table: "SalesQuotations",
                columns: new[] { "CompanyId", "QuotationNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseQuotations_CompanyId_QuotationNumber",
                table: "PurchaseQuotations",
                columns: new[] { "CompanyId", "QuotationNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_DraftCode",
                table: "JournalEntries",
                column: "DraftCode",
                unique: true,
                filter: "[DraftCode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransfers_CompanyId_TransferNumber",
                table: "CashTransfers",
                columns: new[] { "CompanyId", "TransferNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashReceipts_CompanyId_ReceiptNumber",
                table: "CashReceipts",
                columns: new[] { "CompanyId", "ReceiptNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashPayments_CompanyId_PaymentNumber",
                table: "CashPayments",
                columns: new[] { "CompanyId", "PaymentNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                table: "OpeningBalanceLines",
                column: "OpeningBalanceId",
                principalTable: "OpeningBalances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                table: "OpeningBalanceLines");

            migrationBuilder.DropIndex(
                name: "IX_SalesQuotations_CompanyId_QuotationNumber",
                table: "SalesQuotations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseQuotations_CompanyId_QuotationNumber",
                table: "PurchaseQuotations");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_DraftCode",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashTransfers_CompanyId_TransferNumber",
                table: "CashTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashReceipts_CompanyId_ReceiptNumber",
                table: "CashReceipts");

            migrationBuilder.DropIndex(
                name: "IX_CashPayments_CompanyId_PaymentNumber",
                table: "CashPayments");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "PurchaseReturns");

            migrationBuilder.CreateIndex(
                name: "IX_SalesQuotations_QuotationNumber",
                table: "SalesQuotations",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseQuotations_QuotationNumber",
                table: "PurchaseQuotations",
                column: "QuotationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_DraftCode",
                table: "JournalEntries",
                column: "DraftCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransfers_TransferNumber",
                table: "CashTransfers",
                column: "TransferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashReceipts_ReceiptNumber",
                table: "CashReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashPayments_PaymentNumber",
                table: "CashPayments",
                column: "PaymentNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                table: "OpeningBalanceLines",
                column: "OpeningBalanceId",
                principalTable: "OpeningBalances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
