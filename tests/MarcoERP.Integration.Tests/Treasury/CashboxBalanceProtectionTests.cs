using System;
using FluentAssertions;
using MarcoERP.Domain.Entities.Treasury;
using MarcoERP.Domain.Exceptions.Treasury;
using Xunit;

namespace MarcoERP.Integration.Tests.Treasury
{
    /// <summary>
    /// Tests for Cashbox domain-level negative balance protection.
    /// Verifies that no treasury operation can produce a negative cashbox balance.
    /// </summary>
    public sealed class CashboxBalanceProtectionTests
    {
        private static Cashbox CreateCashbox(decimal initialDeposit = 0m)
        {
            var cashbox = new Cashbox("CBX-0001", "الصندوق الرئيسي", "Main Cashbox", accountId: 1);
            if (initialDeposit > 0)
                cashbox.IncreaseBalance(initialDeposit);
            return cashbox;
        }

        // ── IncreaseBalance Tests ───────────────────────────────

        [Fact]
        public void IncreaseBalance_ValidAmount_IncreasesBalance()
        {
            var cashbox = CreateCashbox();

            cashbox.IncreaseBalance(1000m);

            cashbox.Balance.Should().Be(1000m);
        }

        [Fact]
        public void IncreaseBalance_MultipleDeposits_AccumulatesCorrectly()
        {
            var cashbox = CreateCashbox();

            cashbox.IncreaseBalance(500m);
            cashbox.IncreaseBalance(300m);
            cashbox.IncreaseBalance(200m);

            cashbox.Balance.Should().Be(1000m);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void IncreaseBalance_NonPositiveAmount_Throws(decimal amount)
        {
            var cashbox = CreateCashbox();

            Action act = () => cashbox.IncreaseBalance(amount);

            act.Should().Throw<TreasuryDomainException>()
                .WithMessage("*أكبر من صفر*");
        }

        // ── DecreaseBalance Tests ───────────────────────────────

        [Fact]
        public void DecreaseBalance_SufficientBalance_DecreasesBalance()
        {
            var cashbox = CreateCashbox(initialDeposit: 1000m);

            cashbox.DecreaseBalance(400m);

            cashbox.Balance.Should().Be(600m);
        }

        [Fact]
        public void DecreaseBalance_ExactBalance_ResultsInZero()
        {
            var cashbox = CreateCashbox(initialDeposit: 500m);

            cashbox.DecreaseBalance(500m);

            cashbox.Balance.Should().Be(0m);
        }

        [Fact]
        public void DecreaseBalance_ExceedsBalance_ThrowsTreasuryDomainException()
        {
            var cashbox = CreateCashbox(initialDeposit: 500m);

            Action act = () => cashbox.DecreaseBalance(500.01m);

            act.Should().Throw<TreasuryDomainException>()
                .WithMessage("*غير كافٍ*");
        }

        [Fact]
        public void DecreaseBalance_ZeroBalance_ThrowsTreasuryDomainException()
        {
            var cashbox = CreateCashbox(); // Balance = 0

            Action act = () => cashbox.DecreaseBalance(1m);

            act.Should().Throw<TreasuryDomainException>()
                .WithMessage("*غير كافٍ*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void DecreaseBalance_NonPositiveAmount_Throws(decimal amount)
        {
            var cashbox = CreateCashbox(initialDeposit: 1000m);

            Action act = () => cashbox.DecreaseBalance(amount);

            act.Should().Throw<TreasuryDomainException>()
                .WithMessage("*أكبر من صفر*");
        }

        // ── Scenario: Payment > Balance (Phase 2 deliverable) ───

        [Fact]
        public void PostPayment_LargerThanBalance_ThrowsDomainException()
        {
            // Arrange: Cashbox with 100 balance
            var cashbox = CreateCashbox(initialDeposit: 100m);

            // Act: Attempt to decrease by 150 (simulates posting a payment of 150)
            Action act = () => cashbox.DecreaseBalance(150m);

            // Assert: Domain prevents negative balance
            act.Should().Throw<TreasuryDomainException>()
                .Which.Message.Should().Contain("غير كافٍ");
        }

        // ── Scenario: Transfer between cashboxes ────────────────

        [Fact]
        public void Transfer_SourceInsufficientBalance_ThrowsDomainException()
        {
            // Arrange
            var source = CreateCashbox(initialDeposit: 200m);
            var target = new Cashbox("CBX-0002", "خزنة فرعية", "Sub Cash", accountId: 2);

            // Act: Try transfer of 300 from source (balance = 200)
            Action act = () => source.DecreaseBalance(300m);

            // Assert
            act.Should().Throw<TreasuryDomainException>()
                .Which.Message.Should().Contain("غير كافٍ");

            // Target should not be affected
            target.Balance.Should().Be(0m);
        }

        [Fact]
        public void Transfer_SufficientBalance_UpdatesBothCashboxes()
        {
            // Arrange
            var source = CreateCashbox(initialDeposit: 1000m);
            var target = new Cashbox("CBX-0002", "خزنة فرعية", "Sub Cash", accountId: 2);

            // Act: Transfer 400
            source.DecreaseBalance(400m);
            target.IncreaseBalance(400m);

            // Assert
            source.Balance.Should().Be(600m);
            target.Balance.Should().Be(400m);
        }

        // ── Scenario: Receipt then Payment ──────────────────────

        [Fact]
        public void ReceiptThenPayment_BalanceTrackedCorrectly()
        {
            var cashbox = CreateCashbox();

            // Receive 5000
            cashbox.IncreaseBalance(5000m);
            cashbox.Balance.Should().Be(5000m);

            // Pay 3000
            cashbox.DecreaseBalance(3000m);
            cashbox.Balance.Should().Be(2000m);

            // Pay 2000 (exact remaining)
            cashbox.DecreaseBalance(2000m);
            cashbox.Balance.Should().Be(0m);

            // Any further payment should fail
            Action act = () => cashbox.DecreaseBalance(0.01m);
            act.Should().Throw<TreasuryDomainException>();
        }
    }
}
