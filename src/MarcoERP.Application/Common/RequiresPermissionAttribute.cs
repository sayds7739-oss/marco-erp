using System;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Phase 4 – Centralized Authorization Enforcement.
    /// Declares the permission key required to invoke this service method.
    /// Applied on interface method declarations — enforced automatically by
    /// <see cref="AuthorizationProxy{TService}"/> at the DI boundary.
    /// Methods without this attribute are treated as anonymous (e.g. read queries).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class RequiresPermissionAttribute : Attribute
    {
        public string PermissionKey { get; }

        public RequiresPermissionAttribute(string permissionKey)
        {
            PermissionKey = permissionKey ?? throw new ArgumentNullException(nameof(permissionKey));
        }
    }
}
