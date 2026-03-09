using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MarcoERP.Domain.Entities.Accounting;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Purchases;
using MarcoERP.Domain.Entities.Settings;
using MarcoERP.Persistence;
using MarcoERP.Persistence.Interceptors;
using Xunit;

namespace MarcoERP.Integration.Tests.Interceptors
{
    /// <summary>
    /// Integration tests for HardDeleteProtectionInterceptor.
    /// Verifies that both SoftDeletableEntity descendants and IImmutableFinancialRecord
    /// entities cannot be hard-deleted through EF Core.
    /// </summary>
    public sealed class HardDeleteProtectionInterceptorTests
    {
        private static MarcoDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<MarcoDbContext>()
                .UseInMemoryDatabase(dbName)
                .AddInterceptors(new HardDeleteProtectionInterceptor())
                .Options;

            return new MarcoDbContext(options);
        }

        private static async Task SeedProductionModeDisabled(MarcoDbContext context)
        {
            var setting = new SystemSetting("IsProductionMode", "false", "معطل لأغراض الاختبار", "General", "Boolean");
            context.SystemSettings.Add(setting);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Remove_ImmutableFinancialRecord_ThrowsInvalidOperationException()
        {
            // Arrange
            var dbName = $"HardDeleteTest_{Guid.NewGuid():N}";
            using var context = CreateContext(dbName);

            // Disable production mode so we test the immutable record guard
            await SeedProductionModeDisabled(context);

            var line = JournalEntryLine.Create(
                accountId: 1,
                lineNumber: 1,
                debitAmount: 100m,
                creditAmount: 0m,
                description: "Test debit line",
                createdAt: DateTime.UtcNow);

            context.JournalEntryLines.Add(line);
            await context.SaveChangesAsync();

            // Act
            context.JournalEntryLines.Remove(line);
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.Which.Message.Should().Contain("حذف السجلات المالية الثابتة ممنوع");
            ex.Which.Message.Should().Contain("JournalEntryLine");
        }

        [Fact]
        public async Task Remove_ImmutableFinancialRecord_Sync_ThrowsInvalidOperationException()
        {
            // Arrange
            var dbName = $"HardDeleteSyncTest_{Guid.NewGuid():N}";
            using var context = CreateContext(dbName);

            // Disable production mode so we test the immutable record guard
            await SeedProductionModeDisabled(context);

            var line = JournalEntryLine.Create(
                accountId: 1,
                lineNumber: 1,
                debitAmount: 0m,
                creditAmount: 50m,
                description: "Test credit line",
                createdAt: DateTime.UtcNow);

            context.JournalEntryLines.Add(line);
            context.SaveChanges();

            // Act
            context.JournalEntryLines.Remove(line);
            Action act = () => context.SaveChanges();

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*حذف السجلات المالية الثابتة ممنوع*")
                .WithMessage("*JournalEntryLine*");
        }

        [Fact]
        public async Task ReplaceLines_RemovedPurchaseInvoiceLine_DoesNotThrowInProductionMode()
        {
            // Arrange
            var dbName = $"HardDeletePurchaseLine_{Guid.NewGuid():N}";

            await using (var seedContext = CreateContext(dbName))
            {
                var invoice = new PurchaseInvoice("PI-TEST-001", DateTime.UtcNow.Date, 1, 1, "test");
                invoice.CreatedAt = DateTime.UtcNow;
                invoice.CreatedBy = "test";
                invoice.AddLine(1, 1, 2m, 10m, 1m, 0m, 14m);
                invoice.AddLine(1, 1, 3m, 12m, 1m, 0m, 14m);

                seedContext.PurchaseInvoices.Add(invoice);
                await seedContext.SaveChangesAsync();
            }

            await using var context = CreateContext(dbName);
            var tracked = await context.PurchaseInvoices
                .Include(x => x.Lines)
                .FirstAsync();

            var keep = tracked.Lines.OrderBy(x => x.Id).First();
            var payload = new List<PurchaseInvoiceLine>
            {
                new PurchaseInvoiceLine(
                    keep.ProductId,
                    keep.UnitId,
                    keep.Quantity,
                    keep.UnitPrice,
                    keep.ConversionFactor,
                    keep.DiscountPercent,
                    keep.VatRate,
                    keep.Id)
            };

            // Act
            tracked.ReplaceLines(payload);
            Func<Task> act = () => context.SaveChangesAsync();

            // Assert
            await act.Should().NotThrowAsync();
            tracked.Lines.Should().HaveCount(1);
        }
    }
}
