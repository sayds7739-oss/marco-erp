using System;

namespace MarcoERP.Domain.Exceptions.Accounting
{
    /// <summary>
    /// Domain exception for opening balance invariant violations.
    /// </summary>
    public sealed class OpeningBalanceDomainException : Exception
    {
        public OpeningBalanceDomainException(string message) : base(message)
        {
        }
    }
}
