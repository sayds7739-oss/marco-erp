using System;
using FluentAssertions;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Enums;
using Xunit;

namespace MarcoERP.Domain.Tests.Treasury
{
    public class CashReceiptTests
    {
        private CashReceipt CreateValidDraft()
        {
            return new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = "CR-001",
                ReceiptDate = new DateTime(2026, 2, 1),
                CashboxId = 1,
                AccountId = 1,
                Amount = 500m,
                Description = "سند قبض اختبار"
            });
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesDraft()
        {
            var cr = CreateValidDraft();
            cr.ReceiptNumber.Should().Be("CR-001");
            cr.Status.Should().Be(InvoiceStatus.Draft);
            cr.Amount.Should().Be(500m);
            cr.CashboxId.Should().Be(1);
            cr.AccountId.Should().Be(1);
        }

        [Fact]
        public void Constructor_EmptyNumber_ThrowsException()
        {
            Action act = () => new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = "",
                ReceiptDate = DateTime.Now,
                CashboxId = 1,
                AccountId = 1,
                Amount = 500m,
                Description = "وصف"
            });
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_ZeroAmount_ThrowsException()
        {
            Action act = () => new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = "CR-001",
                ReceiptDate = DateTime.Now,
                CashboxId = 1,
                AccountId = 1,
                Amount = 0m,
                Description = "وصف"
            });
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_NegativeAmount_ThrowsException()
        {
            Action act = () => new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = "CR-001",
                ReceiptDate = DateTime.Now,
                CashboxId = 1,
                AccountId = 1,
                Amount = -100m,
                Description = "وصف"
            });
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_EmptyDescription_ThrowsException()
        {
            Action act = () => new CashReceipt(new CashReceiptDraft
            {
                ReceiptNumber = "CR-001",
                ReceiptDate = DateTime.Now,
                CashboxId = 1,
                AccountId = 1,
                Amount = 500m,
                Description = ""
            });
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Post_DraftReceipt_ChangesToPosted()
        {
            var cr = CreateValidDraft();
            cr.Post(1);
            cr.Status.Should().Be(InvoiceStatus.Posted);
            cr.JournalEntryId.Should().Be(1);
        }

        [Fact]
        public void Post_AlreadyPosted_ThrowsException()
        {
            var cr = CreateValidDraft();
            cr.Post(1);
            Action act = () => cr.Post(2);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Cancel_PostedReceipt_ChangesToCancelled()
        {
            var cr = CreateValidDraft();
            cr.Post(1);
            cr.Cancel();
            cr.Status.Should().Be(InvoiceStatus.Cancelled);
        }

        [Fact]
        public void Cancel_DraftReceipt_ThrowsException()
        {
            var cr = CreateValidDraft();
            Action act = () => cr.Cancel();
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void UpdateHeader_DraftReceipt_UpdatesFields()
        {
            var cr = CreateValidDraft();
            cr.UpdateHeader(new DateTime(2026, 3, 1), 2, 2, 1000m, "وصف جديد", 5, null, "ملاحظات");
            cr.Amount.Should().Be(1000m);
            cr.CashboxId.Should().Be(2);
            cr.AccountId.Should().Be(2);
            cr.CustomerId.Should().Be(5);
        }

        [Fact]
        public void UpdateHeader_PostedReceipt_ThrowsException()
        {
            var cr = CreateValidDraft();
            cr.Post(1);
            Action act = () => cr.UpdateHeader(DateTime.Now, 2, 2, 1000m, "وصف", null, null, null);
            act.Should().Throw<Exception>();
        }
    }

    public class CashPaymentTests
    {
        private CashPayment CreateValidDraft()
        {
            return new CashPayment(new CashPaymentDraft
            {
                PaymentNumber = "CP-001",
                PaymentDate = new DateTime(2026, 2, 1),
                CashboxId = 1,
                AccountId = 1,
                Amount = 300m,
                Description = "سند صرف اختبار"
            });
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesDraft()
        {
            var cp = CreateValidDraft();
            cp.PaymentNumber.Should().Be("CP-001");
            cp.Status.Should().Be(InvoiceStatus.Draft);
            cp.Amount.Should().Be(300m);
        }

        [Fact]
        public void Constructor_ZeroAmount_ThrowsException()
        {
            Action act = () => new CashPayment(new CashPaymentDraft
            {
                PaymentNumber = "CP-001",
                PaymentDate = DateTime.Now,
                CashboxId = 1,
                AccountId = 1,
                Amount = 0m,
                Description = "وصف"
            });
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Post_DraftPayment_ChangesToPosted()
        {
            var cp = CreateValidDraft();
            cp.Post(1);
            cp.Status.Should().Be(InvoiceStatus.Posted);
        }

        [Fact]
        public void Cancel_PostedPayment_ChangesToCancelled()
        {
            var cp = CreateValidDraft();
            cp.Post(1);
            cp.Cancel();
            cp.Status.Should().Be(InvoiceStatus.Cancelled);
        }

        [Fact]
        public void UpdateHeader_DraftPayment_UpdatesFields()
        {
            var cp = CreateValidDraft();
            cp.UpdateHeader(new DateTime(2026, 3, 1), 2, 2, 800m, "وصف جديد", 3, null, "ملاحظات");
            cp.Amount.Should().Be(800m);
            cp.SupplierId.Should().Be(3);
        }
    }

    public class CashTransferTests
    {
        private CashTransfer CreateValidDraft()
        {
            return new CashTransfer("CT-001", new DateTime(2026, 2, 1), 1, 2, 5000m, "تحويل بين الصناديق");
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesDraft()
        {
            var ct = CreateValidDraft();
            ct.TransferNumber.Should().Be("CT-001");
            ct.Status.Should().Be(InvoiceStatus.Draft);
            ct.Amount.Should().Be(5000m);
            ct.SourceCashboxId.Should().Be(1);
            ct.TargetCashboxId.Should().Be(2);
        }

        [Fact]
        public void Constructor_SameCashbox_ThrowsException()
        {
            Action act = () => new CashTransfer("CT-001", DateTime.Now, 1, 1, 5000m, "تحويل");
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_ZeroAmount_ThrowsException()
        {
            Action act = () => new CashTransfer("CT-001", DateTime.Now, 1, 2, 0m, "تحويل");
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Post_DraftTransfer_ChangesToPosted()
        {
            var ct = CreateValidDraft();
            ct.Post(1);
            ct.Status.Should().Be(InvoiceStatus.Posted);
        }

        [Fact]
        public void Cancel_PostedTransfer_ChangesToCancelled()
        {
            var ct = CreateValidDraft();
            ct.Post(1);
            ct.Cancel();
            ct.Status.Should().Be(InvoiceStatus.Cancelled);
        }

        [Fact]
        public void UpdateHeader_SameCashbox_ThrowsException()
        {
            var ct = CreateValidDraft();
            Action act = () => ct.UpdateHeader(DateTime.Now, 3, 3, 1000m, "تحويل", null);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void UpdateHeader_DraftTransfer_UpdatesFields()
        {
            var ct = CreateValidDraft();
            ct.UpdateHeader(new DateTime(2026, 3, 1), 3, 4, 10000m, "تحويل كبير", "ملاحظة");
            ct.SourceCashboxId.Should().Be(3);
            ct.TargetCashboxId.Should().Be(4);
            ct.Amount.Should().Be(10000m);
        }
    }
}
