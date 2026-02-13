using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterpartyAndSalesRepToInvoicesReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "SalesReturns",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyType",
                table: "SalesReturns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "SalesReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "SalesReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SupplierId",
                table: "PurchaseReturns",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyCustomerId",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CounterpartyType",
                table: "PurchaseReturns",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "PurchaseReturns",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SupplierId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "SalesRepresentativeId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_SalesRepresentativeId",
                table: "SalesReturns",
                column: "SalesRepresentativeId",
                filter: "[SalesRepresentativeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_SupplierId",
                table: "SalesReturns",
                column: "SupplierId",
                filter: "[SupplierId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_CounterpartyCustomerId",
                table: "PurchaseReturns",
                column: "CounterpartyCustomerId",
                filter: "[CounterpartyCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SalesRepresentativeId",
                table: "PurchaseReturns",
                column: "SalesRepresentativeId",
                filter: "[SalesRepresentativeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns",
                column: "SupplierId",
                filter: "[SupplierId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SalesRepresentativeId",
                table: "PurchaseInvoices",
                column: "SalesRepresentativeId",
                filter: "[SalesRepresentativeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId",
                filter: "[SupplierId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_SalesRepresentatives_SalesRepresentativeId",
                table: "PurchaseInvoices",
                column: "SalesRepresentativeId",
                principalTable: "SalesRepresentatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Customers_CounterpartyCustomerId",
                table: "PurchaseReturns",
                column: "CounterpartyCustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_SalesRepresentatives_SalesRepresentativeId",
                table: "PurchaseReturns",
                column: "SalesRepresentativeId",
                principalTable: "SalesRepresentatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_SalesRepresentatives_SalesRepresentativeId",
                table: "SalesReturns",
                column: "SalesRepresentativeId",
                principalTable: "SalesRepresentatives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Suppliers_SupplierId",
                table: "SalesReturns",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_SalesRepresentatives_SalesRepresentativeId",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Customers_CounterpartyCustomerId",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_SalesRepresentatives_SalesRepresentativeId",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_SalesRepresentatives_SalesRepresentativeId",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Suppliers_SupplierId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_SalesRepresentativeId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_SupplierId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_CounterpartyCustomerId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_SalesRepresentativeId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_SalesRepresentativeId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "CounterpartyType",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "CounterpartyCustomerId",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "CounterpartyType",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "SalesRepresentativeId",
                table: "PurchaseInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "SalesReturns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SupplierId",
                table: "PurchaseReturns",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SupplierId",
                table: "PurchaseInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_SupplierId",
                table: "PurchaseReturns",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId");
        }
    }
}
