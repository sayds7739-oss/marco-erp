using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceHeaderDiscountAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId",
                table: "PurchaseQuotationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotationLines_SalesQuotations_SalesQuotationId",
                table: "SalesQuotationLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cashboxes_Balance_NonNegative",
                table: "Cashboxes");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "SalesInvoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "SalesInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountAmount",
                table: "SalesInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountPercent",
                table: "SalesInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryFee",
                table: "PurchaseInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountAmount",
                table: "PurchaseInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeaderDiscountPercent",
                table: "PurchaseInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Cashboxes_AccountId",
                table: "Cashboxes",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cashboxes_Accounts_AccountId",
                table: "Cashboxes",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId",
                table: "PurchaseQuotationLines",
                column: "PurchaseQuotationId",
                principalTable: "PurchaseQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuotationLines_SalesQuotations_SalesQuotationId",
                table: "SalesQuotationLines",
                column: "SalesQuotationId",
                principalTable: "SalesQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cashboxes_Accounts_AccountId",
                table: "Cashboxes");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId",
                table: "PurchaseQuotationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotationLines_SalesQuotations_SalesQuotationId",
                table: "SalesQuotationLines");

            migrationBuilder.DropIndex(
                name: "IX_Cashboxes_AccountId",
                table: "Cashboxes");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "HeaderDiscountAmount",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "HeaderDiscountPercent",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "DeliveryFee",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "HeaderDiscountAmount",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "HeaderDiscountPercent",
                table: "PurchaseInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "SalesInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cashboxes_Balance_NonNegative",
                table: "Cashboxes",
                sql: "[Balance] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseQuotationLines_PurchaseQuotations_PurchaseQuotationId",
                table: "PurchaseQuotationLines",
                column: "PurchaseQuotationId",
                principalTable: "PurchaseQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuotationLines_SalesQuotations_SalesQuotationId",
                table: "SalesQuotationLines",
                column: "SalesQuotationId",
                principalTable: "SalesQuotations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
