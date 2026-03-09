using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MarcoERP.Application.Common;
using MarcoERP.Application.Interfaces;
using Xunit;

namespace MarcoERP.Integration.Tests.Security
{
    /// <summary>
    /// Phase 4 – Centralized Authorization Enforcement tests.
    /// Verifies that AuthorizationProxy automatically enforces
    /// [RequiresPermission] attributes on interface methods.
    /// </summary>
    public sealed class AuthorizationProxyTests
    {
        private const string TestPermission = "test.permission";
        private const string OtherPermission = "other.permission";

        // ── Test Interface & Implementation ─────────────────────

        /// <summary>
        /// A minimal test interface with attributed and non-attributed methods.
        /// </summary>
        public interface ITestService
        {
            /// <summary>Guarded method returning ServiceResult{T}.</summary>
            [RequiresPermission(TestPermission)]
            Task<ServiceResult<string>> GuardedGenericAsync(string input, CancellationToken ct = default);

            /// <summary>Guarded method returning ServiceResult (non-generic).</summary>
            [RequiresPermission(TestPermission)]
            Task<ServiceResult> GuardedNonGenericAsync(int id, CancellationToken ct = default);

            /// <summary>Method with a different permission.</summary>
            [RequiresPermission(OtherPermission)]
            Task<ServiceResult<int>> DifferentPermissionAsync(CancellationToken ct = default);

            /// <summary>Anonymous method (no attribute) — always allowed.</summary>
            Task<ServiceResult<string>> AnonymousAsync(CancellationToken ct = default);
        }

        private sealed class FakeTestService : ITestService
        {
            public Task<ServiceResult<string>> GuardedGenericAsync(string input, CancellationToken ct = default)
                => Task.FromResult(ServiceResult<string>.Success($"ok:{input}"));

            public Task<ServiceResult> GuardedNonGenericAsync(int id, CancellationToken ct = default)
                => Task.FromResult(ServiceResult.Success());

            public Task<ServiceResult<int>> DifferentPermissionAsync(CancellationToken ct = default)
                => Task.FromResult(ServiceResult<int>.Success(42));

            public Task<ServiceResult<string>> AnonymousAsync(CancellationToken ct = default)
                => Task.FromResult(ServiceResult<string>.Success("anonymous"));
        }

        // ── Helpers ─────────────────────────────────────────────

        private static Mock<ICurrentUserService> CreateUser(
            bool isAuthenticated,
            params string[] permissions)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(u => u.IsAuthenticated).Returns(isAuthenticated);
            mock.Setup(u => u.HasPermission(It.IsAny<string>()))
                .Returns<string>(key => Array.IndexOf(permissions, key) >= 0);
            return mock;
        }

        private static ITestService CreateProxy(ICurrentUserService currentUser)
            => AuthorizationProxy<ITestService>.Create(new FakeTestService(), currentUser);

        // ═══════════════════════════════════════════════════════
        // ██  1. Unauthenticated user → blocked on guarded methods
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_UnauthenticatedUser_BlocksGuardedGenericMethod()
        {
            var user = CreateUser(isAuthenticated: false);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedGenericAsync("hello");

            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("تسجيل الدخول");
        }

        [Fact]
        public async Task Proxy_UnauthenticatedUser_BlocksGuardedNonGenericMethod()
        {
            var user = CreateUser(isAuthenticated: false);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedNonGenericAsync(1);

            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("تسجيل الدخول");
        }

        [Fact]
        public async Task Proxy_UnauthenticatedUser_AllowsAnonymousMethod()
        {
            var user = CreateUser(isAuthenticated: false);
            var svc = CreateProxy(user.Object);

            var result = await svc.AnonymousAsync();

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be("anonymous");
        }

        // ═══════════════════════════════════════════════════════
        // ██  2. Authenticated, wrong permission → blocked
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_WrongPermission_BlocksGuardedGenericMethod()
        {
            var user = CreateUser(isAuthenticated: true, OtherPermission);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedGenericAsync("hello");

            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("الصلاحية");
        }

        [Fact]
        public async Task Proxy_WrongPermission_BlocksGuardedNonGenericMethod()
        {
            var user = CreateUser(isAuthenticated: true, OtherPermission);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedNonGenericAsync(1);

            result.IsFailure.Should().BeTrue();
            result.ErrorMessage.Should().Contain("الصلاحية");
        }

