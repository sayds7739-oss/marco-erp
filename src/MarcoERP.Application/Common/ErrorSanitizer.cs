using System;

namespace MarcoERP.Application.Common
{
    /// <summary>
    /// Sanitizes exception messages to prevent raw technical errors from
    /// reaching the user interface. Converts EF Core, database, and other
    /// infrastructure exceptions into user-friendly Arabic messages.
    /// </summary>
    public static class ErrorSanitizer
    {
        /// <summary>
        /// Converts an InvalidOperationException message to a user-friendly Arabic message.
        /// Domain-originated IOEs (Arabic text) pass through; infrastructure IOEs are replaced.
        /// </summary>
        public static string Sanitize(InvalidOperationException ex, string operationContext)
        {
            if (ex == null)
                return $"حدث خطأ غير متوقع أثناء {operationContext}.";

            var msg = ex.Message ?? string.Empty;

            // ── EF Core entity tracking conflicts ──
            if (msg.Contains("cannot be tracked because another instance", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("is already being tracked", StringComparison.OrdinalIgnoreCase))
            {
                return $"حدث تعارض داخلي في تتبع البيانات أثناء {operationContext}. يرجى إعادة المحاولة.";
            }

            // ── EF Core DbContext disposed ──
            if (msg.Contains("disposed", StringComparison.OrdinalIgnoreCase)
                && msg.Contains("context", StringComparison.OrdinalIgnoreCase))
            {
                return $"انتهت جلسة الاتصال بقاعدة البيانات أثناء {operationContext}. يرجى إعادة المحاولة.";
            }

            // ── EF Core: Sequence contains no elements (LINQ) ──
            if (msg.Contains("Sequence contains no", StringComparison.OrdinalIgnoreCase))
            {
                return $"لم يتم العثور على البيانات المطلوبة أثناء {operationContext}.";
            }

            // ── EF Core: Collection was modified during enumeration ──
            if (msg.Contains("Collection was modified", StringComparison.OrdinalIgnoreCase))
            {
                return $"حدث تعارض في البيانات أثناء {operationContext}. يرجى إعادة المحاولة.";
            }

            // ── EF Core: second operation on same context ──
            if (msg.Contains("second operation", StringComparison.OrdinalIgnoreCase)
                && msg.Contains("previous operation", StringComparison.OrdinalIgnoreCase))
            {
                return $"حدث تعارض في عمليات قاعدة البيانات أثناء {operationContext}. يرجى الانتظار وإعادة المحاولة.";
            }

            // ── If the message is in Arabic, it's a domain message — pass through ──
            if (ContainsArabic(msg))
                return msg;

            // ── Default: generic message for unknown IOEs ──
            return $"حدث خطأ أثناء {operationContext}. يرجى المحاولة مرة أخرى أو التواصل مع الدعم الفني.";
        }

        /// <summary>
        /// Sanitizes a generic exception into a user-friendly message.
        /// </summary>
        public static string SanitizeGeneric(Exception ex, string operationContext)
        {
            if (ex == null)
                return $"حدث خطأ غير متوقع أثناء {operationContext}.";

            if (ex is InvalidOperationException ioe)
                return Sanitize(ioe, operationContext);

            // ── EF Core concurrency conflict ──
            if (ex.GetType().Name == "DbUpdateConcurrencyException"
                || ex.GetType().BaseType?.Name == "DbUpdateConcurrencyException")
                return "تم تعديل البيانات من قبل مستخدم آخر. يرجى إعادة تحميل الصفحة والمحاولة مرة أخرى.";

            var msg = ex.Message ?? string.Empty;
            var inner = ex.InnerException?.Message ?? string.Empty;

            // ── SQL unique constraint ──
            if (inner.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return $"خطأ في {operationContext}: يوجد سجل مكرر. تأكد من عدم تكرار البيانات.";
            }

            // ── SQL foreign key ──
            if (inner.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase))
            {
                return $"خطأ في {operationContext}: لا يمكن الحذف لوجود سجلات مرتبطة.";
            }

            // ── SQL timeout ──
            if (msg.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || inner.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return $"انتهت مهلة الاتصال أثناء {operationContext}. يرجى إعادة المحاولة.";
            }

            // ── SQL connection ──
            if (msg.Contains("connection", StringComparison.OrdinalIgnoreCase)
                && (msg.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("refused", StringComparison.OrdinalIgnoreCase)))
            {
                return $"تعذر الاتصال بقاعدة البيانات أثناء {operationContext}. تأكد من تشغيل الخادم.";
            }

            // ── Arabic domain message — pass through ──
            if (ContainsArabic(msg))
                return msg;

            return $"حدث خطأ غير متوقع أثناء {operationContext}. يرجى المحاولة مرة أخرى أو التواصل مع الدعم الفني.";
        }

        private static bool ContainsArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
            {
                if (c >= '\u0600' && c <= '\u06FF') return true;
                if (c >= '\uFE70' && c <= '\uFEFF') return true;
            }
            return false;
        }
    }
}
