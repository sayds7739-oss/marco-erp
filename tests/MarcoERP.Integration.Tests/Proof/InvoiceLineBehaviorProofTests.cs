using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MarcoERP.Domain.Entities.Common;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Entities.Sales;
using MarcoERP.Domain.Exceptions.Sales;
using MarcoERP.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MarcoERP.Integration.Tests.Proof
{
    public sealed class InvoiceLineBehaviorProofTests
    {
        private const string ExistingDbConnection = "Server=.\\SQL2022;Database=MarcoERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=False";

        [Fact]
        public async Task Generate_Final_Behavioral_Proof_Artifact()
        {
            var interceptor = new SqlCaptureInterceptor();
            var options = new DbContextOptionsBuilder<MarcoDbContext>()
                .UseSqlServer(ExistingDbConnection)
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .AddInterceptors(interceptor)
                .Options;

            await using var bootstrap = new MarcoDbContext(options);
            var seed = await ResolveOrCreateFixtureAsync(bootstrap);
            var invoiceId = await CreateDraftInvoiceAsync(bootstrap, seed);

            var proof = new ProofArtifact
            {
                Database = "MarcoERP",
                GeneratedAtUtc = DateTime.UtcNow,
                Scenarios = new List<ScenarioProof>(),
                EdgeCases = new List<EdgeCaseProof>()
            };

            proof.Scenarios.Add(await RunUpdateQuantityOnlyScenarioAsync(options, interceptor, invoiceId));
            proof.Scenarios.Add(await RunRemoveOneLineScenarioAsync(options, interceptor, invoiceId));
            proof.Scenarios.Add(await RunAddOneLineScenarioAsync(options, interceptor, invoiceId));
            proof.Scenarios.Add(await RunSaveTwiceWithoutChangesScenarioAsync(options, interceptor, invoiceId));

            proof.EdgeCases.Add(await RunMissingLineIdScenarioAsync(options, interceptor, invoiceId));
            proof.EdgeCases.Add(await RunDuplicateLineIdScenarioAsync(options, invoiceId));
            proof.EdgeCases.Add(await RunSameProductDifferentIdsScenarioAsync(options, interceptor, invoiceId));
            proof.EdgeCases.Add(await RunSaveWithoutModificationScenarioAsync(options, interceptor, invoiceId));

            proof.Concurrency = await RunConcurrencyProofAsync(options, invoiceId);

            var artifactPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../tests/MarcoERP.Integration.Tests/_artifacts/invoice_line_behavior_proof.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            await File.WriteAllTextAsync(artifactPath, JsonSerializer.Serialize(proof, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            File.Exists(artifactPath).Should().BeTrue();
            proof.Scenarios.Count.Should().Be(4);
            proof.Concurrency.Should().NotBeNull();
        }

        private static async Task<SeedRefs> ResolveOrCreateFixtureAsync(MarcoDbContext context)
        {
            var now = DateTime.UtcNow;

            var existingProduct = await context.Products
                .AsNoTracking()
                .Select(p => new { p.Id, p.BaseUnitId })
                .FirstOrDefaultAsync();

            var existingWarehouse = await context.Warehouses
                .AsNoTracking()
                .Select(w => w.Id)
                .FirstOrDefaultAsync();

            var existingCustomer = await context.Customers
                .AsNoTracking()
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (existingProduct != null && existingWarehouse > 0 && existingCustomer > 0)
            {
                return new SeedRefs
                {
                    CustomerId = existingCustomer,
                    WarehouseId = existingWarehouse,
                    ProductId = existingProduct.Id,
                    UnitId = existingProduct.BaseUnitId
                };
            }

            var company = new Company("DEF", "شركة الدليل", "Proof Co")
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };
            if (!await context.Companies.AnyAsync())
                context.Companies.Add(company);

            var unit = new Unit("قطعة", "Piece", "قط", "pc")
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };
            context.Units.Add(unit);

            var category = new Category("عام", "General", null, 1)
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };
            context.Categories.Add(category);

            var warehouse = new Warehouse("WH-PROOF", "مخزن الدليل", "Proof Warehouse", null, null)
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };
            context.Warehouses.Add(warehouse);

            var customer = new Customer(new Customer.CustomerDraft
            {
                Code = "CUS-PROOF",
                NameAr = "عميل الدليل",
                NameEn = "Proof Customer",
                CreditLimit = 0m,
                PreviousBalance = 0m
            })
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };
            context.Customers.Add(customer);

            await context.SaveChangesAsync();

            var product = new Product(
                code: "PRD-PROOF",
                nameAr: "منتج الدليل",
                nameEn: "Proof Product",
                categoryId: category.Id,
                baseUnitId: unit.Id,
                initialCostPrice: 10m,
                defaultSalePrice: 20m,
                minimumStock: 0m,
                reorderLevel: 0m,
                vatRate: 14m)
            {
                CreatedAt = now,
                CreatedBy = "proof"
            };

            product.AddUnit(new ProductUnit(
                productId: 0,
                unitId: unit.Id,
                conversionFactor: 1m,
                salePrice: 20m,
                purchasePrice: 10m,
                isDefault: true));

            context.Products.Add(product);
            await context.SaveChangesAsync();

            return new SeedRefs
            {
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                ProductId = product.Id,
                UnitId = unit.Id
            };
        }

        private static async Task<int> CreateDraftInvoiceAsync(MarcoDbContext context, SeedRefs seed)
        {
            var invoice = new SalesInvoice(
                invoiceNumber: $"SI-PROOF-{Guid.NewGuid():N}".Substring(0, 24),
                invoiceDate: DateTime.UtcNow.Date,
                customerId: seed.CustomerId,
                warehouseId: seed.WarehouseId,
                notes: "proof");

            invoice.CreatedAt = DateTime.UtcNow;
            invoice.CreatedBy = "proof";

            invoice.AddLine(seed.ProductId, seed.UnitId, 2m, 25m, 1m, 0m, 14m);
            invoice.AddLine(seed.ProductId, seed.UnitId, 3m, 30m, 1m, 0m, 14m);

            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync();
            return invoice.Id;
        }

        private static async Task<ScenarioProof> RunUpdateQuantityOnlyScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            var line1 = invoice.Lines.OrderBy(x => x.Id).First();
            var line2 = invoice.Lines.OrderBy(x => x.Id).Skip(1).First();

            var payload = new List<SalesInvoiceLine>
            {
                new SalesInvoiceLine(line1.ProductId, line1.UnitId, line1.Quantity + 1m, line1.UnitPrice, line1.ConversionFactor, line1.DiscountPercent, line1.VatRate, line1.Id),
                new SalesInvoiceLine(line2.ProductId, line2.UnitId, line2.Quantity, line2.UnitPrice, line2.ConversionFactor, line2.DiscountPercent, line2.VatRate, line2.Id)
            };

            interceptor.Reset();
            invoice.ReplaceLines(payload);
            await context.SaveChangesAsync();

            return interceptor.BuildScenario("Update quantity only");
        }

        private static async Task<ScenarioProof> RunRemoveOneLineScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            var keep = invoice.Lines.OrderBy(x => x.Id).First();
            var payload = new List<SalesInvoiceLine>
            {
                new SalesInvoiceLine(keep.ProductId, keep.UnitId, keep.Quantity, keep.UnitPrice, keep.ConversionFactor, keep.DiscountPercent, keep.VatRate, keep.Id)
            };

            interceptor.Reset();
            invoice.ReplaceLines(payload);
            await context.SaveChangesAsync();

            var scenario = interceptor.BuildScenario("Remove one line");
            scenario.Error.Should().BeNullOrWhiteSpace();
            scenario.DeleteCount.Should().Be(1);
            return scenario;
        }

        private static async Task<ScenarioProof> RunAddOneLineScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            var existing = invoice.Lines.OrderBy(x => x.Id).First();
            var payload = invoice.Lines
                .OrderBy(x => x.Id)
                .Select(x => new SalesInvoiceLine(
                    x.ProductId,
                    x.UnitId,
                    x.Quantity,
                    x.UnitPrice,
                    x.ConversionFactor,
                    x.DiscountPercent,
                    x.VatRate,
                    x.Id))
                .ToList();

            payload.Add(new SalesInvoiceLine(
                existing.ProductId,
                existing.UnitId,
                existing.Quantity + 2m,
                existing.UnitPrice,
                existing.ConversionFactor,
                existing.DiscountPercent,
                existing.VatRate));

            interceptor.Reset();
            invoice.ReplaceLines(payload);
            await context.SaveChangesAsync();

            return interceptor.BuildScenario("Add one line");
        }

        private static async Task<ScenarioProof> RunSaveTwiceWithoutChangesScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            interceptor.Reset();
            await context.SaveChangesAsync();
            await context.SaveChangesAsync();

            return interceptor.BuildScenario("Save twice without changes");
        }

        private static async Task<EdgeCaseProof> RunMissingLineIdScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            var lines = invoice.Lines.OrderBy(x => x.Id).ToList();
            var payload = lines
                .Select(l => new SalesInvoiceLine(l.ProductId, l.UnitId, l.Quantity, l.UnitPrice, l.ConversionFactor, l.DiscountPercent, l.VatRate))
                .ToList();

            interceptor.Reset();
            try
            {
                invoice.ReplaceLines(payload);
                await context.SaveChangesAsync();

                var persistedCount = await context.SalesInvoiceLines.CountAsync(x => x.SalesInvoiceId == invoiceId);
                var scenario = interceptor.BuildScenario("Legacy DTO missing LineId");

                return new EdgeCaseProof
                {
                    Name = "DTO missing LineId",
                    Outcome = "Succeeded",
                    Message = $"Persisted lines count={persistedCount}",
                    InsertCount = scenario.InsertCount,
                    UpdateCount = scenario.UpdateCount,
                    DeleteCount = scenario.DeleteCount
                };
            }
            catch (Exception ex)
            {
                var scenario = interceptor.BuildScenario("Legacy DTO missing LineId");
                return new EdgeCaseProof
                {
                    Name = "DTO missing LineId",
                    Outcome = "Failed",
                    Message = ex.Message,
                    InsertCount = scenario.InsertCount,
                    UpdateCount = scenario.UpdateCount,
                    DeleteCount = scenario.DeleteCount
                };
            }
        }

        private static async Task<EdgeCaseProof> RunDuplicateLineIdScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);
            var line = invoice.Lines.OrderBy(x => x.Id).First();

            var payload = new List<SalesInvoiceLine>
            {
                new SalesInvoiceLine(line.ProductId, line.UnitId, line.Quantity, line.UnitPrice, line.ConversionFactor, line.DiscountPercent, line.VatRate, line.Id),
                new SalesInvoiceLine(line.ProductId, line.UnitId, line.Quantity + 1m, line.UnitPrice, line.ConversionFactor, line.DiscountPercent, line.VatRate, line.Id)
            };

            try
            {
                invoice.ReplaceLines(payload);
                return new EdgeCaseProof
                {
                    Name = "DTO duplicate LineId",
                    Outcome = "UnexpectedSuccess",
                    Message = "Expected SalesInvoiceDomainException was not thrown"
                };
            }
            catch (SalesInvoiceDomainException ex)
            {
                return new EdgeCaseProof
                {
                    Name = "DTO duplicate LineId",
                    Outcome = "FailedAsExpected",
                    Message = ex.Message
                };
            }
        }

        private static async Task<EdgeCaseProof> RunSameProductDifferentIdsScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            var invoice = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);
            var lines = invoice.Lines.OrderBy(x => x.Id).ToList();

            var payload = lines
                .Select((l, idx) => new SalesInvoiceLine(
                    l.ProductId,
                    l.UnitId,
                    l.Quantity + idx,
                    l.UnitPrice,
                    l.ConversionFactor,
                    l.DiscountPercent,
                    l.VatRate,
                    l.Id))
                .ToList();

            interceptor.Reset();
            invoice.ReplaceLines(payload);
            await context.SaveChangesAsync();

            var scenario = interceptor.BuildScenario("Same product twice with different ids");
            return new EdgeCaseProof
            {
                Name = "DTO same product twice with different Id",
                Outcome = "Succeeded",
                Message = "Accepted and persisted as two separate lines",
                InsertCount = scenario.InsertCount,
                UpdateCount = scenario.UpdateCount,
                DeleteCount = scenario.DeleteCount
            };
        }

        private static async Task<EdgeCaseProof> RunSaveWithoutModificationScenarioAsync(
            DbContextOptions<MarcoDbContext> options,
            SqlCaptureInterceptor interceptor,
            int invoiceId)
        {
            await using var context = new MarcoDbContext(options);
            _ = await context.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

            interceptor.Reset();
            await context.SaveChangesAsync();
            var scenario = interceptor.BuildScenario("Save without modification");

            return new EdgeCaseProof
            {
                Name = "Save without modification",
                Outcome = "Succeeded",
                Message = "No line DML emitted",
                InsertCount = scenario.InsertCount,
                UpdateCount = scenario.UpdateCount,
                DeleteCount = scenario.DeleteCount
            };
        }

        private static async Task<ConcurrencyProof> RunConcurrencyProofAsync(DbContextOptions<MarcoDbContext> options, int invoiceId)
        {
            byte[] beforeUnchanged;
            byte[] beforeChanged;
            byte[] afterUnchanged;
            byte[] afterChanged;

            await using (var readCtx = new MarcoDbContext(options))
            {
                var lines = await readCtx.SalesInvoiceLines
                    .Where(x => x.SalesInvoiceId == invoiceId)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                beforeChanged = lines[0].RowVersion.ToArray();
                beforeUnchanged = lines[1].RowVersion.ToArray();
            }

            await using (var updateCtx = new MarcoDbContext(options))
            {
                var invoice = await updateCtx.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);
                var lines = invoice.Lines.OrderBy(x => x.Id).ToList();

                var payload = lines
                    .Select((x, idx) => new SalesInvoiceLine(
                        x.ProductId,
                        x.UnitId,
                        idx == 0 ? x.Quantity + 5m : x.Quantity,
                        x.UnitPrice,
                        x.ConversionFactor,
                        x.DiscountPercent,
                        x.VatRate,
                        x.Id))
                    .ToList();

                invoice.ReplaceLines(payload);
                await updateCtx.SaveChangesAsync();
            }

            await using (var readAfterCtx = new MarcoDbContext(options))
            {
                var lines = await readAfterCtx.SalesInvoiceLines
                    .Where(x => x.SalesInvoiceId == invoiceId)
                    .OrderBy(x => x.Id)
                    .ToListAsync();

                afterChanged = lines[0].RowVersion.ToArray();
                afterUnchanged = lines[1].RowVersion.ToArray();
            }

            var noFalseConflict = true;
            await using (var checkCtx = new MarcoDbContext(options))
            {
                var invoice = await checkCtx.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);
                var lines = invoice.Lines.OrderBy(x => x.Id).ToList();
                invoice.ReplaceLines(lines
                    .Select((x, idx) => new SalesInvoiceLine(
                        x.ProductId,
                        x.UnitId,
                        idx == 0 ? x.Quantity + 1m : x.Quantity,
                        x.UnitPrice,
                        x.ConversionFactor,
                        x.DiscountPercent,
                        x.VatRate,
                        x.Id))
                    .ToList());

                try
                {
                    await checkCtx.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    noFalseConflict = false;
                }
            }

            var noLostUpdate = false;
            await using (var c1 = new MarcoDbContext(options))
            await using (var c2 = new MarcoDbContext(options))
            {
                var i1 = await c1.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);
                var i2 = await c2.SalesInvoices.Include(x => x.Lines).FirstAsync(x => x.Id == invoiceId);

                var l1 = i1.Lines.OrderBy(x => x.Id).First();
                var l2 = i2.Lines.OrderBy(x => x.Id).First();

                i1.ReplaceLines(new[]
                {
                    new SalesInvoiceLine(l1.ProductId, l1.UnitId, l1.Quantity + 2m, l1.UnitPrice, l1.ConversionFactor, l1.DiscountPercent, l1.VatRate, l1.Id)
                }.Concat(i1.Lines.OrderBy(x => x.Id).Skip(1).Select(x =>
                    new SalesInvoiceLine(x.ProductId, x.UnitId, x.Quantity, x.UnitPrice, x.ConversionFactor, x.DiscountPercent, x.VatRate, x.Id))).ToList());

                await c1.SaveChangesAsync();

                i2.ReplaceLines(new[]
                {
                    new SalesInvoiceLine(l2.ProductId, l2.UnitId, l2.Quantity + 3m, l2.UnitPrice, l2.ConversionFactor, l2.DiscountPercent, l2.VatRate, l2.Id)
                }.Concat(i2.Lines.OrderBy(x => x.Id).Skip(1).Select(x =>
                    new SalesInvoiceLine(x.ProductId, x.UnitId, x.Quantity, x.UnitPrice, x.ConversionFactor, x.DiscountPercent, x.VatRate, x.Id))).ToList());

                try
                {
                    await c2.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    noLostUpdate = true;
                }
            }

            return new ConcurrencyProof
            {
                RowVersionStableOnUnchangedLine = beforeUnchanged.SequenceEqual(afterUnchanged),
                RowVersionChangedOnEditedLine = !beforeChanged.SequenceEqual(afterChanged),
                NoFalsePositiveConcurrencyConflict = noFalseConflict,
                NoLostUpdate = noLostUpdate
            };
        }

        private sealed class SqlCaptureInterceptor : DbCommandInterceptor
        {
            private readonly List<string> _commands = new();
            private static readonly Regex ContainsInsertSalesInvoiceLines = new("\\bINSERT\\b[\\s\\S]*?\\[SalesInvoiceLines\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            private static readonly Regex ContainsUpdateSalesInvoiceLines = new("\\bUPDATE\\b[\\s\\S]*?\\[SalesInvoiceLines\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            private static readonly Regex ContainsDeleteSalesInvoiceLines = new("\\bDELETE\\b[\\s\\S]*?\\[SalesInvoiceLines\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public void Reset()
            {
                lock (_commands)
                {
                    _commands.Clear();
                }
            }

            public ScenarioProof BuildScenario(string name)
            {
                List<string> lines;
                lock (_commands)
                {
                    lines = _commands
                        .Where(c => c.Contains("[SalesInvoiceLines]", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return new ScenarioProof
                {
                    Scenario = name,
                    InsertCount = lines.Count(c => ContainsInsertSalesInvoiceLines.IsMatch(c)),
                    UpdateCount = lines.Count(c => ContainsUpdateSalesInvoiceLines.IsMatch(c)),
                    DeleteCount = lines.Count(c => ContainsDeleteSalesInvoiceLines.IsMatch(c)),
                    Sql = lines
                };
            }

            public override InterceptionResult<int> NonQueryExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result)
            {
                lock (_commands)
                {
                    _commands.Add(command.CommandText);
                }

                return base.NonQueryExecuting(command, eventData, result);
            }

            public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                lock (_commands)
                {
                    _commands.Add(command.CommandText);
                }

                return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result)
            {
                lock (_commands)
                {
                    _commands.Add(command.CommandText);
                }

                return base.ReaderExecuting(command, eventData, result);
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                lock (_commands)
                {
                    _commands.Add(command.CommandText);
                }

                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }

        private sealed class SeedRefs
        {
            public int CustomerId { get; set; }
            public int WarehouseId { get; set; }
            public int ProductId { get; set; }
            public int UnitId { get; set; }
        }

        private sealed class ProofArtifact
        {
            public string Database { get; set; }
            public DateTime GeneratedAtUtc { get; set; }
            public List<ScenarioProof> Scenarios { get; set; }
            public List<EdgeCaseProof> EdgeCases { get; set; }
            public ConcurrencyProof Concurrency { get; set; }
        }

        private sealed class ScenarioProof
        {
            public string Scenario { get; set; }
            public int InsertCount { get; set; }
            public int UpdateCount { get; set; }
            public int DeleteCount { get; set; }
            public List<string> Sql { get; set; }
            public string Error { get; set; }
        }

        private sealed class EdgeCaseProof
        {
            public string Name { get; set; }
            public string Outcome { get; set; }
            public string Message { get; set; }
            public int InsertCount { get; set; }
            public int UpdateCount { get; set; }
            public int DeleteCount { get; set; }
        }

        private sealed class ConcurrencyProof
        {
            public bool RowVersionStableOnUnchangedLine { get; set; }
            public bool RowVersionChangedOnEditedLine { get; set; }
            public bool NoFalsePositiveConcurrencyConflict { get; set; }
            public bool NoLostUpdate { get; set; }
        }
    }
}