        // ═══════════════════════════════════════════════════════
        // ██  3. Authenticated, correct permission → allowed
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_CorrectPermission_AllowsGuardedGenericMethod()
        {
            var user = CreateUser(isAuthenticated: true, TestPermission);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedGenericAsync("hello");

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be("ok:hello");
        }

        [Fact]
        public async Task Proxy_CorrectPermission_AllowsGuardedNonGenericMethod()
        {
            var user = CreateUser(isAuthenticated: true, TestPermission);
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedNonGenericAsync(99);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Proxy_CorrectPermission_AllowsDifferentPermissionMethod()
        {
            var user = CreateUser(isAuthenticated: true, OtherPermission);
            var svc = CreateProxy(user.Object);

            var result = await svc.DifferentPermissionAsync();

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be(42);
        }

        // ═══════════════════════════════════════════════════════
        // ██  4. Anonymous methods always pass regardless of auth
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_AuthenticatedNoPerms_AllowsAnonymousMethod()
        {
            var user = CreateUser(isAuthenticated: true);
            var svc = CreateProxy(user.Object);

            var result = await svc.AnonymousAsync();

            result.IsSuccess.Should().BeTrue();
            result.Data.Should().Be("anonymous");
        }

        // ═══════════════════════════════════════════════════════
        // ██  5. Multiple permissions — each method checks its own key
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_MultiplePermissions_EachMethodChecksOwn()
        {
            // User has TestPermission but NOT OtherPermission
            var user = CreateUser(isAuthenticated: true, TestPermission);
            var svc = CreateProxy(user.Object);

            var guarded = await svc.GuardedGenericAsync("ok");
            guarded.IsSuccess.Should().BeTrue("user has test.permission");

            var different = await svc.DifferentPermissionAsync();
            different.IsFailure.Should().BeTrue("user lacks other.permission");
        }

        // ═══════════════════════════════════════════════════════
        // ██  6. Generic return type correctly constructed
        // ═══════════════════════════════════════════════════════

        [Fact]
        public async Task Proxy_Unauthorized_ServiceResultGenericHasDefaultData()
        {
            var user = CreateUser(isAuthenticated: true); // no permissions
            var svc = CreateProxy(user.Object);

            var result = await svc.GuardedGenericAsync("test");

            result.IsFailure.Should().BeTrue();
            result.Data.Should().BeNull("ServiceResult<string>.Failure should have default data");
            result.Errors.Should().ContainSingle();
        }

        [Fact]
        public async Task Proxy_Unauthorized_ServiceResultIntGenericHasDefaultData()
        {
            var user = CreateUser(isAuthenticated: true); // no permissions
            var svc = CreateProxy(user.Object);

            var result = await svc.DifferentPermissionAsync();

            result.IsFailure.Should().BeTrue();
            result.Data.Should().Be(0, "ServiceResult<int>.Failure should have default(int)");
        }

        // ═══════════════════════════════════════════════════════
        // ██  7. Real interface attribute verification (canary test)
        // ═══════════════════════════════════════════════════════

        [Fact]
        public void RealInterfaces_HaveRequiresPermissionAttributes()
        {
            // Verify a sample of REAL production interfaces have the attribute
            var cashReceiptCreate = typeof(Application.Interfaces.Treasury.ICashReceiptService)
                .GetMethod("CreateAsync");
            var attr = cashReceiptCreate?.GetCustomAttributes(typeof(RequiresPermissionAttribute), true);

            attr.Should().NotBeNullOrEmpty("ICashReceiptService.CreateAsync must have [RequiresPermission]");
            ((RequiresPermissionAttribute)attr[0]).PermissionKey.Should().Be(PermissionKeys.TreasuryCreate);
        }

        [Fact]
        public void RealInterfaces_QueryMethodsHaveNoAttribute()
        {
            // Verify that read-only methods do NOT have the attribute
            var cashReceiptGetAll = typeof(Application.Interfaces.Treasury.ICashReceiptService)
                .GetMethod("GetAllAsync");
            var attr = cashReceiptGetAll?.GetCustomAttributes(typeof(RequiresPermissionAttribute), true);

            attr.Should().BeEmpty("ICashReceiptService.GetAllAsync should NOT have [RequiresPermission]");
        }
    }
}
