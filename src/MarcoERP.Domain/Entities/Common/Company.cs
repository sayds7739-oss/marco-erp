using System;
using MarcoERP.Domain.Exceptions;

namespace MarcoERP.Domain.Entities.Common
{
    /// <summary>
    /// Represents a company (شركة) in the system.
    /// Currently the system operates with a single default company (Id=1).
    /// This entity exists to architecturally prepare for Multi-Company support.
    /// </summary>
    public sealed class Company : AuditableEntity
    {
        // ── Constructors ─────────────────────────────────────────

        /// <summary>EF Core only.</summary>
        private Company() { }

        /// <summary>
        /// Creates a new Company with full invariant validation.
        /// </summary>
        public Company(string code, string nameAr, string nameEn = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new CommonDomainException("كود الشركة مطلوب.");

            if (string.IsNullOrWhiteSpace(nameAr))
                throw new CommonDomainException("اسم الشركة بالعربي مطلوب.");

            Code = code.Trim();
            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
            IsActive = true;
        }

        // ── Properties ───────────────────────────────────────────

        /// <summary>Unique company code (e.g. "DEF" for default).</summary>
        public string Code { get; private set; }

        /// <summary>Arabic company name.</summary>
        public string NameAr { get; private set; }

        /// <summary>English company name (optional).</summary>
        public string NameEn { get; private set; }

        /// <summary>Whether this company is active.</summary>
        public bool IsActive { get; private set; }

        // ── Methods ──────────────────────────────────────────────

        /// <summary>Updates company information.</summary>
        public void Update(string nameAr, string nameEn)
        {
            if (string.IsNullOrWhiteSpace(nameAr))
                throw new CommonDomainException("اسم الشركة بالعربي مطلوب.");

            NameAr = nameAr.Trim();
            NameEn = nameEn?.Trim();
        }

        /// <summary>Deactivates the company.</summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>Activates the company.</summary>
        public void Activate()
        {
            IsActive = true;
        }
    }
}
