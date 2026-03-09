using System;
using FluentAssertions;
using MarcoERP.Domain.Entities.Inventory;
using MarcoERP.Domain.Enums;
using Xunit;

namespace MarcoERP.Domain.Tests.Inventory
{
    public class ProductTests
    {
        private Product CreateValidProduct()
        {
            return new Product(
                "P001", "منتج اختبار", "Test Product",
                categoryId: 1, baseUnitId: 1,
                initialCostPrice: 100m, defaultSalePrice: 150m,
                minimumStock: 10m, reorderLevel: 20m, vatRate: 15m);
        }

        // ── Constructor ─────────────────────────────────────────

        [Fact]
        public void Constructor_ValidParameters_CreatesActiveProduct()
        {
            var p = CreateValidProduct();

            p.Code.Should().Be("P001");
            p.NameAr.Should().Be("منتج اختبار");
            p.NameEn.Should().Be("Test Product");
            p.Status.Should().Be(ProductStatus.Active);
            p.CostPrice.Should().Be(100m);
            p.WeightedAverageCost.Should().Be(100m);
            p.DefaultSalePrice.Should().Be(150m);
            p.VatRate.Should().Be(15m);
        }

        [Fact]
        public void Constructor_EmptyCode_ThrowsException()
        {
            Action act = () => new Product(
                "", "منتج", "Product", 1, 1, 100m, 150m, 10m, 20m, 15m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_EmptyNameAr_ThrowsException()
        {
            Action act = () => new Product(
                "P001", "", "Product", 1, 1, 100m, 150m, 10m, 20m, 15m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_NegativeCostPrice_ThrowsException()
        {
            Action act = () => new Product(
                "P001", "منتج", "Product", 1, 1, -100m, 150m, 10m, 20m, 15m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_InvalidVatRate_ThrowsException()
        {
            Action act = () => new Product(
                "P001", "منتج", "Product", 1, 1, 100m, 150m, 10m, 20m, 101m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void Constructor_ZeroCategoryId_ThrowsException()
        {
            Action act = () => new Product(
                "P001", "منتج", "Product", 0, 1, 100m, 150m, 10m, 20m, 15m);
            act.Should().Throw<Exception>();
        }

        // ── Update ──────────────────────────────────────────────

        [Fact]
        public void Update_ValidParameters_UpdatesFields()
        {
            var p = CreateValidProduct();
            p.Update("اسم جديد", "New Name", 2, 200m, 5m, 10m, 5m, "111222", "وصف");

            p.NameAr.Should().Be("اسم جديد");
            p.NameEn.Should().Be("New Name");
            p.CategoryId.Should().Be(2);
            p.DefaultSalePrice.Should().Be(200m);
        }

        [Fact]
        public void Update_NegativeSalePrice_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.Update("اسم", "Name", 1, -10m, 5m, 10m, 5m, null, null);
            act.Should().Throw<Exception>();
        }

        // ── WeightedAverageCost ─────────────────────────────────

        [Fact]
        public void UpdateWeightedAverageCost_ValidPurchase_CalculatesCorrectly()
        {
            var p = CreateValidProduct();
            // WAC = ((existingQty * currentWAC) + (newQty * newUnitCost)) / (existingQty + newQty)
            // ((10 * 100) + (10 * 120)) / (10 + 10) = 2200 / 20 = 110
            p.UpdateWeightedAverageCost(10m, 10m, 120m);

            p.WeightedAverageCost.Should().Be(110m);
            p.CostPrice.Should().Be(120m); // CostPrice tracks last unit cost, not WAC
        }

        [Fact]
        public void UpdateWeightedAverageCost_ZeroExistingQty_UsesNewCost()
        {
            var p = CreateValidProduct();
            p.UpdateWeightedAverageCost(0m, 50m, 200m);
            p.WeightedAverageCost.Should().Be(200m);
        }

        [Fact]
        public void UpdateWeightedAverageCost_ZeroReceivedQty_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.UpdateWeightedAverageCost(10m, 0m, 100m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void UpdateWeightedAverageCost_NegativeUnitCost_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.UpdateWeightedAverageCost(10m, 5m, -50m);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void UpdateWeightedAverageCost_NegativeExistingQty_ResetsToNewCost()
        {
            // C-07: When stock is negative (AllowNegativeStock cancel/adjustment),
            // the WAC formula produces distorted results.  Fix resets to new cost.
            var p = CreateValidProduct();
            // existingQty = -5 (negative stock from cancel flow)
            p.UpdateWeightedAverageCost(-5m, 10m, 200m);
            p.WeightedAverageCost.Should().Be(200m);
            p.CostPrice.Should().Be(200m);
        }

        // ── ProductUnit Management ──────────────────────────────

        [Fact]
        public void AddUnit_ValidUnit_AddsToCollection()
        {
            var p = CreateValidProduct();
            var unit = new ProductUnit(0, 2, 12m, 180m, 120m);
            p.AddUnit(unit);
            p.ProductUnits.Count.Should().Be(1);
        }

        [Fact]
        public void AddUnit_DuplicateUnitId_ThrowsException()
        {
            var p = CreateValidProduct();
            var unit1 = new ProductUnit(0, 2, 12m, 180m, 120m);
            var unit2 = new ProductUnit(0, 2, 6m, 90m, 60m);
            p.AddUnit(unit1);

            Action act = () => p.AddUnit(unit2);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void AddUnit_NullUnit_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.AddUnit(null);
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void RemoveUnit_ExistingUnit_Removes()
        {
            var p = CreateValidProduct();
            var unit = new ProductUnit(0, 2, 12m, 180m, 120m);
            p.AddUnit(unit);
            p.RemoveUnit(2);
            p.ProductUnits.Count.Should().Be(0);
        }

        [Fact]
        public void RemoveUnit_BaseUnit_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.RemoveUnit(1); // baseUnitId = 1
            act.Should().Throw<Exception>();
        }

        [Fact]
        public void RemoveUnit_NonExistentUnit_ThrowsException()
        {
            var p = CreateValidProduct();
            Action act = () => p.RemoveUnit(999);
            act.Should().Throw<Exception>();
        }

        // ── Status Transitions ──────────────────────────────────

        [Fact]
        public void Deactivate_ActiveProduct_Deactivates()
        {
            var p = CreateValidProduct();
            p.Deactivate();
            p.Status.Should().Be(ProductStatus.Inactive);
        }

        [Fact]
        public void Activate_InactiveProduct_Activates()
        {
            var p = CreateValidProduct();
            p.Deactivate();
            p.Activate();
            p.Status.Should().Be(ProductStatus.Active);
        }

        [Fact]
        public void Discontinue_Product_SetsDiscontinued()
        {
            var p = CreateValidProduct();
            p.Discontinue();
            p.Status.Should().Be(ProductStatus.Discontinued);
        }
    }
}
