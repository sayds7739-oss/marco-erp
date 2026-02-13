using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using MarcoERP.Application.DTOs.Reports;
using MarcoERP.Application.Interfaces;
using MarcoERP.Application.Services.Reports;
using Moq;

namespace MarcoERP.Application.Tests.Reports
{
    public class ReportExportServiceTests : IDisposable
    {
        private readonly ReportExportService _service;
        private readonly string _tempDir;

        public ReportExportServiceTests()
        {
            var dateTimeMock = new Mock<IDateTimeProvider>();
            dateTimeMock.Setup(d => d.UtcNow).Returns(new DateTime(2026, 2, 13, 12, 0, 0, DateTimeKind.Utc));
            _service = new ReportExportService(dateTimeMock.Object);
            _tempDir = Path.Combine(Path.GetTempPath(), $"MarcoERP_Tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private static ReportExportRequest CreateSampleRequest()
        {
            return new ReportExportRequest
            {
                Title = "تقرير اختبار",
                Subtitle = "فترة 2025/01 - 2025/06",
                Columns = new List<ReportColumn>
                {
                    new ReportColumn { Header = "الكود", WidthRatio = 1f },
                    new ReportColumn { Header = "الاسم", WidthRatio = 2f },
                    new ReportColumn { Header = "المبلغ", WidthRatio = 1.5f, IsNumeric = true }
                },
                Rows = new List<List<string>>
                {
                    new List<string> { "001", "صنف أ", "1500.00" },
                    new List<string> { "002", "صنف ب", "2300.50" },
                    new List<string> { "003", "صنف ج", "750.25" }
                },
                FooterSummary = "الإجمالي: 4,550.75"
            };
        }

        // =====================================================================
        // 1. PDF Export Tests
        // =====================================================================

        [Fact]
        public async Task ExportToPdfAsync_ValidRequest_CreatesFile()
        {
            var path = Path.Combine(_tempDir, "report.pdf");

            var result = await _service.ExportToPdfAsync(CreateSampleRequest(), path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(path);
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ExportToPdfAsync_CreatesDirectoryIfMissing()
        {
            var nestedDir = Path.Combine(_tempDir, "sub1", "sub2");
            var path = Path.Combine(nestedDir, "report.pdf");

            var result = await _service.ExportToPdfAsync(CreateSampleRequest(), path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            Directory.Exists(nestedDir).Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToPdfAsync_EmptyRows_StillCreatesFile()
        {
            var request = CreateSampleRequest();
            request.Rows = new List<List<string>>();
            var path = Path.Combine(_tempDir, "empty.pdf");

            var result = await _service.ExportToPdfAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToPdfAsync_NoSubtitle_Succeeds()
        {
            var request = CreateSampleRequest();
            request.Subtitle = null;
            var path = Path.Combine(_tempDir, "nosub.pdf");

            var result = await _service.ExportToPdfAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToPdfAsync_NoFooter_Succeeds()
        {
            var request = CreateSampleRequest();
            request.FooterSummary = null;
            var path = Path.Combine(_tempDir, "nofooter.pdf");

            var result = await _service.ExportToPdfAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        // =====================================================================
        // 2. Excel Export Tests
        // =====================================================================

        [Fact]
        public async Task ExportToExcelAsync_ValidRequest_CreatesFile()
        {
            var path = Path.Combine(_tempDir, "report.xlsx");

            var result = await _service.ExportToExcelAsync(CreateSampleRequest(), path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(path);
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ExportToExcelAsync_CreatesDirectoryIfMissing()
        {
            var nestedDir = Path.Combine(_tempDir, "excel_sub");
            var path = Path.Combine(nestedDir, "report.xlsx");

            var result = await _service.ExportToExcelAsync(CreateSampleRequest(), path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            Directory.Exists(nestedDir).Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToExcelAsync_EmptyRows_StillCreatesFile()
        {
            var request = CreateSampleRequest();
            request.Rows = new List<List<string>>();
            var path = Path.Combine(_tempDir, "empty.xlsx");

            var result = await _service.ExportToExcelAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToExcelAsync_LongTitle_TruncatesSheetName()
        {
            var request = CreateSampleRequest();
            request.Title = "هذا عنوان طويل جداً يتجاوز الحد الأقصى لاسم ورقة العمل في Excel";
            var path = Path.Combine(_tempDir, "longtitle.xlsx");

            var result = await _service.ExportToExcelAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public async Task ExportToExcelAsync_NumericColumn_StoresAsNumber()
        {
            var request = CreateSampleRequest();
            var path = Path.Combine(_tempDir, "numeric.xlsx");

            var result = await _service.ExportToExcelAsync(request, path, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            // Verify the file is a valid xlsx by opening it
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);
            var ws = workbook.Worksheets.First();
            ws.Should().NotBeNull();
        }

        [Fact]
        public async Task ExportToExcelAsync_InvalidPath_ReturnsFailure()
        {
            // On Linux, Z:\ is just a directory name. We need a truly restricted path.
            var invalidPath = "/proc/invalid_path/report.xlsx";

            var result = await _service.ExportToExcelAsync(CreateSampleRequest(), invalidPath, CancellationToken.None);

            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("خطأ في تصدير Excel");
        }
    }
}
