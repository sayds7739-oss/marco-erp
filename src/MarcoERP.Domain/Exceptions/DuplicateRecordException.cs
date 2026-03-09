using System;

namespace MarcoERP.Domain.Exceptions
{
    /// <summary>
    /// يُطرح عند محاولة إدخال سجل مكرر ينتهك قيد التفرّد — Thrown when a duplicate record violates a unique constraint.
    /// </summary>
    public class DuplicateRecordException : Exception
    {
        public DuplicateRecordException()
            : base("فشل الحفظ بسبب تعارض في سجل مكرر.")
        {
        }

        public DuplicateRecordException(string message)
            : base(message)
        {
        }

        public DuplicateRecordException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
