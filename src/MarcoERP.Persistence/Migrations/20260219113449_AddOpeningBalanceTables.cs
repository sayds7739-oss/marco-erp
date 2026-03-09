using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarcoERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYearId = table.Column<int>(type: "int", nullable: false),
                    BalanceDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDebit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCredit = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.Id);
                    table.CheckConstraint("CK_OpeningBalances_PostedBalanced", "[Status] <> 1 OR [TotalDebit] = [TotalCredit]");
                    table.ForeignKey(
                        name: "FK_OpeningBalances_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalances_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalanceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpeningBalanceId = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    DebitAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    WarehouseId = table.Column<int>(type: "int", nullable: true),
                    CashboxId = table.Column<int>(type: "int", nullable: true),
                    BankAccountId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalanceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Cashboxes_CashboxId",
                        column: x => x.CashboxId,
                        principalTable: "Cashboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_OpeningBalances_OpeningBalanceId",
                        column: x => x.OpeningBalanceId,
                        principalTable: "OpeningBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpeningBalanceLines_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_AccountId",
                table: "OpeningBalanceLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_BankAccountId",
                table: "OpeningBalanceLines",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_CashboxId",
                table: "OpeningBalanceLines",
                column: "CashboxId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_CustomerId",
                table: "OpeningBalanceLines",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_LineType",
                table: "OpeningBalanceLines",
                column: "LineType");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_OpeningBalanceId",
                table: "OpeningBalanceLines",
                column: "OpeningBalanceId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_ProductId",
                table: "OpeningBalanceLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_SupplierId",
                table: "OpeningBalanceLines",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalanceLines_WarehouseId",
                table: "OpeningBalanceLines",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_BalanceDate",
                table: "OpeningBalances",
                column: "BalanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_FiscalYearId",
                table: "OpeningBalances",
                column: "FiscalYearId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_JournalEntryId",
                table: "OpeningBalances",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_Status",
                table: "OpeningBalances",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningBalanceLines");

            migrationBuilder.DropTable(
                name: "OpeningBalances");
        }
    }
}
