using System;

namespace MarcoERP.Domain.Exceptions
{
    /// <summary>
    /// Domain exception for common/shared entities (Company, SoftDeletableEntity, etc.).
    /// </summary>
    public sealed class CommonDomainException : Exception
    {
        public CommonDomainException(string message) : base(message)
        {
        }
    }
}
