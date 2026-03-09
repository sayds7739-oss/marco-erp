using System;

namespace MarcoERP.Domain.Entities.Common
{
    /// <summary>
    /// Extends BaseEntity with standard audit tracking fields.
    /// All mutable domain entities should inherit from this.
    /// </summary>
    public abstract class AuditableEntity : BaseEntity
    {
        /// <summary>UTC timestamp of record creation.</summary>
        public DateTime CreatedAt { get; internal set; }

        /// <summary>Username of the creator.</summary>
        public string CreatedBy { get; internal set; }

        /// <summary>UTC timestamp of last modification (null if never modified).</summary>
        public DateTime? ModifiedAt { get; internal set; }

        /// <summary>Username of last modifier (null if never modified).</summary>
        public string ModifiedBy { get; internal set; }
    }
}
