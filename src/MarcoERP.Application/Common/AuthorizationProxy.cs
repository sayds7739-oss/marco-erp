using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using MarcoERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Phase 4 – Centralized Authorization Enforcement.
    /// A DispatchProxy that intercepts every call on a service interface,
    /// reads <see cref="RequiresPermissionAttribute"/> from the interface method,
    /// and enforces authorization before forwarding to the real implementation.
    /// <para>
    /// Methods without the attribute are forwarded unconditionally (anonymous / read-only).
    /// Unauthorized calls receive a failed <see cref="ServiceResult"/> or
    /// <see cref="ServiceResult{T}"/> — exactly matching the existing contract.
    /// </para>
    /// </summary>
    public class AuthorizationProxy<TService> : DispatchProxy where TService : class
    {
        // Assigned after Create() via the Init helper
        private TService _inner = null!;
        private ICurrentUserService _currentUser = null!;

        private static readonly ConcurrentDictionary<MethodInfo, RequiresPermissionAttribute> _attrCache = new();

        private const string NotAuthenticatedMsg = "يجب تسجيل الدخول أولاً.";
        private const string NotAuthorizedMsg = "لا تملك الصلاحية لتنفيذ هذه العملية.";

        /// <summary>
        /// Creates a proxy instance wrapping the real service implementation
        /// with automatic authorization enforcement.
        /// </summary>
        public static TService Create(TService inner, ICurrentUserService currentUser)
        {
            // DispatchProxy.Create returns an instance typed as TService
            object proxy = Create<TService, AuthorizationProxy<TService>>();
            var typed = (AuthorizationProxy<TService>)proxy;
            typed._inner = inner ?? throw new ArgumentNullException(nameof(inner));
            typed._currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            return (TService)proxy;
        }

        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            // Read attribute from the interface method declaration (cached to avoid per-call reflection)
            var attr = _attrCache.GetOrAdd(targetMethod, m => m.GetCustomAttribute<RequiresPermissionAttribute>());
            if (attr != null)
            {
                if (!_currentUser.IsAuthenticated)
                    return BuildUnauthorizedResult(targetMethod.ReturnType, NotAuthenticatedMsg);

                if (!_currentUser.HasPermission(attr.PermissionKey))
                    return BuildUnauthorizedResult(targetMethod.ReturnType, NotAuthorizedMsg);
            }

            // Authorized (or no attribute) — forward to real implementation
            return targetMethod.Invoke(_inner, args);
        }

        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Constructs a <c>Task.FromResult(ServiceResult.Failure(msg))</c> or
        /// <c>Task.FromResult(ServiceResult&lt;T&gt;.Failure(msg))</c> that
        /// matches the declared return type of the intercepted method.
        /// </summary>
        private static object BuildUnauthorizedResult(Type returnType, string message)
        {
            // Expected: Task<ServiceResult> or Task<ServiceResult<T>>
            if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            {
                throw new InvalidOperationException(
                    $"AuthorizationProxy: unexpected return type '{returnType.FullName}'. " +
                    "Only Task<ServiceResult> and Task<ServiceResult<T>> are supported.");
            }

            var innerType = returnType.GetGenericArguments()[0]; // ServiceResult or ServiceResult<T>

            object failedResult;

            if (innerType == typeof(ServiceResult))
            {
                // Non-generic ServiceResult
                failedResult = ServiceResult.Failure(message);
            }
            else if (innerType.IsGenericType &&
                     innerType.GetGenericTypeDefinition() == typeof(ServiceResult<>))
            {
                // Generic ServiceResult<T> — call Failure(string) via reflection
                var failureMethod = innerType.GetMethod(
                    nameof(ServiceResult.Failure),
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);

                failedResult = failureMethod!.Invoke(null, new object[] { message })!;
            }
            else
            {
                throw new InvalidOperationException(
                    $"AuthorizationProxy: unsupported inner type '{innerType.FullName}'. " +
                    "Expected ServiceResult or ServiceResult<T>.");
            }

            // Wrap in Task.FromResult<TResult>(failedResult)
            var fromResult = typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(innerType);

            return fromResult.Invoke(null, new[] { failedResult })!;
        }
    }

    /// <summary>
    /// Extension method for DI registration of authorization-proxied services.
    /// </summary>
    public static class AuthorizationProxyExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TImpl"/> as scoped, then wraps it
        /// with <see cref="AuthorizationProxy{TService}"/> for the
        /// <typeparamref name="TInterface"/> resolution.
        /// </summary>
        public static IServiceCollection AddAuthorizedService<TInterface, TImpl>(
            this IServiceCollection services)
            where TInterface : class
            where TImpl : class, TInterface
        {
            services.AddScoped<TImpl>();
            services.AddScoped<TInterface>(sp =>
                AuthorizationProxy<TInterface>.Create(
                    sp.GetRequiredService<TImpl>(),
                    sp.GetRequiredService<ICurrentUserService>()));
            return services;
        }
    }
}
